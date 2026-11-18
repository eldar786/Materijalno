using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

public static class InitialStockChecks
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Blur(TextBox input,IInputElement next){input.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,Environment.TickCount,input,next){RoutedEvent=Keyboard.LostKeyboardFocusEvent});input.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);}
    static Dictionary<string,string> Hashes(){
        var result=new Dictionary<string,string>();using(var c=new SqlConnection(DatabaseConfiguration.MaterialConnectionString)){c.Open();foreach(string name in new[]{"mat","tabela_materijala","nalmat","document_counter"}){
            using(var cmd=new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.["+name+"] ORDER BY "+(name=="document_counter"?"name,book_year":"id")+" FOR XML RAW,BINARY BASE64))",c)){
                string xml=(string)cmd.ExecuteScalar();using(var sha=SHA256.Create())result[name]=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(xml))).Replace("-","");
            }
        }}return result;
    }
    static decimal Number(DbfRecord r,string field)=>string.IsNullOrWhiteSpace(r.Values[field])?0:decimal.Parse(r.Values[field],CultureInfo.InvariantCulture);
    [STAThread] public static int Main(string[] args){try{
        var original=Hashes();
        var saved=new Mat{Id=1,Status="P",IsDraft=false,BookYear=2026,Kljnaz=1001,Vrijed=50m};
        var current=new Mat{Id=2,Status="P",IsDraft=false,BookYear=2026,Kljnaz=1001,Kolic=4m,Nc=5m,Vrijed=20m};
        var list=new ObservableCollection<Mat>{saved,current,new Mat{Status="P",IsDraft=true,BookYear=2026,Kljnaz=1001,Vrijed=999},new Mat{Status="U",IsDraft=false,BookYear=2026,Kljnaz=1001,Vrijed=888},new Mat{Status="P",IsDraft=false,BookYear=2025,Kljnaz=1001,Vrijed=777},new Mat{Status="P",IsDraft=false,BookYear=2026,Kljnaz=1002,Vrijed=666}};
        var vm=new PocetnoStanjePrviPutViewModel{MatList=list,CurrentItemMat=current};
        Check(vm.SumVrijed==70,"Initial warehouse total excludes other warehouses/years/types/drafts");
        current.Nc=6;vm.NabavnaCijena();Check(current.Vrijed==24&&vm.SumVrijed==74,"Editing replaces current line once");
        vm.NabavnaCijena();vm.NabavnaCijena();Check(vm.SumVrijed==74,"Repeated NC validation does not accumulate total");
        var draft=new Mat{Status="P",IsDraft=true,BookYear=2026,Kljnaz=1001,Kolic=3,Nc=1.005m};vm.CurrentItemMat=draft;vm.NabavnaCijena();
        Check(draft.Vrijed==3.02m&&vm.SumVrijed==77.02m,"New-line preview uses two-decimal accounting rounding");
        Check(ReferenceEquals(vm.MatList,list),"Recalculation does not reload or replace local input list");
        draft.Nc=null;vm.NabavnaCijena();Check(draft.Vrijed==null&&vm.SumVrijed==74,"Missing price clears old line amount");
        var receipt1=new Mat{Id=3,FoxRecno=10,Status="U",IsDraft=false,BookYear=2026,Ident="094001",Kljnaz=1001,Nc=1.23m};
        var receipt2=new Mat{Id=4,FoxRecno=11,Status="U",IsDraft=false,BookYear=2026,Ident="094001",Kljnaz=1001,Nc=9.99m};
        Check(InitialStockRules.SuggestedPrice(new[]{receipt2,receipt1},"094001",1001,2026)==1.23m,"Suggested NC is first receipt in Fox physical order, not latest price");
        Check(InitialStockRules.SuggestedPrice(new[]{receipt1},"094001",1002,2026)==null,"Other-warehouse price is not reused");
        Check(InitialStockRules.SuggestedPrice(new[]{receipt1},"94001",1001,2026)==null,"Leading-zero material codes remain distinct");
        // The isolated UI fixture represents an active unsaved entry.
        PocetnoStanjePrviPutViewModel.isNovaKalkulacijaClicked=true;
        var view=new PocetnoStanjePrviPut{DataContext=vm};view.Measure(new Size(1600,1000));view.Arrange(new Rect(0,0,1600,1000));view.UpdateLayout();
        var konto=(TextBox)view.FindName("txtKonto3");var nc=(TextBox)view.FindName("txtNc");var qty=(TextBox)view.FindName("txtKolic");var total=(TextBox)view.FindName("txtUkupno");var ident=(TextBox)view.FindName("txtIdent");
        vm.CurrentItemTabMaterijalaK2=new TabelaMaterijala{Ident="094001",Konto2="1234567"};view.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Check(konto.Text=="1234567","Konto3 refreshes from selected material.Konto2");
        vm.CurrentItemTabMaterijalaK2=new TabelaMaterijala{Ident="2",Konto2="7654321"};view.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Check(konto.Text=="7654321","Changing material metadata updates Konto3 binding");
        qty.SetCurrentValue(TextBox.TextProperty,"10");Blur(qty,nc);nc.SetCurrentValue(TextBox.TextProperty,"12,3456789");Blur(nc,qty);
        Check(draft.Kolic==10&&draft.Nc==12.3456789m&&draft.Vrijed==123.46m&&vm.SumVrijed==197.46m,"Quantity/NC blur recalculates line and warehouse total through real WPF handlers");
        Check(total.Text=="197,46","Updated total is visible with two decimals");Blur(nc,qty);Check(vm.SumVrijed==197.46m,"Repeated UI blur leaves total unchanged");
        vm.CurrentItemMat=current;Check(vm.SumVrijed==74,"Cancelling preview returns to stored total");
        list.Remove(saved);Check(vm.SumVrijed==24,"Removing a saved row refreshes total");
        using(var db=new materijalno_knjigovodstvoContext()){
            var all=db.Mat.AsNoTracking().ToList();var materials=db.TabelaMaterijala.AsNoTracking().ToList();
            int year=int.Parse(db.ApplicationSettings.Single(s=>s.KeyName=="ActiveBookYear").Value,CultureInfo.InvariantCulture);
            var receipts=all.Where(m=>m.Status=="U"&&m.BookYear==year).ToList();
            var foxMaster=DbfReader.Read(Path.Combine(args[0],"fox_source","mater.dbf")).Records.Where(r=>!r.Deleted).ToList();
            foreach(var f in foxMaster){var material=materials.Single(m=>m.Ident==f.Values["IDENT"].Trim());Check((material.Konto2??"").Trim()==f.Values["KONTO2"].Trim(),"Konto3 master agrees with Fox: "+material.Ident);}
            Check(foxMaster.All(r=>string.IsNullOrWhiteSpace(r.Values["KONTO2"])),"All 175 Fox material Konto2 values are empty; no account invented");
            var fox=DbfReader.Read(Path.Combine(args[0],"fox_source","mat.dbf")).Records.Where(r=>!r.Deleted).ToList();int compared=0;
            foreach(var g in receipts.GroupBy(m=>new{m.Ident,m.Kljnaz})){
                var first=g.OrderBy(m=>m.FoxRecno??int.MaxValue).ThenBy(m=>m.Id).First();
                var expected=fox.FirstOrDefault(r=>r.Values["STATUS"]=="U"&&r.Values["IDENT"].Trim()==g.Key.Ident&&Number(r,"KLJNAZ")==g.Key.Kljnaz);
                if(expected!=null&&first.FoxRecno==expected.RecordNumber){Check(InitialStockRules.SuggestedPrice(receipts,g.Key.Ident,g.Key.Kljnaz.Value,year)==Number(expected,"NC"),"Imported NC agrees with first Fox U: "+g.Key.Kljnaz+"/"+g.Key.Ident);compared++;}
            }
            Console.WriteLine("Fox receipt groups independently compared="+compared);
            foreach(var g in all.Where(m=>m.Status=="P"&&m.BookYear==year).GroupBy(m=>m.Kljnaz)){
                var selected=g.First();var actual=InitialStockRules.WarehouseTotal(all,selected);Check(actual==g.Sum(m=>m.Vrijed??0),"Existing saved warehouse total: "+g.Key+" = "+actual.ToString("0.00",CultureInfo.InvariantCulture));
            }
            // Exercise actual reference blur with read-only SQL lookups; the draft exists only in memory.
            var reference=receipts.GroupBy(m=>new{m.Ident,m.Kljnaz}).First();var firstReceipt=reference.OrderBy(m=>m.FoxRecno??int.MaxValue).ThenBy(m=>m.Id).First();
            var memory=new Mat{Id=-170011,Status="P",IsDraft=true,BookYear=year,Kljnaz=reference.Key.Kljnaz,Kolic=2,Nc=999,Ident=null};
            vm.MatList=new ObservableCollection<Mat>(all.Where(m=>m.Status=="P"));vm.CurrentItemMat=memory;
            ident.SetCurrentValue(TextBox.TextProperty,reference.Key.Ident);Blur(ident,qty);
            Check(ReferenceEquals(vm.CurrentItemMat,memory)&&memory.Nc==firstReceipt.Nc,"Material change replaces old NC using real reference event and current SQL");
            Check(vm.CurrentItemTabMaterijalaK2.Ident==reference.Key.Ident,"Konto3 master selected by Ident, not by an old account value");
            memory.Nc=7.5m;vm.Osvjezi();Check(memory.Nc==7.5m,"Unchanged reference preserves manually entered NC");
            var noReceipt=materials.First(m=>!receipts.Any(r=>r.Kljnaz==memory.Kljnaz&&r.Ident==m.Ident));ident.SetCurrentValue(TextBox.TextProperty,noReceipt.Ident);Blur(ident,qty);
            Check(memory.Nc==null&&memory.Vrijed==null&&memory.Kolic==2,"Material without a receipt clears previous NC/value and preserves quantity");
            Check(!db.ChangeTracker.HasChanges(),"Read-only SQL checks have no pending changes");
        }
        var after=Hashes();Check(original.All(p=>after[p.Key]==p.Value),"SQL ledger/master/financial rows/counters unchanged including row versions");
        File.WriteAllText(Path.Combine(args[0],"data_hashes_unchanged.txt"),string.Join(Environment.NewLine,after.Select(p=>p.Key+"="+p.Value)));
        Console.WriteLine("ALL PASSED "+checks+"; Fox files read only; current SQL read only; no business saves or Oracle calls.");return 0;
    }catch(Exception e){Console.WriteLine(e);return 1;}}
}
