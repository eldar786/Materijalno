using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.WinForms;

public static class ScenarioReportTests
{
    static string Folder;
    static List<Mat> FixtureRows, AllRows;
    static List<PrometCase> Cases;
    static List<object> Results=new List<object>();
    static readonly DateTime From=new DateTime(2026,11,1),To=new DateTime(2026,11,4);
    static decimal Sum(DataTable t,string column)=>t.Rows.Cast<DataRow>().Where(r=>r[column]!=DBNull.Value).Sum(r=>Convert.ToDecimal(r[column]));
    static IEnumerable<DataRow> NewRows(DataTable t)=>t.Rows.Cast<DataRow>().Where(r=>t.Columns.Contains("Ident")&&r["Ident"]!=DBNull.Value&&PrometScenarioTests.Materials.Contains(Convert.ToInt32(r["Ident"])));
    public static void Run()
    {
        var culture=CultureInfo.GetCultureInfo("bs-Latn-BA");
        System.Threading.Thread.CurrentThread.CurrentCulture=culture;System.Threading.Thread.CurrentThread.CurrentUICulture=culture;
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        Folder=Path.Combine(PrometScenarioTests.Work,"reports");Directory.CreateDirectory(Folder);
        Cases=PrometScenarioTests.Json.Deserialize<List<PrometCase>>(File.ReadAllText(Path.Combine(PrometScenarioTests.Work,"scenario_saved.json")));
        using(var db=PrometScenarioTests.Db()){AllRows=db.Mat.AsNoTracking().ToList();FixtureRows=AllRows.Where(m=>PrometScenarioTests.Materials.Contains(m.Ident??0)).ToList();}
        try {
            foreach(var group in Cases.GroupBy(c=>c.Group)) {
                var item=group.First();var selected=FixtureRows.First(m=>m.Status==item.Status&&m.Brfak==item.Number&&(item.Status!="M"||m.Kolic>0));
                string name="Dokument_"+item.Number;
                if(item.Status=="U") {
                    var summary=SummaryReports.Create(SummaryReportKind.ReceiptDocument,From,To,1000,item.Number);
                    PrometScenarioTests.Check(name+" receipt lines/value",summary.Rows.Rows.Count==group.Count()&&summary.Total==FixtureRows.Where(m=>m.Brfak==item.Number&&m.Status=="U").Sum(m=>m.Vrijed));
                    ExportSummary(name,summary);
                } else {
                    string report=item.Status=="I"?"IzlazMaterijala":item.Status=="V"?"Povrat":"Medjuskladisnica";
                    var table=ExportRdlc(name,report,selected);
                    PrometScenarioTests.Check(name+" correct lines",table.Rows.Count==group.Count());
                    PrometScenarioTests.Check(name+" correct saved value",Sum(table,"Vrijed")==FixtureRows.Where(m=>m.Status==item.Status&&m.Brfak==item.Number&&(item.Status!="M"||m.Kolic>0)).Sum(m=>m.Vrijed));
                    if(item.Status=="M")PrometScenarioTests.Check(name+" correct receiving warehouse",table.Rows.Cast<DataRow>().All(r=>Convert.ToInt32(r["Kljnaz"])==item.Destination));
                }
                var financial=ExportRdlc("Finansije_"+item.Number,"NaloziZaFinansijePrintanjeNaloga",selected);
                PrometScenarioTests.Check("Financial print "+item.Number+" balanced",financial.Rows.Count>0&&Sum(financial,"Dug1st")==Sum(financial,"Pot1st"));
            }
            var filter=new Mat{Ident=990201,Kljnaz=1000,Datun=From,Datnar=To};
            var journal=ExportRdlc("Dnevnik","Dnevnik",filter);
            var expected=FixtureRows.Where(m=>m.Ident==990201&&m.Kljnaz==1000&&m.Datun>=From&&m.Datun<To.AddDays(1)).ToList();
            PrometScenarioTests.Check("Dnevnik all movements",journal.Rows.Count==expected.Count&&Sum(journal,"Kolic")==expected.Sum(m=>m.Kolic)&&Sum(journal,"Vrijed")==expected.Sum(m=>m.Vrijed));
            var card=ExportRdlc("AnalitickaKartica_sa_prenosom","AnalitickaKartica",new Mat{Ident=990201,Kljnaz=1000,Datun=From.AddDays(1),Datnar=To});
            PrometScenarioTests.Check("Analytical card opening reconciles",card.Rows.Cast<DataRow>().Any(r=>Convert.ToString(r["Brfak"])=="PRENOS")&&Sum(card,"Kolic")==expected.Sum(m=>m.Kolic)&&Sum(card,"Vrijed")==expected.Sum(m=>m.Vrijed));
            var transfer=FixtureRows.First(m=>m.Status=="M"&&m.Kolic>0);
            var one=ExportRdlc("MedjuskladisniceJedna","MedjuskladisniceJedna",transfer);
            PrometScenarioTests.Check("Single transfer all lines",one.Rows.Count==FixtureRows.Count(m=>m.Status=="M"&&m.Kolic>0&&m.Brfak==transfer.Brfak));
            var transfers=ExportRdlc("MedjuskladisniceSve","MedjuskladisniceSve",filter);
            PrometScenarioTests.Check("All transfers exactly once",NewRows(transfers).Count()==4&&NewRows(transfers).Sum(r=>Convert.ToDecimal(r["Vrijed"]))==FixtureRows.Where(m=>m.Status=="M"&&m.Kolic>0).Sum(m=>m.Vrijed));
            var warehouseTransfers=ExportRdlc("MedjuskladisniceZaSkladiste","MedjuskladisniceZaSkladiste",new Mat{Ident=990201,Kljnaz=1001,Datun=From,Datnar=To});
            PrometScenarioTests.Check("Warehouse transfer target filter",NewRows(warehouseTransfers).Count()==FixtureRows.Count(m=>m.Status=="M"&&m.Kolic<0&&m.Kljnaz1==1001));
            var issues=ExportRdlc("PregledIzlazaZaSveKalkulacije","PregledIzlazaZaSveKalkulacije",filter);
            PrometScenarioTests.Check("Issue overview warehouse filter",NewRows(issues).Count()==FixtureRows.Count(m=>m.Status=="I"&&m.Kljnaz==1000));
            foreach(string report in new[]{"PregledTroskovaPoKontima","PregledTroskovaPoKontimaINalozimaZaSvaSkladista"}) {
                var costs=ExportRdlc(report,report,filter);
                var issues=AllRows.Where(m=>m.Status=="I"&&m.Datun>=From&&m.Datun<To.AddDays(1));
                var expectedCost=report=="PregledTroskovaPoKontima" ? issues.GroupBy(m=>new{m.Kljnaz,m.Konto1}).Sum(g=>Math.Abs(g.Sum(m=>m.Vrijed??0))) : -issues.Sum(m=>m.Vrijed??0);
                PrometScenarioTests.Check(report+" original Fox issue costs",costs.Rows.Count>0&&Sum(costs,"Vrijed")==expectedCost);
            }
            CheckStock("Warehouse stock",ExportRdlc("StanjeZalihaZaSkladiste","StanjeZalihaZaSkladiste",new Mat{Kljnaz=1000,Datun=To,Datnar=To}),1000);
            CheckStock("All warehouse stock",ExportRdlc("StanjeZalihaZaSvaSkladista","StanjeZalihaZaSvaSkladista",new Mat{Datun=To,Datnar=To}),null);
            var rec=ExportRdlc("RekapitulacijaZaliha","RekapitulacijaZaliha",new Mat{Datun=To,Datnar=To});
            PrometScenarioTests.Check("Stock recapitulation reconciles",Sum(rec,"Vrijed")==AllRows.Where(m=>m.Datun<To.AddDays(1)).Sum(m=>m.Vrijed));
            CheckStock("Inventory slips",ExportRdlc("StampanjeInventurnihListica","StampanjeInventurnihListica",new Mat{Kljnaz=1000,Datun=To}),1000,false);
            PrometScenarioTests.Check("Inventory print new counts",NewRows(ExportRdlc("UnosInventurnogStanja","UnosInventurnogStanja",filter)).Count()==2);
            var diffRows=NewRows(ExportRdlc("StampanjeInventurnihZaliha","StampanjeInventurnihZaliha",filter)).ToList();
            PrometScenarioTests.Check("Inventory shortage/surplus separately",diffRows.Count==2&&Convert.ToDecimal(diffRows[0]["KolicRazlikaManjak"])==1&&Convert.ToDecimal(diffRows[0]["KolicRazlikaVisak"])==2);
            PrometScenarioTests.Check("U/I/M/V excluded from opening",!NewRows(ExportRdlc("PocetnoStanje_odvojeno_od_prometa","PocetnoStanjePrviPut",filter)).Any());
            ExportRdlc("SifarnikSkladista","Skladiste",filter);
            foreach(SummaryReportKind kind in Enum.GetValues(typeof(SummaryReportKind))) {
                string number=kind==SummaryReportKind.ReceiptDocument?Cases.First(c=>c.Status=="U").Number:kind==SummaryReportKind.IssueDocument?Cases.First(c=>c.Status=="I").Number:null;
                var summary=SummaryReports.Create(kind,From,To,null,number);
                var raw=AllRows.Where(m=>m.Datun>=From&&m.Datun<To.AddDays(1));
                bool receipt=kind==SummaryReportKind.Receipts||kind==SummaryReportKind.ReceiptDocument||kind==SummaryReportKind.ReceiptsByAccount;
                raw=receipt?raw.Where(m=>m.Status=="U"):raw.Where(m=>m.Status=="I"||m.Status=="V");if(number!=null)raw=raw.Where(m=>m.Brfak==number);
                PrometScenarioTests.Check(kind+" summary reconciles",summary.Total==raw.Sum(m=>(receipt?1:-1)*(m.Vrijed??0)));
                ExportSummary("Pregled_"+kind,summary);
            }
            PrometScenarioTests.WriteJson("reports_manifest.json",Results);
        }finally{app.Shutdown();}
    }
    static void CheckStock(string name,DataTable table,int? warehouse,bool weighted=true)
    {
        foreach(var group in FixtureRows.Where(m=>!warehouse.HasValue||m.Kljnaz==warehouse).GroupBy(m=>new{m.Ident,m.Kljnaz})) {
            var row=NewRows(table).Single(r=>Convert.ToInt32(r["Ident"])==group.Key.Ident&&Convert.ToInt32(r["Kljnaz"])==group.Key.Kljnaz);
            decimal qty=group.Sum(m=>m.Kolic??0),value=group.Sum(m=>m.Vrijed??0);
            PrometScenarioTests.Check(name+" "+group.Key.Ident+"/"+group.Key.Kljnaz,Convert.ToDecimal(row["Kolic"])==qty&&Convert.ToDecimal(row["Vrijed"])==value);
            if(table.Columns.Contains("Nc")) {
                decimal price=weighted?Math.Round(value/qty,9,MidpointRounding.AwayFromZero):group.OrderBy(m=>m.Id).Last().Nc.Value;
                PrometScenarioTests.Check(name+(weighted?" weighted price ":" Fox printed price ")+group.Key.Ident,Convert.ToDecimal(row["Nc"])==price);
            }
        }
    }
    static DataTable ExportRdlc(string output,string report,Mat selected)
    {
        Console.WriteLine("REPORT START "+output);
        var ui=typeof(Materijalno.UI.App).Assembly;var vmAssembly=typeof(GlavniViewModel).Assembly;
        string vmName=report=="Skladiste"?"SifarnikSkladista":report=="Povrat"?"PovratMaterijala":report;
        var vmType=vmAssembly.GetType("Materijalno.ViewModel."+vmName+"ViewModel",true);
        var vm=vmType.GetConstructor(Type.EmptyTypes)!=null?Activator.CreateInstance(vmType):Activator.CreateInstance(vmType,new object[]{new GlavniViewModel()});
        vmType.GetProperty("CurrentItemMat")?.SetValue(vm,selected);
        vmType.GetProperty("Brfak")?.SetValue(vm,selected.Brfak);
        vmType.GetProperty("Godina")?.SetValue(vm,2026);
        using(var db=PrometScenarioTests.Db()){
            if(vmType.GetProperty("CurrentItemInv")!=null)vmType.GetProperty("CurrentItemInv").SetValue(vm,db.Inv.AsNoTracking().First(i=>i.Kljnaz==1000));
            if(vmType.GetProperty("CurrentItemSklad")!=null)vmType.GetProperty("CurrentItemSklad").SetValue(vm,db.SifarnikSkladista.AsNoTracking().Single(w=>w.Kljnaz==1000));
        }
        string windowName=report=="RekapitulacijaZaliha"?"RekapitulacijaZalihaIvjestaj":report+"Izvjestaj";
        var type=ui.GetType("Materijalno.UI.Izvjestaji."+windowName,true);var window=(Window)Activator.CreateInstance(type,new[]{vm});
        try {
            var table=(DataTable)type.GetField("reportDt",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window);
            if(table==null)throw new Exception(output+" missing real report table.");
            var viewer=(ReportViewer)type.GetFields(BindingFlags.NonPublic|BindingFlags.Instance).Single(f=>f.FieldType==typeof(ReportViewer)).GetValue(window);
            string mime,encoding,extension;string[] streams;Warning[] warnings;
            var pdf=viewer.LocalReport.Render("PDF",null,out mime,out encoding,out extension,out streams,out warnings);
            if(warnings.Any(w=>w.Severity.ToString()=="Error"))throw new Exception(output+": "+string.Join("; ",warnings.Select(w=>w.Message)));
            if(pdf.Length<1000)throw new Exception(output+" empty PDF.");
            File.WriteAllBytes(Path.Combine(Folder,output+".pdf"),pdf);
            table.WriteXml(Path.Combine(Folder,output+".xml"),XmlWriteMode.WriteSchema);
            Results.Add(new{Report=output,Format="PDF",Rows=table.Rows.Count,NewMaterialRows=NewRows(table).Count(),Warnings=warnings.Select(w=>new{w.Code,w.Message}).ToArray()});
            Console.WriteLine("REPORT PASS "+output+" rows="+table.Rows.Count);return table;
        }finally{window.Close();}
    }
    static void ExportSummary(string name,SummaryReport summary)
    {
        if(string.IsNullOrWhiteSpace(summary.Rows.TableName))summary.Rows.TableName="ReportRows";
        var document=Materijalno.UI.Helpers.SummaryReportPreview.CreateDocument(summary);
        var paginator=((IDocumentPaginatorSource)document).DocumentPaginator;paginator.ComputePageCount();var pages=new List<string>();
        for(int index=0;index<paginator.PageCount;index++){
            using(var page=paginator.GetPage(index)){
                var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new Rect(page.Size));dc.DrawRectangle(new VisualBrush(page.Visual),null,new Rect(page.Size));}
                var bitmap=new RenderTargetBitmap((int)Math.Ceiling(page.Size.Width*1.5),(int)Math.Ceiling(page.Size.Height*1.5),144,144,PixelFormats.Pbgra32);bitmap.Render(drawing);
                string file=name+"_"+(index+1).ToString("D3")+".png";var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(Folder,file)))encoder.Save(stream);pages.Add(file);
            }
        }
        if(pages.Count==0)throw new Exception(name+" no rendered pages.");
        summary.Rows.WriteXml(Path.Combine(Folder,name+".xml"),XmlWriteMode.WriteSchema);
        Results.Add(new{Report=name,Format="PNG",Rows=summary.Rows.Rows.Count,Pages=pages,Total=summary.Total});Console.WriteLine("REPORT PASS "+name+" pages="+pages.Count);
    }
}
