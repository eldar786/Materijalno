using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;

public static class PrometAfterOpeningChecks
{
    static string work; static int checks; static Fx fox;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly List<object> cases = new List<object>();
    static string Cs => DatabaseConfiguration.MaterialConnectionString;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Save(string file, object value) => File.WriteAllText(Path.Combine(work, file), new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(value), new UTF8Encoding(false));
    static DataTable Read(DbConnection c, DbTransaction tx, string sql)
    {
        using (var command = c.CreateCommand()) { command.Transaction = tx; command.CommandText = sql; command.CommandTimeout = 90; using (var reader = command.ExecuteReader()) { var table = new DataTable(); table.Load(reader); return table; } }
    }
    static void Execute(DbConnection c, DbTransaction tx, string sql) { using (var command = c.CreateCommand()) { command.Transaction = tx; command.CommandText = sql; command.ExecuteNonQuery(); } }
    static Dictionary<string, string> Hashes(DbConnection c = null, DbTransaction tx = null)
    {
        if (c == null) using (var db = new materijalno_knjigovodstvoContext()) { var connection = db.Database.GetDbConnection(); connection.Open(); return Hashes(connection); }
        var result = new Dictionary<string, string>();
        foreach (var table in new[] { "mat", "inv", "nalmat", "material_document", "document_counter", "app_settings", "financial_batch", "financial_export_log", "year_close", "tabela_materijala", "sifarnik_skladista", "sifarnik_konta", "sifarnik_materijal_skladiste_konto" }) {
            string order = table == "document_counter" ? "name,book_year" : table == "app_settings" ? "key_name" : "id";
            object value = Read(c, tx, "SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.[" + table + "] ORDER BY " + order + " FOR XML RAW,BINARY BASE64))").Rows[0][0];
            using (var sha = SHA256.Create()) result[table] = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value == DBNull.Value ? "" : (string)value))).Replace("-", "");
        }
        return result;
    }
    static bool Same(Dictionary<string, string> a, Dictionary<string, string> b) => a.All(p => b[p.Key] == p.Value);
    static Func<materijalno_knjigovodstvoContext> Factory(DbConnection c, DbTransaction tx) => () => {
        var db = new materijalno_knjigovodstvoContext(new DbContextOptionsBuilder<materijalno_knjigovodstvoContext>().UseSqlServer(c).Options);
        db.Database.UseTransaction(tx); return db;
    };
    static List<Mat> Rows(DbConnection c, DbTransaction tx) { using (var db = Factory(c, tx)()) return db.Mat.AsNoTracking().OrderBy(m => m.Id).ToList(); }
    static Mat Row(DbConnection c, DbTransaction tx, int id) { using (var db = Factory(c, tx)()) return db.Mat.AsNoTracking().Single(m => m.Id == id); }
    static void Scenario(string name, Action<DbConnection, DbTransaction, MaterialPostingService> test)
    {
        var before = Hashes();
        using (var root = new materijalno_knjigovodstvoContext()) {
            var c = root.Database.GetDbConnection(); c.Open(); using (var tx = c.BeginTransaction(IsolationLevel.Serializable)) {
                try { fox.Reset(Rows(c, tx)); test(c, tx, new MaterialPostingService(Factory(c, tx))); }
                finally { tx.Rollback(); }
            }
        }
        Check(Same(before, Hashes()), "All SQL rows/row versions/counters unchanged after " + name);
        Console.WriteLine("PASS " + name);
    }
    sealed class Fx : IDisposable
    {
        dynamic app;
        static string Q(string value) => "'" + value.Replace("'", "''") + "'";
        static string N(decimal value) => value.ToString("0.########################", Inv);
        public Fx()
        {
            app = Activator.CreateInstance(Type.GetTypeFromProgID("VisualFoxPro.Application.9")); app.Visible = false;
            app.DoCmd("SET RESOURCE OFF"); app.DoCmd("SET SAFETY OFF"); app.DoCmd("SET DEFAULT TO " + Q(work));
            app.DoCmd("PUBLIC f_ident,f_wh,f_qty,f_value,f_count");
        }
        public void Reset(IEnumerable<Mat> opening)
        {
            if ((bool)app.Eval("USED('fxmat')")) app.DoCmd("USE IN fxmat");
            app.DoCmd("CREATE CURSOR fxmat (kljnaz C(4),ident C(9),brfak C(15),kolic N(12,2),nc N(17,9),vrijed N(11,2),status C(1),sqlid I)");
            app.DoCmd("INDEX ON brfak TAG brfak");
            foreach (var row in opening) Add(row, row.Kolic ?? 0, row.Nc ?? 0, row.Vrijed ?? 0);
        }
        public void Add(Mat row, decimal quantity, decimal nc, decimal value)
        {
            app.DoCmd("INSERT INTO fxmat VALUES (" + Q(row.Kljnaz.Value.ToString(Inv)) + "," + Q(row.Ident) + "," + Q(row.Brfak ?? "") + "," + N(quantity) + "," + N(nc) + "," + N(value) + "," + Q(row.Status) + "," + row.Id + ")");
        }
        public void Replace(Mat row, decimal quantity, decimal nc, decimal value)
        {
            app.DoCmd("SELECT fxmat"); app.DoCmd("SET FILTER TO"); app.DoCmd("LOCATE FOR sqlid=" + row.Id);
            Check((bool)app.Eval("FOUND()"), "Fox memory row found for update");
            app.DoCmd("REPLACE kolic WITH " + N(quantity) + ",nc WITH " + N(nc) + ",vrijed WITH " + N(value));
        }
        void Filter(string code, int warehouse)
        {
            app.DoCmd("SELECT fxmat"); app.DoCmd("SET FILTER TO");
            app.DoCmd("f_ident=PADR(" + Q(code) + ",9)"); app.DoCmd("f_wh=PADR(" + Q(warehouse.ToString(Inv)) + ",4)");
        }
        decimal Eval(string text) => Convert.ToDecimal(app.Eval(text), Inv);
        public StockBalance Balance(string code, int warehouse)
        {
            Filter(code, warehouse); app.DoCmd("SUM kolic TO f_qty FOR ident=f_ident AND kljnaz=f_wh"); app.DoCmd("SUM vrijed TO f_value FOR ident=f_ident AND kljnaz=f_wh");
            return new StockBalance { Quantity = Eval("ROUND(f_qty,2)"), Value = Eval("ROUND(f_value,2)") };
        }
        public decimal Price(StockBalance stock) => stock.Quantity > 0 && stock.Value > 0 ? Eval("ROUND(" + N(stock.Value) + "/" + N(stock.Quantity) + ",9)") : 0;
        public decimal Value(decimal quantity, decimal nc) => Eval("ROUND(" + N(quantity) + "*" + N(nc) + ",2)");
        public decimal Receipt(decimal invoice, decimal customs, decimal tax, decimal other, decimal freight, decimal quantity) => Eval("ROUND((" + N(invoice) + "+" + N(customs) + "+" + N(tax) + "+" + N(other) + "+" + N(freight) + ")/" + N(quantity) + ",9)");
        public decimal? ReturnPrice(string code, int warehouse)
        {
            Filter(code, warehouse); app.DoCmd("COUNT TO f_count FOR ident=f_ident AND kljnaz=f_wh"); if (Eval("f_count") == 0) return null;
            app.DoCmd("SET ORDER TO TAG brfak"); app.DoCmd("SET FILTER TO ident=f_ident AND kljnaz=f_wh"); app.DoCmd("GO BOTTOM"); return Eval("nc");
        }
        public void Dispose()
        {
            object instance = (object)app;
            if (instance == null) return;
            try { app.Quit(); }
            finally { try { Marshal.FinalReleaseComObject(instance); } catch (COMException) { } app = null; }
        }
    }
    static Mat New(MaterialPostingService service, string status, string code, int warehouse, decimal quantity, int day)
    {
        return new Mat { Status = status, Ident = code, Kljnaz = warehouse, Kolic = quantity, Datun = new DateTime(2026, 2, day), Datnar = new DateTime(2026, 2, day), Brfak = service.ReserveDocumentNumber(status, 2026), Brdok = "TEMP NC TEST", Redbr = 1 };
    }
    static void CheckBalance(MaterialPostingService service, string code, int warehouse)
    {
        var expected = fox.Balance(code, warehouse); var actual = service.Balance(code, warehouse);
        Check(expected.Quantity == actual.Quantity && expected.Value == actual.Value, "Fox/SQL signed balance " + warehouse + "/" + code);
        if (actual.Quantity > 0 && actual.Value > 0) Check(fox.Price(expected) == actual.UnitCost, "Fox/SQL weighted NC " + warehouse + "/" + code);
    }
    static void Result(string status, Mat row, decimal expectedNC, decimal expectedValue)
    {
        Check(row.Nc == expectedNC && row.Vrijed == expectedValue, "Saved " + status + " NC/value agrees with native Fox calculation");
        cases.Add(new { Status = status, row.Ident, row.Kljnaz, row.Brfak, row.Kolic, NC = row.Nc, Value = row.Vrijed, FoxNC = expectedNC, FoxValue = expectedValue });
    }
    static Mat Receipt(DbConnection c, DbTransaction tx, MaterialPostingService service, string code, decimal quantity, decimal invoice, int day)
    {
        decimal customs = 3.45m, tax = 17.23m, other = 2.34m, freight = 1.11m;
        var input = New(service, "U", code, 1000, quantity, day); input.Fvrijed = invoice.ToString(Inv); input.Cartro = customs.ToString(Inv); input.Zavtro = other.ToString(Inv); input.Porppp = tax; input.Trospe = freight;
        decimal expectedNC = fox.Receipt(invoice, customs, tax, other, freight, quantity), expectedValue = invoice + customs + tax + other + freight;
        var row = service.Save(input); row = Row(c, tx, row.Id); Result("U", row, expectedNC, expectedValue);
        Check(row.Fcj == LedgerMath.Price(invoice / quantity).ToString("0.#########", Inv), "Receipt invoice unit price and NC remain distinct");
        fox.Add(row, quantity, expectedNC, expectedValue); CheckBalance(service, code, 1000); return row;
    }
    static Mat Issue(DbConnection c, DbTransaction tx, MaterialPostingService service, string code, int warehouse, decimal quantity, int day)
    {
        var balance = fox.Balance(code, warehouse); decimal expectedNC = fox.Price(balance), expectedValue = -fox.Value(quantity, expectedNC);
        var row = service.Save(New(service, "I", code, warehouse, quantity, day)); row = Row(c, tx, row.Id); Result("I", row, expectedNC, expectedValue);
        Check(row.Kolic == -quantity, "Issue saved with negative quantity"); fox.Add(row, -quantity, expectedNC, expectedValue); CheckBalance(service, code, warehouse); return row;
    }
    static Mat Transfer(DbConnection c, DbTransaction tx, MaterialPostingService service, string code, int from, int to, decimal quantity, int day)
    {
        var balance = fox.Balance(code, from); decimal expectedNC = fox.Price(balance), expectedValue = fox.Value(quantity, expectedNC);
        var input = New(service, "M", code, from, quantity, day); input.Kljnaz1 = to;
        var saved = service.SaveTransfer(input);
        using (var db = Factory(c, tx)()) {
            var pair = db.Mat.AsNoTracking().Where(m => m.TransferPairId == saved.TransferPairId).OrderBy(m => m.Id).ToList();
            Check(pair.Count == 2 && pair.Sum(m => m.Kolic) == 0 && pair.Sum(m => m.Vrijed) == 0, "Transfer pair balances quantity and value");
            foreach (var row in pair) { int sign = row.Kljnaz == from ? -1 : 1; Result("M", row, expectedNC, sign * expectedValue); fox.Add(row, sign * quantity, expectedNC, sign * expectedValue); }
        }
        CheckBalance(service, code, from); CheckBalance(service, code, to); return saved;
    }
    static Mat Return(DbConnection c, DbTransaction tx, MaterialPostingService service, string code, int warehouse, decimal quantity, int day)
    {
        decimal? price = fox.ReturnPrice(code, warehouse); Check(price.HasValue && price > 0, "Fox return reference price is positive");
        Check(service.ReturnPrice(code, warehouse) == price, "Return selection agrees with Fox BRFAK index");
        decimal expectedValue = fox.Value(quantity, price.Value); var row = service.Save(New(service, "V", code, warehouse, quantity, day)); row = Row(c, tx, row.Id);
        Result("V", row, price.Value, expectedValue); Check(row.Kolic == quantity, "Return saved with positive quantity");
        fox.Add(row, quantity, price.Value, expectedValue); CheckBalance(service, code, warehouse); return row;
    }
    static void UiPreviews()
    {
        foreach (string code in new[] { "13", "2", "087001" }) {
            var input = new Mat { Ident = code, Kljnaz = 1000, Kolic = 37, Fvrijed = "123.45", Cartro = "3.45", Zavtro = "2.34", Porppp = 17.23m, Trospe = 1.11m };
            // Invoke the calculation alone; this avoids the unrelated partner/Oracle lookup in the receipt screen constructor.
            var receipt = (UlazMaterijalaViewModel)FormatterServices.GetUninitializedObject(typeof(UlazMaterijalaViewModel)); receipt.CurrentItemMat = input; receipt.NabavnaCijena();
            Check(input.Nc == fox.Receipt(123.45m, 3.45m, 17.23m, 2.34m, 1.11m, 37) && input.Vrijed == 147.58m, "Receipt preview matches Fox " + code);
            var source = new MaterialPostingService().Balance(code, 1000); decimal nc = fox.Price(source);
            var issue = new IzlazMaterijalaViewModel { CurrentItemMat = new Mat { Ident = code, Kljnaz = 1000, Kolic = 3 } }; issue.NabavnaCijena();
            Check(issue.CurrentItemMat.Nc == nc && issue.CurrentItemMat.Vrijed == -fox.Value(3, nc), "Issue preview matches Fox " + code);
            var transfer = new MedjuskladisnicaViewModel { CurrentItemMat = new Mat { Ident = code, Kljnaz = 1000, Kljnaz1 = 1001, Kolic = 3 } }; transfer.NabavnaCijena();
            Check(transfer.CurrentItemMat.Nc == nc && transfer.CurrentItemMat.Vrijed == fox.Value(3, nc), "Transfer preview matches Fox " + code);
            var returned = new PovratMaterijalaViewModel { CurrentItemMat = new Mat { Ident = code, Kljnaz = 1000, Kolic = 3 } }; returned.NabavnaCijena();
            var last = new MaterialPostingService().ReturnPrice(code, 1000);
            Check(returned.CurrentItemMat.Nc == last && returned.CurrentItemMat.Vrijed == fox.Value(3, last.Value), "Return preview matches Fox " + code);
        }
    }
    [STAThread] public static int Main(string[] args)
    {
        work = args[0]; bool passed = false; string error = null; Dictionary<string, string> before = null;
        try {
            Check(Process.GetProcessesByName("Materijalno.UI").Length == 0, "User application closed");
            var target = new System.Data.SqlClient.SqlConnectionStringBuilder(Cs);
            Check(target.DataSource == "192.168.1.213" && target.InitialCatalog == "materijalno_fox_20261105_102038", "Current database only");
            before = Hashes(); Save("hashes_before.json", before);
            using (fox = new Fx()) {
                using (var db = new materijalno_knjigovodstvoContext()) {
                    var opening = db.Mat.AsNoTracking().OrderBy(m => m.Id).ToList(); Check(opening.Count == 218 && opening.All(m => m.Status == "P" && m.BookYear == 2026), "Only new 2026 opening state exists"); fox.Reset(opening);
                }
                if (args.Length < 2 || args[1] != "edge-only") {
                UiPreviews();
                foreach (string code in new[] { "13", "2", "087001" }) Scenario("U_I_M_V_sequence_" + code, (c, tx, service) => {
                    Receipt(c, tx, service, code, 37, 123.45m, 1);
                    var firstIssue = Issue(c, tx, service, code, 1000, 13, 2);
                    var firstTransfer = Transfer(c, tx, service, code, 1000, 1001, 7, 3);
                    Return(c, tx, service, code, 1000, 5, 4);
                    Receipt(c, tx, service, code, 23, 89.12m, 5);
                    var stable = Row(c, tx, firstTransfer.Id); var resaved = service.SaveTransfer(stable); var reloaded = Row(c, tx, resaved.Id);
                    Check(stable.Nc == reloaded.Nc && stable.Vrijed == reloaded.Vrijed, "Unchanged transfer keeps its historical NC/value after later receipt");
                    stable = Row(c, tx, firstIssue.Id); reloaded = service.Save(stable);
                    Check(stable.Nc == reloaded.Nc && stable.Vrijed == reloaded.Vrijed, "Unchanged issue keeps historical NC/value after later receipt");
                    Issue(c, tx, service, code, 1000, 9.25m, 6);
                    Transfer(c, tx, service, code, 1001, 1000, 2.50m, 7);
                    Return(c, tx, service, code, 1001, 1.25m, 8);
                });
                Scenario("all_218_openings_full_issue_and_return", (c, tx, service) => {
                    foreach (var opening in Rows(c, tx).ToArray()) { Issue(c, tx, service, opening.Ident, opening.Kljnaz.Value, opening.Kolic.Value, 10); Return(c, tx, service, opening.Ident, opening.Kljnaz.Value, opening.Kolic.Value, 11); }
                });
                Scenario("insufficient_and_zero_quantity", (c, tx, service) => {
                    var stock = service.Balance("13", 1000); var original = Hashes(c, tx);
                    foreach (string status in new[] { "U", "I", "M", "V" }) {
                        var input = New(service, status, "13", 1000, 0, 12); input.Fvrijed = "10"; input.Kljnaz1 = 1001;
                        bool rejected = false; try { service.Save(input); } catch (InvalidOperationException) { rejected = true; }
                        Check(rejected, "Zero quantity rejected for " + status);
                    }
                    foreach (string status in new[] { "I", "M" }) {
                        var input = New(service, status, "13", 1000, stock.Quantity + 1, 12); input.Kljnaz1 = 1001;
                        bool rejected = false; try { service.Save(input); } catch (InvalidOperationException e) { rejected = e.Message.Contains("zalihe"); }
                        Check(rejected, "Insufficient stock rejected for " + status);
                    }
                    Check(Read(c, tx, "SELECT COUNT(*) FROM dbo.mat").Rows[0].Field<int>(0) == 218, "Rejected operations insert no rows");
                });
                }
                Scenario("return_without_positive_reference_price", (c, tx, service) => {
                    Execute(c, tx, "UPDATE dbo.mat SET kolic=0,nc=0,vrijed=0 WHERE ident='13' AND kljnaz=1000;");
                    var input = New(service, "V", "13", 1000, 1, 14); bool rejected = false;
                    try { service.Save(input); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "Return with reference NC zero is rejected like Fox IF nacij>0");
                });
            }
            passed = true;
        } catch (Exception exception) { error = exception.ToString(); Console.WriteLine(error); }
        finally {
            if (before != null) { var after = Hashes(); Save("hashes_after.json", after); if (!Same(before, after)) { error += "\nSQL data changed after rollback."; passed = false; } }
            Save("cases.json", cases); Save("result.json", new { Passed = passed, Checks = checks, TestCases = cases.Count, EveryScenarioRolledBack = true, SqlBackupCreated = false, OracleUsed = false, Error = error });
        }
        Console.WriteLine((passed ? "ALL PASSED " : "FAILED ") + checks + " checks; no test rows retained; no SQL backup."); return passed ? 0 : 1;
    }
}
