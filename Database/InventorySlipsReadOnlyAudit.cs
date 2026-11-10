using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.Reporting.WinForms;

// Renders the installed application's inventory slips without writing ledger data.
public static class InventorySlipsReadOnlyAudit
{
    [STAThread] public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try {
            var catalog = new System.Data.SqlClient.SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
            if (catalog != args[0] || !catalog.StartsWith("materijalno_fox_")) throw new Exception("Unexpected database.");
            Directory.CreateDirectory(args[1]);
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("bs-Latn-BA");
            Console.WriteLine("Application: " + typeof(Materijalno.UI.Izvjestaji.StampanjeInventurnihListicaIzvjestaj).Assembly.Location);
            Console.WriteLine("Database: " + catalog);
            foreach (var warehouse in new[] {1000, 1001, 1002}) {
                var window = new Materijalno.UI.Izvjestaji.StampanjeInventurnihListicaIzvjestaj(new StampanjeInventurnihListicaViewModel {CurrentItemMat = new Mat {Kljnaz = warehouse}});
                try {
                    var fields = window.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                    var table = (DataTable)fields.Single(f => f.Name == "reportDt").GetValue(window);
                    var viewer = (ReportViewer)fields.Single(f => f.FieldType == typeof(ReportViewer)).GetValue(window);
                    var name = Path.Combine(args[1], "InventurniListici_" + warehouse);
                    foreach (var format in new[] {"PDF", "EXCELOPENXML"}) {
                        string mime, encoding, extension; string[] streams; Warning[] warnings;
                        var bytes = viewer.LocalReport.Render(format, null, out mime, out encoding, out extension, out streams, out warnings);
                        if (warnings.Length > 0) throw new Exception(string.Join("; ", warnings.Select(w => w.Message)));
                        File.WriteAllBytes(name + (format == "PDF" ? ".pdf" : ".xlsx"), bytes);
                    }
                    table.WriteXml(name + ".xml", XmlWriteMode.WriteSchema);
                    Console.WriteLine("PASS rendered warehouse " + warehouse + ": " + table.Rows.Count + " materials");
                } finally { window.Close(); }
            }
            return 0;
        } catch (Exception ex) { Console.WriteLine(ex); return 1; }
        finally { app.Shutdown(); }
    }
}
