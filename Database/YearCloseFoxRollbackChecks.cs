using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Materijalno.Import;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// No database backup or SQL row dumps. All preview/error tests ALWAYS roll back.
// The explicit apply mode stages the authorized 2025 MAT/INV and commits only
// after the real service result matches the actual Fox form's output field by field.
public static class YearCloseFoxRollbackChecks
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly string[] Fields = { "kljnaz", "ident", "datun", "datnar", "analst", "brdok", "brnar", "brfak", "kolic", "nc", "vrijed", "redbr", "status", "konto1", "konto2", "fcj", "troskovi", "cartro", "zavtro", "ppp", "tarifa", "fvrijed", "porppp", "kontosklad", "kontosklad1", "trospe", "ourst", "mjtst" };
    static readonly string[] Protected = { "financial_batch", "financial_export_log", "document_counter", "tabela_materijala", "sifarnik_skladista", "sifarnik_konta", "sifarnik_materijal_skladiste_konto" };
    static string work, source, reference; static int checks;
    static DbfTable sourceMat, sourceInv, foxMat;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    static string Cs => DatabaseConfiguration.MaterialConnectionString;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Save(string name, object value) => File.WriteAllText(Path.Combine(work, name), Json.Serialize(value), new UTF8Encoding(false));
    static DataTable Read(DbConnection connection, DbTransaction transaction, string sql)
    {
        using (var command = connection.CreateCommand()) { command.Transaction = transaction; command.CommandText = sql; command.CommandTimeout = 120; using (var reader = command.ExecuteReader()) { var table = new DataTable(); table.Load(reader); return table; } }
    }
    static void Execute(DbConnection connection, DbTransaction transaction, string sql)
    {
        using (var command = connection.CreateCommand()) { command.Transaction = transaction; command.CommandText = sql; command.CommandTimeout = 120; command.ExecuteNonQuery(); }
    }
    static int Count(DbConnection c, DbTransaction tx, string table) => Convert.ToInt32(Read(c, tx, "SELECT COUNT(*) FROM dbo.[" + table + "]").Rows[0][0]);
    static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    static Dictionary<string, string> Hashes(DbConnection connection = null, DbTransaction transaction = null)
    {
        if (connection == null) using (var db = new materijalno_knjigovodstvoContext()) { var c = db.Database.GetDbConnection(); c.Open(); return Hashes(c, null); }
        var output = new Dictionary<string, string>();
        foreach (string name in new[] { "mat", "inv", "nalmat", "material_document", "financial_batch", "financial_export_log", "document_counter", "app_settings", "year_close", "tabela_materijala", "sifarnik_skladista", "sifarnik_konta", "sifarnik_materijal_skladiste_konto" }) {
            string order = name == "app_settings" ? "key_name" : name == "document_counter" ? "name,book_year" : "id";
            object value = Read(connection, transaction, "SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.[" + name + "] ORDER BY " + order + " FOR XML RAW,BINARY BASE64))").Rows[0][0];
            output[name] = Hash(Encoding.UTF8.GetBytes(value == DBNull.Value ? "" : (string)value));
        }
        return output;
    }
    static bool Same(Dictionary<string, string> a, Dictionary<string, string> b) => a.All(p => b[p.Key] == p.Value);
    static void CheckOriginalFoxFiles()
    {
        var manifest = Json.Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(Path.Combine(work, "fox_source_manifest.json")));
        foreach (var item in manifest) Check(Hash(File.ReadAllBytes((string)item["Source"])) == (string)item["SHA256"], "Original Fox source unchanged: " + item["File"]);
    }
    static object ConvertField(string value, string column, Type type)
    {
        string text = (value ?? "").Trim();
        if (column == "ident") return text.Length == 0 ? null : (object)text;
        if (type == typeof(DateTime)) return text.Length == 0 ? null : (object)DateTime.ParseExact(text, "yyyyMMdd", Invariant);
        if (type == typeof(decimal)) return text.Length == 0 ? 0m : decimal.Parse(text, Invariant);
        if (type == typeof(int)) {
            if (text.Length == 0) return null;
            int number = checked((int)decimal.Parse(text, Invariant));
            return (column == "kljnaz" || column == "kljnaz1") && number <= 0 ? null : (object)number;
        }
        if (type == typeof(string)) return text;
        throw new Exception("Unsupported DBF conversion " + column);
    }
    static DataTable Map(DbConnection c, DbTransaction tx, string name, DbfTable fox)
    {
        var table = new DataTable(name);
        using (var command = c.CreateCommand()) { command.Transaction = tx; command.CommandText = "SELECT TOP(0) * FROM dbo.[" + name + "]"; using (var reader = command.ExecuteReader()) for (int i = 0; i < reader.FieldCount; i++) if (reader.GetName(i) != "row_version") table.Columns.Add(reader.GetName(i), reader.GetFieldType(i)); }
        foreach (var record in fox.Records.Where(r => !r.Deleted)) {
            var row = table.NewRow(); row["id"] = record.RecordNumber; row["fox_recno"] = record.RecordNumber;
            foreach (var field in fox.Fields) {
                string column = field.Name.Equals("KONTOSKLA2", StringComparison.OrdinalIgnoreCase) ? "kontosklad1" : field.Name.ToLowerInvariant();
                row[column] = ConvertField(record.Values[field.Name], column, table.Columns[column].DataType) ?? DBNull.Value;
            }
            bool draft = row.IsNull("ident") || row.IsNull("kljnaz"); row["is_draft"] = draft; row["is_archived"] = false;
            if (name == "mat") { row["book_year"] = row.IsNull("datun") ? 2025 : ((DateTime)row["datun"]).Year; row["line_token"] = Guid.NewGuid(); }
            else { row["inventory_date"] = row["datun"]; if (!draft && !row.IsNull("datun")) row["inventory_session_id"] = InventoryService.SessionId((int)row["kljnaz"], (DateTime)row["datun"]); }
            table.Rows.Add(row);
        }
        return table;
    }
    static void Bulk(DbConnection c, DbTransaction tx, DataTable table)
    {
        using (var bulk = new SqlBulkCopy((SqlConnection)c, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.TableLock, (SqlTransaction)tx)) {
            bulk.DestinationTableName = "dbo.[" + table.TableName + "]"; bulk.BulkCopyTimeout = 120;
            foreach (DataColumn column in table.Columns) bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            bulk.WriteToServer(table);
        }
    }
    static void Stage2025(DbConnection c, DbTransaction tx)
    {
        var mat = Map(c, tx, "mat", sourceMat); var inv = Map(c, tx, "inv", sourceInv);
        Execute(c, tx, "DELETE FROM dbo.mat; DELETE FROM dbo.inv;"); Bulk(c, tx, mat); Bulk(c, tx, inv);
        Check(Count(c, tx, "mat") == mat.Rows.Count && Count(c, tx, "inv") == inv.Rows.Count, "Source 2025 rows staged in existing tables");
        foreach (var expected in new[] { mat, inv }) {
            var actual = Read(c, tx, "SELECT " + string.Join(",", expected.Columns.Cast<DataColumn>().Select(col => "[" + col.ColumnName + "]")) + " FROM dbo.[" + expected.TableName + "] ORDER BY id");
            for (int i = 0; i < expected.Rows.Count; i++) foreach (DataColumn col in expected.Columns) Check(Equals(expected.Rows[i][col], actual.Rows[i][col.ColumnName]), "Staged DBF field " + expected.TableName + "." + col.ColumnName);
        }
    }
    static Func<materijalno_knjigovodstvoContext> Factory(DbConnection connection, DbTransaction transaction, DbCommandInterceptor interceptor = null)
    {
        return () => { var builder = new DbContextOptionsBuilder<materijalno_knjigovodstvoContext>(); builder.UseSqlServer(connection); if (interceptor != null) builder.AddInterceptors(interceptor); var db = new materijalno_knjigovodstvoContext(builder.Options); db.Database.UseTransaction(transaction); return db; };
    }
    static DataTable Business(DbConnection c, DbTransaction tx, string table) => Read(c, tx, "SELECT " + string.Join(",", Fields.Select(n => "[" + n + "]")) + " FROM dbo.[" + table + "] WHERE is_draft=0 AND is_archived=0 ORDER BY kljnaz,ident");
    static void CompareInvCopy(DataTable expected, DataTable actual)
    {
        Check(expected.Rows.Count == actual.Rows.Count, "All inventory business rows copied once");
        for (int i = 0; i < expected.Rows.Count; i++) foreach (string field in Fields) Check(Equals(expected.Rows[i][field], actual.Rows[i][field]), "Copied inventory field " + field);
    }
    static void CompareActualFox(DataTable sql)
    {
        var rows = foxMat.Records.Where(r => !r.Deleted && !string.IsNullOrWhiteSpace(r.Values["IDENT"]) && !string.IsNullOrWhiteSpace(r.Values["KLJNAZ"]))
            .OrderBy(r => r.Values["KLJNAZ"].Trim(), StringComparer.Ordinal).ThenBy(r => r.Values["IDENT"].Trim(), StringComparer.Ordinal).ToList();
        Check(sql.Rows.Count == rows.Count, "SQL business row count equals real Fox form result");
        for (int i = 0; i < rows.Count; i++) foreach (string field in Fields) {
            string foxName = field == "kontosklad1" ? "KONTOSKLA2" : field.ToUpperInvariant();
            object expected = ConvertField(rows[i].Values[foxName], field, sql.Columns[field].DataType) ?? DBNull.Value;
            Check(Equals(expected, sql.Rows[i][field]), "Actual Fox field " + field + " row " + i);
        }
    }
    sealed class FailCleanup : DbCommandInterceptor
    {
        public bool Reached;
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            if (command.CommandText.Contains("DELETE FROM dbo.inv")) { Reached = true; throw new InvalidOperationException("Injected cleanup failure"); }
            return result;
        }
    }
    static object Transfer(DbConnection c, DbTransaction tx, bool compareFox)
    {
        var expected = Business(c, tx, "inv"); var protectedBefore = Hashes(c, tx);
        int year = new YearCloseService(Factory(c, tx)).CloseFromInventory();
        var actual = Business(c, tx, "mat"); CompareInvCopy(expected, actual); if (compareFox) CompareActualFox(actual);
        Check(Count(c, tx, "mat") == expected.Rows.Count, "Physical MAT contains only new opening rows; no old or archived rows");
        Check(Count(c, tx, "inv") == 0, "Physical INV is empty, including old archived rows and placeholders");
        Check(Count(c, tx, "nalmat") == 0, "Physical NALMAT is empty");
        var protectedAfter = Hashes(c, tx); foreach (string name in Protected) Check(protectedBefore[name] == protectedAfter[name], "Protected metadata unchanged " + name);
        bool repeated = false; try { new YearCloseService(Factory(c, tx)).CloseFromInventory(); } catch (InvalidOperationException e) { repeated = e.Message.Contains("Već je formirano"); }
        Check(repeated && Same(protectedAfter, Hashes(c, tx)), "Repeated click cannot clear or duplicate new opening");
        return new { Year = year, MatRows = actual.Rows.Count, InvRows = Count(c, tx, "inv"), NalmatRows = Count(c, tx, "nalmat"), Quantity = actual.AsEnumerable().Sum(r => (decimal)r["kolic"]), Value = actual.AsEnumerable().Sum(r => (decimal)r["vrijed"]) };
    }
    static void Run(string variant, bool apply)
    {
        var before = Hashes(); bool committed = false; object summary = null;
        using (var root = new materijalno_knjigovodstvoContext()) {
            var c = root.Database.GetDbConnection(); c.Open(); using (var tx = c.BeginTransaction(IsolationLevel.Serializable)) {
                try {
                    Stage2025(c, tx);
                    if (variant == "cleanup_failure") {
                        var staged = Hashes(c, tx); var fail = new FailCleanup(); bool rejected = false;
                        try { new YearCloseService(Factory(c, tx, fail)).CloseFromInventory(); } catch (Exception e) { rejected = e.ToString().Contains("Injected cleanup failure"); }
                        Check(rejected && fail.Reached, "Failure reached final cleanup after successful MAT insertion");
                    } else {
                        if (variant == "zero") Execute(c, tx, "UPDATE dbo.inv SET kolic=0,nc=0.123456789,vrijed=0 WHERE id=(SELECT MIN(id) FROM dbo.inv WHERE is_draft=0); INSERT INTO dbo.nalmat(rostav,datnsta,sifakt,bronsta,sintstav,brdokst,kukistav,datdokst,datvalst) VALUES(1,'20250101',61,-1,5113000,'TEMP TEST',0,'20250101','');");
                        summary = Transfer(c, tx, variant != "zero");
                    }
                    CheckOriginalFoxFiles();
                    if (apply) { Check(variant == "actual", "Apply only actual source data"); tx.Commit(); committed = true; }
                } finally { if (!committed) tx.Rollback(); }
            }
        }
        if (!apply) Check(Same(before, Hashes()), "All SQL rows and row versions restored after " + variant);
        Save(variant + (apply ? "_applied.json" : "_rollback.json"), new { Passed = true, Applied = committed, Summary = summary, SqlBackupCreated = false });
        Console.WriteLine("PASS " + variant + (apply ? " COMMITTED" : " ROLLED BACK"));
    }
    public static int Main(string[] args)
    {
        try {
            work = args[0]; source = args[1]; reference = args[2]; bool apply = args.Length == 4 && args[3] == "apply-physical-Fox-year-close";
            Check(Process.GetProcessesByName("Materijalno.UI").Length == 0, "User application closed");
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(Cs);
            Check(builder.DataSource == "192.168.1.213" && builder.InitialCatalog == "materijalno_fox_20261105_102038", "Explicit existing database only");
            CheckOriginalFoxFiles(); sourceMat = DbfReader.Read(Path.Combine(source, "mat.dbf")); sourceInv = DbfReader.Read(Path.Combine(source, "inv.dbf")); foxMat = DbfReader.Read(Path.Combine(reference, "mat.dbf"));
            if (apply) {
                var proof = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(work, "physical_checks.json")));
                Check((bool)proof["Passed"] && !(bool)proof["Applied"], "Passing mandatory rollback rehearsal"); Run("actual", true);
            } else { Run("actual", false); Run("zero", false); Run("cleanup_failure", false); }
            Save(apply ? "physical_apply_result.json" : "physical_checks.json", new { Passed = true, Applied = apply, Checks = checks, SqlBackupCreated = false, OracleUsed = false, OriginalFoxFilesUnchanged = true });
            Console.WriteLine("ALL PASSED " + checks + " checks; SQL backup not created; Oracle unused."); return 0;
        } catch (Exception e) { Console.WriteLine(e); return 1; }
    }
}
