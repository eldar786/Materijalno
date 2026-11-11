using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Materijalno.UI;
using Materijalno.ViewModel;
using Materijalno.ViewModel.Services;
public static class FinancialInputVisualCheck {
 [STAThread] public static int Main(string[] args) {
  try {
   var views=new UserControl[]{new CentralniMagacin(),new ZaduzenjeProdavnice(),new NaloziMedjuskladisnica(),new PovratUSkladiste()};
   var models=new FinancialOrderViewModel[]{new CentralniMagacinViewModel(null),new NaloziZaduzenjeProdavniceViewModel(null),new NaloziMedjuskladisnicaViewModel(null),new PovratUSkladisteViewModel(null)};
   string[] samples={"10-0007","61-0023","M-0191","61-0024"};
   for(int i=0;i<views.Length;i++) {
    var view=views[i];view.DataContext=models[i];
    view.Measure(new Size(900,360));view.Arrange(new Rect(0,0,900,360));view.UpdateLayout();
    var text=(TextBox)view.FindName("txtBrojKalkulacije");
    text.SetCurrentValue(TextBox.TextProperty,samples[i]);text.GetBindingExpression(TextBox.TextProperty).UpdateSource();
    view.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);view.UpdateLayout();
    if(models[i].BrojMedjuskladisnice!=samples[i])throw new Exception("ViewModel binding failed");
    var host=(ScrollViewer)text.Template.FindName("PART_ContentHost",text);
    Console.WriteLine(view.GetType().Name+" text="+text.Text+" field="+text.ActualHeight+" host="+host.ActualHeight+" viewport="+host.ViewportHeight+" extent="+host.ExtentHeight+" padding="+text.Padding+" rect="+text.GetRectFromCharacterIndex(0)+" foreground="+text.Foreground);
    var image=new RenderTargetBitmap(900,360,96,96,PixelFormats.Pbgra32);image.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));
    using(var file=File.Create(Path.Combine(args[0],view.GetType().Name+".png")))png.Save(file);
    if(args.Length>1&&args[1]=="verify"&&host.ViewportHeight<18)throw new Exception("Input text is vertically clipped: "+view.GetType().Name);
    models[i].BrojMedjuskladisnice="9999";view.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    if(text.Text!="9999")throw new Exception("Model to view binding failed");
   }
   Console.WriteLine("PASS four financial inputs; two-way binding; no Formiraj command executed; no database writes.");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}
 }
}
