using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Materijalno.Import;

// Narrow archive replacement: mat/inv only, plus missing material_document metadata.
// No Oracle client, schema changes, master-data rewrites, journal resets or year close.
public static class ReplaceMatInvFromArchive
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    static readonly string[] ProtectedTables = {
        "app_settings", "document_counter", "year_close", "nalmat", "financial_batch", "financial_export_log",
        "tabela_materijala", "sifarnik_skladista", "sifarnik_konta", "sifarnik_materijal_skladiste_konto",
        "fox_import_manifest", "fox_raw.mat", "fox_raw.inv"
    };
    static SqlConnection db;
    static SqlTransaction transaction;
    static string work, source, live;
    static long checkedFields;
    static readonly Dictionary<string, DbfTable> fox = new Dictionary<string, DbfTable>();
    static readonly Dictionary<string, string> sourceHashes = new Dictionary<string, string>();
    static string Name(string table) { return table.Contains(".") ? string.Join(".", table.Split('.').Select(Quote)) : "dbo." + Quote(table); }
    static string Quote(string name) { return "[" + name.Replace("]", "]]") + "]"; }
    static string Text(string value) { return (value ?? "").Trim(); }
    static decimal? Number(string value) { return string.IsNullOrWhiteSpace(value) ? (decimal?)null : decimal.Parse(value, NumberStyles.Number, Invariant); }
    static int? Integer(string value) { var n = Number(value); if (!n.HasValue) return null; if (n.Value != decimal.Truncate(n.Value)) throw new Exception("Fractional integer: " + value); return checked((int)n.Value); }
    static DateTime? Date(string value) { return string.IsNullOrWhiteSpace(value) ? (DateTime?)null : DateTime.ParseExact(Text(value), "yyyyMMdd", Invariant); }
    static string Hash(byte[] data) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", ""); }
    static Guid Stable(string value) { using (var sha = SHA256.Create()) { var result = new byte[16]; Array.Copy(sha.ComputeHash(Encoding.UTF8.GetBytes(value)), result, 16); return new Guid(result); } }
    static SqlCommand Command(string sql) { return new SqlCommand(sql, db, transaction) { CommandTimeout = 120 }; }
    static void Execute(string sql) { using (var command = Command(sql)) command.ExecuteNonQuery(); }
    static DataTable Read(string sql) { using (var command = Command(sql)) using (var reader = command.ExecuteReader()) { var table = new DataTable(); table.Load(reader); return table; } }
    static DataTable Empty(string table)
    {
        using (var command = Command("SELECT TOP(0) * FROM " + Name(table))) using (var reader = command.ExecuteReader()) {
            var result = new DataTable(table);
            for (int i = 0; i < reader.FieldCount; i++) if (reader.GetName(i) != "row_version") result.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
            return result;
        }
    }
    static string TableHash(DataTable table)
    {
        var rows = new List<string>();
        foreach (DataRow row in table.Rows) using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) {
            foreach (DataColumn column in table.Columns) {
                object value = row[column]; writer.Write(column.ColumnName); writer.Write(column.DataType.FullName); writer.Write(value != DBNull.Value);
                if (value == DBNull.Value) continue;
                if (value is byte[]) { var bytes = (byte[])value; writer.Write(bytes.Length); writer.Write(bytes); }
                else if (value is DateTime) writer.Write(((DateTime)value).Ticks);
                else writer.Write(Convert.ToString(value, Invariant));
            }
            writer.Flush(); rows.Add(Hash(stream.ToArray()));
        }
        rows.Sort(StringComparer.Ordinal); return Hash(Encoding.UTF8.GetBytes(string.Join("\n", rows)));
    }
    static string TableHash(string table) { return TableHash(Read("SELECT * FROM " + Name(table))); }
    static string SchemaHash()
    {
        return TableHash(Read("SELECT s.name schema_name,t.name table_name,c.column_id,c.name column_name,ty.name type_name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed,ISNULL(dc.definition,'') default_definition FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id"));
    }
    static void CheckSource()
    {
        foreach (var item in sourceHashes) {
            if (Hash(File.ReadAllBytes(Path.Combine(source, item.Key + ".dbf"))) != item.Value) throw new Exception("Snapshot changed: " + item.Key);
            using (var file = new FileStream(Path.Combine(live, item.Key + ".dbf"), FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create()) if (BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") != item.Value) throw new Exception("Live archive changed: " + item.Key);
        }
    }
    static object Field(DbfField field, string value, string column, Type targetType)
    {
        if (column == "ident") return string.IsNullOrWhiteSpace(value) ? null : (object)Text(value);
        if (targetType == typeof(DateTime)) return Date(value);
        if (targetType == typeof(int)) {
            int? number = Integer(value);
            if (new[] { "kljnaz", "kljnaz1" }.Contains(column) && number <= 0) return null;
            return number;
        }
        if (targetType == typeof(decimal)) return Number(value) ?? 0m;
        if (targetType == typeof(string)) return Text(value);
        throw new Exception("Unsupported field mapping: " + field.Name);
    }
    static DataTable Map(string table)
    {
        var result = Empty(table);
        foreach (var record in fox[table].Records.Where(r => !r.Deleted)) {
            var row = result.NewRow(); row["id"] = record.RecordNumber; row["fox_recno"] = record.RecordNumber;
            foreach (var field in fox[table].Fields) {
                string column = field.Name.Equals("KONTOSKLA2", StringComparison.OrdinalIgnoreCase) ? "kontosklad1" : field.Name.ToLowerInvariant();
                if (!result.Columns.Contains(column)) throw new Exception("Missing SQL column: " + table + "." + column);
                row[column] = Field(field, record.Values[field.Name], column, result.Columns[column].DataType) ?? DBNull.Value;
            }
            bool draft = row.IsNull("ident") || row.IsNull("kljnaz") || Text(Convert.ToString(row["ident"], Invariant)) == "0";
            if (draft && !row.IsNull("ident") && Text((string)row["ident"]) == "0") row["ident"] = DBNull.Value;
            row["is_draft"] = draft; row["is_archived"] = false;
            if (table == "mat") {
                row["book_year"] = row.IsNull("datun") ? 2025 : ((DateTime)row["datun"]).Year;
                row["line_token"] = Stable("archive2025:" + sourceHashes[table] + ":mat:" + record.RecordNumber);
                if (!draft && (int)row["book_year"] != 2025) throw new Exception("Unexpected year in archive MAT.");
            } else {
                row["inventory_date"] = row["datun"];
                if (!draft && !row.IsNull("datun")) row["inventory_session_id"] = Stable(((int)row["kljnaz"]).ToString(Invariant) + ":" + ((DateTime)row["datun"]).ToString("yyyy-MM-dd", Invariant));
            }
            result.Rows.Add(row);
        }
        return result;
    }
    static string DocumentKey(string status, int year, string number, int warehouse) { return status + "|" + year + "|" + number + "|" + warehouse; }
    static DataTable Documents(DataTable mat)
    {
        var result = Empty("material_document"); var existing = Read("SELECT * FROM dbo.material_document");
        var documents = existing.AsEnumerable().ToDictionary(r => DocumentKey((string)r["status"], (int)r["book_year"], (string)r["number"], (int)r["warehouse"]));
        var rows = mat.AsEnumerable().Where(r => !(bool)r["is_draft"]);
        foreach (var group in rows.GroupBy(r => DocumentKey((string)r["status"], (int)r["book_year"], (string)r["brfak"], (string)r["status"] == "M" ? 0 : (int)r["kljnaz"]))) {
            var first = group.First(); string status = (string)first["status"], number = (string)first["brfak"]; int year = (int)first["book_year"];
            int warehouse = status == "M" ? (int)group.First(r => (decimal)r["kolic"] < 0)["kljnaz"] : (int)first["kljnaz"];
            string key = DocumentKey(status, year, number, warehouse); DataRow document;
            if (!documents.TryGetValue(key, out document)) {
                document = result.NewRow(); document["id"] = Stable("archive2025:document:" + key); document["status"] = status;
                document["book_year"] = year; document["number"] = number; document["warehouse"] = warehouse; document["created_at"] = DateTime.UtcNow;
                result.Rows.Add(document); documents.Add(key, document);
            }
            foreach (var row in group) row["document_id"] = document["id"];
        }
        foreach (var group in rows.Where(r => (string)r["status"] == "M").GroupBy(r => {
            int a = (int)r["kljnaz"], b = (int)r["kljnaz1"];
            return string.Join("|", new[] { Convert.ToString(r["brfak"], Invariant), (string)r["ident"], ((DateTime)r["datun"]).ToString("yyyyMMdd"), Math.Min(a,b) + ":" + Math.Max(a,b), Math.Abs((decimal)r["kolic"]).ToString(Invariant), Math.Abs((decimal)r["vrijed"]).ToString(Invariant) });
        })) {
            var negative = group.Where(r => (decimal)r["kolic"] < 0).OrderBy(r => (int)r["id"]).ToList();
            var positive = group.Where(r => (decimal)r["kolic"] > 0).OrderBy(r => (int)r["id"]).ToList();
            if (negative.Count == 0 || negative.Count != positive.Count || group.Count() != negative.Count * 2) throw new Exception("Unpaired archive transfer: " + group.Key);
            for (int i = 0; i < negative.Count; i++) {
                if ((int)negative[i]["kljnaz"] != (int)positive[i]["kljnaz1"] || (int)negative[i]["kljnaz1"] != (int)positive[i]["kljnaz"] || (decimal)negative[i]["kolic"] + (decimal)positive[i]["kolic"] != 0 || (decimal)negative[i]["vrijed"] + (decimal)positive[i]["vrijed"] != 0) throw new Exception("Invalid archive transfer pair.");
                var id = Stable("archive2025:transfer:" + group.Key + "|" + i); negative[i]["transfer_pair_id"] = id; positive[i]["transfer_pair_id"] = id;
            }
        }
        return result;
    }
    static void ValidateMasters(params DataTable[] tables)
    {
        var materials = new HashSet<string>(Read("SELECT ident FROM dbo.tabela_materijala").AsEnumerable().Select(r => (string)r[0]), StringComparer.Ordinal);
        var warehouses = new HashSet<int>(Read("SELECT kljnaz FROM dbo.sifarnik_skladista").AsEnumerable().Select(r => (int)r[0]));
        var missing = new HashSet<string>();
        foreach (var table in tables) foreach (DataRow row in table.Rows) {
            if (!row.IsNull("ident") && !materials.Contains((string)row["ident"])) missing.Add("material:" + row["ident"]);
            foreach (string column in new[] { "kljnaz", "kljnaz1" }) if (table.Columns.Contains(column) && !row.IsNull(column) && !warehouses.Contains((int)row[column])) missing.Add("warehouse:" + row[column]);
        }
        if (missing.Count != 0) throw new Exception("Missing referenced master data: " + string.Join(",", missing.OrderBy(s => s)));
    }
    static void Bulk(DataTable table)
    {
        if (table.Rows.Count == 0) return;
        using (var bulk = new SqlBulkCopy(db, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.TableLock, transaction)) {
            bulk.DestinationTableName = Name(table.TableName); bulk.BulkCopyTimeout = 120;
            foreach (DataColumn column in table.Columns) bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            bulk.WriteToServer(table);
        }
    }
    static void Verify(DataTable expected)
    {
        var actual = Read("SELECT " + string.Join(",", expected.Columns.Cast<DataColumn>().Select(c => Quote(c.ColumnName))) + " FROM " + Name(expected.TableName));
        if (actual.Rows.Count != expected.Rows.Count) throw new Exception("Row count differs: " + expected.TableName);
        var lookup = expected.AsEnumerable().ToDictionary(r => (int)r["id"]);
        foreach (DataRow row in actual.Rows) {
            DataRow wanted; if (!lookup.TryGetValue((int)row["id"], out wanted)) throw new Exception("Unexpected SQL row.");
            foreach (DataColumn column in expected.Columns) { checkedFields++; if (!Equals(wanted[column.ColumnName], row[column.ColumnName])) throw new Exception("Different field: " + expected.TableName + "/" + row["id"] + "/" + column.ColumnName); }
        }
        Console.WriteLine("VERIFIED " + expected.TableName + ": " + actual.Rows.Count + " rows, every mapped field.");
    }
    static object Summary(DataTable table)
    {
        return new { Rows = table.Rows.Count, Posted = table.AsEnumerable().Count(r => !(bool)r["is_draft"]), Draft = table.AsEnumerable().Count(r => (bool)r["is_draft"]), Quantity = table.AsEnumerable().Sum(r => r.Field<decimal?>("kolic") ?? 0), Value = table.AsEnumerable().Sum(r => r.Field<decimal?>("vrijed") ?? 0), LeadingZeroIdents = table.AsEnumerable().Count(r => !r.IsNull("ident") && ((string)r["ident"]).StartsWith("0", StringComparison.Ordinal)), MinDate = table.AsEnumerable().Select(r => r.Field<DateTime?>("datun")).Min(), MaxDate = table.AsEnumerable().Select(r => r.Field<DateTime?>("datun")).Max() };
    }
    public static int Main(string[] args)
    {
        try {
            if (args.Length < 4) throw new Exception("Expected snapshot, config, run directory, live archive; optional apply token.");
            source = Path.GetFullPath(args[0]); work = Path.GetFullPath(args[2]); live = args[3]; bool apply = args.Length == 5 && args[4] == "apply-mat-inv-archive2025";
            var manifest = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(source, "snapshot_manifest.json")));
            foreach (Dictionary<string, object> file in (System.Collections.IEnumerable)manifest["Files"]) sourceHashes.Add((string)file["Table"], (string)file["SHA256"]);
            if (sourceHashes.Count != 2 || !sourceHashes.ContainsKey("mat") || !sourceHashes.ContainsKey("inv")) throw new Exception("Only MAT and INV snapshots are accepted.");
            CheckSource(); foreach (string table in new[] { "mat", "inv" }) fox.Add(table, DbfReader.Read(Path.Combine(source, table + ".dbf")));
            var cfg = XDocument.Load(args[1]); var connection = cfg.Root.Element("connectionStrings").Elements("add").Single(e => (string)e.Attribute("name") == "MaterijalnoDb");
            var builder = new SqlConnectionStringBuilder((string)connection.Attribute("connectionString")) { ApplicationName = "Materijalno_MatInv_Archive2025_Replacement", ConnectTimeout = 10 };
            if (builder.DataSource != "192.168.1.213" || builder.InitialCatalog != "materijalno_fox_20261105_102038") throw new Exception("Unexpected database target.");
            using (db = new SqlConnection(builder.ConnectionString)) {
                db.Open(); var mat = Map("mat"); var inventory = Map("inv"); ValidateMasters(mat, inventory); var documents = Documents(mat);
                var before = new Dictionary<string, string>(); foreach (string table in ProtectedTables) before.Add(table, TableHash(table));
                string originalSchema = SchemaHash(), oldMat = TableHash("mat"), oldInv = TableHash("inv"); var oldDocs = Read("SELECT * FROM dbo.material_document");
                var plan = new { Database = db.Database, Server = builder.DataSource, Source = live, Mat = Summary(mat), Inv = Summary(inventory), NewDocumentMetadataRows = documents.Rows.Count, ActiveBookYear = Convert.ToString(Read("SELECT value FROM dbo.app_settings WHERE key_name='ActiveBookYear'").Rows[0][0], Invariant), ActiveBookYearUnchanged = true, SourceDatesAndNCUnchanged = true, MatBeforeHash = oldMat, InvBeforeHash = oldInv, SchemaHash = originalSchema, ProtectedTableHashes = before };
                File.WriteAllText(Path.Combine(work, apply ? "apply_plan.json" : "preflight.json"), Json.Serialize(plan), new UTF8Encoding(false));
                if (!apply) { Console.WriteLine("PREFLIGHT OK " + Json.Serialize(plan)); return 0; }
                var backup = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(work, "backup_before.json")));
                if (!(bool)backup["Verified"] || (string)backup["Database"] != db.Database) throw new Exception("Verified full backup is required.");
                var proof = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(work, "preflight.json")));
                if ((string)proof["MatBeforeHash"] != oldMat || (string)proof["InvBeforeHash"] != oldInv) throw new Exception("MAT/INV changed since preflight; no data deleted.");
                var saved = new DataSet("BeforeMatInvReplacement");
                foreach (string table in new[] { "mat", "inv", "material_document", "app_settings", "document_counter" }) { var data = Read("SELECT * FROM " + Name(table)); data.TableName = table; saved.Tables.Add(data); }
                saved.WriteXml(Path.Combine(work, "before_tables.xml"), XmlWriteMode.WriteSchema);
                using (transaction = db.BeginTransaction(IsolationLevel.Serializable)) {
                    try {
                        Execute("SET XACT_ABORT ON; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Materijalno:ledger',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Ledger is busy',1;");
                        if (TableHash("mat") != oldMat || TableHash("inv") != oldInv) throw new Exception("Concurrent ledger changes; import cancelled.");
                        CheckSource(); Bulk(documents); Execute("DELETE FROM dbo.mat; DELETE FROM dbo.inv;"); Bulk(mat); Bulk(inventory);
                        Verify(mat); Verify(inventory);
                        foreach (var item in before) if (TableHash(item.Key) != item.Value) throw new Exception("Protected table changed: " + item.Key);
                        var afterDocs = Read("SELECT * FROM dbo.material_document"); var originalIds = new HashSet<Guid>(oldDocs.AsEnumerable().Select(r => (Guid)r["id"]));
                        var retained = afterDocs.Clone(); foreach (DataRow row in afterDocs.Rows) if (originalIds.Contains((Guid)row["id"])) retained.ImportRow(row);
                        if (TableHash(retained) != TableHash(oldDocs) || afterDocs.Rows.Count != oldDocs.Rows.Count + documents.Rows.Count) throw new Exception("Old document metadata changed.");
                        if (SchemaHash() != originalSchema || Read("SELECT name FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1").Rows.Count != 0) throw new Exception("Schema or FK trust changed.");
                        foreach (string table in new[] { "mat", "inv" }) Execute("DBCC CHECKIDENT ('dbo." + table + "', RESEED, " + fox[table].Records.Count + ") WITH NO_INFOMSGS");
                        CheckSource(); transaction.Commit(); transaction = null;
                    } catch { if (transaction != null) { transaction.Rollback(); transaction = null; } throw; }
                }
                var after = new DataSet("AfterMatInvReplacement"); foreach (string table in new[] { "mat", "inv" }) { var data = Read("SELECT * FROM " + Name(table)); data.TableName = table; after.Tables.Add(data); }
                after.WriteXml(Path.Combine(work, "after_tables.xml"), XmlWriteMode.WriteSchema);
                File.WriteAllText(Path.Combine(work, "import_result.json"), Json.Serialize(new { Applied = true, Passed = true, Database = db.Database, FieldsChecked = checkedFields, Mat = Summary(mat), Inv = Summary(inventory), SourceUnchanged = true, SchemaUnchanged = true, ProtectedTablesUnchanged = true, OldDocumentsRetained = true, OracleUsed = false, ActiveBookYearUnchanged = true }), new UTF8Encoding(false));
                Console.WriteLine("COMMITTED MAT/INV archive replacement. Checked " + checkedFields + " fields; other data and active year unchanged; Oracle unused.");
            }
            return 0;
        } catch (Exception error) { Console.WriteLine(error.GetType().Name + ": " + error.Message); return 1; }
    }
}
