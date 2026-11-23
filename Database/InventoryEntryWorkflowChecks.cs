using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Materijalno.Import;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.UI;
using Materijalno.UI.Izvjestaji;
using Materijalno.ViewModel;
using Materijalno.ViewModel.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.WinForms;

public static class InventoryEntryWorkflowChecks
{
    static string work; static int checks; static Window host;
    static UnosInventurnogStanja view; static UnosInventurnogStanjaViewModel vm;
    static HashSet<int> originalIds; static readonly Dictionary<int, Inv> owned = new Dictionary<int, Inv>();
    static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    static string Cs => DatabaseConfiguration.MaterialConnectionString;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
    static void Json(string file, object value) => File.WriteAllText(Path.Combine(work, file), new JavaScriptSerializer().Serialize(value));
    static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    static TextBox Box(string name) => (TextBox)view.FindName(name);
    static Button Button(string name) => (Button)view.FindName(name);
    static void Type(string name, string text) { var box = Box(name); Keyboard.Focus(box); box.SetCurrentValue(TextBox.TextProperty, text); box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource(); Pump(); }
    static void Enter(string name) { var box = Box(name); Keyboard.Focus(box); box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view), Environment.TickCount, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent }); Pump(); }
    static void FocusIs(string name) { Pump(); Check(ReferenceEquals(Keyboard.FocusedElement, view.FindName(name)), "Keyboard focus " + name); }
    static void Track(Inv item) { if (item != null && item.Id > 0 && originalIds != null && !originalIds.Contains(item.Id)) { owned[item.Id] = item; Json("owned_rows.json", owned.Select(p => new { Id = p.Key, p.Value.Ident, p.Value.Kljnaz, p.Value.InventorySessionId }).ToArray()); } }
    static Inv Read(int id) { using (var db = new materijalno_knjigovodstvoContext()) return db.Inv.IgnoreQueryFilters().AsNoTracking().SingleOrDefault(i => i.Id == id); }
    static int Count() { using (var db = new materijalno_knjigovodstvoContext()) return db.Inv.IgnoreQueryFilters().Count(); }
    static decimal Total(int warehouse) { using (var db = new materijalno_knjigovodstvoContext()) return db.Inv.Where(i => i.Status == "P" && i.Kljnaz == warehouse).Select(i => (decimal?)i.Vrijed).Sum() ?? 0; }
    static void Enabled(params string[] expected)
    {
        var names = new[] { "btnPrvi", "btnZadnji", "btnTrazi", "btnNovaKalkulacija", "btnSnimi", "btnStampa", "btnPrethodni", "btnSljedeci", "btnNovaStavka", "btnIzmjena", "btnSlijedecaStavka", "btnOdustani", "btnBrisanje", "btnIzlaz" };
        Pump(); foreach (string name in names) Check(Button(name).IsEnabled == expected.Contains(name), "Button " + name + " enabled=" + expected.Contains(name));
    }
    static void BrowseButtons() => Enabled("btnPrvi", "btnZadnji", "btnTrazi", "btnNovaKalkulacija", "btnStampa", "btnPrethodni", "btnSljedeci", "btnNovaStavka", "btnIzmjena", "btnBrisanje", "btnIzlaz");
    static Dictionary<string, string> Hashes()
    {
        var output = new Dictionary<string, string>(); using (var connection = new SqlConnection(Cs)) { connection.Open();
            foreach (string name in new[] { "mat", "inv", "material_document", "nalmat", "financial_batch", "financial_export_log", "document_counter", "app_settings", "tabela_materijala", "sifarnik_materijal_skladiste_konto" }) {
                string order = name == "app_settings" ? "key_name" : name == "document_counter" ? "name,book_year" : "id";
                using (var command = new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.[" + name + "] ORDER BY " + order + " FOR XML RAW,BINARY BASE64))", connection)) {
                    command.CommandTimeout = 120; object value = command.ExecuteScalar();
                    using (var sha = SHA256.Create()) output[name] = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value == DBNull.Value ? "" : (string)value))).Replace("-", "");
                }
            }
        } return output;
    }
    static void Cleanup()
    {
        Track(vm?.CurrentItemInv);
        using (var connection = new SqlConnection(Cs)) { connection.Open(); using (var tx = connection.BeginTransaction(IsolationLevel.Serializable)) {
            using (var command = new SqlCommand("SET XACT_ABORT ON; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Materijalno:ledger',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Ledger busy',1", connection, tx)) command.ExecuteNonQuery();
            int removed = 0;
            foreach (var pair in owned) using (var command = new SqlCommand("DELETE FROM dbo.inv WHERE id=@id AND fox_recno IS NULL AND ident=@ident AND kljnaz=@warehouse AND inventory_session_id=@session", connection, tx)) {
                command.Parameters.AddWithValue("@id", pair.Key); command.Parameters.AddWithValue("@ident", pair.Value.Ident); command.Parameters.AddWithValue("@warehouse", pair.Value.Kljnaz); command.Parameters.AddWithValue("@session", pair.Value.InventorySessionId); removed += command.ExecuteNonQuery();
            }
            tx.Commit(); Json("cleanup.json", new { Complete = true, OwnedIds = owned.Keys.ToArray(), RemainingTemporaryRowsRemoved = removed });
        } }
    }
    static void QuoteComparison(string snapshot)
    {
        var records = DbfReader.Read(Path.Combine(snapshot, "mat.dbf")).Records.Where(r => !r.Deleted && !string.IsNullOrWhiteSpace(r.Values["IDENT"]) && !string.IsNullOrWhiteSpace(r.Values["KLJNAZ"]));
        var results = new List<object>();
        foreach (var group in records.GroupBy(r => new { Ident = r.Values["IDENT"].Trim(), Warehouse = int.Parse(r.Values["KLJNAZ"].Trim()) })) {
            decimal quantity = group.Sum(r => decimal.Parse(r.Values["KOLIC"], CultureInfo.InvariantCulture));
            decimal value = group.Sum(r => decimal.Parse(r.Values["VRIJED"], CultureInfo.InvariantCulture));
            decimal expected = quantity > 0 && value > 0 ? Math.Round(value / quantity, 9, MidpointRounding.AwayFromZero) : 0;
            var input = new Inv { Ident = group.Key.Ident, Kljnaz = group.Key.Warehouse, InventoryDate = new DateTime(2026, 1, 1), Kolic = 1 };
            decimal actual = new InventoryService().Quote(input);
            Check(actual == expected, "Fox weighted NC " + group.Key.Warehouse + "/" + group.Key.Ident);
            results.Add(new { group.Key.Warehouse, group.Key.Ident, Quantity = quantity, Value = value, ExpectedNC = expected, ActualNC = actual });
        }
        Json("fox_quote_comparison.json", results);
    }
    static void Shot(string file)
    {
        view.UpdateLayout(); var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using (var stream = File.Create(Path.Combine(work, file))) encoder.Save(stream);
    }
    [STAThread] public static int Main(string[] args)
    {
        work = args[0]; bool passed = false, cleaned = false, restored = false; string error = null; Dictionary<string, string> before = null;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("bs-Latn-BA");
        try {
            Check(new SqlConnectionStringBuilder(Cs).InitialCatalog == "materijalno_fox_20261105_102038", "Current database only");
            Check(Process.GetProcessesByName("Materijalno.UI").Length == 0, "User application closed");
            Check((bool)new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(work, "backup_before.json")))["Verified"], "Verified backup before temporary saves");
            before = Hashes(); Json("hashes_before.json", before);
            using (var db = new materijalno_knjigovodstvoContext()) {
                originalIds = new HashSet<int>(db.Inv.IgnoreQueryFilters().Select(i => i.Id).ToList());
                foreach (var code in new[] { "13", "126", "129" }) Check(!db.Inv.IgnoreQueryFilters().Any(i => i.Kljnaz == 1000 && i.Ident == code), "Fixture absent before test 1000/" + code);
            }
            QuoteComparison(args[1]);
            vm = new UnosInventurnogStanjaViewModel(new GlavniViewModel());
            view = new UnosInventurnogStanja { DataContext = vm };
            host = new Window { Title = "Provjera inventurnog unosa", Content = view, Width = 1500, Height = 730 };
            host.Show(); host.Activate(); Pump(); BrowseButtons();
            Check(vm.CurrentItemInv.Kljnaz == 1000 && !Box("txtKolic").IsEnabled, "Fox Kljnazi browsing order and read-only fields");
            decimal baseline = Total(1000); int initialCount = Count();
            vm.NovaStavkaCommand.Execute(null); Pump(); FocusIs("txtIdent"); Enabled("btnSnimi", "btnOdustani");
            Check(Count() == initialCount && vm.CurrentItemInv.Id == 0, "Nova stavka does not persist an empty row");
            Check(!Box("OpBroj").IsEnabled, "Existing warehouse stays fixed for Nova stavka");
            int blocked = 0; var guard = new PrometNavigationGuard(message => blocked++); guard.TrackPage(vm);
            Check(!guard.TryNavigate(() => { }) && blocked == 1, "Left menu blocked while inventory entry is unsaved");
            Type("txtIdent", "13"); Enter("txtIdent"); FocusIs("txtKolic");
            decimal price = new InventoryService().Quote(new Inv { Ident = "13", Kljnaz = 1000, InventoryDate = new DateTime(2026, 1, 1) });
            Check(price > 0 && vm.CurrentItemInv.Nc == price, "1000/13 price comes from 2025 ledger for 2026 inventory");
            Type("txtKolic", "25"); decimal value = Math.Round(25 * price, 2, MidpointRounding.AwayFromZero);
            Check(vm.Vrijednost == value && Box("txtVrijed").Text == value.ToString("N2", German), "Value refreshes immediately while quantity is entered");
            Check(vm.UkupnaVrijednost == baseline + value, "Preview warehouse total includes current row once");
            Enter("txtKolic"); FocusIs("txtNc"); Check(Box("txtKolic").Text == "25,00", "Enter formats quantity N2");
            Enter("txtNc"); FocusIs("btnSnimi"); Shot("inventory_before_save.png");
            var saved = vm.SaveCurrentInventory(); Track(saved); Pump(); FocusIs("btnSlijedecaStavka"); Enabled("btnSlijedecaStavka", "btnOdustani");
            Check(Read(saved.Id).Nc == price && Read(saved.Id).Vrijed == value && Read(saved.Id).Datun == new DateTime(2026, 1, 1), "SQL saves weighted NC, value and Fox January 1 date");
            Check(guard.TryNavigate(() => { }), "Saved entry releases navigation guard");
            int fixtureId = saved.Id;
            vm.SlijStavkaButtonCommand.Execute(null); Pump(); FocusIs("txtIdent"); Enabled("btnSnimi", "btnOdustani");
            Check(vm.CurrentItemInv.Kljnaz == 1000 && vm.Kolicina == 0 && vm.NabavnaCijenaUnos == 0 && string.IsNullOrEmpty(vm.CurrentItemInv.Ident), "Slij. stavka retains warehouse and clears material, NC and quantity");
            Check(Count() == initialCount + 1, "Slij. stavka does not insert a draft");
            Type("txtIdent", "087001"); Enter("txtIdent"); Check(vm.CurrentItemInv.Ident == "087001" && vm.NabavnaCijenaUnos > 0, "Leading-zero material survives lookup and refreshes NC");
            vm.CancelCurrentInventory(); Pump(); FocusIs("btnIzmjena"); BrowseButtons();
            Check(vm.CurrentItemInv.Id == fixtureId && Read(fixtureId) != null, "Cancel next item keeps the saved previous row");
            var old = Read(fixtureId); vm.UpdateCommand.Execute(null); Pump(); FocusIs("txtKolic"); Enabled("btnSnimi", "btnOdustani");
            Check(Read(fixtureId).RowVersion.SequenceEqual(old.RowVersion), "Izmjena itself performs no save");
            Check(Box("txtKolic").SelectedText == Box("txtKolic").Text, "Editing selects the complete quantity");
            Type("txtKolic", "30"); Enter("txtKolic"); Type("txtNc", "1,234567891"); Enter("txtNc");
            Check(vm.Vrijednost == 37.04m, "Manual NC refreshes row value");
            saved = vm.SaveCurrentInventory(); Track(saved); Pump(); FocusIs("btnIzmjena"); BrowseButtons();
            Check(Read(fixtureId).Nc == 1.234567891m && Read(fixtureId).Vrijed == 37.04m, "Save preserves confirmed manual NC as Fox does");
            Check(vm.UkupnaVrijednost == baseline + 37.04m && Total(1000) == vm.UkupnaVrijednost, "Edited total matches fresh SQL without accumulation");
            old = Read(fixtureId); vm.BeginEdit(); Type("txtKolic", "200"); Type("txtNc", "10"); vm.CancelCurrentInventory(); Pump();
            Check(Read(fixtureId).RowVersion.SequenceEqual(old.RowVersion) && vm.Kolicina == 30 && vm.NabavnaCijenaUnos == old.Nc && vm.Vrijednost == old.Vrijed, "Odustani restores saved values without modifying SQL");
            vm.BeginNewItem(); Type("txtIdent", "2"); Enter("txtIdent"); Type("txtKolic", "1"); Enter("txtKolic");
            bool duplicate = false; try { vm.SaveCurrentInventory(); } catch (InvalidOperationException e) { duplicate = e.Message.Contains("već unesen"); }
            Check(duplicate && vm.HasUnsavedEntry && Count() == initialCount + 1, "Duplicate inventory is rejected and entry remains editable"); vm.CancelCurrentInventory();
            vm.BeginNewWarehouse(); Pump(); FocusIs("OpBroj"); Check(Count() == initialCount + 1, "Novo skl. does not insert a blank row");
            Type("OpBroj", "1000"); Enter("OpBroj"); FocusIs("txtIdent"); Type("txtIdent", "126"); Enter("txtIdent");
            Check(vm.NabavnaCijenaUnos == 0, "Fox zero stock produces NC zero without misleading history error");
            Type("txtKolic", "10"); Enter("txtKolic"); Type("txtNc", "0,173245000"); Enter("txtNc");
            var manual = vm.SaveCurrentInventory(); Track(manual); Check(Read(manual.Id).Nc == 0.173245m && Read(manual.Id).Vrijed == 1.73m, "Confirmed price saves a surplus with no stock"); vm.CancelCurrentInventory();
            vm.BeginNewItem(); Type("txtIdent", "129"); Enter("txtIdent"); Type("txtKolic", "0");
            int beforeZero = Count(); bool zeroRejected = false;
            try { vm.SaveCurrentInventory(); } catch (InvalidOperationException error) { zeroRejected = error.Message.Contains("veću od nule"); }
            Check(zeroRejected && Count() == beforeZero && vm.IsEntryEditable, "Fox zero count is rejected without saving or leaving entry mode");
            FocusIs("txtKolic"); vm.CancelCurrentInventory();
            vm.BeginNewItem(); Type("txtIdent", "13"); Enter("txtIdent"); Type("txtKolic", "abc");
            bool invalid = false; try { vm.SaveCurrentInventory(); } catch (InvalidOperationException) { invalid = true; }
            Check(invalid && Validation.GetHasError(Box("txtKolic")), "Invalid numeric text cannot save an old numeric value"); vm.CancelCurrentInventory();
            // Select the temporary row only, print it, then exercise real deletion.
            view.DataContext = vm = new UnosInventurnogStanjaViewModel(new GlavniViewModel(), Read(fixtureId)); Pump();
            vm.PrintCommand.Execute(null); Pump();
            var report = app.Windows.OfType<UnosInventurnogStanjaIzvjestaj>().Single();
            var reportTable = (DataTable)report.GetType().GetField("reportDt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(report);
            var line = reportTable.AsEnumerable().Single(r => (string)r["Ident"] == "13");
            Check((decimal)line["Nc"] == 1.234567891m && (decimal)line["Vrijed"] == 37.04m, "Print button opens report with saved NC and value");
            var viewer = (ReportViewer)report.GetType().GetField("_reportViewer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(report);
            string mime, encoding, extension; string[] streams; Warning[] warnings;
            File.WriteAllBytes(Path.Combine(work, "inventory_report.pdf"), viewer.LocalReport.Render("PDF", null, out mime, out encoding, out extension, out streams, out warnings));
            Check(warnings.Length == 0, "Inventory report renders successfully"); report.Close(); host.Activate();
            vm.DeleteCurrentInventory(); Pump(); Check(Read(fixtureId) == null && vm.CurrentItemInv.Id != fixtureId && vm.InvList.All(i => i.Id != fixtureId), "Brisanje refreshes selection and removes deleted row");
            Check(vm.UkupnaVrijednost == Total(vm.CurrentItemInv.Kljnaz.Value), "Total refreshes after deletion");
            Shot("inventory_after_delete.png"); passed = true;
            Json("case_1000_13.json", new { WeightedNC = price, TestQuantity = 25, TestValue = value, ConfirmedManualNC = 1.234567891m, Date = "2026-01-01", DeletedAfterTest = true });
        } catch (Exception exception) { error = exception.ToString(); Console.WriteLine(error); }
        finally {
            try { if (originalIds != null) { Cleanup(); cleaned = true; } } catch (Exception exception) { error += "\nCleanup: " + exception; passed = false; }
            if (before != null && cleaned) { var after = Hashes(); Json("hashes_after.json", after); restored = before.All(p => after[p.Key] == p.Value); if (!restored) { error += "\nDatabase fingerprints changed after cleanup."; passed = false; } }
            if (host != null) host.Close(); app.Shutdown();
            Json("workflow_result.json", new { Passed = passed, Checks = checks, Cleaned = cleaned, Error = error, OriginalInventoryRestored = restored, UserDeleted1000Ident13NotRestored = restored });
        }
        Console.WriteLine((passed ? "ALL PASSED " : "FAILED ") + checks + " checks; temporary fixtures removed; original data retained."); return passed ? 0 : 1;
    }
}
