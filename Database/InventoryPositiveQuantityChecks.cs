using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;

// Native Fox EMPTY(0) parity, real screen validation, and real service saves.
// Every SQL test write belongs to one externally owned rollback transaction.
public static class InventoryPositiveQuantityChecks
{
    static string work;static int checks;static dynamic fox;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static void Save(string file,object value)=>File.WriteAllText(Path.Combine(work,file),new JavaScriptSerializer().Serialize(value));
    static DataTable Read(DbConnection c,DbTransaction tx,string sql){using(var cmd=c.CreateCommand()){cmd.Transaction=tx;cmd.CommandText=sql;cmd.CommandTimeout=90;using(var r=cmd.ExecuteReader()){var t=new DataTable();t.Load(r);return t;}}}
    static Dictionary<string,string> Hashes(){
        var result=new Dictionary<string,string>();using(var db=new materijalno_knjigovodstvoContext()){
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
    static bool Rejected(Action action){try{action();return false;}catch(InvalidOperationException ex){return ex.Message=="Unesite količinu veću od nule.";}}
    static Inv New(decimal? quantity)=>new Inv{Ident="13",Kljnaz=1000,Kolic=quantity,Nc=1.25m,Datun=new DateTime(DateTime.Today.Year,1,1),InventoryDate=new DateTime(DateTime.Today.Year,1,1),Redbr=1,Status="P"};
    static void Screen(){
        var vm=new UnosInventurnogStanjaViewModel();vm.BeginNewWarehouse();
        vm.CurrentItemInv.Kljnaz=1000;vm.CurrentItemInv.Ident="13";string focus=null;vm.RequestFocus+=target=>focus=target;
        foreach(decimal? q in new decimal?[]{0,null,-1}){
            vm.Kolicina=q;focus=null;
            Check(Rejected(vm.ValidateQuantity),"Quantity validation rejects zero/blank/negative");
            Check(focus=="Quantity","Invalid quantity requests quantity focus");
            Check(Rejected(()=>vm.SaveCurrentInventory()),"Snimi rejects invalid quantity before save");
            Check(vm.IsEntryEditable&&vm.HasUnsavedEntry&&vm.CurrentItemInv.Id==0,"Rejected entry remains editable and has no SQL ID");
        }
        vm.Kolicina=2.5m;vm.NabavnaCijenaUnos=1.25m;vm.ValidateQuantity();
        Check(vm.Vrijednost==Convert.ToDecimal(fox.Eval("ROUND(2.5*1.25,2)")),"Positive quantity still recalculates value like Fox");
        vm.CancelCurrentInventory();Check(!vm.HasUnsavedEntry,"Odustani still cancels invalid/unfinished entry");
    }
    static void Service(){
        using(var root=new materijalno_knjigovodstvoContext()){
            var c=root.Database.GetDbConnection();c.Open();using(var tx=c.BeginTransaction(IsolationLevel.Serializable)){
                try{
                    var create=Factory(c,tx);var service=new InventoryService(create);
                    int count=Convert.ToInt32(Read(c,tx,"SELECT COUNT(*) FROM dbo.inv").Rows[0][0]);
                    foreach(decimal? q in new decimal?[]{0,null,-1}){
                        Check(Rejected(()=>service.Save(New(q))),"Service rejects invalid new quantity");
                        Check(Convert.ToInt32(Read(c,tx,"SELECT COUNT(*) FROM dbo.inv").Rows[0][0])==count,"Rejected new entry inserts no row");
                    }
                    var saved=service.Save(New(2.5m));
                    Check(saved.Kolic==2.5m&&saved.Nc==1.25m&&saved.Vrijed==Convert.ToDecimal(fox.Eval("ROUND(2.5*1.25,2)")),"Positive inventory saves correct quantity, NC and value");
                    Check(saved.Status=="P"&&!saved.IsDraft,"Saved inventory keeps normal status and posted flag");
                    foreach(decimal? q in new decimal?[]{0,null,-1}){
                        Inv edit;using(var db=create())edit=db.Inv.AsNoTracking().Single(i=>i.Id==saved.Id);
                        var oldVersion=(byte[])edit.RowVersion.Clone();edit.Kolic=q;
                        Check(Rejected(()=>service.Save(edit)),"Service rejects invalid edited quantity");
                        using(var db=create()){
                            var actual=db.Inv.AsNoTracking().Single(i=>i.Id==saved.Id);
                            Check(actual.Kolic==2.5m&&actual.Vrijed==3.13m&&actual.RowVersion.SequenceEqual(oldVersion),"Rejected edit leaves saved row and row version unchanged");
                        }
                    }
                    using(var db=create())saved=db.Inv.AsNoTracking().Single(i=>i.Id==saved.Id);
                    saved.Kolic=4;saved=service.Save(saved);
                    Check(saved.Kolic==4&&saved.Nc==1.25m&&saved.Vrijed==5,"Positive edit still saves correctly");
                }finally{tx.Rollback();}
            }
        }
    }
    [STAThread]public static int Main(string[] args){
        work=args[0];bool passed=false,unchanged=false;string error=null;Dictionary<string,string> before=null;
        try{
            Check(!DatabaseConfiguration.OracleExportEnabled,"Oracle disabled in verification process");
            using(var db=new materijalno_knjigovodstvoContext())Check(db.Database.GetDbConnection().Database=="materijalno_fox_20261105_102038","Existing working database only");
            before=Hashes();Save("hashes_before.json",before);
            fox=Activator.CreateInstance(Type.GetTypeFromProgID("VisualFoxPro.Application.9"));fox.Visible=false;fox.DoCmd("SET RESOURCE OFF");
            Check((bool)fox.Eval("EMPTY(0)"),"Native Fox EMPTY rejects numeric zero");
            Check(!(bool)fox.Eval("EMPTY(0.01)"),"Native Fox accepts nonzero quantity");
            Screen();Service();passed=true;
        }catch(Exception ex){error=ex.ToString();Console.WriteLine(error);}
        finally{if((object)fox!=null){object instance=(object)fox;try{fox.Quit();}finally{Marshal.FinalReleaseComObject(instance);}}}
        if(before!=null){try{var after=Hashes();Save("hashes_after.json",after);unchanged=before.All(p=>after[p.Key]==p.Value);}catch(Exception ex){error=(error??"")+"\n"+ex;}if(!unchanged)passed=false;}
        Save("result.json",new{Passed=passed,Checks=checks,AllOriginalSqlRowsUnchanged=unchanged,EveryWriteRolledBack=true,SqlBackupCreated=false,OracleUsed=false,Error=error});
        Console.WriteLine("RESULT passed="+passed+" checks="+checks+" unchanged="+unchanged);return passed?0:1;
    }
}
