using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using Materijalno.Import;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.WinForms;

public static class ReceiptPrintTests
{
    static int passed;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; }
    static decimal Num(DbfRecord row, string field) { string s=row.Values[field].Trim(); return s==""?0:decimal.Parse(s,CultureInfo.InvariantCulture); }
    static void Render(ReceiptPrintReport data, string output)
    {
        using(var report=new LocalReport()) {
            PrintWindow.ConfigureReport(report,data);
            string mime,encoding,extension;string[] streams;Warning[] warnings;
            byte[] pdf=report.Render("PDF",null,out mime,out encoding,out extension,out streams,out warnings);
            Check(pdf.Length>1500,"PDF not empty");
            Check(!warnings.Any(w=>w.Severity==Severity.Error),"PDF has no render errors");
            File.WriteAllBytes(output,pdf);
            Console.WriteLine("RENDER "+Path.GetFileName(output)+"; rows="+data.Rows.Rows.Count+"; warnings="+warnings.Length);
            foreach(var w in warnings) Console.WriteLine(w.Code+": "+w.Message);
        }
    }
    [STAThread]
    public static int Main(string[] args)
    {
        try {
            Directory.CreateDirectory(args[1]);
            using(var db=new materijalno_knjigovodstvoContext()) {
                var before=db.Mat.IgnoreQueryFilters().Count();
                var all=db.Mat.AsNoTracking().Where(m=>m.Status=="U").ToList();
                var materials=db.TabelaMaterijala.AsNoTracking().ToList();
                var fox=DbfReader.Read(Path.Combine(args[0],"mat.dbf")).Records.ToDictionary(r=>r.RecordNumber);
                var foxMaterials=DbfReader.Read(Path.Combine(args[0],"mater.dbf")).Records.Where(r=>!r.Deleted).GroupBy(r=>Num(r,"ident")).ToDictionary(g=>g.Key,g=>g.First());
                int compared=0;
                foreach(var m in all.Where(m=>m.FoxRecno.HasValue)) {
                    var r=fox[m.FoxRecno.Value];
                    Check(!r.Deleted&&r.Values["status"].Trim()=="U","Fox source receipt exists "+m.Id);
                    var data=ReceiptPrintReport.Build(new[]{m},materials,"","","");var row=data.Rows.Rows[0];
                    foreach(string field in new[]{"Kolic","Fcj","Fvrijed","Troskovi","Cartro","Trospe","Zavtro","Ppp","Porppp","Nc","Vrijed"})
                        Check((decimal)row[field]==Num(r,field),"Fox amount mismatch: id="+m.Id+" field="+field);
                    Check((decimal)row["TotalCosts"]==Num(r,"cartro")+Num(r,"trospe")+Num(r,"zavtro"),"Fox cost formula "+m.Id);
                    Check(data.DocumentDate==(r.Values["datnar"].Trim()==""?"":DateTime.ParseExact(r.Values["datnar"].Trim(),"yyyyMMdd",CultureInfo.InvariantCulture).ToString("dd.MM.yyyy")),"Document uses DATNAR "+m.Id);
                    DbfRecord fm;
                    if(foxMaterials.TryGetValue(m.Ident??0,out fm)) {
                        Check((string)row["Jedm"]==fm.Values["jedm"].Trim(),"Actual unit "+m.Id);
                        Check((string)row["Tarifa"]==Num(fm,"siftar").ToString(CultureInfo.InvariantCulture),"Master tariff "+m.Id);
                    }
                    compared++;
                }
                Check(compared>0,"Imported receipts compared");
                var groups=all.GroupBy(m=>new{m.BookYear,m.Kljnaz,m.Brfak,m.Datun,m.Datnar,m.Analst,m.Brdok}).ToList();
                var samples=groups.OrderByDescending(g=>g.Count()).Take(2).Concat(groups.Where(g=>g.Any(m=>LedgerMath.ParseAmount(m.Cartro)+(m.Trospe??0)+LedgerMath.ParseAmount(m.Zavtro)!=0)).Take(1))
                    .Concat(groups.OrderByDescending(g=>g.Max(m=>m.Id)).Take(1)).Distinct().ToList();
                int i=0;
                foreach(var g in samples) {
                    var selected=g.OrderByDescending(m=>m.Id).First();
                    var data=ReceiptPrintReport.Load(selected.Id,"");
                    Check(data.Rows.Rows.Count==g.Count(),"All and only matching receipt lines");
                    Check(data.Rows.AsEnumerable().Sum(r=>(decimal)r["Vrijed"])==g.Sum(m=>m.Vrijed??0),"Stored receipt total");
                    Check(data.Rows.AsEnumerable().Sum(r=>(decimal)r["Fvrijed"])==g.Sum(m=>LedgerMath.ParseAmount(m.Fvrijed)),"Invoice total");
                    Check(data.Rows.AsEnumerable().Sum(r=>(decimal)r["Porppp"])==g.Sum(m=>m.Porppp??0),"VAT total");
                    Check(data.Account==(g.OrderBy(m=>m.FoxRecno??int.MaxValue).ThenBy(m=>m.Id).First().Konto1?.ToString()??""),"KONTO1 header");
                    Render(data,Path.Combine(args[1],"Ulaz_"+(++i)+"_"+data.Number.Replace('/','-')+".pdf"));
                    Console.WriteLine("DOCUMENT "+data.Number+"; warehouse="+selected.Kljnaz+"; stored total="+data.Rows.AsEnumerable().Sum(r=>(decimal)r["Vrijed"]));
                }
                var unit=new TabelaMaterijala { Ident=99111,Nazmat="PROVJERA ČĆŽŠĐ - NAZIV MATERIJALA SA DUŽIM OPISOM",Jedm="KG",Siftar=5 };
                var synthetic=Enumerable.Range(1,13).Select(n=>new Mat { Id=n,IsDraft=false,Status="U",Ident=99111,Redbr=n,Brfak="QA-13",Brdok="RAČUN-TEST",Datun=new DateTime(2026,11,9),Datnar=new DateTime(2026,11,8),
                    Kolic=1000,Fcj="12.345678901",Fvrijed="12345.68",Nc=13.123456789m,Vrijed=13123.46m,Cartro="100.00",Trospe=200m,Zavtro="477.78",Troskovi=6.30m,Ppp=n==1?"0":"17",Porppp=n==1?0:2098.77m }).ToList();
                var multi=ReceiptPrintReport.Build(synthetic,new[]{unit},"MAGACIN REGION CENTAR 1. INTERNO","DOBAVLJAČ ČĆŽŠĐ","MATERIJAL U MAGACINU");
                Check((string)multi.Rows.Rows[0]["Jedm"]=="KG","Unit is not hardcoded KOM");
                Check((string)multi.Rows.Rows[0]["Tarifa"]=="5","Tariff from material master");
                Check((decimal)multi.Rows.Rows[0]["Ppp"]==0,"VAT rate is not hardcoded 17");
                Check((decimal)multi.Rows.Rows[0]["Nc"]==13.123456789m,"NC retains nine decimals");
                Check((decimal)multi.Rows.Rows[0]["Fcj"]==12.345678901m,"Invoice price not modified for display");
                Check(multi.DocumentDate=="08.11.2026","Document date differs from entry date");
                var preview=new PrintWindow(multi);
                var viewer=(ReportViewer)preview.FindName("_reportViewer");
                Check(preview.Title=="Ulaz u magacin - od dobavljača","WPF preview constructs with correct title");
                Check(viewer.LocalReport.DataSources.Count==1 && viewer.ZoomMode==ZoomMode.PageWidth,"WPF viewer configured for the receipt");
                preview.Close();
                Render(multi,Path.Combine(args[1],"Ulaz_QA_13_stavki.pdf"));
                var blank=new Mat { Id=90,IsDraft=false,Status="U",Datun=null,Datnar=null };
                var blanks=ReceiptPrintReport.Build(new[]{blank},new TabelaMaterijala[0],null,null,null);
                Check(blanks.DocumentDate==""&& (decimal)blanks.Rows.Rows[0]["Nc"]==0,"Nullable data handled");
                Render(blanks,Path.Combine(args[1],"Ulaz_QA_prazna_polja.pdf"));
                Check(ReceiptPrintReport.Build(synthetic.Concat(new[]{new Mat { IsDraft=true,Status="U"},new Mat{IsArchived=true,IsDraft=false,Status="U"},new Mat{IsDraft=false,Status="I"}}),new[]{unit},"","","").Rows.Rows.Count==13,"Draft/archived/other-status excluded");
                Check(db.Mat.IgnoreQueryFilters().Count()==before,"Read-only test: row count unchanged");
                Check(!db.ChangeTracker.HasChanges(),"No tracked modifications");
                Console.WriteLine("FOX RECEIPTS COMPARED "+compared+"; ALL PASSED "+passed+"; no data written.");
            }
            return 0;
        } catch(Exception e) {Console.WriteLine(e);return 1;}
    }
}
