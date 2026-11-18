using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Materijalno.Import;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.UI;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;

public static class InitialStockPostingChecks
{
    static string work;static int checks;static PocetnoStanjePrviPutViewModel vm;
    static HashSet<int> originalIds;static HashSet<Guid> originalDocuments;
    static readonly Dictionary<int,Guid> owned=new Dictionary<int,Guid>();
    static readonly HashSet<Guid> testDocuments=new HashSet<Guid>();
    static readonly List<Dictionary<string,object>> results=new List<Dictionary<string,object>>();
    static readonly string[] codes={"25","163","094001"};
    static readonly decimal[] quantities={11m,7m,13m};
    const int warehouse=1000;
    static string Cs=>DatabaseConfiguration.MaterialConnectionString;
    static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks++;Console.WriteLine("PASS "+label);}
    static string Dec(decimal d)=>d.ToString("0.#########",CultureInfo.InvariantCulture);
    static void SaveJson(string name,object value)=>File.WriteAllText(Path.Combine(work,name),new JavaScriptSerializer().Serialize(value));
    static void Pump(Dispatcher d)=>d.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    static void Blur(TextBox t,IInputElement next){t.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,Environment.TickCount,t,next){RoutedEvent=Keyboard.LostKeyboardFocusEvent});Pump(t.Dispatcher);}
    static void TrackCurrent(){var row=vm?.CurrentItemMat;if(row!=null&&row.Id>0&&originalIds!=null&&!originalIds.Contains(row.Id)){owned[row.Id]=row.LineToken;SaveJson("owned_rows.json",owned.Select(p=>new{Id=p.Key,LineToken=p.Value}).ToArray());}}
    static Dictionary<string,string> Hashes(){
        var hashes=new Dictionary<string,string>();using(var c=new SqlConnection(Cs)){c.Open();foreach(string table in new[]{"mat","inv","tabela_materijala","sifarnik_materijal_skladiste_konto","material_document","nalmat","financial_batch","financial_export_log","document_counter"}){
            using(var cmd=new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.["+table+"] ORDER BY "+(table=="document_counter"?"name,book_year":"id")+" FOR XML RAW,BINARY BASE64))",c)){cmd.CommandTimeout=120;var value=cmd.ExecuteScalar();string xml=value==DBNull.Value?"":(string)value;using(var sha=SHA256.Create())hashes[table]=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(xml))).Replace("-","");}
        }}return hashes;
    }
    static void Cleanup(){
        TrackCurrent();if(owned.Count==0)return;
        using(var c=new SqlConnection(Cs)){c.Open();using(var tx=c.BeginTransaction(IsolationLevel.Serializable)){
            Action<string> exec=sql=>{using(var q=new SqlCommand(sql,c,tx)){q.CommandTimeout=120;q.ExecuteNonQuery();}};
            exec("SET XACT_ABORT ON; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Materijalno:ledger',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Ledger busy during test cleanup',1;");
            foreach(var pair in owned){
                using(var q=new SqlCommand("DELETE FROM dbo.mat WHERE id=@id AND line_token=@token AND status='P' AND fox_recno IS NULL",c,tx)){
                    q.Parameters.AddWithValue("@id",pair.Key);q.Parameters.AddWithValue("@token",pair.Value);int affected=q.ExecuteNonQuery();if(affected!=1)throw new InvalidOperationException("Cannot identify the exact owned test row for cleanup: "+pair.Key);
                }
            }
            foreach(var id in testDocuments.Where(id=>!originalDocuments.Contains(id))){
                using(var q=new SqlCommand("DELETE FROM dbo.material_document WHERE id=@id AND status='P' AND warehouse=1000 AND number='' AND NOT EXISTS(SELECT 1 FROM dbo.mat WHERE document_id=@id) AND NOT EXISTS(SELECT 1 FROM dbo.financial_batch WHERE source_document_id=@id)",c,tx)){q.Parameters.AddWithValue("@id",id);q.ExecuteNonQuery();}
            }
            tx.Commit();
        }}
        SaveJson("cleanup.json",new{Complete=true,RemovedRows=owned.Count,Ids=owned.Keys.ToArray(),ExistingRowsTouched=false,Note="SQL identity and global row-version counters naturally advance during real inserts; business counters are verified separately."});
        Console.WriteLine("CLEANUP removed exactly "+owned.Count+" owned test rows.");
    }
    [STAThread] public static int Main(string[] args){
        bool passed=false,cleaned=false;string failure=null;Dictionary<string,string> before=null;
        work=args[0];
        try{
            var target=new SqlConnectionStringBuilder(Cs);Check(target.InitialCatalog=="materijalno_fox_20261105_102038","Only current authorized database");
            Check(Process.GetProcessesByName("Materijalno.UI").Length==0,"Application is closed during temporary postings");
            var backup=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(work,"backup_before.json")));
            Check((bool)backup["Verified"]&&(string)backup["Database"]==target.InitialCatalog,"Verified full backup before actual saves");
            before=Hashes();SaveJson("hashes_before.json",before);
            int year;decimal baseline;List<Mat> existing;
            using(var db=new materijalno_knjigovodstvoContext()){
                existing=db.Mat.IgnoreQueryFilters().AsNoTracking().ToList();originalIds=new HashSet<int>(existing.Select(m=>m.Id));originalDocuments=new HashSet<Guid>(db.MaterialDocuments.AsNoTracking().Select(d=>d.Id).ToList());
                year=int.Parse(db.ApplicationSettings.Single(s=>s.KeyName=="ActiveBookYear").Value,CultureInfo.InvariantCulture);
                Check(year==2026,"Fox snapshot and open book year agree");
                baseline=existing.Where(m=>!m.IsDraft&&!m.IsArchived&&m.Status=="P"&&m.Kljnaz==warehouse&&m.BookYear==year).Sum(m=>m.Vrijed??0);
                foreach(string code in codes){Check(!existing.Any(m=>!m.IsArchived&&m.Status=="P"&&m.Kljnaz==warehouse&&m.Ident==code),"No pre-existing opening row for "+code);Check(db.SifarnikMaterijalSkladisteKonto.Any(l=>l.Sifmat==code&&l.Sifskla==warehouse),"Existing valid warehouse link for "+code);}
            }
            var fox=DbfReader.Read(Path.Combine(work,"fox_source","mat.dbf")).Records.Where(r=>!r.Deleted).ToList();
            var prices=new Dictionary<string,decimal>();foreach(string code in codes){var first=fox.First(r=>r.Values["STATUS"]=="U"&&r.Values["IDENT"].Trim()==code&&r.Values["KLJNAZ"].Trim()==warehouse.ToString());prices[code]=decimal.Parse(first.Values["NC"],CultureInfo.InvariantCulture);}
            SaveJson("plan.json",new{Database=target.InitialCatalog,Warehouse=warehouse,BookYear=year,Baseline=baseline,Rows=codes.Select((code,i)=>new{Ident=code,Quantity=quantities[i],FoxPrice=prices[code],FoxExpectedValue=Math.Round(quantities[i]*prices[code],2,MidpointRounding.AwayFromZero)}).ToArray(),CleanupAfterTest=true});
            // Construct the real application VM and use the same WPF command bindings as its page.
            vm=new PocetnoStanjePrviPutViewModel(new GlavniViewModel());
            var view=new PocetnoStanjePrviPut{DataContext=vm};view.Measure(new Size(1600,1000));view.Arrange(new Rect(0,0,1600,1000));view.UpdateLayout();Pump(view.Dispatcher);
            var wh=(TextBox)view.FindName("OpBroj");var ident=(TextBox)view.FindName("txtIdent");var qty=(TextBox)view.FindName("txtKolic");var nc=(TextBox)view.FindName("txtNc");var total=(TextBox)view.FindName("txtUkupno");
            var create=(Button)view.FindName("btnNovaKalkulacija");var save=(Button)view.FindName("btnSnimi");var next=(Button)view.FindName("btnSlijedecaStavka");decimal addition=0;
            for(int i=0;i<codes.Length;i++){
                Check(Process.GetProcessesByName("Materijalno.UI").Length==0,"No other application opened before posting "+(i+1));
                var command=i==0?create.Command:next.Command;Check(command.CanExecute(null),i==0?"Novo skl. is available":"Slij. stavka is available after save");
                command.Execute(null);TrackCurrent();Check(vm.CurrentItemMat.IsDraft&&owned.ContainsKey(vm.CurrentItemMat.Id),"Actual new draft inserted by application command");
                wh.SetCurrentValue(TextBox.TextProperty,warehouse.ToString());Blur(wh,ident);ident.SetCurrentValue(TextBox.TextProperty,codes[i]);Blur(ident,qty);
                Check(vm.CurrentItemMat.Ident==codes[i]&&vm.CurrentItemMat.Nc==prices[codes[i]],"Automatic NC matches first Fox receipt for "+codes[i]);
                qty.SetCurrentValue(TextBox.TextProperty,quantities[i].ToString(CultureInfo.GetCultureInfo("de-DE")));Blur(qty,nc);Blur(nc,qty);
                decimal expected=Math.Round(quantities[i]*prices[codes[i]],2,MidpointRounding.AwayFromZero);
                Check(vm.CurrentItemMat.Vrijed==expected&&vm.SumVrijed==baseline+addition+expected,"Preview line and cumulative warehouse total agree before save");
                Check(save.Command.CanExecute(null),"Snimi command available");
                Console.WriteLine("SAVING "+codes[i]+"; quantity="+Dec(quantities[i])+"; NC="+Dec(prices[codes[i]])+"; expected="+Dec(expected)+"; dismiss the success-only Potvrda dialog.");Console.Out.Flush();
                save.Command.Execute(null);Pump(view.Dispatcher);TrackCurrent();
                using(var db=new materijalno_knjigovodstvoContext()){
                    var stored=db.Mat.AsNoTracking().Single(m=>m.Id==vm.CurrentItemMat.Id);if(stored.DocumentId.HasValue)testDocuments.Add(stored.DocumentId.Value);
                    decimal sqlTotal=db.Mat.Where(m=>m.Status=="P"&&m.Kljnaz==warehouse&&m.BookYear==year).Sum(m=>m.Vrijed)??0;addition+=expected;
                    Check(!stored.IsDraft&&stored.Status=="P"&&stored.Kolic==quantities[i]&&stored.Nc==prices[codes[i]]&&stored.Vrijed==expected,"Committed row reloaded from independent EF context: "+codes[i]);
                    Check(sqlTotal==baseline+addition&&vm.SumVrijed==sqlTotal,"Warehouse total agrees after actual save "+(i+1));
                    Check(total.Text==sqlTotal.ToString("N2",CultureInfo.GetCultureInfo("de-DE")),"WPF total display agrees after save");
                    Check(stored.Datun==new DateTime(year,1,1)&&stored.FoxRecno==null,"Opening date and Fox source marker are correct");
                    var master=db.TabelaMaterijala.AsNoTracking().Single(m=>m.Ident==codes[i]);Check(stored.Konto1==master.Konto1&&(stored.Konto2??"")== (master.Konto2??""),"Both material accounts agree after save");
                    results.Add(new Dictionary<string,object>{{"Id",stored.Id},{"Ident",codes[i]},{"Quantity",stored.Kolic},{"Nc",stored.Nc},{"Value",stored.Vrijed},{"WarehouseTotal",sqlTotal},{"FoxPrice",prices[codes[i]]},{"FoxExpectedValue",expected}});SaveJson("saved_results.json",results);
                    Console.WriteLine("SAVED "+codes[i]+"; id="+stored.Id+"; NC="+Dec(stored.Nc.Value)+"; value="+Dec(stored.Vrijed.Value)+"; total="+Dec(sqlTotal));
                }
            }
            var reopened=new PocetnoStanjePrviPutViewModel(new GlavniViewModel());foreach(int id in owned.Keys){reopened.CurrentItemMat=reopened.MatList.Single(m=>m.Id==id);Check(reopened.SumVrijed==baseline+addition,"Reopened page retains saved total for row "+id);}
            bool duplicateRejected=false;try{new MaterialPostingService().Save(new Mat{Status="P",Ident=codes[0],Kljnaz=warehouse,Kolic=1,Nc=prices[codes[0]],BookYear=year});}catch(InvalidOperationException e){duplicateRejected=e.Message.Contains("već postoji");}
            Check(duplicateRejected,"Duplicate opening state rejected without another saved row");
            passed=true;
        }catch(Exception e){failure=e.GetType().Name+": "+e.Message;Console.WriteLine("FAIL "+failure);}
        finally{try{Cleanup();cleaned=true;}catch(Exception e){failure=(failure??"")+" CLEANUP: "+e.GetType().Name+": "+e.Message;Console.WriteLine("FAIL "+failure);}}
        try{
            if(before!=null){var after=Hashes();SaveJson("hashes_after.json",after);Check(before.All(p=>after[p.Key]==p.Value),"All existing rows, account links, documents, financial rows and business counters restored exactly after cleanup");}
        }catch(Exception e){passed=false;failure=(failure??"")+" AFTER: "+e.Message;Console.WriteLine("FAIL "+failure);}
        SaveJson("result.json",new{Passed=passed&&cleaned,CleanupComplete=cleaned,Checks=checks,SavedRows=results.Count,Failure=failure,NewDatabaseCreated=false,FoxModified=false});
        if(passed&&cleaned){Console.WriteLine("ALL PASSED "+checks+"; three actual application saves; fresh SQL reload and reopened VM; exact cleanup; no Fox writes or financial export.");return 0;}return 1;
    }
}
