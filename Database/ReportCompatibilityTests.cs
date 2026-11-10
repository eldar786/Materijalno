using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.WinForms;

public static class ReportCompatibilityTests
{
    static string Folder;
    static int Checks;
    static void Check(string title,bool result){if(!result)throw new Exception(title);Console.WriteLine("PASS "+title);Checks++;}
    [STAThread] public static int Main(string[] args)
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        try{
            Folder=args[0];Directory.CreateDirectory(Folder);
            System.Threading.Thread.CurrentThread.CurrentCulture=CultureInfo.GetCultureInfo("bs-Latn-BA");
            var main=new GlavniViewModel();main.OtvoriAnalitickaKartica();
            var opened=(AnalitickaKarticaViewModel)main.OdabraniVM;
            var diary=new DnevnikViewModel(main);
            Check("Actual menu constructors have complete report periods",opened.CurrentItemMat?.Datun!=null&&opened.CurrentItemMat.Datnar!=null&&opened.PrintCommand!=null&&diary.CurrentItemMat?.Datun!=null&&diary.CurrentItemMat.Datnar!=null);
            var screen=new Materijalno.UI.AnalitickaKartica{DataContext=opened};
            screen.Measure(new Size(1600,900));screen.Arrange(new Rect(0,0,1600,900));
            Render("Analiticka_iz_menija",new Materijalno.UI.Izvjestaji.AnalitickaKarticaIzvjestaj(opened));
            using(var db=new materijalno_knjigovodstvoContext()){
                var old=db.Mat.AsNoTracking().First(m=>m.Status=="P"&&m.Kljnaz==1000&&m.Datnar==null);
                var filter=new Mat{Ident=old.Ident,Kljnaz=1000,Datun=new DateTime(2026,1,1),Datnar=new DateTime(2026,11,4)};
                var table=Render("Analiticka_stari_prazni_datumi",new Materijalno.UI.Izvjestaji.AnalitickaKarticaIzvjestaj(new AnalitickaKarticaViewModel{CurrentItemMat=filter}));
                Check("Original opening row with empty document date renders",table.Rows.Count>0&&table.Rows.Cast<DataRow>().Any(r=>r["Datnar"]==DBNull.Value));
                var defaults=Render("Analiticka_prazan_period",new Materijalno.UI.Izvjestaji.AnalitickaKarticaIzvjestaj(new AnalitickaKarticaViewModel{CurrentItemMat=new Mat{Ident=old.Ident,Kljnaz=1000}}));
                Check("Empty filter dates use defaults without nullable exception",defaults!=null);
                var groups=db.Mat.AsNoTracking().Where(m=>m.Ident==990201||m.Ident==990202).Select(m=>new{m.Ident,m.Kljnaz}).Distinct().ToList();
                foreach(var group in groups){
                    var journal=Render("Dnevnik_"+group.Ident+"_"+group.Kljnaz,new Materijalno.UI.Izvjestaji.DnevnikIzvjestaj(new DnevnikViewModel{CurrentItemMat=new Mat{Ident=group.Ident,Kljnaz=group.Kljnaz,Datun=new DateTime(2026,11,1),Datnar=new DateTime(2026,11,4)}}));
                    Check("Journal transfer side retained "+group.Ident+"/"+group.Kljnaz,journal.Columns.Contains("Medus")&&journal.Rows.Count>0);
                }
                var existing=db.Inv.AsNoTracking().First(i=>i.Kljnaz==1000);
                var warehouses=db.SifarnikSkladista.AsNoTracking().Select(w=>w.Kljnaz).OrderBy(w=>w).ToList();
                foreach(int warehouse in warehouses) {
                    bool hasInventory=db.Inv.Any(i=>i.Kljnaz==warehouse&&!i.IsDraft);
                    bool hasBook=db.Mat.Any(i=>i.Kljnaz==warehouse);
                    var selection=new Inv{Kljnaz=warehouse,InventorySessionId=existing.InventorySessionId,InventoryDate=existing.InventoryDate};
                    try {
                        var differences=Render("InventurneZalihe_"+warehouse,new Materijalno.UI.Izvjestaji.StampanjeInventurnihZalihaIzvjestaj(new StampanjeInventurnihZalihaViewModel{CurrentItemInv=selection}));
                        Check("Inventory warehouse "+warehouse+" selects its own snapshot or zero count",(hasInventory||hasBook)&&differences.Rows.Count>0&&(hasInventory||differences.Rows.Cast<DataRow>().All(r=>Convert.ToDecimal(r["InvKolic"])==0&&Convert.ToDecimal(r["InvVrijed"])==0)));
                    } catch(InventoryReportUnavailableException ex) {
                        Check("Inventory warehouse "+warehouse+" has no book rows or count",!hasInventory&&!hasBook&&ex.Message.Contains(warehouse.ToString()));
                    }
                }
                foreach(int warehouse in new[]{1000,1001,1002})
                    Render("InventurniListici_"+warehouse,new Materijalno.UI.Izvjestaji.StampanjeInventurnihListicaIzvjestaj(new StampanjeInventurnihListicaViewModel{CurrentItemMat=new Mat{Kljnaz=warehouse,Datun=new DateTime(2026,1,1)}}));
            }
            // Isolated temporary count verifies switching from the existing
            // warehouse's session to another warehouse with its own snapshot.
            var catalog=new System.Data.SqlClient.SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
            if(catalog!="materijalno_fox_test_20261104_215755")throw new Exception("Wrong test catalog");
            var inventoryService=new InventoryService();Inv temporary=null;
            try {
                temporary=inventoryService.Save(new Inv{Kljnaz=1001,Ident=990201,Datun=new DateTime(2026,11,4),Kolic=1,Redbr=1});
                using(var db=new materijalno_knjigovodstvoContext()){
                    var source=db.Inv.AsNoTracking().First(i=>i.Kljnaz==1000);
                    var changed=new Inv{Kljnaz=1001,InventorySessionId=source.InventorySessionId,InventoryDate=new DateTime(2000,1,1)};
                    var differences=Render("InventurneZalihe_promjena_skladista",new Materijalno.UI.Izvjestaji.StampanjeInventurnihZalihaIzvjestaj(new StampanjeInventurnihZalihaViewModel{CurrentItemInv=changed}));
                    Check("Stale source session resolves target warehouse snapshot",differences.Rows.Cast<DataRow>().All(r=>Convert.ToInt32(r["Kljnaz"])==1001)&&Convert.ToDecimal(differences.Rows.Cast<DataRow>().Single(r=>Convert.ToInt32(r["Ident"])==990201)["InvKolic"])==1);
                }
            }finally{if(temporary!=null)inventoryService.Delete(temporary);}
            Console.WriteLine("ALL PASSED "+Checks);return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}finally{app.Shutdown();}
    }
    static DataTable Render(string name,Window window)
    {
        try{
            var fields=window.GetType().GetFields(BindingFlags.NonPublic|BindingFlags.Instance);
            var table=(DataTable)fields.Single(f=>f.Name=="reportDt").GetValue(window);
            var viewer=(ReportViewer)fields.Single(f=>f.FieldType==typeof(ReportViewer)).GetValue(window);
            foreach(string format in new[]{"PDF","EXCELOPENXML"}){
                string mime,encoding,extension;string[] streams;Warning[] warnings;
                byte[] data=viewer.LocalReport.Render(format,null,out mime,out encoding,out extension,out streams,out warnings);
                if(warnings.Length>0)throw new Exception(name+": "+string.Join(";",warnings.Select(w=>w.Message)));
                File.WriteAllBytes(Path.Combine(Folder,name+(format=="PDF"?".pdf":".xlsx")),data);
            }
            table.WriteXml(Path.Combine(Folder,name+".xml"),XmlWriteMode.WriteSchema);return table;
        }finally{window.Close();}
    }
}
