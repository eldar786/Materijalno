using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;

public sealed class PrometCase
{
    public int Index { get; set; }
    public string Status { get; set; }
    public string Group { get; set; }
    public int Ident { get; set; }
    public int Warehouse { get; set; }
    public int Destination { get; set; }
    public decimal Quantity { get; set; }
    public decimal Invoice { get; set; }
    public decimal Customs { get; set; }
    public decimal Vat { get; set; }
    public decimal Other { get; set; }
    public decimal Freight { get; set; }
    public int Line { get; set; }
    public string Date { get; set; }
    public string DocumentDate { get; set; }
    public string WorkOrder { get; set; }
    public string Number { get; set; }
    public decimal BeforeQuantity { get; set; }
    public decimal BeforeValue { get; set; }
    public decimal PreviewPrice { get; set; }
    public decimal SavedPrice { get; set; }
    public decimal SavedQuantity { get; set; }
    public decimal SavedValue { get; set; }
    public decimal AfterQuantity { get; set; }
    public decimal AfterValue { get; set; }
}

public static class PrometScenarioTests
{
    public static readonly int[] Materials = {990201,990202};
    public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength=int.MaxValue };
    public static string Work;
    public static int Checks;
    public static materijalno_knjigovodstvoContext Db() => new materijalno_knjigovodstvoContext();
    public static void Check(string name, bool condition)
    { if(!condition)throw new Exception("FAIL "+name);Checks++;Console.WriteLine("PASS "+name); }
    public static void WriteJson(string name,object value) => File.WriteAllText(Path.Combine(Work,name),Json.Serialize(value));

    [STAThread]
    public static int Main(string[] args)
    {
        try {
            Work=args[1]; Directory.CreateDirectory(Work);
            var catalog=new SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
            if(!catalog.StartsWith("materijalno_fox_test_",StringComparison.Ordinal))throw new Exception("Isolated test database required.");
            Console.WriteLine("TEST DATABASE "+catalog);
            if(args[0]=="save") SaveCases();
            else if(args[0]=="reports") ScenarioReportTests.Run();
            else throw new Exception("Unknown test mode.");
            Console.WriteLine("ALL PASSED "+Checks);return 0;
        } catch(Exception e) { Console.WriteLine(e);return 1; }
    }

    static void SaveCases()
    {
        using(var db=Db()) {
            // This program refuses every non-test catalog before any write.
            db.Database.ExecuteSqlRaw("DELETE FROM dbo.financial_export_log; DELETE FROM dbo.nalmat WHERE fox_recno IS NULL; DELETE FROM dbo.financial_batch; DELETE FROM dbo.inv WHERE fox_recno IS NULL; DELETE FROM dbo.mat WHERE fox_recno IS NULL; DELETE FROM dbo.material_document WHERE NOT EXISTS(SELECT 1 FROM dbo.mat WHERE document_id=material_document.id); DELETE FROM dbo.document_counter;");
            foreach(var counter in new[]{new[]{"broju","PROMET_U"},new[]{"broji","PROMET_IV"},new[]{"brojm","PROMET_M"}})
                db.Database.ExecuteSqlRaw("INSERT dbo.document_counter(name,book_year,last_number) SELECT {0},CONVERT(int,GODBRNA),CONVERT(int,BRNAL) FROM fox_raw.["+counter[0]+"] WHERE fox_deleted=0 AND TRY_CONVERT(int,GODBRNA)>2000 AND TRY_CONVERT(int,BRNAL) IS NOT NULL",counter[1]);
            var template=db.TabelaMaterijala.Single(m=>m.Ident==13);
            foreach(int ident in Materials) {
                if(!db.TabelaMaterijala.Any(m=>m.Ident==ident)) {
                    db.SifarnikMaterijala.Add(new SifarnikMaterijala{Ident=ident,Nazmat="E2E PONDER "+(ident==990201?"ALFA":"BETA"),Jedm="kom",Siftar=template.Siftar,Konto1=template.Konto1,Konto2=template.Konto2});db.SaveChanges();
                    foreach(int warehouse in new[]{1000,1001,1002}) {
                        var account=db.SifarnikMaterijalSkladisteKonto.Single(l=>l.Sifmat==13&&l.Sifskla==warehouse).Sifkonta;
                        db.SifarnikMaterijalSkladisteKonto.Add(new SifarnikMaterijalSkladisteKonto{Sifmat=ident,Sifskla=warehouse,Sifkonta=account});
                    }db.SaveChanges();
                }
            }
        }
        var cases=Json.Deserialize<List<PrometCase>>(File.ReadAllText(Path.Combine(Work,"scenario_input.json")));
        var numbers=new Dictionary<string,string>();var posting=new MaterialPostingService();
        foreach(var item in cases) {
            if(!numbers.ContainsKey(item.Group))numbers[item.Group]=posting.ReserveDocumentNumber(item.Status,2026);
            item.Number=numbers[item.Group];
            using(var db=Db()) {var old=db.Mat.Where(m=>m.Ident==item.Ident&&m.Kljnaz==item.Warehouse).ToList();item.BeforeQuantity=old.Sum(m=>m.Kolic??0);item.BeforeValue=old.Sum(m=>m.Vrijed??0);}
            var input=new Mat{Ident=item.Ident,Kljnaz=item.Warehouse,Kljnaz1=item.Destination==0?(int?)null:item.Destination,Status=item.Status,
                Brfak=item.Number,Brdok="E2E-"+item.Group,Redbr=item.Line,Datun=DateTime.Parse(item.Date,CultureInfo.InvariantCulture),
                Datnar=DateTime.Parse(item.Status=="M"?item.Date:item.DocumentDate,CultureInfo.InvariantCulture),Kolic=item.Quantity,
                Analst=item.Status=="U"?"1RS03":"",Brnar=item.Status=="M"?"":item.WorkOrder,Medus="",Konto2="",Ppp=item.Status=="U"?"17":"",Tarifa=item.Status=="U"?"1":"",
                Fvrijed=item.Invoice.ToString("F2",CultureInfo.InvariantCulture),Cartro=item.Customs.ToString("F2",CultureInfo.InvariantCulture),
                Zavtro=item.Other.ToString("F2",CultureInfo.InvariantCulture),Porppp=item.Vat,Trospe=item.Freight};
            switch(item.Status) {
                case "U":var receiptVm=new UlazMaterijalaViewModel(new GlavniViewModel()){CurrentItemMat=input};receiptVm.NabavnaCijena();break;
                case "I":var issueVm=new IzlazMaterijalaViewModel{CurrentItemMat=input};issueVm.NabavnaCijena();break;
                case "V":var returnVm=new PovratMaterijalaViewModel{CurrentItemMat=input};returnVm.NabavnaCijena();break;
                case "M":var transferVm=new MedjuskladisnicaViewModel{CurrentItemMat=input};transferVm.NabavnaCijena();break;
            }
            item.PreviewPrice=input.Nc??-1;
            var saved=posting.Save(input);
            using(var db=Db()) {
                var stored=db.Mat.AsNoTracking().Single(m=>m.Id==saved.Id);
                item.SavedPrice=stored.Nc.Value;item.SavedQuantity=stored.Kolic.Value;item.SavedValue=stored.Vrijed.Value;
                Check(item.Number+"/"+item.Line+" persisted quantity/price/header",stored.Nc==item.PreviewPrice&&Math.Abs(stored.Kolic.Value)==item.Quantity&&stored.Brdok=="E2E-"+item.Group&&stored.Redbr==item.Line&&stored.Ident==item.Ident&&!stored.IsDraft);
                if(item.Status=="I"||item.Status=="M")Check(item.Number+" weighted price from posted balance",stored.Nc==Math.Round(item.BeforeValue/item.BeforeQuantity,9,MidpointRounding.AwayFromZero));
                if(item.Status=="M") {
                    var pair=db.Mat.Where(m=>m.TransferPairId==stored.TransferPairId).ToList();
                    Check(item.Number+" complete opposite warehouse pair",pair.Count==2&&pair.Sum(m=>m.Kolic)==0&&pair.Sum(m=>m.Vrijed)==0&&pair.All(m=>m.Nc==stored.Nc));
                }
                var all=db.Mat.Where(m=>m.Ident==item.Ident&&m.Kljnaz==item.Warehouse).ToList();item.AfterQuantity=all.Sum(m=>m.Kolic??0);item.AfterValue=all.Sum(m=>m.Vrijed??0);
                int count=db.Mat.Count();posting.Save(stored);Check(item.Number+" repeated save is not a duplicate",db.Mat.Count()==count);
            }
        }
        var inventory=new InventoryService();
        foreach(int material in Materials) {
            var balance=posting.Balance(material,1000);
            inventory.Save(new Inv{Ident=material,Kljnaz=1000,Datun=new DateTime(2026,11,4),Kolic=balance.Quantity+(material==990201?-1:2),Redbr=material==990201?1:2});
        }
        // Complete the same inventory session for the original stock, so an
        // uncounted original item cannot inflate the fixture's shortage totals.
        using(var db=Db()) {
            var originalStock=db.Mat.AsNoTracking().Where(m=>m.Kljnaz==1000&&m.Datun<new DateTime(2026,11,5)).ToList()
                .Where(m=>!Materials.Contains(m.Ident.Value)).GroupBy(m=>m.Ident.Value);
            int line=3;
            foreach(var material in originalStock)
                inventory.Save(new Inv{Ident=material.Key,Kljnaz=1000,Datun=new DateTime(2026,11,4),Kolic=material.Sum(m=>m.Kolic??0),Redbr=line++});
        }
        var finance=new FinancialPostingService();foreach(var document in cases.GroupBy(c=>c.Group)) {
            var batch=finance.Prepare(document.First().Status,document.First().Number);
            using(var db=Db()){var lines=db.Nalmat.Where(n=>n.BatchId==batch.Id).ToList();Check(document.Key+" local financial journal balanced",lines.Count>0&&lines.Sum(n=>n.Dug1st)==lines.Sum(n=>n.Pot1st));}
        }
        WriteJson("scenario_saved.json",cases);
        using(var db=Db()) {
            var fields=typeof(Mat).GetProperties().Where(p=>new[]{"Id","Kljnaz","Kljnaz1","Ident","Datun","Analst","Brdok","Brnar","Datnar","Brfak","Kolic","Nc","Vrijed","Redbr","Status","Konto1","Konto2","Fcj","Troskovi","Cartro","Zavtro","Ppp","Tarifa","Fvrijed","Porppp","Medus","Kontosklad","Kontosklad1","Trospe","Ourst","Mjtst"}.Contains(p.Name)).ToList();
            var rows=db.Mat.AsNoTracking().Where(m=>Materials.Contains(m.Ident.Value)).OrderBy(m=>m.Id).ToList();
            WriteJson("wpf_saved_rows.json",rows.Select(m=>fields.ToDictionary(p=>p.Name,p=>p.GetValue(m) is DateTime?((DateTime)p.GetValue(m)).ToString("yyyyMMdd") : p.GetValue(m))));
            WriteJson("wpf_balances.json",rows.GroupBy(m=>new{m.Ident,m.Kljnaz}).Select(g=>new{g.Key.Ident,g.Key.Kljnaz,Quantity=g.Sum(m=>m.Kolic),Value=g.Sum(m=>m.Vrijed)}));
        }
    }
}
