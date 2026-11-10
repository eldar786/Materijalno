using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Materijalno.Model;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Materijalno.ViewModel.Services;

// Read-only visual smoke check. No document commands, SQL writes or Oracle calls.
public static class DesktopDesignVisualTests
{
    static int checks;
    static string output;
    static void Check(bool value, string message) { if(!value) throw new Exception(message); checks++; Console.WriteLine("PASS "+message); }
    static void Pump() { var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame); }
    public class PreviewData
    {
        public Mat CurrentItemMat {get;set;} = new Mat {Datun=new DateTime(2026,9,30),Datnar=new DateTime(2026,9,30),Brfak="10-26",Brdok="124/2026",Kljnaz=1000,Kljnaz1=1001,Ident=13,Kolic=1234.56m,Nc=0.889181818m,Vrijed=1097.75m,Fvrijed="1000.00",Cartro="0.00",Zavtro="0.00",Trospe=0,Porppp=170};
        public string NazivMaterijala {get;set;} = "ADIN​G ROLNE TERMO 79X30";
        public string NazivSkladista {get;set;} = "MAGACIN REGION CENTAR";
        public string SifraKomitenta {get;set;} = "Dobavljač";
        public DateTime Od {get;set;} = new DateTime(2026,1,1);
        public DateTime Do {get;set;} = new DateTime(2026,9,30);
    }
    static void Render(FrameworkElement view,string name,int width,int height)
    {
        view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();Pump();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(output,name+".png")))encoder.Save(stream);
        Check(view.ActualWidth>0&&view.ActualHeight>0,name+" renders without XAML/resource errors");
    }
    static IEnumerable<T> Children<T>(DependencyObject p) where T:DependencyObject
    {
        for(int n=0;n<VisualTreeHelper.GetChildrenCount(p);n++) {var c=VisualTreeHelper.GetChild(p,n);if(c is T)yield return (T)c;foreach(var d in Children<T>(c))yield return d;}
    }
    [STAThread] public static int Main(string[] args)
    {
        output=args[0];Directory.CreateDirectory(output);
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        try {
            var catalog=new System.Data.SqlClient.SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
            Check(catalog=="materijalno_fox_test_20261106_084736","isolated test database required");
            Check(!DatabaseConfiguration.OracleExportEnabled,"Oracle export disabled");
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Materijalno.UI;component/Themes/FinanceTheme.xaml",UriKind.Relative)});
            var shell=new Materijalno.UI.GlavniView();
            var menuButton=(Button)shell.FindName("materijala");
            menuButton.Command.Execute(null); // Opens a read-only master list.
            menuButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,menuButton));Pump();
            Check(((TextBlock)shell.FindName("PageText")).Text=="Šifarnik materijala","header follows the selected page");
            Check(((TextBlock)shell.FindName("SectionText")).Text=="ŠIFARNICI"&&Equals(menuButton.Tag,"Selected"),"menu section and persistent selection match");
            Render(shell,"Shell_materials",1440,850);
            var assembly=typeof(Materijalno.UI.GlavniView).Assembly;
            foreach(var type in assembly.GetTypes().Where(t=>t.Namespace=="Materijalno.UI"&&t.IsSubclassOf(typeof(UserControl))&&t.GetConstructor(Type.EmptyTypes)!=null).OrderBy(t=>t.Name)) {
                var view=(UserControl)Activator.CreateInstance(type);
                if(type.Name!="GlavniView")view.DataContext=new PreviewData();
                if(type.Name=="SifarnikMaterijala")view.DataContext=new SifarnikMaterijalaViewModel(new GlavniViewModel());
                Render(view,type.Name,1100,740);
                if(new[]{"UlazMaterijala","IzlazMaterijala","Medjuskladisnica","AnalitickaKartica","StampanjeInventurnihZaliha","SifarnikMaterijala"}.Contains(type.Name))Render(view,type.Name+"_small",790,580);
                if(type.Name=="UlazMaterijala") {
                    var fields=Children<TextBox>(view).Where(t=>t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path=="CurrentItemMat.Nc").ToList();
                    Check(fields.Count==1&&fields[0].TextAlignment==TextAlignment.Right,"unit cost remains bound and aligned right");
                    Check(fields[0].Text.Contains("889181818"),"nine decimal cost precision preserved");
                    Check(Children<Button>(view).Any(b=>Equals(b.Content,"Snimi")),"transaction action labels preserved");
                }
            }
            // Exercise the real finance input bindings without executing Formiraj.
            foreach(var view in new UserControl[]{new Materijalno.UI.CentralniMagacin(),new Materijalno.UI.ZaduzenjeProdavnice(),new Materijalno.UI.PovratUSkladiste(),new Materijalno.UI.NaloziMedjuskladisnica()}) {
                var main=new GlavniViewModel();
                if(view is Materijalno.UI.CentralniMagacin)main.OtvoriCentralniMagacin();
                else if(view is Materijalno.UI.ZaduzenjeProdavnice)main.OtvoriZaduzenjeProdavnice();
                else if(view is Materijalno.UI.PovratUSkladiste)main.OtvoriPovratUSkladiste();
                else main.OtvoriNaloziMedjuskladisnice();
                var vm=(FinancialOrderViewModel)main.OdabraniVM;view.DataContext=vm;Render(view,view.GetType().Name+"_bound",790,580);
                var input=(TextBox)view.FindName("txtBrojKalkulacije");var button=(Button)view.FindName("btnFormiraj");
                input.Text="DESIGN-PREVIEW";input.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                Check(vm.BrojMedjuskladisnice=="DESIGN-PREVIEW"&&ReferenceEquals(button.Command,vm.FormirajCommand),view.GetType().Name+" real binding and command preserved");
                Check(input.TranslatePoint(new Point(input.ActualWidth,0),view).X+8<=button.TranslatePoint(new Point(0,0),view).X,view.GetType().Name+" input/action separated");
            }
            Console.WriteLine("ALL PASSED "+checks+"; no save/print/post command executed");return 0;
        } catch(Exception e) {Console.WriteLine(e.ToString());return 1;} finally {app.Shutdown();}
    }
}
