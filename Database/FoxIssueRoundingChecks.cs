using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;

// Native Fox calculation checks and real service saves. Every SQL test write
// belongs to an externally owned transaction that is ALWAYS rolled back.
public static class FoxIssueRoundingChecks
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly List<object> cases = new List<object>();
    static int checks, savedRows, groupsChecked;
    static string work;
    static Fx fox;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Save(string file, object value) => File.WriteAllText(Path.Combine(work, file), new JavaScriptSerializer { MaxJsonLength=int.MaxValue }.Serialize(value));
    static DataTable Read(DbConnection c, DbTransaction tx, string sql) {
        using(var cmd=c.CreateCommand()) { cmd.Transaction=tx;cmd.CommandText=sql;cmd.CommandTimeout=90;
            using(var r=cmd.ExecuteReader()) { var t=new DataTable();t.Load(r);return t; } }
    }
    static void Exec(DbConnection c, DbTransaction tx, string sql) {
        using(var cmd=c.CreateCommand()) { cmd.Transaction=tx;cmd.CommandText=sql;cmd.CommandTimeout=90;cmd.ExecuteNonQuery(); }
    }
    static Dictionary<string,string> Hashes() {
        using(var db=new materijalno_knjigovodstvoContext()) {
            var c=db.Database.GetDbConnection();c.Open();var result=new Dictionary<string,string>();
            foreach(string table in new[]{"mat","inv","nalmat","material_document","document_counter","app_settings","financial_batch","financial_export_log","year_close","tabela_materijala","sifarnik_skladista","sifarnik_konta","sifarnik_materijal_skladiste_konto"}) {
                string order=table=="document_counter"?"name,book_year":table=="app_settings"?"key_name":"id";
                object value=Read(c,null,"SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.["+table+"] ORDER BY "+order+" FOR XML RAW,BINARY BASE64))").Rows[0][0];
                using(var sha=SHA256.Create())result[table]=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value==DBNull.Value?"":(string)value))).Replace("-","");
            }
            return result;
        }
    }
    static Func<materijalno_knjigovodstvoContext> Factory(DbConnection c,DbTransaction tx) => () => {
        var db=new materijalno_knjigovodstvoContext(new DbContextOptionsBuilder<materijalno_knjigovodstvoContext>().UseSqlServer(c).Options);
        db.Database.UseTransaction(tx);return db;
    };
    sealed class Fx : IDisposable {
        dynamic app;
        static string N(decimal n)=>n.ToString("0.########################",Inv);
        static string Q(string s)=>"'"+s.Replace("'","''")+"'";
        public Fx(IEnumerable<Mat> rows) {
            app=Activator.CreateInstance(Type.GetTypeFromProgID("VisualFoxPro.Application.9"));app.Visible=false;
            app.DoCmd("SET RESOURCE OFF");app.DoCmd("SET DEFAULT TO "+Q(work));
            app.DoCmd("CREATE CURSOR fxmat (kljnaz C(4),ident C(9),kolic N(12,2),vrijed N(11,2))");
            app.DoCmd("PUBLIC f_ident,f_wh,f_qty,f_value");
            foreach(var row in rows)app.DoCmd("INSERT INTO fxmat VALUES ("+Q(row.Kljnaz.Value.ToString(Inv))+","+Q(row.Ident)+","+N(row.Kolic??0)+","+N(row.Vrijed??0)+")");
        }
        decimal Eval(string expression)=>Convert.ToDecimal(app.Eval(expression),Inv);
        public StockBalance Balance(string ident,int warehouse) {
            app.DoCmd("SELECT fxmat");app.DoCmd("f_ident=PADR("+Q(ident)+",9)");app.DoCmd("f_wh=PADR("+Q(warehouse.ToString(Inv))+",4)");
            app.DoCmd("SUM kolic TO f_qty FOR ident=f_ident AND kljnaz=f_wh");app.DoCmd("SUM vrijed TO f_value FOR ident=f_ident AND kljnaz=f_wh");
            return new StockBalance{Quantity=Eval("ROUND(f_qty,2)"),Value=Eval("ROUND(f_value,2)")};
        }
        public decimal Price(StockBalance s)=>s.Quantity==0||s.Value==0?0:Eval("ROUND("+N(s.Value)+"/"+N(s.Quantity)+",9)");
        public decimal Value(decimal quantity,decimal nc)=>Eval("ROUND("+N(quantity)+"*"+N(nc)+",2)");
        public void Dispose() {
            object instance=(object)app;if(instance==null)return;
            try{app.Quit();}finally{try{Marshal.FinalReleaseComObject(instance);}catch(COMException){}app=null;}
        }
    }
    static void Calculation(StockBalance stock,decimal quantity,string name) {
        decimal price=fox.Price(stock),value=fox.Value(quantity,price);
        Check(stock.UnitCost==price,"Native Fox NC: "+name);
        Check(LedgerMath.IssueValue(stock,quantity)==value,"Native Fox issue value: "+name);
        cases.Add(new{Name=name,stock.Quantity,stock.Value,IssuedQuantity=quantity,NC=price,IssuedValue=value,RemainingValue=stock.Value-value});
    }
    static void Preview(Mat source,StockBalance stock) {
        decimal expected=fox.Value(stock.Quantity,fox.Price(stock));
        var issue=(IzlazMaterijalaViewModel)FormatterServices.GetUninitializedObject(typeof(IzlazMaterijalaViewModel));
        issue.CurrentItemMat=new Mat{Ident=source.Ident,Kljnaz=source.Kljnaz,Kolic=stock.Quantity};issue.NabavnaCijena();
        Check(issue.CurrentItemMat.Nc==fox.Price(stock)&&issue.CurrentItemMat.Vrijed==-expected,"Full issue UI preview matches Fox");
        var transfer=(MedjuskladisnicaViewModel)FormatterServices.GetUninitializedObject(typeof(MedjuskladisnicaViewModel));
        transfer.CurrentItemMat=new Mat{Ident=source.Ident,Kljnaz=source.Kljnaz,Kljnaz1=source.Kljnaz==1000?1001:1000,Kolic=stock.Quantity};transfer.NabavnaCijena();
        Check(transfer.CurrentItemMat.Nc==fox.Price(stock)&&transfer.CurrentItemMat.Vrijed==expected,"Full transfer UI preview matches Fox");
    }
    static void SaveScenario(string status,StockBalance fixture,decimal quantity,int index) {
        var before=Hashes();
        using(var root=new materijalno_knjigovodstvoContext()) {
            var c=root.Database.GetDbConnection();c.Open();using(var tx=c.BeginTransaction(IsolationLevel.Serializable)) {
                try {
                    // Existing SQL rows are changed only inside this rollback transaction.
                    Check(Convert.ToInt32(Read(c,tx,"SELECT COUNT(*) FROM dbo.mat WHERE ident='13' AND kljnaz=1000 AND status='P' AND is_draft=0").Rows[0][0])==1,"One existing opening row available for rollback fixture");
                    // Keep the existing opening row and isolate it from subsequent
                    // movements. Do not turn I/M rows into invalid zero-quantity rows.
                    Exec(c,tx,"DELETE FROM dbo.mat WHERE ident='13' AND kljnaz=1000 AND status<>'P'; UPDATE dbo.mat SET kolic="+fixture.Quantity.ToString(Inv)+",vrijed="+fixture.Value.ToString(Inv)+" WHERE ident='13' AND kljnaz=1000 AND status='P' AND is_draft=0;");
                    var service=new MaterialPostingService(Factory(c,tx));var stock=service.Balance("13",1000);
                    Check(stock.Quantity==fixture.Quantity&&stock.Value==fixture.Value,"SQL fixture balance");
                    int year=service.ActiveYear();decimal nc=fox.Price(stock),amount=fox.Value(quantity,nc);
                    var input=new Mat{Status=status,Ident="13",Kljnaz=1000,Kljnaz1=status=="M"?(int?)1001:null,Kolic=quantity,
                        Datun=new DateTime(year,2,1),Datnar=new DateTime(year,2,1),Brfak=service.ReserveDocumentNumber(status,year),Brdok="ROUNDING TEST",Redbr=1};
                    var saved=service.Save(input);
                    using(var db=Factory(c,tx)()) {
                        var rows=status=="M"?db.Mat.AsNoTracking().Where(m=>m.TransferPairId==saved.TransferPairId).ToList():db.Mat.AsNoTracking().Where(m=>m.Id==saved.Id).ToList();
                        Check(rows.Count==(status=="M"?2:1),"Saved issue/transfer row count");
                        foreach(var row in rows) {
                            int sign=row.Kljnaz==1000?-1:1;
                            Check(row.Nc==nc&&row.Vrijed==sign*amount&&row.Kolic==sign*quantity,"Saved NC, quantity, value match Fox "+status);
                            savedRows++;
                        }
                        if(status=="M")Check(rows.Sum(r=>r.Kolic)==0&&rows.Sum(r=>r.Vrijed)==0,"Transfer pair remains balanced");
                    }
                    var remaining=service.Balance("13",1000);
                    Check(remaining.Quantity==stock.Quantity-quantity&&remaining.Value==stock.Value-amount,"Rounding remainder matches Fox");
                    Console.WriteLine("PASS save "+status+" fixture "+index+" value="+amount+" remainder="+remaining.Value);
                } finally { tx.Rollback(); }
            }
        }
        var after=Hashes();Check(before.All(p=>after[p.Key]==p.Value),"All SQL rows unchanged after rollback");
    }
    [STAThread] public static int Main(string[] args) {
        work=args[0];bool passed=false;string error=null;Dictionary<string,string> before=null;
        try {
            Check(!DatabaseConfiguration.OracleExportEnabled,"Oracle export disabled in test process");
            using(var db=new materijalno_knjigovodstvoContext()) {
                Check(db.Database.GetDbConnection().Database=="materijalno_fox_20261105_102038","Existing working database only");
                var rows=db.Mat.AsNoTracking().ToList();before=Hashes();Save("hashes_before.json",before);
                using(fox=new Fx(rows)) {
                    foreach(var group in rows.GroupBy(r=>new{r.Ident,r.Kljnaz})) {
                        var sql=new StockBalance{Quantity=group.Sum(r=>r.Kolic??0),Value=group.Sum(r=>r.Vrijed??0)};
                        var native=fox.Balance(group.Key.Ident,group.Key.Kljnaz.Value);
                        Check(sql.Quantity==native.Quantity&&sql.Value==native.Value,"Native Fox stock totals "+group.Key);
                        if(sql.Quantity<=0||sql.Value<0)continue;
                        groupsChecked++;Calculation(sql,sql.Quantity,"full "+group.Key);
                        decimal part=Math.Truncate(sql.Quantity*50)/100;
                        if(part>0&&part<sql.Quantity)Calculation(sql,part,"partial "+group.Key);
                        if(groupsChecked<=6)Preview(group.First(),sql);
                    }
                    var fixtures=new[]{new StockBalance{Quantity=20000000m,Value=1234567.89m},new StockBalance{Quantity=30000000m,Value=1234567.90m},new StockBalance{Quantity=100m,Value=0m}};
                    Check(fox.Value(fixtures[0].Quantity,fox.Price(fixtures[0]))!=fixtures[0].Value,"Regression detects the removed full-balance shortcut");
                    for(int i=0;i<fixtures.Length;i++) {
                        var fixture=fixtures[i];Calculation(fixture,fixture.Quantity,"edge "+i);
                        SaveScenario("I",fixture,fixture.Quantity,i);SaveScenario("M",fixture,fixture.Quantity,i);
                    }
                    SaveScenario("I",fixtures[0],10000000m,3);SaveScenario("M",fixtures[0],10000000m,3);
                    foreach(decimal invalid in new[]{0m,-1m,101m}) {
                        bool rejected=false;try{LedgerMath.IssueValue(new StockBalance{Quantity=100,Value=10},invalid);}catch(InvalidOperationException){rejected=true;}
                        Check(rejected,"Quantity validation preserved");
                    }
                }
                Check(!db.ChangeTracker.HasChanges(),"Read-only context unchanged");
            }
            var after=Hashes();Save("hashes_after.json",after);Check(before.All(p=>after[p.Key]==p.Value),"All original SQL data unchanged");passed=true;
        } catch(Exception ex) { error=ex.ToString();Console.WriteLine(error); }
        bool unchanged=false;
        if(before!=null) {
            try { var after=Hashes();Save("hashes_after.json",after);unchanged=before.All(p=>after[p.Key]==p.Value); }
            catch(Exception ex) { error=(error??"")+"\nFinal data verification: "+ex; }
            if(!unchanged) { passed=false;error=(error??"")+"\nOriginal SQL rows differ after rollback."; }
        }
        Save("cases.json",cases);Save("result.json",new{Passed=passed,Checks=checks,StockGroupsChecked=groupsChecked,SavedTestRows=savedRows,EveryWriteRolledBack=true,AllOriginalSqlRowsUnchanged=unchanged,SqlBackupCreated=false,OracleUsed=false,Error=error});
        Console.WriteLine("RESULT passed="+passed+" checks="+checks+" stock groups="+groupsChecked+" saved rollback rows="+savedRows);
        return passed?0:1;
    }
}
