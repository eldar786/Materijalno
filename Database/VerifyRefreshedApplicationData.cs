using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Microsoft.EntityFrameworkCore;

// Read-only application-model checks. Does not reserve, save, form or send anything.
public static class VerifyRefreshedApplicationData
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    public static int Main(string[] args)
    {
        try{
            using(var db=new materijalno_knjigovodstvoContext()){
                Check(db.Database.GetDbConnection().Database=="materijalno_fox_20261105_102038","Same application database");
                Check(!DatabaseConfiguration.OracleExportEnabled,"Verification process cannot export to Oracle");
                int year=new MaterialPostingService().ActiveYear();var posted=db.Mat.AsNoTracking().ToList();var all=db.Mat.IgnoreQueryFilters().AsNoTracking().ToList();
                var journals=db.Nalmat.AsNoTracking().ToList();
                Check(year==2026&&posted.Count==3975&&all.Count==3984,"3975 posted and 9 draft rows visible correctly");
                Check(all.All(m=>m.FoxRecno.HasValue),"No earlier SQL-only test rows remain");
                Check(journals.Count==5602&&journals.All(n=>n.FoxRecno.HasValue&&n.BatchId==null),"All 5602 journals originate in the latest Fox data");
                Check(db.FinancialBatches.Count()==0&&db.FinancialExportLogs.Count()==0,"Old test export metadata removed from the active copy");
                Check(posted.Sum(m=>m.Kolic??0)==13221650m&&posted.Sum(m=>m.Vrijed??0)==1864456.77m,"Ledger quantity and value equal Fox control totals");
                Check(journals.Sum(n=>n.Dug1st??0)==1526463.88m&&journals.Sum(n=>n.Pot1st??0)==1526463.88m,"Imported financial journal totals equal Fox");
                Check(db.TabelaMaterijala.Count()==175&&db.SifarnikSkladista.Count()==13&&db.SifarnikKonta.Count()==503,"Material, warehouse and account masters load");
                Check(db.SifarnikMaterijala.Count()==175,"Material maintenance view sees the same 175 materials");
                Check(db.SifarnikMaterijalSkladisteKonto.Count()==1463,"1463 unique material/warehouse account links");
                foreach(var pair in posted.Where(m=>m.Status=="M").GroupBy(m=>m.TransferPairId))
                    Check(pair.Key.HasValue&&pair.Count()==2&&pair.Sum(m=>m.Kolic??0)==0&&pair.Sum(m=>m.Vrijed??0)==0,"Transfer pair "+pair.Min(m=>m.FoxRecno));
                var balances=posted.GroupBy(m=>new{m.Kljnaz,m.Ident}).Select(g=>new{Warehouse=g.Key.Kljnaz,Material=g.Key.Ident,Quantity=g.Sum(m=>m.Kolic??0),Value=g.Sum(m=>m.Vrijed??0)}).ToList();
                File.WriteAllText(Path.Combine(args[0],"application_balances.json"),new JavaScriptSerializer().Serialize(balances));
                foreach(var b in balances.Where(b=>b.Quantity>0).Take(8)){
                    var balance=new MaterialPostingService().Balance(b.Material.Value,b.Warehouse.Value);
                    Check(balance.Quantity==b.Quantity&&balance.Value==b.Value&&balance.UnitCost==LedgerMath.Price(b.Value/b.Quantity),"Stock balance and weighted price read correctly "+b.Warehouse+"/"+b.Material);
                }
                DateTime first=new DateTime(year,1,1),last=posted.Max(m=>m.Datun).Value;
                var receipt=SummaryReports.Create(SummaryReportKind.Receipts,first,last,null,null);
                Check(receipt.Rows.Rows.Count==posted.Count(m=>m.Status=="U")&&receipt.Total==posted.Where(m=>m.Status=="U").Sum(m=>m.Vrijed??0),"Receipt overview reads the refreshed data");
                var costs=FoxCostReports.Create(first,last,true);
                Check(costs.AsEnumerable().Sum(r=>(decimal)r["Vrijed"])==-posted.Where(m=>m.Status=="I").Sum(m=>m.Vrijed??0),"Cost report totals read the refreshed data");
                var selected=posted.Where(m=>m.Status=="U").OrderByDescending(m=>m.Id).First();
                var print=ReceiptPrintReport.Load(selected.Id,"");
                Check(print.Rows.Rows.Count>0&&print.Number==selected.Brfak.Trim(),"Latest receipt print data loads");
                foreach(var name in new[]{"PROMET_U","PROMET_IV","PROMET_M","FIN_61"}){
                    var counter=db.DocumentCounters.Single(c=>c.Name==name&&c.BookYear==year);
                    int expected=name=="PROMET_U"?26:name=="PROMET_IV"?239:name=="PROMET_M"?197:400;
                    Check(counter.LastNumber==expected,"Fox counter "+name+" = "+expected);
                }
                Check(posted.Count(m=>m.Status=="M"&&m.Brfak=="M-191")==14&&journals.Count(n=>n.Brfak=="M-191")==14,"Latest Fox M-191 has 14 source rows and 14 journal rows");
                Check(!db.ChangeTracker.HasChanges(),"No application writes made during verification");
                var result=new{Passed=true,Checks=checks,Database=db.Database.GetDbConnection().Database,Posted=posted.Count,Drafts=all.Count-posted.Count,Journals=journals.Count,StockGroups=balances.Count,Quantity="13221650.00",Value="1864456.77",OracleConnectionsUsed=false};
                File.WriteAllText(Path.Combine(args[0],"application_verification.json"),new JavaScriptSerializer().Serialize(result));
                Console.WriteLine("ALL PASSED "+checks+" application checks; no writes or Oracle connections.");
            }
            return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
