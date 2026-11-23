using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Microsoft.EntityFrameworkCore;

// Tests the real posting service against Fox's YEAR(DATE()) rule. All SQL
// mutations belong to an external transaction and are always rolled back.
public static class OpeningCalendarDateChecks
{
    static string work, material;static int checks, scenarios;static DateTime foxDate;
    static readonly List<object> cases=new List<object>();
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static void Save(string file,object value)=>File.WriteAllText(Path.Combine(work,file),new JavaScriptSerializer().Serialize(value));
    static DataTable Read(DbConnection c,DbTransaction tx,string sql){using(var cmd=c.CreateCommand()){cmd.Transaction=tx;cmd.CommandText=sql;cmd.CommandTimeout=90;using(var r=cmd.ExecuteReader()){var t=new DataTable();t.Load(r);return t;}}}
    static void Exec(DbConnection c,DbTransaction tx,string sql){using(var cmd=c.CreateCommand()){cmd.Transaction=tx;cmd.CommandText=sql;cmd.CommandTimeout=90;cmd.ExecuteNonQuery();}}
    static Dictionary<string,string> Hashes(){
        var result=new Dictionary<string,string>();
        using(var db=new materijalno_knjigovodstvoContext()){
            var c=db.Database.GetDbConnection();c.Open();
            foreach(string table in new[]{"mat","inv","nalmat","material_document","document_counter","app_settings","financial_batch","financial_export_log","year_close","tabela_materijala","sifarnik_skladista","sifarnik_konta","sifarnik_materijal_skladiste_konto"}){
                string order=table=="document_counter"?"name,book_year":table=="app_settings"?"key_name":"id";
                var value=Read(c,null,"SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.["+table+"] ORDER BY "+order+" FOR XML RAW,BINARY BASE64))").Rows[0][0];
                using(var sha=SHA256.Create())result[table]=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value==DBNull.Value?"":(string)value))).Replace("-","");
            }
        }return result;
    }
    static Func<materijalno_knjigovodstvoContext> Factory(DbConnection c,DbTransaction tx)=>()=>{
        var db=new materijalno_knjigovodstvoContext(new DbContextOptionsBuilder<materijalno_knjigovodstvoContext>().UseSqlServer(c).Options);db.Database.UseTransaction(tx);return db;
    };
    static void Scenario(int activeYear,bool persistedDraft){
        var before=Hashes();
        using(var root=new materijalno_knjigovodstvoContext()){
            var c=root.Database.GetDbConnection();c.Open();using(var tx=c.BeginTransaction(IsolationLevel.Serializable)){
                try{
                    Exec(c,tx,"UPDATE dbo.app_settings SET value='"+activeYear+"' WHERE key_name='ActiveBookYear';");
                    var create=Factory(c,tx);var service=new MaterialPostingService(create);
                    var input=new Mat{Status="P",Ident=material,Kljnaz=1000,Kolic=10,Nc=1.25m,Datun=new DateTime(1999,3,5),Datnar=new DateTime(1999,3,5),BookYear=1999,Brfak="DATE-CHECK",Brdok="",Redbr=1};
                    if(persistedDraft){using(var db=create()){input.IsDraft=true;db.Mat.Add(input);db.SaveChanges();}}
                    var saved=service.Save(input);
                    Check(saved.Datun==foxDate,"New opening DATUN matches native Fox calendar date");
                    Check(saved.Status=="P"&&!saved.IsDraft,"Opening posted with status P");
                    Check(saved.BookYear==foxDate.Year,"Technical book year follows saved DATUN");
                    Check(saved.Kolic==10&&saved.Nc==1.25m&&saved.Vrijed==12.50m,"Quantity, price and value unchanged by date rule");
                    Check(service.ActiveYear()==activeYear,"Saving does not change active year setting");
                    using(var db=create()){
                        var actual=db.Mat.AsNoTracking().Single(m=>m.Id==saved.Id);
                        Check(actual.Datun==foxDate&&actual.BookYear==foxDate.Year,"SQL reload keeps calendar date and year");
                        Check(db.MaterialDocuments.Single(d=>d.Id==actual.DocumentId).BookYear==foxDate.Year,"Document metadata uses saved calendar year");
                        Check(ReportQueries.Period(db,foxDate,new DateTime(foxDate.Year,12,31)).Any(m=>m.Id==saved.Id),"Opening visible in matching year's reports");
                    }
                    bool rejected=false;
                    try{service.Save(new Mat{Status="P",Ident=material,Kljnaz=1000,Kolic=1,Nc=1,Brfak="DATE-CHECK"});}catch(InvalidOperationException e){rejected=e.Message.Contains("već postoji");}
                    Check(rejected,"Duplicate-opening validation unchanged");
                    // Emulate an existing historical opening. Fox's edit branch
                    // uses GATHER and does not replace DATUN with today's year.
                    DateTime historic=new DateTime(foxDate.Year-2,1,1);
                    Exec(c,tx,"UPDATE dbo.mat SET datun='"+historic.ToString("yyyyMMdd",CultureInfo.InvariantCulture)+"',datnar=NULL,book_year="+historic.Year+" WHERE id="+saved.Id+";");
                    Mat edited;using(var db=create())edited=db.Mat.AsNoTracking().Single(m=>m.Id==saved.Id);
                    edited.Kolic=11;edited.Datun=new DateTime(2001,1,1);
                    edited=service.Save(edited);
                    Check(edited.Datun==historic,"Edit preserves existing opening DATUN");
                    Check(edited.Datnar==null,"Edit does not invent a historical DATNAR");
                    Check(edited.BookYear==historic.Year&&edited.Vrijed==13.75m,"Edit metadata and value remain consistent");
                    cases.Add(new{ActiveYear=activeYear,PersistedDraft=persistedDraft,NewDatun=foxDate.ToString("yyyy-MM-dd"),NewBookYear=saved.BookYear,EditedDatun=edited.Datun.Value.ToString("yyyy-MM-dd")});
                }finally{tx.Rollback();}
            }
        }
        var after=Hashes();Check(before.All(p=>after[p.Key]==p.Value),"All SQL rows and settings restored by rollback");scenarios++;
        Console.WriteLine("PASS activeYear="+activeYear+" persistedDraft="+persistedDraft+" Fox DATUN="+foxDate.ToString("yyyy-MM-dd"));
    }
    [STAThread]public static int Main(string[] args){
        work=args[0];bool passed=false,unchanged=false;string error=null;Dictionary<string,string> before=null;dynamic fox=null;
        try{
            Check(!DatabaseConfiguration.OracleExportEnabled,"Oracle disabled in verification process");
            using(var db=new materijalno_knjigovodstvoContext()){
                Check(db.Database.GetDbConnection().Database=="materijalno_fox_20261105_102038","Existing working database only");
                material=db.SifarnikMaterijalSkladisteKonto.Where(l=>l.Sifskla==1000&&!db.Mat.Any(m=>m.Ident==l.Sifmat&&m.Kljnaz==1000)).OrderBy(l=>l.Sifmat).Select(l=>l.Sifmat).First();
            }
            before=Hashes();Save("hashes_before.json",before);
            fox=Activator.CreateInstance(Type.GetTypeFromProgID("VisualFoxPro.Application.9"));fox.Visible=false;fox.DoCmd("SET RESOURCE OFF");
            foxDate=Convert.ToDateTime(fox.Eval("DATE(YEAR(DATE()),1,1)"));
            Check(foxDate==new DateTime(DateTime.Today.Year,1,1),"Fox and .NET agree on current calendar year");
            foreach(int year in new[]{foxDate.Year-1,foxDate.Year,foxDate.Year+1})foreach(bool draft in new[]{false,true})Scenario(year,draft);
            passed=true;
        }catch(Exception ex){error=ex.ToString();Console.WriteLine(error);}
        finally{if((object)fox!=null){object instance=(object)fox;try{fox.Quit();}finally{Marshal.FinalReleaseComObject(instance);}}}
        if(before!=null){try{var after=Hashes();Save("hashes_after.json",after);unchanged=before.All(p=>after[p.Key]==p.Value);}catch(Exception ex){error=(error??"")+"\n"+ex;}if(!unchanged)passed=false;}
        Save("cases.json",cases);Save("result.json",new{Passed=passed,Checks=checks,Scenarios=scenarios,FoxCalendarDate=foxDate.ToString("yyyy-MM-dd"),AllOriginalSqlRowsUnchanged=unchanged,EveryWriteRolledBack=true,SqlBackupCreated=false,OracleUsed=false,Error=error});
        Console.WriteLine("RESULT passed="+passed+" checks="+checks+" scenarios="+scenarios);return passed?0:1;
    }
}
