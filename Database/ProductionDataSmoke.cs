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
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.WinForms;

public static class ProductionDataSmoke
{
    static string Folder;
    [STAThread] public static int Main(string[] args)
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        try{
            string catalog=new System.Data.SqlClient.SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
            if(catalog!=args[0]||!catalog.StartsWith("materijalno_fox_2026"))throw new Exception("Wrong production copy.");
            Folder=Path.Combine(args[1],"read_only_reports");Directory.CreateDirectory(Folder);
            System.Threading.Thread.CurrentThread.CurrentCulture=CultureInfo.GetCultureInfo("bs-Latn-BA");
            decimal quantity,value;DateTime last;int year;
            using(var db=new materijalno_knjigovodstvoContext()){
                var rows=db.Mat.AsNoTracking().ToList();quantity=rows.Sum(r=>r.Kolic??0);value=rows.Sum(r=>r.Vrijed??0);last=rows.Max(r=>r.Datun).Value;year=last.Year;
                if(rows.Count!=3916||quantity!=13201650m||value!=1807828.77m||db.TabelaMaterijala.Count()!=175||db.Nalmat.Count()!=5544)throw new Exception("Production baseline differs.");
                if(db.Mat.Any(m=>m.Ident==990201||m.Ident==990202))throw new Exception("Test fixture found.");
            }
            var main=new GlavniViewModel();
            foreach(Type type in new[]{typeof(UlazMaterijalaViewModel),typeof(IzlazMaterijalaViewModel),typeof(MedjuskladisnicaViewModel),typeof(PovratMaterijalaViewModel)}){
                var vm=Activator.CreateInstance(type,new object[]{main});
                if(type.GetProperty("CurrentItemMat").GetValue(vm)==null)throw new Exception(type.Name+" empty initial view");
                Console.WriteLine("PASS production menu "+type.Name);
            }
            var filter=new Mat{Ident=13,Kljnaz=1000,Datun=new DateTime(year,1,1),Datnar=last};
            Render("Dnevnik_13_1000",new Materijalno.UI.Izvjestaji.DnevnikIzvjestaj(new DnevnikViewModel{CurrentItemMat=filter}));
            Render("Analiticka_13_1000",new Materijalno.UI.Izvjestaji.AnalitickaKarticaIzvjestaj(new AnalitickaKarticaViewModel{CurrentItemMat=filter}));
            Render("InventurniListici_1000",new Materijalno.UI.Izvjestaji.StampanjeInventurnihListicaIzvjestaj(new StampanjeInventurnihListicaViewModel{CurrentItemMat=filter}));
            var stock=Render("Stanje_svih_skladista",new Materijalno.UI.Izvjestaji.StanjeZalihaZaSvaSkladistaIzvjestaj(new StanjeZalihaZaSvaSkladistaViewModel{CurrentItemMat=new Mat{Datun=last,Datnar=last}}));
            if(stock.Rows.Cast<DataRow>().Sum(r=>Convert.ToDecimal(r["Kolic"]))!=quantity||stock.Rows.Cast<DataRow>().Sum(r=>Convert.ToDecimal(r["Vrijed"]))!=value)throw new Exception("Stock report differs from imported ledger.");
            var inventory=Render("InventurneZalihe_1000",new Materijalno.UI.Izvjestaji.StampanjeInventurnihZalihaIzvjestaj(new StampanjeInventurnihZalihaViewModel{CurrentItemInv=new Inv{Kljnaz=1000}}));
            if(inventory.Rows.Cast<DataRow>().Any(r=>Convert.ToDecimal(r["InvKolic"])!=0||Convert.ToDecimal(r["InvVrijed"])!=0))throw new Exception("Unexpected saved inventory in production source.");
            Console.WriteLine("PASS empty original INV prints zero count with book balances, matching Fox");
            Console.WriteLine("ALL PASSED production menus, actual reports and stock totals; no new records created.");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}finally{app.Shutdown();}
    }
    static DataTable Render(string name,Window window)
    {
        try{
            var fields=window.GetType().GetFields(BindingFlags.NonPublic|BindingFlags.Instance);
            var table=(DataTable)fields.Single(f=>f.Name=="reportDt").GetValue(window);
            var viewer=(ReportViewer)fields.Single(f=>f.FieldType==typeof(ReportViewer)).GetValue(window);
            string mime,encoding,extension;string[] streams;Warning[] warnings;
            byte[] data=viewer.LocalReport.Render("PDF",null,out mime,out encoding,out extension,out streams,out warnings);
            if(warnings.Length!=0)throw new Exception(name+": "+string.Join(";",warnings.Select(w=>w.Message)));
            File.WriteAllBytes(Path.Combine(Folder,name+".pdf"),data);
            table.WriteXml(Path.Combine(Folder,name+".xml"),XmlWriteMode.WriteSchema);
            Console.WriteLine("PASS actual production report "+name+" rows="+table.Rows.Count);return table;
        }finally{window.Close();}
    }
}
