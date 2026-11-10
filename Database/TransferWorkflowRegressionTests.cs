using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;

public static class TransferWorkflowRegressionTests
{
    static int checks;
    static string output;
    static readonly string[] Buttons={"btnStampa","btnSljedeci","btnPrethodni","btnPrvi","btnZadnji","btnTrazi","btnNovaMedjuskladisnica","btnSnimi","btnNovaStavka","btnSlijedecaStavka","btnBrisanje","btnOdustani","btnIzlaz"};
    static readonly List<string> Numbers=new List<string>();
    static materijalno_knjigovodstvoContext Db()=>new materijalno_knjigovodstvoContext();
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static void Defaults(Mat row,string stage)
    {
        Check(row.Fcj=="0"&&row.Troskovi==0&&row.Cartro=="0"&&row.Zavtro=="0"&&row.Ppp=="0"&&row.Tarifa==""&&
            row.Fvrijed=="0"&&row.Porppp==0&&row.Trospe==0&&row.Ourst==0&&row.Mjtst==0,
            stage+" has complete Fox-compatible numeric/text defaults");
    }
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    static void Enabled(Materijalno.UI.Medjuskladisnica view,string stage,params string[] expected)
    {
        Pump();var actual=Buttons.Where(n=>((Button)view.FindName(n)).IsEnabled).OrderBy(n=>n).ToArray();
        Check(actual.SequenceEqual(expected.OrderBy(n=>n)),stage+" enabled buttons: "+string.Join(", ",actual));
    }
    static void Run(Button button){Check(button.IsEnabled,"action available: "+button.Name);button.Command.Execute(button.CommandParameter);Pump();}
    static void Enter(TextBox input,Window window)
    {
        var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent};input.RaiseEvent(key);Pump();Check(key.Handled,"quantity Enter handled");
    }
    static void Screenshot(FrameworkElement view,string name)
    {
        var bitmap=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(output,name+".png")))png.Save(stream);
    }
    static void SetItem(MedjuskladisnicaViewModel vm,int material,int year)
    {
        vm.SourceWarehouse=1000;vm.DestinationWarehouse=1001;
        vm.CurrentItemMat.Ident=material;vm.CurrentItemMat.Datun=new DateTime(year,11,6);vm.CurrentItemMat.Datnar=new DateTime(year,11,6);vm.CurrentItemMat.Brdok="QA-TRANSFER";
        vm.CurrentItemMat=vm.CurrentItemMat;
    }
    static void Quantity(MedjuskladisnicaViewModel vm,Materijalno.UI.Medjuskladisnica view,Window window,decimal qty)
    {
        var input=(TextBox)view.FindName("txtKolic");Keyboard.Focus(input);input.Text=qty.ToString("0.00",System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        Enter(input,window);
        Check(ReferenceEquals(Keyboard.FocusedElement,view.FindName("btnSnimi")),"quantity Enter focuses Snimi");
        Check(vm.CurrentItemMat.Kolic==qty,"quantity binding committed before Snimi");
    }
    [STAThread]public static int Main(string[] args)
    {
        string expected=args[0];output=args[1];Directory.CreateDirectory(output);
        var catalog=new SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
        if(!expected.StartsWith("materijalno_fox_test_20261106_")||catalog!=expected)throw new Exception("Isolated test configuration required.");
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Window window=null;
        var posting=new MaterialPostingService();int material=13,year=posting.ActiveYear();
        var sourceStart=posting.Balance(material,1000);var targetStart=posting.Balance(material,1001);
        int startingRows;using(var db=Db())startingRows=db.Mat.IgnoreQueryFilters().Count();
        try{
            Check(!DatabaseConfiguration.OracleExportEnabled,"Oracle export disabled");
            Check(sourceStart.Quantity>3,"test material has sufficient source stock");
            var vm=new MedjuskladisnicaViewModel(); // No unrelated supplier/Oracle lookup.
            var factoryA=MaterialPostingService.CreateTransferDraft();var factoryB=MaterialPostingService.CreateTransferDraft();
            Defaults(factoryA,"transfer draft factory");
            Check(factoryA.FoxRecno==null&&factoryB.FoxRecno==null&&factoryA.LineToken!=factoryB.LineToken,"new WPF lines have unique identities and no invented Fox record number");
            var view=new Materijalno.UI.Medjuskladisnica{DataContext=vm};
            window=new Window{Title="Međuskladišnica – provjera na testnoj bazi",Content=view,Width=1320,Height=920,ShowInTaskbar=false};window.Show();Pump();
            Func<string,Button> button=n=>(Button)view.FindName(n);
            Run(button("btnNovaMedjuskladisnica"));Numbers.Add(vm.CurrentItemMat.Brfak);
            Enabled(view,"new transfer","btnSnimi","btnOdustani");
            int draftId=vm.CurrentItemMat.Id;SetItem(vm,material,year);
            Check(vm.CurrentItemMat.IsDraft&&vm.CurrentItemMat.Redbr==1,"new transfer starts as one draft line");
            Defaults(vm.CurrentItemMat,"Nova međuskladišnica draft");
            using(var db=Db())Defaults(db.Mat.IgnoreQueryFilters().AsNoTracking().Single(m=>m.Id==draftId),"persisted new-transfer draft");
            vm.CurrentItemMat.Kolic=sourceStart.Quantity+1;
            try{vm.SaveCurrentLine();throw new Exception("Oversized transfer accepted");}catch(InvalidOperationException){Check(true,"insufficient stock rejects save");}
            Enabled(view,"failed save stays in input","btnSnimi","btnOdustani");
            var input=(TextBox)view.FindName("txtKolic");Keyboard.Focus(input);input.Text="invalid";Enter(input,window);
            Check(Validation.GetHasError(input)&&ReferenceEquals(Keyboard.FocusedElement,input),"invalid quantity retains focus");
            Quantity(vm,view,window,.25m);
            Check(vm.CurrentItemMat.Nc==sourceStart.UnitCost&&vm.CurrentItemMat.Vrijed==LedgerMath.IssueValue(sourceStart,.25m),"quantity preview uses weighted source cost");
            using(var db=Db())Check(db.Mat.IgnoreQueryFilters().Single(m=>m.Id==draftId).IsDraft&&!db.Mat.Any(m=>m.Id==draftId),"quantity Enter does not post the draft");
            Screenshot(view,"01_new_transfer");
            Run(button("btnSnimi"));
            Enabled(view,"saved line","btnSlijedecaStavka","btnIzlaz");
            Check(ReferenceEquals(Keyboard.FocusedElement,button("btnSlijedecaStavka")),"Snimi focuses Slij. stavka after successful posting");
            var first=vm.CurrentItemMat;string number=first.Brfak;Guid? firstPair=first.TransferPairId;
            using(var db=Db()){
                var pair=db.Mat.AsNoTracking().Where(m=>m.TransferPairId==firstPair).ToList();
                Check(pair.Count==2&&pair.Sum(m=>m.Kolic)==0&&pair.Sum(m=>m.Vrijed)==0&&pair.All(m=>!m.IsDraft),"Snimi creates exactly two balanced posted transfer rows");
                foreach(var row in pair)Defaults(row,row.Medus=="1"?"posted destination row":"posted source row");
                Check(pair.All(m=>m.FoxRecno==null),"neither posted WPF transfer row has a false Fox provenance");
            }
            Check(vm.SourceWarehouse==1000&&vm.DestinationWarehouse==1001&&vm.CurrentItemTabMaterijala.Ident==material,"saved line displays the original source, target and material");
            Screenshot(view,"02_saved_transfer");
            Run(button("btnSlijedecaStavka"));
            Enabled(view,"next line","btnSnimi","btnOdustani");
            Check(ReferenceEquals(Keyboard.FocusedElement,view.FindName("txtIdent")),"Slij. stavka focuses material Ident");
            Check(vm.CurrentItemMat.Redbr==2&&vm.CurrentItemMat.Brfak==number&&vm.SourceWarehouse==1000&&vm.DestinationWarehouse==1001&&vm.CurrentItemMat.Datun==first.Datun,"next line retains document header and warehouse orientation");
            Check(!vm.CurrentItemMat.Ident.HasValue&&vm.CurrentItemTabMaterijala==null,"next line clears previous material and description");
            Defaults(vm.CurrentItemMat,"Slij. stavka draft");
            using(var db=Db())Defaults(db.Mat.IgnoreQueryFilters().AsNoTracking().Single(m=>m.Id==vm.CurrentItemMat.Id),"persisted Slij. stavka draft");
            vm.CurrentItemMat.Ident=material;vm.CurrentItemMat=vm.CurrentItemMat;Quantity(vm,view,window,.5m);Run(button("btnSnimi"));var second=vm.CurrentItemMat;
            Enabled(view,"second saved line","btnSlijedecaStavka","btnIzlaz");
            Run(button("btnSlijedecaStavka"));vm.CurrentItemMat.Ident=material;vm.CurrentItemMat=vm.CurrentItemMat;Quantity(vm,view,window,.75m);Run(button("btnSnimi"));var third=vm.CurrentItemMat;
            Check(third.Redbr==3,"third line increments document line number");
            Run(button("btnIzlaz"));
            Check(vm.CanBrowseTransfers&&button("btnStampa").IsEnabled&&button("btnBrisanje").IsEnabled&&button("btnNovaStavka").IsEnabled&&!button("btnSnimi").IsEnabled&&!button("btnSlijedecaStavka").IsEnabled,"Izlaz restores browsing actions after completed document");
            vm.CurrentItemMat=second;vm.CurrentIndex=vm.MatList.ToList().FindIndex(m=>m.Id==second.Id);vm.DeleteCurrentLine();Pump();
            Check(vm.CurrentItemMat!=null&&vm.CurrentItemMat.Id!=second.Id&&vm.CurrentItemMat.TransferPairId!=second.TransferPairId,"delete refreshes selection away from the removed middle line");
            using(var db=Db())Check(!db.Mat.IgnoreQueryFilters().Any(m=>m.TransferPairId==second.TransferPairId),"delete removes both rows of selected transfer line");
            vm.CurrentItemMat=third;vm.CurrentIndex=vm.MatList.ToList().FindIndex(m=>m.Id==third.Id);vm.DeleteCurrentLine();Pump();
            Check(vm.CurrentItemMat==null||vm.CurrentItemMat.Id!=third.Id,"deleting last displayed row selects a remaining row");
            var oldSelection=vm.CurrentItemMat;Run(button("btnNovaMedjuskladisnica"));Numbers.Add(vm.CurrentItemMat.Brfak);int cancelled=vm.CurrentItemMat.Id;
            Enabled(view,"new input again","btnSnimi","btnOdustani");vm.CancelCurrentLine();Pump();
            Check(vm.CanBrowseTransfers&&vm.CurrentItemMat?.Id==oldSelection?.Id,"Odustani restores the previous displayed line");
            using(var db=Db())Check(!db.Mat.IgnoreQueryFilters().Any(m=>m.Id==cancelled),"Odustani removes only the unsaved draft");
            vm.CurrentItemMat=first;vm.CurrentIndex=vm.MatList.ToList().FindIndex(m=>m.Id==first.Id);Run(button("btnNovaStavka"));
            Enabled(view,"Nova stavka on existing document","btnSnimi","btnOdustani");Check(ReferenceEquals(Keyboard.FocusedElement,view.FindName("txtIdent")),"Nova stavka also focuses material");Defaults(vm.CurrentItemMat,"Nova stavka draft");vm.CancelCurrentLine();
            vm.CurrentItemMat=first;vm.CurrentIndex=vm.MatList.ToList().FindIndex(m=>m.Id==first.Id);vm.DeleteCurrentLine();Pump();
            Check(vm.CurrentItemMat==null||vm.CurrentItemMat.TransferPairId!=firstPair,"delete refreshes material and warehouse bindings");
            // Exercise the same empty-list branch used after deleting the final row.
            vm.MatList.Clear();
            using(var db=Db())typeof(MedjuskladisnicaViewModel).GetMethod("UpdateCurrentItemData",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(vm,new object[]{db});Pump();
            Check(vm.CurrentItemMat==null&&vm.CurrentItemTabMaterijala==null&&vm.CurrentItemTabSkladista==null&&vm.CurrentItemTabSkladistaUlaza==null&&vm.SourceWarehouseName==null&&vm.DestinationWarehouseName==null,"empty transfer list clears row, material and both warehouse descriptions");
            Check(!button("btnStampa").IsEnabled&&!button("btnBrisanje").IsEnabled,"empty list disables actions requiring a saved row");
            vm.Osvjezi();Check(vm.CurrentItemMat==null,"empty row LostFocus refresh is safe");
            Screenshot(view,"03_empty_selection");
            var sourceEnd=posting.Balance(material,1000);var targetEnd=posting.Balance(material,1001);
            Check(sourceEnd.Quantity==sourceStart.Quantity&&sourceEnd.Value==sourceStart.Value&&targetEnd.Quantity==targetStart.Quantity&&targetEnd.Value==targetStart.Value,"transfers and deletes restore exact source and target stock");
            // Exercise the service directly: callers may submit old all-null drafts.
            string manualNumber="QA"+DateTime.Now.ToString("MMddHHmmss");Numbers.Add(manualNumber);
            var manual=new Mat{Status="M",Kljnaz=1000,Kljnaz1=1001,Ident=material,Kolic=.1m,Brfak=manualNumber,Brdok="QA-DEFAULTS",Redbr=1,Datun=new DateTime(year,11,6),Datnar=new DateTime(year,11,6)};
            var direct=posting.SaveTransfer(manual);
            using(var db=Db()){
                var pair=db.Mat.AsNoTracking().Where(m=>m.TransferPairId==direct.TransferPairId).ToList();
                foreach(var row in pair)Defaults(row,"direct API row "+row.Medus);
                // Simulate imported provenance solely on these disposable test rows.
                db.Database.ExecuteSqlRaw("UPDATE dbo.mat SET fox_recno = 900000 + id WHERE transfer_pair_id = {0}",direct.TransferPairId.Value);
            }
            var unchanged=posting.SaveTransfer(direct);
            using(var db=Db())Check(db.Mat.AsNoTracking().Where(m=>m.TransferPairId==direct.TransferPairId).All(m=>m.FoxRecno==900000+m.Id),"editing a transfer preserves pre-existing Fox provenance");
            posting.Delete(unchanged);
            Console.WriteLine("ALL PASSED "+checks+"; isolated test database="+catalog+"; no Oracle connection used");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
        finally{
            window?.Close();
            using(var db=Db()){
                foreach(var number in Numbers.Distinct()){
                    db.Database.ExecuteSqlRaw("DELETE FROM dbo.mat WHERE brfak = {0} AND status = 'M'",number);
                    db.MaterialDocuments.RemoveRange(db.MaterialDocuments.Where(d=>d.Status=="M"&&d.Number==number));
                }db.SaveChanges();
                if(db.Mat.IgnoreQueryFilters().Count()!=startingRows)throw new Exception("Temporary test row cleanup failed.");
            }
            Console.WriteLine("PASS temporary transfer rows removed; starting ledger row count restored");app.Shutdown();
        }
    }
}
