using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;

public static class ReturnKeyboardRegressionTests
{
    static int checks;
    static readonly List<string> Numbers=new List<string>();
    static materijalno_knjigovodstvoContext Db()=>new materijalno_knjigovodstvoContext();
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    static void Press(UIElement input,Window window,Key key,bool preview=false)
    {
        var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),Environment.TickCount,key){RoutedEvent=preview?Keyboard.PreviewKeyDownEvent:Keyboard.KeyDownEvent};input.RaiseEvent(args);Pump();
        Check(args.Handled,"key handled: "+key);
    }
    static void Type(TextBox input,string text)
    {
        foreach(char c in text){var composition=new TextComposition(InputManager.Current,input,c.ToString());var args=new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputEvent};input.RaiseEvent(args);Pump();}
    }
    static DatePickerTextBox DateBox(DatePicker picker){picker.ApplyTemplate();return (DatePickerTextBox)picker.Template.FindName("PART_TextBox",picker);}
    static void Date(DatePicker picker,Window window,string digits,UIElement next,DateTime expected,Func<DateTime?> bound)
    {
        var input=DateBox(picker);Keyboard.Focus(input);input.SelectAll();
        Type(input,digits.Substring(0,2));Check(input.Text==digits.Substring(0,2)+".",picker.Name+" inserts day dot");
        Type(input,digits.Substring(2,2));Check(input.Text==digits.Substring(0,2)+"."+digits.Substring(2,2)+".",picker.Name+" inserts month dot");
        Type(input,digits.Substring(4));Check(input.Text==expected.ToString("dd.MM.yyyy."),picker.Name+" inserts year dot");
        Press(input,window,Key.Enter,true);
        Check(ReferenceEquals(Keyboard.FocusedElement,next),picker.Name+" Enter skips calendar/read-only controls and focuses next input");
        Check(picker.SelectedDate==expected&&bound()==expected,picker.Name+" Enter commits the correct source property");
    }
    static void Run(Button button){Check(button.IsEnabled,"button available: "+button.Name);button.Command.Execute(button.CommandParameter);Pump();}
    [STAThread]public static int Main(string[] args)
    {
        string expected=args[0];var catalog=new SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
        if(catalog!=expected||!catalog.StartsWith("materijalno_fox_test_20261106_"))throw new Exception("Isolated SQL configuration required.");
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Window window=null;
        var posting=new MaterialPostingService();var stockBefore=posting.Balance(13,1001);int countBefore;
        using(var db=Db())countBefore=db.Mat.IgnoreQueryFilters().Count();
        try{
            Check(!DatabaseConfiguration.OracleExportEnabled,"Oracle export disabled");
            var vm=new PovratMaterijalaViewModel(); // Shared commands, no supplier lookup.
            var view=new Materijalno.UI.PovratMaterijala{DataContext=vm};
            window=new Window{Title="Povrat – provjera tastature na testnoj bazi",Content=view,Width=1300,Height=980,ShowInTaskbar=false};window.Show();Pump();
            var start=(Button)view.FindName("btnNovaKalkulacija");var save=(Button)view.FindName("btnSnimi");var next=(Button)view.FindName("btnSlijedecaStavka");
            var qty=(TextBox)view.FindName("txtKolicina");var ident=(TextBox)view.FindName("txtIdent");var brdok=(TextBox)view.FindName("txtBrdok");
            var datun=(DatePicker)view.FindName("dtpOd");var datnar=(DatePicker)view.FindName("dtpDatnar");
            Run(start);Numbers.Add(vm.CurrentItemMat.Brfak);int draftId=vm.CurrentItemMat.Id;
            Check(vm.PovratAktivan&&save.IsEnabled&&!next.IsEnabled,"Novi povrat enables Snimi before next line");
            vm.CurrentItemMat.Kljnaz=1001;vm.CurrentItemMat.Ident=13;vm.CurrentItemMat=vm.CurrentItemMat;
            Date(datun,window,"06112026",brdok,new DateTime(2026,11,6),()=>vm.CurrentItemMat.Datun);
            brdok.Text="QA-RETURN";brdok.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            Date(datnar,window,"05112026",ident,new DateTime(2026,11,5),()=>vm.CurrentItemMat.Datnar);
            var input=DateBox(datnar);Keyboard.Focus(input);input.SelectAll();Type(input,"31022026");var original=vm.CurrentItemMat.Datnar;
            Press(input,window,Key.Enter,true);
            Check(ReferenceEquals(Keyboard.FocusedElement,input)&&Validation.GetHasError(datnar)&&vm.CurrentItemMat.Datnar==original,"invalid date keeps focus and preserves previous value");
            Date(datnar,window,"29022024",ident,new DateTime(2024,2,29),()=>vm.CurrentItemMat.Datnar);
            Check(!Validation.GetHasError(datnar),"valid leap date clears validation error");
            Date(datnar,window,"05112026",ident,new DateTime(2026,11,5),()=>vm.CurrentItemMat.Datnar);
            Keyboard.Focus(input);input.SelectAll();Type(input,"06");Type(input,".");Check(input.Text=="06.","manually entered dot does not duplicate separator");Type(input,"112026");
            Press(input,window,Key.Back,true);Check(input.Text=="06.11.202","backspace removes a digit and rebuilds the mask");Type(input,"6");Check(input.Text=="06.11.2026.","typing restores year and final dot");
            input.SelectAll();Press(input,window,Key.Delete,true);Check(input.Text=="","Delete clears selected date text");
            var data=new DataObject(DataFormats.UnicodeText,"05.11.2026.");var paste=new DataObjectPastingEventArgs(data,false,DataFormats.UnicodeText){RoutedEvent=DataObject.PastingEvent};input.RaiseEvent(paste);Pump();
            Check(input.Text=="05.11.2026.","pasting formatted date preserves the mask");Press(input,window,Key.Enter,true);
            Check(vm.CurrentItemMat.Datnar==new DateTime(2026,11,5),"pasted date commits correctly");
            Keyboard.Focus(qty);qty.Text="invalid";Press(qty,window,Key.Enter);Check(Validation.GetHasError(qty)&&ReferenceEquals(Keyboard.FocusedElement,qty),"invalid quantity stays in quantity");
            var returnPrice=posting.ReturnPrice(13,1001);Check(returnPrice.HasValue,"previous return price is available");
            Keyboard.Focus(qty);qty.Text="0,25";Press(qty,window,Key.Enter);
            Check(ReferenceEquals(Keyboard.FocusedElement,save),"quantity Enter focuses Snimi");
            Check(vm.CurrentItemMat.Kolic==.25m&&vm.CurrentItemMat.Nc==returnPrice&&vm.CurrentItemMat.Vrijed==LedgerMath.Money(.25m*returnPrice.Value),"quantity commits and LostFocus computes the return price/value");
            using(var db=Db())Check(db.Mat.IgnoreQueryFilters().Single(m=>m.Id==draftId).IsDraft,"quantity Enter alone does not save");
            Run(save);var first=vm.CurrentItemMat;
            Check(ReferenceEquals(Keyboard.FocusedElement,next)&&next.IsEnabled&&!save.IsEnabled,"successful Snimi focuses enabled Slij. stavka");
            using(var db=Db()){
                var stored=db.Mat.AsNoTracking().Single(m=>m.Id==first.Id);
                Check(stored.Status=="V"&&stored.Kolic==.25m&&stored.Vrijed==LedgerMath.Money(.25m*returnPrice.Value)&&stored.Datun==new DateTime(2026,11,6)&&stored.Datnar==new DateTime(2026,11,5),"save persists correct dates, positive return quantity and value");
            }
            Run(next);Check(ReferenceEquals(Keyboard.FocusedElement,ident),"Slij. stavka focuses Ident");
            Check(vm.CurrentItemMat.IsDraft&&vm.CurrentItemMat.Redbr==2&&vm.CurrentItemMat.Brfak==first.Brfak&&vm.CurrentItemMat.Datun==first.Datun&&vm.CurrentItemMat.Datnar==first.Datnar&&!vm.CurrentItemMat.Ident.HasValue,"next return line retains header and clears material");
            vm.CurrentItemMat.Ident=13;vm.CurrentItemMat=vm.CurrentItemMat;Keyboard.Focus(qty);qty.Text="0,50";Press(qty,window,Key.Enter);Run(save);
            Check(ReferenceEquals(Keyboard.FocusedElement,next),"repeated return save also focuses Slij. stavka");
            using(var db=Db())Check(db.Mat.Count(m=>m.Brfak==first.Brfak&&m.Status=="V")==2,"two return saves create exactly two posted lines");
            var stockAfter=posting.Balance(13,1001);Check(stockAfter.Quantity==stockBefore.Quantity+.75m,"return adds exactly the saved quantities to stock");
            Console.WriteLine("ALL PASSED "+checks+"; isolated SQL database="+catalog+"; no Oracle connection used");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
        finally{
            window?.Close();
            using(var db=Db()){
                foreach(var number in Numbers){db.Database.ExecuteSqlRaw("DELETE FROM dbo.mat WHERE brfak={0} AND status='V'",number);db.MaterialDocuments.RemoveRange(db.MaterialDocuments.Where(d=>d.Number==number&&d.Status=="V"));}db.SaveChanges();
                if(db.Mat.IgnoreQueryFilters().Count()!=countBefore)throw new Exception("Test row cleanup mismatch.");
            }
            var stock=posting.Balance(13,1001);if(stock.Quantity!=stockBefore.Quantity||stock.Value!=stockBefore.Value)throw new Exception("Starting test stock not restored.");
            Console.WriteLine("PASS temporary return rows removed; starting stock restored");app.Shutdown();
        }
    }
}

