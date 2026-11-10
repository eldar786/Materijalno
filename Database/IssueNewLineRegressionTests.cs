using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;

public static class IssueNewLineRegressionTests
{
    static int checks, saveActivations;
    static string number="QA"+DateTime.Now.ToString("MMddHHmmss");
    static materijalno_knjigovodstvoContext Db()=>new materijalno_knjigovodstvoContext();
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    static Action Method(IzlazMaterijalaViewModel vm,string name)=>(Action)Delegate.CreateDelegate(typeof(Action),vm,typeof(IzlazMaterijalaViewModel).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance));
    static IzlazMaterijalaViewModel Selected(Mat row)
    {
        // Use the parameterless VM to avoid its unrelated Oracle supplier lookup.
        // The action under test is the production NovaStavkaButton method.
        var vm=new IzlazMaterijalaViewModel{CurrentItemMat=row,MatList=new ObservableCollection<Mat>{row}};
        foreach(var prop in typeof(IzlazMaterijalaViewModel).GetProperties().Where(p=>p.PropertyType==typeof(RelayCommand)&&p.CanWrite))prop.SetValue(vm,new RelayCommand(()=>{}));
        vm.NovaStavkaButtonCommand=new RelayCommand(Method(vm,"NovaStavkaButton"));
        vm.NabavnaCijenaCommand=new RelayCommand(vm.NabavnaCijena);
        vm.SpasiNovuKalkulacijuCommand=new RelayCommand(()=>saveActivations++);
        vm.CurrentItemTabMaterijala=new TabelaMaterijala{Ident=row.Ident.GetValueOrDefault(),Nazmat="Previous material"};
        return vm;
    }
    static KeyEventArgs Enter(TextBox input,Window window)
    {
        var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent};
        input.RaiseEvent(args);Pump();return args;
    }
    [STAThread]public static int Main()
    {
        var catalog=new SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
        if(catalog!="materijalno_fox_test_20261106_084736")throw new Exception("Dedicated existing test database required.");
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Window window=null;
        int countBefore;using(var db=Db())countBefore=db.Mat.IgnoreQueryFilters().Count();
        var posting=new MaterialPostingService();StockBalance initial=posting.Balance(13,1001);
        try{
            Check(!DatabaseConfiguration.OracleExportEnabled,"Oracle export disabled; no supplier lookup used");
            int year=posting.ActiveYear();
            var source=posting.Save(new Mat{Status="I",Brfak=number,Brdok="QA-NEWLINE",Datun=new DateTime(year,9,30),Datnar=new DateTime(year,9,29),Kljnaz=1001,Ident=13,Kolic=1,Redbr=1,Ourst=7,Mjtst=12345});
            var before=posting.Balance(13,1001);int visibleBefore;
            using(var db=Db()){
                visibleBefore=db.Mat.Count();
                db.Mat.Add(new Mat{Status="I",Brfak=number,Kljnaz=1001,BookYear=year,Redbr=5});
                db.Mat.Add(new Mat{Status="I",Brfak=number,Kljnaz=1001,BookYear=year-1,Redbr=99});
                db.SaveChanges();
            }
            var vm=Selected(source);int focusRequests=0;vm.RequestFocusIdent+=()=>focusRequests++;
            vm.NovaStavkaButtonCommand.Execute(null);var draft=vm.CurrentItemMat;
            Check(draft.Id>0&&draft.Id!=source.Id&&draft.IsDraft,"Nova stavka opens the newly saved draft without a filtered First query");
            Check(draft.Redbr==6,"next line number includes existing drafts in the same document/year");
            Check(draft.Brfak==source.Brfak&&draft.Kljnaz==source.Kljnaz&&draft.BookYear==source.BookYear&&draft.Datun==source.Datun&&draft.Datnar==source.Datnar&&draft.Brdok==source.Brdok,"selected calculation and header retained");
            Check(draft.Ourst==source.Ourst&&draft.Mjtst==source.Mjtst,"read-only cost center fields retained");
            Check(vm.CurrentItemTabMaterijala==null&&focusRequests==1,"new item clears previous material name and requests material focus");
            Check(IzlazMaterijalaViewModel.isNovaKalkulacijaClicked&&vm.IsOdustaniEnabled,"new line enters editing mode");
            using(var db=Db()){
                Check(db.Mat.Count()==visibleBefore&&!db.Mat.Any(m=>m.Id==draft.Id)&&db.Mat.IgnoreQueryFilters().Any(m=>m.Id==draft.Id&&m.IsDraft),"draft stays outside posted ledger queries until Snimi");
                var storedSource=db.Mat.AsNoTracking().Single(m=>m.Id==source.Id);
                Check(storedSource.Kolic==source.Kolic&&storedSource.Vrijed==source.Vrijed&&storedSource.Redbr==source.Redbr,"original selected line remains unchanged");
            }
            var again=Selected(source);again.NovaStavkaButtonCommand.Execute(null);
            Check(again.CurrentItemMat.Redbr==7,"a second draft receives a distinct next line number");
            draft.Ident=13;draft.Kolic=0;vm.CurrentItemTabMaterijala=new TabelaMaterijala{Ident=13,Nazmat="ADING ROLNE TERMO 79X30"};
            var view=new Materijalno.UI.IzlazMaterijala{DataContext=vm};
            window=new Window{Title="Izlaz – provjera na testnoj bazi",Content=view,Width=1200,Height=900,ShowInTaskbar=false};window.Show();Pump();
            var input=(TextBox)view.FindName("txtKolicina");var save=(Button)view.FindName("btnSnimi");
            Keyboard.Focus(input);input.Text="0,25";var key=Enter(input,window);
            Check(key.Handled&&ReferenceEquals(Keyboard.FocusedElement,save),"Enter in quantity moves keyboard focus directly to Snimi");
            Check(saveActivations==0,"Enter moves focus without executing save");
            Check(draft.Kolic==-.25m&&draft.Nc==before.UnitCost&&draft.Vrijed==-LedgerMath.IssueValue(before,.25m),"quantity binding commits and LostFocus computes weighted issue price/value before save");
            using(var db=Db())Check(db.Mat.IgnoreQueryFilters().Single(m=>m.Id==draft.Id).IsDraft&&db.Mat.Count()==visibleBefore,"Enter alone leaves the row as an unposted draft");
            var saved=posting.Save(draft);
            using(var db=Db()){
                var persisted=db.Mat.AsNoTracking().Single(m=>m.Id==draft.Id);
                Check(!persisted.IsDraft&&persisted.Kolic==-.25m&&persisted.Vrijed==draft.Vrijed&&persisted.Nc==draft.Nc&&persisted.Brfak==source.Brfak&&persisted.Redbr==6,"Snimi posting service saves that same new line once with correct signs and cost");
            }
            var after=posting.Balance(13,1001);
            Check(after.Quantity==before.Quantity-.25m&&after.Value==before.Value-LedgerMath.IssueValue(before,.25m),"stock changes exactly once by the new line quantity and value");
            Keyboard.Focus(input);input.Text="invalid";Enter(input,window);
            Check(Validation.GetHasError(input)&&ReferenceEquals(Keyboard.FocusedElement,input)&&saveActivations==0,"invalid numeric input retains focus and cannot trigger save");
            Console.WriteLine("ALL PASSED "+checks+"; test database only; no Oracle connection used");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
        finally{
            window?.Close();
            using(var db=Db()){
                db.Database.ExecuteSqlRaw("DELETE FROM dbo.mat WHERE brfak = {0}",number);
                db.MaterialDocuments.RemoveRange(db.MaterialDocuments.Where(d=>d.Number==number));db.SaveChanges();
                if(db.Mat.IgnoreQueryFilters().Count()!=countBefore)throw new Exception("Test cleanup row count mismatch");
            }
            var restored=posting.Balance(13,1001);
            if(restored.Quantity!=initial.Quantity||restored.Value!=initial.Value)throw new Exception("Test stock was not restored");
            Console.WriteLine("PASS temporary test rows removed; starting row count and stock restored");app.Shutdown();
        }
    }
}
