using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.WinForms;

public static class CostInventoryRegressionTests
{
    static int Checks;
    static void Check(string name, bool ok) { if (!ok) throw new Exception(name); Checks++; Console.WriteLine("PASS " + name); }
    static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    [STAThread] public static int Main(string[] args)
    {
        var app = new Application {ShutdownMode = ShutdownMode.OnExplicitShutdown};
        try {
            var catalog = new System.Data.SqlClient.SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
            if (catalog != args[0] || !catalog.StartsWith("materijalno_fox_")) throw new Exception("Wrong database");
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("bs-Latn-BA");
            Directory.CreateDirectory(args[1]);
            string before = Fingerprint();
            EdgeCases();
            foreach (var range in new[] {
                new {Name="Godina", From=new DateTime(2026,1,1), To=new DateTime(2026,11,5)},
                new {Name="PrviKvartal", From=new DateTime(2026,1,1), To=new DateTime(2026,3,31)},
                new {Name="TreciKvartal", From=new DateTime(2026,7,1), To=new DateTime(2026,9,30)} }) {
                var filter = new Mat {Datun=range.From, Datnar=range.To};
                Render("PoKontima_"+range.Name, new Materijalno.UI.Izvjestaji.PregledTroskovaPoKontimaIzvjestaj(new PregledTroskovaPoKontimaViewModel {CurrentItemMat=filter}),args[1]);
                Render("PoKontimaINalozima_"+range.Name, new Materijalno.UI.Izvjestaji.PregledTroskovaPoKontimaINalozimaZaSvaSkladistaIzvjestaj(new PregledTroskovaPoKontimaINalozimaZaSvaSkladistaViewModel {CurrentItemMat=filter}),args[1]);
            }
            var vm = new StampanjeInventurnihZalihaViewModel(new GlavniViewModel());
            var view = new Materijalno.UI.StampanjeInventurnihZaliha {DataContext=vm};
            var box = (TextBox)view.FindName("txtSkladiste"); Pump();
            foreach (int warehouse in new[] {1001,1002,1001}) {
                vm.CurrentItemInv.InventorySessionId = Guid.NewGuid();
                box.Text = warehouse.ToString(); box.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent)); Pump();
                Check("Warehouse survives text entry / lost focus " + warehouse, vm.CurrentItemInv.Kljnaz == warehouse && box.Text == warehouse.ToString());
                Check("Warehouse name resolves " + warehouse, vm.CurrentItemTabSkladista?.Kljnaz == warehouse);
                Check("Changing warehouse clears stale session " + warehouse, !vm.CurrentItemInv.InventorySessionId.HasValue);
                Render("InventurneZalihe_"+warehouse,new Materijalno.UI.Izvjestaji.StampanjeInventurnihZalihaIzvjestaj(vm),args[1]);
            }
            foreach (int warehouse in new[] {1000,1003})
                Render("InventurneZalihe_"+warehouse,new Materijalno.UI.Izvjestaji.StampanjeInventurnihZalihaIzvjestaj(new StampanjeInventurnihZalihaViewModel{CurrentItemInv=new Inv{Kljnaz=warehouse}}),args[1]);
            box.Text="9999";box.GetBindingExpression(TextBox.TextProperty).UpdateSource();box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));Pump();
            Check("Unknown warehouse does not select a different warehouse",vm.CurrentItemInv.Kljnaz==9999&&vm.CurrentItemTabSkladista==null);
            Check("Report validation did not write any data",before==Fingerprint());
            Console.WriteLine("ALL PASSED " + Checks + " checks; database=" + catalog);return 0;
        } catch(Exception e) { Console.WriteLine(e);return 1; } finally { app.Shutdown(); }
    }
    static string Fingerprint()
    {
        using(var db=new materijalno_knjigovodstvoContext()) {
            var mat=db.Mat.IgnoreQueryFilters().AsNoTracking().ToList();var inv=db.Inv.IgnoreQueryFilters().AsNoTracking().ToList();
            return mat.Count+"|"+mat.Sum(m=>m.Vrijed??0)+"|"+mat.Sum(m=>m.Kolic??0)+"|"+inv.Count+"|"+inv.Sum(i=>i.Vrijed)+"|"+inv.Sum(i=>i.Kolic??0)+"|"+db.MaterialDocuments.Count();
        }
    }
    static void EdgeCases()
    {
        var date=new DateTime(2026,1,1);
        var movements=new List<Mat> {
            new Mat {IsDraft=false,Ident=1,Kljnaz=1001,Konto1=500,Status="I",Datun=date,Datnar=null,Brfak="61-1",Vrijed=-100},
            new Mat {IsDraft=false,Ident=1,Kljnaz=1001,Konto1=500,Status="I",Datun=date,Datnar=null,Brfak="61-1",Vrijed=20},
            new Mat {IsDraft=false,Ident=1,Kljnaz=1001,Konto1=500,Status="V",Datun=date,Vrijed=70},
            new Mat {IsDraft=true,Ident=1,Kljnaz=1001,Konto1=500,Status="I",Datun=date,Vrijed=-7},
            new Mat {IsDraft=false,Ident=1,Kljnaz=1002,Konto1=500,Status="I",Datun=date,Datnar=null,Brfak="61-1",Vrijed=-30}
        };
        var materials=new[]{new TabelaMaterijala{Ident=1,Konto1=600}};
        var accounts=new[]{new SifarnikKonta{Sifkonta=600,Nazkont="Isti naziv"},new SifarnikKonta{Sifkonta=500,Nazkont="Isti naziv"}};
        var warehouses=new[]{new SifarnikSkladista{Kljnaz=1001,NazivOrg="Isti naziv"},new SifarnikSkladista{Kljnaz=1002,NazivOrg="Isti naziv"}};
        var byAccount=FoxCostReports.Build(movements,materials,accounts,warehouses,date,date,false);
        var one=byAccount.Rows.Cast<DataRow>().Single(r=>(int)r["Kljnaz"]==1001);
        Check("Fox account detail ABS(SUM) preserves issue corrections",(decimal)one["Vrijed"]==80&&(decimal)one["UkupnoVrijed"]==120&&(int)one["Konto1"]==600);
        Check("Separate warehouses and original Fox absolute totals",byAccount.Rows.Count==2&&byAccount.Rows.Cast<DataRow>().Sum(r=>(decimal)r["UkupnoVrijed"])==150);
        var documents=FoxCostReports.Build(movements,materials,accounts,warehouses,date,date,true);
        one=documents.Rows.Cast<DataRow>().Single();
        Check("Fox account/document consolidates warehouses, excludes returns/drafts and keeps corrections",(decimal)one["Vrijed"]==110&&(int)one["Konto1"]==600&&(int)one["Kljnaz"]==0&&one["DatumDokumenta"]==DBNull.Value);
    }
    static void Render(string name, Window window, string folder)
    {
        try {
            var fields=window.GetType().GetFields(BindingFlags.NonPublic|BindingFlags.Instance);
            var table=(DataTable)fields.Single(f=>f.Name=="reportDt").GetValue(window);
            var viewer=(ReportViewer)fields.Single(f=>f.FieldType==typeof(ReportViewer)).GetValue(window);
            foreach(string format in new[]{"PDF","EXCELOPENXML"}) {
                string mime,encoding,extension;string[] streams;Warning[] warnings;
                byte[] data=viewer.LocalReport.Render(format,null,out mime,out encoding,out extension,out streams,out warnings);
                if(warnings.Length>0)throw new Exception(string.Join(";",warnings.Select(w=>w.Message)));
                File.WriteAllBytes(Path.Combine(folder,name+(format=="PDF"?".pdf":".xlsx")),data);
            }
            table.WriteXml(Path.Combine(folder,name+".xml"),XmlWriteMode.WriteSchema);
            Check(name+" actual PDF / XLSX renders",table.Rows.Count>0);
        } finally {window.Close();}
    }
}
