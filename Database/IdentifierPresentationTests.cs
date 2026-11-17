using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Materijalno.Model.EntityModels;
using Materijalno.UI;
using Microsoft.Reporting.WinForms;

public static class IdentifierPresentationTests
{
    public sealed class InputData { public Mat CurrentItemMat {get;set;}=new Mat(); public Inv CurrentItemInv {get;set;}=new Inv(); }
    static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static System.Collections.Generic.IEnumerable<DependencyObject> Children(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var n in Children(VisualTreeHelper.GetChild(root,i)))yield return n;}
    [STAThread] public static int Main(string[] args)
    {
        try{
            Directory.CreateDirectory(args[0]);
            UserControl[] views={new UlazMaterijala(),new IzlazMaterijala(),new Medjuskladisnica(),new PovratMaterijala(),new PocetnoStanjePrviPut(),new UnosInventurnogStanja()};
            foreach(var view in views){
                var data=new InputData();view.DataContext=data;view.Measure(new Size(1600,1000));view.Arrange(new Rect(0,0,1600,1000));view.UpdateLayout();
                var input=Children(view).OfType<TextBox>().Single(t=>{string p=t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path;return p=="CurrentItemMat.Ident"||p=="CurrentItemInv.Ident";});
                Check(input.MaxLength==9,"Nine-character code input: "+view.GetType().Name);
                foreach(string code in new[]{"094001","000000007","2"}){
                    input.SetCurrentValue(TextBox.TextProperty,code);var binding=input.GetBindingExpression(TextBox.TextProperty);binding.UpdateSource();input.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));view.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);binding.UpdateTarget();
                    string stored=binding.ParentBinding.Path.Path.StartsWith("CurrentItemInv")?data.CurrentItemInv.Ident:data.CurrentItemMat.Ident;
                    Check(stored==code&&input.Text==code,"Focus change retains "+code+" in "+view.GetType().Name);
                }
            }
            int reports=0;
            foreach(string file in Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Izvjestaji"),"*.rdlc")){
                var xml=XDocument.Load(file);XNamespace ns=xml.Root.Name.Namespace;XNamespace rd="http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";
                if(!xml.Descendants(ns+"Field").Any(f=>(string)f.Attribute("Name")=="Ident"))continue;
                using(var report=new LocalReport()){
                    report.ReportPath=file;
                    foreach(var dataset in xml.Descendants(ns+"DataSet")){
                        var table=new DataTable();foreach(var f in dataset.Element(ns+"Fields").Elements(ns+"Field")){
                            string name=(string)f.Attribute("Name"),kind=(string)f.Element(rd+"TypeName")??"System.String";
                            var type=Type.GetType(kind)??typeof(string);type=Nullable.GetUnderlyingType(type)??type;
                            if(name=="Ident")Check(type==typeof(string),"Report identifier is text: "+Path.GetFileName(file));
                            table.Columns.Add(name,type);
                        }
                        for(int i=0;i<2;i++){
                            var row=table.NewRow();foreach(DataColumn c in table.Columns){
                                if(c.DataType==typeof(string))row[c]=c.ColumnName=="Ident"?"094001":c.ColumnName=="Status"?"U":"PROVJERA";
                                else if(c.DataType==typeof(DateTime))row[c]=new DateTime(2026,11,17);
                                else if(c.DataType==typeof(bool))row[c]=false;else row[c]=Convert.ChangeType(10+i,c.DataType);
                            }table.Rows.Add(row);
                        }
                        report.DataSources.Add(new ReportDataSource((string)dataset.Attribute("Name"),table));
                    }
                    var parameters=xml.Descendants(ns+"ReportParameter").Select(p=>{string t=(string)p.Element(ns+"DataType");return new ReportParameter((string)p.Attribute("Name"),t=="DateTime"?"2026-11-17":t=="Boolean"?"false":t=="Integer"||t=="Float"?"1":"PROVJERA");}).ToArray();if(parameters.Length>0)report.SetParameters(parameters);
                    string mime,encoding,extension;string[] streams;Warning[] warnings;
                    byte[] png=report.Render("IMAGE","<DeviceInfo><OutputFormat>PNG</OutputFormat><StartPage>1</StartPage><EndPage>1</EndPage></DeviceInfo>",out mime,out encoding,out extension,out streams,out warnings);
                    Check(png.Length>1000&&!warnings.Any(w=>w.Severity==Severity.Error),"Report renders with leading-zero code: "+Path.GetFileName(file));
                    File.WriteAllBytes(Path.Combine(args[0],Path.GetFileNameWithoutExtension(file)+".png"),png);reports++;
                }
            }
            Console.WriteLine("ALL PASSED "+checks+"; six entry screens; "+reports+" reports; no database writes or Oracle calls.");return 0;
        }catch(Exception e){for(var x=e;x!=null;x=x.InnerException)Console.WriteLine(x.GetType().Name+": "+x.Message);return 1;}
    }
}
