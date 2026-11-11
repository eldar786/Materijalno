using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.UI;
using Materijalno.ViewModel;
using Microsoft.EntityFrameworkCore;

public static class StockFileTests
{
    static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    [STAThread]
    public static int Main(string[] args)
    {
        try{
            var date=new DateTime(2026,10,31);var generated=new DateTime(2026,11,11);
            byte[] actual=StockFileExport.Create(date,generated);
            StockFileExport.Save(actual,Path.Combine(args[0],"zalihesve_sql.TXT"));
            byte[] expected=File.ReadAllBytes(Path.Combine(args[0],"fox_example.TXT"));
            Console.WriteLine("FOX bytes="+expected.Length+"; SQL bytes="+actual.Length+"; exact="+expected.SequenceEqual(actual));
            using(var db=new materijalno_knjigovodstvoContext()){
                var rows=ReportQueries.AtDate(db,date).ToList();
                Console.WriteLine("SQL all value="+rows.Sum(m=>m.Vrijed??0)+"; Fajl value="+rows.Where(m=>m.Redbr.GetValueOrDefault()!=0).Sum(m=>m.Vrijed??0));
                Check(rows.Count>0,"Export reads current saved stock including newly entered rows");
                Check(!db.ChangeTracker.HasChanges(),"No SQL modifications");
            }
            Check(actual.Take(3).SequenceEqual(new byte[]{0xEF,0xBB,0xBF})==false,"No UTF8 BOM");
            Check(actual.Contains((byte)0x8A)&&actual.Contains((byte)0x9A),"Windows-1250 Bosnian letters");
            Check(!actual.Contains((byte)0x0C),"No extra form-feed markers");
            Check(StockFileExport.DefaultPath==@"C:\zalihe\zalihesve.TXT","Exact Fox output path");
            // Verify safe create/overwrite and failure preserving an existing output.
            string path=Path.Combine(args[0],"file_write_test","zalihesve.TXT");StockFileExport.Save(actual,path);
            Check(File.ReadAllBytes(path).SequenceEqual(actual),"Creates destination directory and file");
            StockFileExport.Save(expected,path);Check(File.ReadAllBytes(path).SequenceEqual(expected),"Overwrites existing file");
            bool rejected=false;try{StockFileExport.Save(new byte[0],path);}catch(InvalidOperationException){rejected=true;}
            Check(rejected&&File.ReadAllBytes(path).SequenceEqual(expected),"Empty output leaves previous file intact");
            Check(!Directory.GetFiles(Path.GetDirectoryName(path),"*.tmp").Any(),"No temporary file left behind");
            var view=new StanjeZalihaZaSvaSkladista();view.DataContext=new StanjeZalihaZaSvaSkladistaViewModel{CurrentItemMat=new Mat{Datun=date}};
            view.Measure(new Size(850,400));view.Arrange(new Rect(0,0,850,400));view.UpdateLayout();
            var button=(Button)view.FindName("btnFajl");var print=(Button)view.FindName("btnStampa");
            Check(button!=null&&(string)button.Content=="Fajl"&&button.IsEnabled,"Fajl button visible and enabled");
            Check(button.ActualWidth==110&&button.ActualHeight==40,"Compact Fajl button size");
            Check(Grid.GetColumn(button)>Grid.GetColumn(print),"Fajl appears after Stampa");
            var position=button.TranslatePoint(new Point(0,0),view);
            Check(position.X>=0&&position.X+button.ActualWidth<=850,"Fajl button fits within the form");
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(850,400,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(view);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using(var file=File.Create(Path.Combine(args[0],"Fajl_view.png")))png.Save(file);
            var materials=new[]{new TabelaMaterijala{Ident=7,Nazmat="MATERIJAL ČĆŽŠĐ",Jedm="KG"}};
            var warehouses=new[]{new Materijalno.Model.EntityModels.SifarnikSkladista{Kljnaz=1000,NazivOrg="TEST SKLADIŠTE"}};
            var source=new[]{new Mat{IsDraft=false,BookYear=2026,Datun=date,Ident=7,Kljnaz=1000,Redbr=1,Kolic=3m,Vrijed=4.5m},
                new Mat{IsDraft=false,BookYear=2026,Datun=date,Ident=7,Kljnaz=1000,Redbr=0,Kolic=999,Vrijed=999},
                new Mat{IsDraft=true,BookYear=2026,Datun=date,Ident=7,Kljnaz=1000,Redbr=1,Kolic=888,Vrijed=888},
                new Mat{IsDraft=false,BookYear=2026,Datun=date.AddDays(1),Ident=7,Kljnaz=1000,Redbr=1,Kolic=777,Vrijed=777}};
            string text=Encoding.GetEncoding(1250).GetString(StockFileExport.Build(source,materials,warehouses,new System.Collections.Generic.Dictionary<int,string>{{7,"000007"}},date,generated));
            Check(text.Contains("000007")&&text.Contains("KG")&&text.Contains("3,00")&&text.Contains("4,50"),"Material code, unit and totals preserved");
            Check(!text.Contains("999")&&!text.Contains("888")&&!text.Contains("777"),"REDBR zero, drafts and later dates excluded");
            rejected=false;try{StockFileExport.Build(new Mat[0],materials,warehouses,new System.Collections.Generic.Dictionary<int,string>(),date,generated);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Empty period rejected before writing");
            Check(expected.SequenceEqual(actual),"Byte-for-byte match to the user Fox example");
            Console.WriteLine("ALL PASSED "+checks+"; no database writes; example file unchanged.");return 0;
        }catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
