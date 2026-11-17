using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Microsoft.EntityFrameworkCore;

public static class IdentifierReadOnlyChecks
{
    static int checks;
    static void Check(bool ok, string label) { if(!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    static decimal Amount(DataRow r,string c) => r.IsNull(c)?0:Convert.ToDecimal(r[c],CultureInfo.InvariantCulture);
    public static int Main(string[] args)
    {
        try {
            var before=new DataSet();before.ReadXml(Path.Combine(args[0],"data_before.xml"),XmlReadMode.ReadSchema);
            using(var db=new materijalno_knjigovodstvoContext()) {
                var movements=db.Mat.IgnoreQueryFilters().AsNoTracking().ToList();
                var master=db.TabelaMaterijala.AsNoTracking().ToList();
                var links=db.SifarnikMaterijalSkladisteKonto.AsNoTracking().ToList();
                var inventory=db.Inv.IgnoreQueryFilters().AsNoTracking().ToList();
                Check(movements.Count==before.Tables["mat"].Rows.Count,"All ledger rows retained");
                Check(master.Count==before.Tables["tabela_materijala"].Rows.Count,"All materials retained");
                Check(links.Count==before.Tables["sifarnik_materijal_skladiste_konto"].Rows.Count,"All warehouse links retained");
                Check(inventory.Count==before.Tables["inv"].Rows.Count,"All inventory rows retained");
                foreach(string code in new[]{"087001","088001","089001","090001","091001","092001","093001","094001","095001","096001","097001","098001"}) {
                    Check(master.Any(m=>m.Ident==code),"Exact master identifier "+code);
                    string old=int.Parse(code).ToString();Check(!movements.Any(m=>m.Ident==old),"No collapsed ledger identifier "+old);
                }
                Check(master.Any(m=>m.Ident=="2"),"Short code 2 unchanged");
                var originals=before.Tables["mat"].AsEnumerable().Where(r=>!r.IsNull("ident"))
                    .GroupBy(r=>new{Ident=Convert.ToInt32(r["ident"]),Warehouse=r.IsNull("kljnaz")?0:Convert.ToInt32(r["kljnaz"])})
                    .ToDictionary(g=>g.Key,g=>new{Quantity=g.Sum(r=>Amount(r,"kolic")),Value=g.Sum(r=>Amount(r,"vrijed"))});
                var actual=movements.Where(m=>m.Ident!=null).GroupBy(m=>new{Ident=int.Parse(m.Ident),Warehouse=m.Kljnaz??0})
                    .ToDictionary(g=>g.Key,g=>new{Quantity=g.Sum(m=>m.Kolic??0),Value=g.Sum(m=>m.Vrijed??0)});
                Check(originals.Count==actual.Count&&originals.All(p=>actual.ContainsKey(p.Key)&&actual[p.Key].Quantity==p.Value.Quantity&&actual[p.Key].Value==p.Value.Value),"Every warehouse/material quantity and value unchanged");
                var selected=movements.First(m=>!m.IsDraft&&!m.IsArchived&&m.Status=="U"&&m.Ident=="094001");
                var receipt=ReceiptPrintReport.Load(selected.Id,"");
                Check(receipt.Rows.Columns["Ident"].DataType==typeof(string)&&receipt.Rows.AsEnumerable().Any(r=>(string)r["Ident"]=="094001"),"Receipt report reads 094001 from current database");
                var from=new DateTime(2026,1,1);var to=new DateTime(2026,12,31);
                foreach(bool byDocument in new[]{false,true}) {
                    var costs=FoxCostReports.Create(from,to,byDocument);
                    Check(costs.Columns["Vrijed"].DataType==typeof(decimal)&&costs.Rows.Count>0,"Cost report resolves material accounts; byDocument="+byDocument);
                }
                var summary=SummaryReports.Create(SummaryReportKind.Receipts,from,to,null,null);
                Check(summary.Rows.Columns["Ident"].DataType==typeof(string)&&summary.Rows.AsEnumerable().Any(r=>(string)r["Ident"]=="094001"),"Summary report retains 094001");
                var balance=new MaterialPostingService().Balance("094001",1001);
                var rows=db.Mat.AsNoTracking().Where(m=>m.Ident=="094001"&&m.Kljnaz==1001).ToList();
                Check(balance.Quantity==rows.Sum(m=>m.Kolic??0)&&balance.Value==rows.Sum(m=>m.Vrijed??0),"Stock/weighted-cost source resolves exact text identifier");
                Check(!db.ChangeTracker.HasChanges(),"Read-only checks have no pending changes");
            }
            var export=StockFileExport.Create(new DateTime(2026,10,31),new DateTime(2026,11,17));
            File.WriteAllBytes(Path.Combine(args[0],"zalihesve_after_identifiers.TXT"),export);
            var text=Encoding.GetEncoding(1250).GetString(export);
            Check(text.Contains("094001")&&text.Contains("087001"),"Stock TXT export preserves leading zeros");
            Console.WriteLine("ALL PASSED "+checks+"; existing database; read only; no Oracle calls.");return 0;
        } catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
