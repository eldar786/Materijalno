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

public static class InitialStockEditDeleteChecks
{
    static string work;static int checks;static PocetnoStanjePrviPutViewModel vm;static PocetnoStanjePrviPut view;
    static HashSet<int> originalIds;static HashSet<Guid> originalDocuments;
    static readonly Dictionary<int,Guid> owned=new Dictionary<int,Guid>();
    static readonly HashSet<Guid> documents=new HashSet<Guid>();
    static readonly List<object> results=new List<object>();
    static readonly string[] codes={"25","163","094001"};
    static string Cs=>DatabaseConfiguration.MaterialConnectionString;
    static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks++;Console.WriteLine("PASS "+text);}
    static void Json(string name,object value)=>File.WriteAllText(Path.Combine(work,name),new JavaScriptSerializer().Serialize(value));
    static void Pump()=>view.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    static TextBox Box(string name)=>(TextBox)view.FindName(name);
    static void Blur(TextBox t,IInputElement next){t.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,Environment.TickCount,t,next){RoutedEvent=Keyboard.LostKeyboardFocusEvent});Pump();}
    static void Bind(PocetnoStanjePrviPutViewModel value){vm=value;view=new PocetnoStanjePrviPut{DataContext=vm};view.Measure(new Size(1600,1000));view.Arrange(new Rect(0,0,1600,1000));view.UpdateLayout();Pump();}
    static void Track(){var m=vm?.CurrentItemMat;if(m!=null&&m.Id>0&&originalIds!=null&&!originalIds.Contains(m.Id)){owned[m.Id]=m.LineToken;if(m.DocumentId.HasValue)documents.Add(m.DocumentId.Value);Json("owned_rows.json",owned.Select(p=>new{Id=p.Key,Token=p.Value}).ToArray());}}
    static Mat Read(int id){using(var db=new materijalno_knjigovodstvoContext())return db.Mat.IgnoreQueryFilters().AsNoTracking().SingleOrDefault(m=>m.Id==id);}
    static decimal Total(int warehouse){using(var db=new materijalno_knjigovodstvoContext())return db.Mat.Where(m=>m.Status=="P"&&m.Kljnaz==warehouse&&m.BookYear==2026).Sum(m=>m.Vrijed)??0;}
    static void Select(int id){vm.CurrentIndex=vm.MatList.ToList().FindIndex(m=>m.Id==id);vm.CurrentItemMat=vm.MatList[vm.CurrentIndex];vm.Osvjezi();Pump();}
    static void DisplayAfterSaveOrDelete(){var current=Read(vm.CurrentItemMat.Id);Check(current!=null&&current.Nc==vm.CurrentItemMat.Nc,"Selected row NC agrees with fresh SQL after refresh");Check(vm.SumVrijed==Total(current.Kljnaz.Value)&&Box("txtUkupno").Text==vm.SumVrijed.ToString("N2",CultureInfo.GetCultureInfo("de-DE")),"Visible total agrees with selected warehouse after refresh");}
    static Dictionary<string,string> Hashes(){var r=new Dictionary<string,string>();using(var c=new SqlConnection(Cs)){c.Open();foreach(string table in new[]{"mat","inv","tabela_materijala","sifarnik_materijal_skladiste_konto","material_document","nalmat","financial_batch","financial_export_log","document_counter"})using(var q=new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.["+table+"] ORDER BY "+(table=="document_counter"?"name,book_year":"id")+" FOR XML RAW,BINARY BASE64))",c)){q.CommandTimeout=120;var raw=q.ExecuteScalar();using(var sha=SHA256.Create())r[table]=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw==DBNull.Value?"":(string)raw))).Replace("-","");}}return r;}
    static void Cleanup(){Track();using(var c=new SqlConnection(Cs)){c.Open();using(var tx=c.BeginTransaction(IsolationLevel.Serializable)){
        using(var q=new SqlCommand("SET XACT_ABORT ON; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Materijalno:ledger',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Ledger busy',1",c,tx))q.ExecuteNonQuery();int removed=0;
        foreach(var pair in owned){using(var q=new SqlCommand("DELETE FROM dbo.mat WHERE id=@id AND line_token=@token AND status='P' AND fox_recno IS NULL",c,tx)){q.Parameters.AddWithValue("@id",pair.Key);q.Parameters.AddWithValue("@token",pair.Value);removed+=q.ExecuteNonQuery();}}
        foreach(Guid id in documents.Where(id=>!originalDocuments.Contains(id)))using(var q=new SqlCommand("DELETE FROM dbo.material_document WHERE id=@id AND status='P' AND NOT EXISTS(SELECT 1 FROM dbo.mat WHERE document_id=@id) AND NOT EXISTS(SELECT 1 FROM dbo.financial_batch WHERE source_document_id=@id)",c,tx)){q.Parameters.AddWithValue("@id",id);q.ExecuteNonQuery();}
        tx.Commit();Json("cleanup.json",new{Complete=true,OwnedRows=owned.Count,RemainingRowsRemoved=removed,Ids=owned.Keys.ToArray()});
    }}}
    [STAThread] public static int Main(string[] args){work=args[0];bool passed=false,cleaned=false;string error=null;Dictionary<string,string> before=null;
        try{
            Check(new SqlConnectionStringBuilder(Cs).InitialCatalog=="materijalno_fox_20261105_102038","Only current authorized database");Check(Process.GetProcessesByName("Materijalno.UI").Length==0,"Application closed before temporary edit/delete test");
            var backup=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(work,"backup_before.json")));Check((bool)backup["Verified"],"Verified backup before writes");before=Hashes();Json("hashes_before.json",before);
            using(var db=new materijalno_knjigovodstvoContext()){originalIds=new HashSet<int>(db.Mat.IgnoreQueryFilters().Select(m=>m.Id).ToList());originalDocuments=new HashSet<Guid>(db.MaterialDocuments.Select(m=>m.Id).ToList());foreach(string code in codes)Check(!db.Mat.IgnoreQueryFilters().Any(m=>m.Status=="P"&&m.Ident==code&&m.Kljnaz==1000&&!m.IsArchived),"No original opening state for fixture "+code);}
            var fox=DbfReader.Read(Path.Combine(work,"fox_source","mat.dbf")).Records.Where(r=>!r.Deleted).ToList();var prices=codes.ToDictionary(code=>code,code=>decimal.Parse(fox.First(r=>r.Values["STATUS"]=="U"&&r.Values["IDENT"].Trim()==code&&r.Values["KLJNAZ"].Trim()=="1000").Values["NC"],CultureInfo.InvariantCulture));
            decimal baseline=Total(1000);var ids=new Dictionary<string,int>();decimal delta=0;
            Bind(new PocetnoStanjePrviPutViewModel(new GlavniViewModel()));
            for(int i=0;i<codes.Length;i++){
                (i==0?vm.NovoSklCommand:vm.SlijStavkaButtonCommand).Execute(null);Track();Box("OpBroj").SetCurrentValue(TextBox.TextProperty,"1000");Blur(Box("OpBroj"),Box("txtIdent"));Box("txtIdent").SetCurrentValue(TextBox.TextProperty,codes[i]);Blur(Box("txtIdent"),Box("txtKolic"));decimal q=new[]{11m,7m,13m}[i];Box("txtKolic").SetCurrentValue(TextBox.TextProperty,q.ToString(CultureInfo.InvariantCulture));Blur(Box("txtKolic"),Box("txtNc"));
                Check(vm.CurrentItemMat.Nc==prices[codes[i]],"Automatic NC matches Fox before test save "+codes[i]);var stored=vm.SaveCurrentOpening();Track();ids[codes[i]]=stored.Id;delta+=Math.Round(q*prices[codes[i]],2,MidpointRounding.AwayFromZero);Check(Total(1000)==baseline+delta,"Fixture saved with correct warehouse total");
            }
            var edits=new[]{new{Code="25",Quantity=11m,Price=4.25m},new{Code="163",Quantity=9m,Price=prices["163"]},new{Code="094001",Quantity=20m,Price=0.2m}};
            foreach(var edit in edits){
                Select(ids[edit.Code]);var old=Read(ids[edit.Code]);Check(Box("txtKolic").IsReadOnly&&Box("txtNc").IsReadOnly,"Saved row is read only before Izmjena");vm.UpdateCommand.Execute(null);Pump();Check(vm.IsEntryEditable&&!Box("txtNc").IsReadOnly&&vm.SpasiNovuKalkulacijuCommand.CanExecute(null)&&vm.OdustaniCommand.CanExecute(null),"Izmjena enters edit mode with Snimi/Odustani");
                Check(Read(old.Id).RowVersion.SequenceEqual(old.RowVersion),"Izmjena click itself does not save or change row version");
                Box("txtKolic").SetCurrentValue(TextBox.TextProperty,edit.Quantity.ToString(CultureInfo.GetCultureInfo("de-DE")));Blur(Box("txtKolic"),Box("txtNc"));Box("txtNc").SetCurrentValue(TextBox.TextProperty,edit.Price.ToString(CultureInfo.GetCultureInfo("de-DE")));Blur(Box("txtNc"),Box("txtKolic"));decimal expected=Math.Round(edit.Quantity*edit.Price,2,MidpointRounding.AwayFromZero);
                Check(Read(old.Id).Nc==old.Nc&&Read(old.Id).Kolic==old.Kolic,"Typing changed NC/quantity does not persist before Snimi");Check(vm.CurrentItemMat.Vrijed==expected&&vm.SumVrijed==baseline+delta-old.Vrijed.Value+expected,"Edit preview replaces old value once, matching Fox quantity-times-price formula");
                vm.SaveCurrentOpening();var actual=Read(old.Id);delta+=expected-old.Vrijed.Value;Check(actual.Id==old.Id&&actual.Nc==edit.Price&&actual.Kolic==edit.Quantity&&actual.Vrijed==expected,"Snimi updates the same SQL row with exact NC/quantity/value");Check(Total(1000)==baseline+delta&&vm.SumVrijed==baseline+delta,"SQL and UI totals agree after edit save");Check(vm.IsEntryReadOnly&&!vm.OdustaniCommand.CanExecute(null)&&!vm.SlijStavkaButtonCommand.CanExecute(null),"Edit save returns to browsing; no pending cancel or new-line state");DisplayAfterSaveOrDelete();results.Add(new{Action="Edit",Ident=edit.Code,Nc=actual.Nc,Quantity=actual.Kolic,Value=actual.Vrijed,WarehouseTotal=Total(1000)});Json("results.json",results);
            }
            Bind(new PocetnoStanjePrviPutViewModel(new GlavniViewModel()));foreach(var edit in edits){Select(ids[edit.Code]);Check(vm.CurrentItemMat.Nc==edit.Price&&vm.SumVrijed==baseline+delta,"Reopened page retains edited NC and total for "+edit.Code);}
            Select(ids["25"]);var cancelBefore=Read(ids["25"]);vm.UpdateCommand.Execute(null);Box("txtNc").SetCurrentValue(TextBox.TextProperty,"999");Blur(Box("txtNc"),Box("txtKolic"));vm.CancelCurrentOpening();Pump();Check(vm.CurrentItemMat.Nc==cancelBefore.Nc&&vm.SumVrijed==baseline+delta&&Read(cancelBefore.Id).RowVersion.SequenceEqual(cancelBefore.RowVersion),"Odustani restores original NC/value/total without deleting or updating saved row");
            Select(ids["163"]);var invalidBefore=Read(ids["163"]);vm.UpdateCommand.Execute(null);Box("txtKolic").SetCurrentValue(TextBox.TextProperty,"0");Blur(Box("txtKolic"),Box("txtNc"));bool rejected=false;try{vm.SaveCurrentOpening();}catch(InvalidOperationException){rejected=true;}Check(rejected&&vm.IsEntryEditable&&Read(invalidBefore.Id).RowVersion.SequenceEqual(invalidBefore.RowVersion),"Invalid edit rejected without database update; editing remains active");vm.CancelCurrentOpening();Check(vm.SumVrijed==baseline+delta,"Cancel after failed save restores correct total");
            foreach(string code in new[]{"094001","163","25"}){
                Select(ids[code]);decimal amount=Read(ids[code]).Vrijed.Value;vm.DeleteCurrentOpening();Pump();delta-=amount;Check(Read(ids[code])==null&&!vm.MatList.Any(m=>m.Id==ids[code])&&vm.CurrentItemMat.Id!=ids[code],"Brisanje removes SQL/list row and selects a surviving record: "+code);Check(Total(1000)==baseline+delta,"Warehouse total decreases by deleted value");DisplayAfterSaveOrDelete();Check(vm.IsEntryReadOnly,"Page returns to browsing after delete");results.Add(new{Action="Delete",Ident=code,RemovedValue=amount,WarehouseTotal=Total(1000),SelectedWarehouse=vm.CurrentItemMat.Kljnaz,SelectedNc=vm.CurrentItemMat.Nc});Json("results.json",results);
            }
            Check(delta==0&&Total(1000)==baseline,"Deleting all test rows restores original warehouse total");
            vm.NovoSklCommand.Execute(null);Track();int draftId=vm.CurrentItemMat.Id;Box("OpBroj").SetCurrentValue(TextBox.TextProperty,"1000");Blur(Box("OpBroj"),Box("txtIdent"));Box("txtIdent").SetCurrentValue(TextBox.TextProperty,"25");Blur(Box("txtIdent"),Box("txtKolic"));Box("txtKolic").SetCurrentValue(TextBox.TextProperty,"5");Blur(Box("txtKolic"),Box("txtNc"));vm.CancelCurrentOpening();Check(Read(draftId)==null&&Total(1000)==baseline,"Cancelling a new draft deletes only that draft, not saved opening states");
            Bind(new PocetnoStanjePrviPutViewModel(new GlavniViewModel()));Check(ids.Values.All(id=>!vm.MatList.Any(m=>m.Id==id)),"Reopened page contains none of the deleted test rows");passed=true;
        }catch(Exception e){error=e.GetType().Name+": "+e.Message;Console.WriteLine("FAIL "+error);}
        finally{try{Cleanup();cleaned=true;}catch(Exception e){error=(error??"")+" CLEANUP: "+e.Message;Console.WriteLine("FAIL "+error);}}
        try{if(before!=null){var after=Hashes();Json("hashes_after.json",after);Check(before.All(p=>after[p.Key]==p.Value),"All original records, links, documents, financial data and business counters identical after test");}}catch(Exception e){passed=false;error=(error??"")+" AFTER: "+e.Message;Console.WriteLine("FAIL "+error);}
        Json("result.json",new{Passed=passed&&cleaned,CleanupComplete=cleaned,Checks=checks,Failure=error,NewDatabaseCreated=false,FoxModified=false});if(passed&&cleaned){Console.WriteLine("ALL PASSED "+checks+"; real edit/save/delete/cancel; independent SQL reload; original data restored; Fox read only.");return 0;}return 1;
    }
}
