using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Materijalno.ViewModel.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.WinForms;

public static class FinancialParityTests
{
    static int checks;
    static string folder;
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
    static readonly DateTime Day=new DateTime(2026,11,5), InvoiceDay=new DateTime(2026,10,20);
    static readonly MaterialPostingService Posting=new MaterialPostingService();
    static readonly FinancialPostingService Finance=new FinancialPostingService();
    static readonly List<object> Cases=new List<object>();
    static readonly List<FinancialBatch> Batches=new List<FinancialBatch>();
    static materijalno_knjigovodstvoContext Db()=>new materijalno_knjigovodstvoContext();
    static void Check(string name,bool value){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Reject(string name,Action action,string contains=null){try{action();}catch(InvalidOperationException e){Check(name,contains==null||e.Message.Contains(contains));return;}throw new Exception(name+" was accepted");}
    static void Write(string name,object value)=>File.WriteAllText(Path.Combine(folder,name),Json.Serialize(value));
    static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
    sealed class Gateway:IFinancialGateway
    {
        public bool Stored,Before,Ack,Conflict;public int Sends,Inspects;
        public RemoteJournalState Inspect(FinancialBatch b,IList<Nalmat> l){Interlocked.Increment(ref Inspects);return Conflict?RemoteJournalState.Different:Stored?RemoteJournalState.Matches:RemoteJournalState.Absent;}
        public void SendAtomically(FinancialBatch b,IList<Nalmat> l){Interlocked.Increment(ref Sends);if(Before)throw new InvalidOperationException("SIMULATED failure before commit");Stored=true;if(Ack){Ack=false;throw new InvalidOperationException("SIMULATED lost acknowledgement");}}
    }
    static object Fields(object source,string[] names)
    {
        return names.ToDictionary(n=>n,n=>{object v=source.GetType().GetProperty(n).GetValue(source);return v is DateTime?(object)((DateTime)v).ToString("yyyyMMdd"):v;});
    }
    static readonly string[] MatFields={"Id","Status","Ident","Kljnaz","Kljnaz1","Brfak","Brdok","Datun","Datnar","Vrijed","Kolic","Fvrijed","Cartro","Zavtro","Porppp","Trospe","Analst","Ourst","Mjtst","Konto1","Kontosklad","Kontosklad1","TransferPairId"};
    static readonly string[] LineFields={"LineNumber","Sintstav","Brdokst","Datnsta","Datdokst","Analst","Ourst","Mjtst","Dug1st","Pot1st","Sifakt","Bronsta","Brfak"};
    static Mat Row(string status,string number,int material,int warehouse,decimal quantity,int line)
    {
        return new Mat{Status=status,Brfak=number,Ident=material,Kljnaz=warehouse,Kolic=quantity,Redbr=line,
            Datun=Day,Datnar=InvoiceDay,Brdok=" FIN-TEST ",Analst="1TEST1",Ourst=123,Mjtst=123456,
            Fvrijed="0",Cartro="0",Zavtro="0",Porppp=0,Trospe=0,Brnar="RN-FIN",Medus=""};
    }
    static string Save(string status,params Mat[] lines)
    {
        string number=Posting.ReserveDocumentNumber(status,2026);
        foreach(var line in lines){line.Status=status;line.Brfak=number;Posting.Save(line);}
        return number;
    }
    static FinancialBatch PrepareThroughView(string status,string number)
    {
        var main=new GlavniViewModel();FinancialOrderViewModel vm;UserControl view;
        switch(status){
            case "U":main.OtvoriCentralniMagacin();view=new Materijalno.UI.CentralniMagacin();break;
            case "I":main.OtvoriZaduzenjeProdavnice();view=new Materijalno.UI.ZaduzenjeProdavnice();break;
            case "V":main.OtvoriPovratUSkladiste();view=new Materijalno.UI.PovratUSkladiste();break;
            default:main.OtvoriNaloziMedjuskladisnice();view=new Materijalno.UI.NaloziMedjuskladisnica();break;
        }
        vm=(FinancialOrderViewModel)main.OdabraniVM;
        view.DataContext=vm;Pump();var box=(TextBox)view.FindName("txtBrojKalkulacije");
        box.Text=" "+number+" ";box.GetBindingExpression(TextBox.TextProperty).UpdateSource();Pump();
        Check(status+" actual input binds typed calculation",vm.BrojMedjuskladisnice.Trim()==number);
        Check(status+" Formiraj button is connected",ReferenceEquals(((Button)view.FindName("btnFormiraj")).Command,vm.FormirajCommand));
        var batch=vm.PrepareLocal();var again=vm.PrepareLocal();Check(status+" repeated preparation keeps one journal",batch.Id==again.Id);
        using(var db=Db()){
            var source=db.Mat.AsNoTracking().Where(m=>m.Brfak==number&&m.Status==status).OrderBy(m=>m.Id).ToList();
            var lines=db.Nalmat.AsNoTracking().Where(n=>n.BatchId==batch.Id).OrderBy(n=>n.LineNumber).ToList();
            Check(status+" balanced, sequenced and pending",lines.Count>=2&&lines.Sum(l=>l.Dug1st)==lines.Sum(l=>l.Pot1st)&&lines.Select(l=>l.LineNumber.Value).SequenceEqual(Enumerable.Range(1,lines.Count))&&batch.State=="Pending");
            Check(status+" document date follows Fox ADATUN",lines.All(l=>l.Datdokst==Day&&l.Datnsta==Day));
            Cases.Add(new{Status=status,Number=number,Journal=batch.Number,Activity=batch.Activity,Source=source.Select(m=>Fields(m,MatFields)),Lines=lines.Select(l=>Fields(l,LineFields)),OracleMapping=FinancialPostingService.OracleLines(batch,lines).Select(l=>Fields(l,new[]{"Sintstav","Brdokst","Datdokst","Analst","Mjtst","Dug1st","Pot1st"}))});
            Render(number,new NaloziZaFinansijePrintanjeNalogaViewModel{Godina=2026,Brfak=" "+number+" "},lines);
        }
        Batches.Add(batch);return batch;
    }
    [STAThread] public static int Main(string[] args)
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        try{
            folder=args[1];Directory.CreateDirectory(folder);
            var catalog=new System.Data.SqlClient.SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
            if(catalog!=args[0]||!catalog.StartsWith("materijalno_fox_test_20261106_"))throw new Exception("Dedicated financial test copy required.");
            Check("Oracle export is disabled and Oracle configuration absent",!DatabaseConfiguration.OracleExportEnabled&&System.Configuration.ConfigurationManager.ConnectionStrings["OracleFinansije"]==null);
            Thread.CurrentThread.CurrentCulture=CultureInfo.GetCultureInfo("bs-Latn-BA");
            if(args.Length>2&&args[2]=="boundary") { BoundaryCases();Console.WriteLine("BOUNDARY PASSED "+checks+"; no Oracle connection used");return 0; }
            if(args.Length>2&&args[2]=="views") { ViewLayouts();Console.WriteLine("VIEWS PASSED "+checks+"; no Oracle connection used");return 0; }
            using(var db=Db()){
                Write("baseline.json",new{Database=catalog,Mat=db.Mat.IgnoreQueryFilters().Count(),Nalmat=db.Nalmat.Count(),ExistingBatches=db.FinancialBatches.Count(),OracleConnectionsUsed=false});
                Check("Active year is 2026",Posting.ActiveYear()==2026);
                var old=db.Mat.First(m=>m.Status=="I"&&db.Nalmat.Any(n=>n.Brfak==m.Brfak&&n.Datnsta.Year==2026));
                int before=db.Nalmat.Count();Reject("Imported Fox journal is not formed twice",()=>Finance.Prepare(old.Status,old.Brfak),"uvezeni");Check("Imported duplicate rejection writes no lines",db.Nalmat.Count()==before);
                Render("Imported_"+old.Brfak,new NaloziZaFinansijePrintanjeNalogaViewModel{Godina=2026,Brfak=old.Brfak},db.Nalmat.Where(n=>n.Brfak==old.Brfak&&n.Datnsta.Year==2026).OrderBy(n=>n.FoxRecno).ToList());
                foreach(int material in new[]{990711,990712,990713}){
                    if(db.TabelaMaterijala.Any(m=>m.Ident==material))throw new Exception("Fixture already exists");
                    db.TabelaMaterijala.Add(new TabelaMaterijala{Ident=material,Nazmat="FIN TEST "+material,Jedm="KOM",Siftar=1,Konto1=5111116});
                    db.SaveChanges();
                    foreach(int wh in new[]{1000,1001}){
                        int account=db.SifarnikMaterijalSkladisteKonto.First(l=>l.Sifmat==13&&l.Sifskla==wh).Sifkonta.Value;
                        db.SifarnikMaterijalSkladisteKonto.Add(new SifarnikMaterijalSkladisteKonto{Sifmat=material,Sifskla=wh,Sifkonta=account});
                    }
                }db.SaveChanges();
            }
            var u1=Row("U","",990711,1000,10.5m,1);u1.Fvrijed="100";u1.Cartro="2.10";u1.Zavtro="0.37";u1.Porppp=17.6m;u1.Trospe=1.23m;
            var u2=Row("U","",990712,1000,20m,2);u2.Fvrijed="80";u2.Porppp=13.6m;u2.Trospe=2m;
            string un1=Save("U",u1,u2);
            var u3=Row("U","",990711,1000,5m,1);u3.Fvrijed="50";string un2=Save("U",u3);
            string i1=Save("I",Row("I","",990711,1000,2.25m,1),Row("I","",990712,1000,3.75m,2));
            string i2=Save("I",Row("I","",990711,1000,1.1m,1));
            var m1=Row("M","",990711,1000,1.5m,1);m1.Kljnaz1=1001;var m2=Row("M","",990712,1000,2.5m,2);m2.Kljnaz1=1001;string mn1=Save("M",m1,m2);
            var m3=Row("M","",990711,1000,.5m,1);m3.Kljnaz1=1001;string mn2=Save("M",m3);
            string v1=Save("V",Row("V","",990711,1000,.75m,1),Row("V","",990712,1000,.5m,2));
            var vr=Row("V","",990711,1001,.2m,1);vr.Mjtst=1234;string v2=Save("V",vr);
            foreach(var item in new[]{Tuple.Create("U",un1),Tuple.Create("U",un2),Tuple.Create("I",i1),Tuple.Create("I",i2),Tuple.Create("V",v1),Tuple.Create("V",v2),Tuple.Create("M",mn1),Tuple.Create("M",mn2)})PrepareThroughView(item.Item1,item.Item2);
            Write("financial_cases.json",Cases);
            Reject("Missing calculation rejected",()=>Finance.Prepare("U","10-NOT-FOUND"));Reject("Empty calculation rejected",()=>Finance.Prepare("I"," "));Reject("Opening balance cannot form finance",()=>Finance.Prepare("P","P"));
            Reject("Empty print selection rejected",()=>new Materijalno.UI.Izvjestaji.NaloziZaFinansijePrintanjeNalogaIzvjestaj(new NaloziZaFinansijePrintanjeNalogaViewModel{Brfak=""}));
            Reject("Missing print selection rejected",()=>new Materijalno.UI.Izvjestaji.NaloziZaFinansijePrintanjeNalogaIzvjestaj(new NaloziZaFinansijePrintanjeNalogaViewModel{Brfak="NOT-FOUND"}));
            Reject("Invalid print year rejected",()=>new Materijalno.UI.Izvjestaji.NaloziZaFinansijePrintanjeNalogaIzvjestaj(new NaloziZaFinansijePrintanjeNalogaViewModel{Brfak=un1,Godina=0}));
            Reject("Printing another year cannot mix the current journal",()=>new Materijalno.UI.Izvjestaji.NaloziZaFinansijePrintanjeNalogaIzvjestaj(new NaloziZaFinansijePrintanjeNalogaViewModel{Brfak=un1,Godina=2025}));
            var ack=new Gateway{Ack=true};Reject("Simulated lost acknowledgement is recorded",()=>Finance.Export(Batches[0].Id,ack));Check("Retry finds prior remote commit",Finance.Export(Batches[0].Id,ack).State=="Sent"&&ack.Sends==1);Finance.Export(Batches[0].Id,ack);Check("Sent journal is not resent",ack.Sends==1);
            var fail=new Gateway{Before=true};Reject("Simulated precommit failure is retryable",()=>Finance.Export(Batches[2].Id,fail));fail.Before=false;Check("Retry after precommit failure succeeds",Finance.Export(Batches[2].Id,fail).State=="Sent"&&fail.Sends==2);
            var conflict=new Gateway{Conflict=true};Reject("Different remote journal is blocked",()=>Finance.Export(Batches[4].Id,conflict));Check("Conflicting remote journal receives no writes",conflict.Sends==0);
            var parallel=new Gateway();Task.WaitAll(Task.Run(()=>Finance.Export(Batches[6].Id,parallel)),Task.Run(()=>Finance.Export(Batches[6].Id,parallel)));Check("Concurrent export sends only once",parallel.Sends==1);
            using(var db=Db()){
                var line=db.Nalmat.First(n=>n.BatchId==Batches[1].Id);int? original=line.Mjtst;line.Mjtst=54321;db.SaveChanges();
                var never=new Gateway();Reject("Changed cost center is detected before export",()=>Finance.Export(Batches[1].Id,never),"promijenjen");Check("Tampered journal reaches no gateway",never.Sends==0&&never.Inspects==0);line.Mjtst=original;db.SaveChanges();
                var source=db.Mat.First(m=>m.Brfak==un1);Reject("Prepared journal locks source document",()=>Posting.Delete(source));
            }
            var sample=Lines(Batches[3]);Reject("Real Oracle gateway cannot open in test mode",()=>new OracleFinancialGateway().Inspect(Batches[3],sample),"nije uključen");Reject("Real Oracle send cannot open in test mode",()=>new OracleFinancialGateway().SendAtomically(Batches[3],sample),"nije uključen");
            var raw=FinancialPostingService.OracleLines(Batches[3],sample);string hash=FinancialPostingService.Hash(raw);raw[0].Mjtst++;Check("Remote cost center contributes to equality check",hash!=FinancialPostingService.Hash(raw));
            using(var db=Db()){
                Write("final_test_state.json",new{Database=catalog,FixtureDocuments=Batches.Count,FixtureLines=db.Nalmat.Count(n=>n.BatchId!=null),ImportedLines=db.Nalmat.Count(n=>n.FoxRecno!=null),OracleConnectionsUsed=false,OracleExportEnabled=DatabaseConfiguration.OracleExportEnabled,Checks=checks});
            }
            Console.WriteLine("ALL PASSED "+checks+"; isolated database="+catalog+"; no Oracle connection used");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}finally{app.Shutdown();}
    }
    static IList<Nalmat> Lines(FinancialBatch batch){using(var db=Db())return db.Nalmat.AsNoTracking().Where(n=>n.BatchId==batch.Id).OrderBy(n=>n.LineNumber).ToList();}
    static void ViewLayouts()
    {
        foreach(var status in new[]{"U","I","V","M"}){
            var main=new GlavniViewModel();UserControl view;
            switch(status){case "U":main.OtvoriCentralniMagacin();view=new Materijalno.UI.CentralniMagacin();break;case "I":main.OtvoriZaduzenjeProdavnice();view=new Materijalno.UI.ZaduzenjeProdavnice();break;case "V":main.OtvoriPovratUSkladiste();view=new Materijalno.UI.PovratUSkladiste();break;default:main.OtvoriNaloziMedjuskladisnice();view=new Materijalno.UI.NaloziMedjuskladisnica();break;}
            var vm=(FinancialOrderViewModel)main.OdabraniVM;view.DataContext=vm;
            view.Measure(new Size(1100,650));view.Arrange(new Rect(0,0,1100,650));view.UpdateLayout();Pump();
            var text=(TextBox)view.FindName("txtBrojKalkulacije");var button=(Button)view.FindName("btnFormiraj");text.Text="TEST-VIEW";text.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            Check(status+" final view binds the input",vm.BrojMedjuskladisnice=="TEST-VIEW");
            Check(status+" final view has connected command",ReferenceEquals(button.Command,vm.FormirajCommand));
            var inputOrigin=text.TranslatePoint(new Point(0,0),view);var buttonOrigin=button.TranslatePoint(new Point(0,0),view);
            Check(status+" input and button do not overlap",inputOrigin.X+text.ActualWidth+8<=buttonOrigin.X&&text.ActualWidth>=180);
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1100,650,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(view);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(folder,"View_"+status+".png")))encoder.Save(stream);
        }
    }
    static void BoundaryCases()
    {
        var invalid=new[]{new Nalmat{Sintstav=5111116,Dug1st=10,Pot1st=10},new Nalmat{Sintstav=1010100,Dug1st=1,Pot1st=1}};
        Reject("Balanced but two-sided lines are rejected",()=>FinancialPostingService.Validate(invalid));
        invalid[0].Dug1st=0;invalid[0].Pot1st=0;Reject("Empty financial line is rejected",()=>FinancialPostingService.Validate(invalid));
        int batches,number;
        using(var db=Db()){batches=db.FinancialBatches.Count();number=db.DocumentCounters.Single(c=>c.Name=="FIN_10"&&c.BookYear==2026).LastNumber;}
        var gift=Row("U","",990713,1000,1,1);var zero=Save("U",gift);
        Reject("Zero-value calculation does not create an empty journal",()=>Finance.Prepare("U",zero));
        using(var db=Db())Check("Failed zero journal rolls back number and header",db.FinancialBatches.Count()==batches&&db.DocumentCounters.Single(c=>c.Name=="FIN_10"&&c.BookYear==2026).LastNumber==number);
        var numberBefore=0;using(var db=Db())numberBefore=db.DocumentCounters.Single(c=>c.Name=="FIN_61"&&c.BookYear==2026).LastNumber;
        var issue=Save("I",Row("I","",990711,1000,.05m,1));
        var first=Task.Run(()=>Finance.Prepare("I",issue));var second=Task.Run(()=>Finance.Prepare("I",issue));Task.WaitAll(first,second);
        Check("Concurrent first preparation returns one journal",first.Result.Id==second.Result.Id);
        using(var db=Db())Check("Concurrent first preparation reserves one number",db.FinancialBatches.Count(b=>b.SourceDocumentId==first.Result.SourceDocumentId)==1&&db.DocumentCounters.Single(c=>c.Name=="FIN_61"&&c.BookYear==2026).LastNumber==numberBefore+1);
        var lines=Lines(first.Result);var hash=FinancialPostingService.Hash(lines);lines[0].Ourst++;
        Check("Department changes participate in local integrity hash",hash!=FinancialPostingService.Hash(lines));
        Write("boundary_results.json",new{Checks=checks,OracleConnectionsUsed=false,ConcurrentCalculation=issue,ZeroCalculation=zero,Status="Passed"});
    }
    static void Render(string name,NaloziZaFinansijePrintanjeNalogaViewModel vm,IList<Nalmat> expected)
    {
        var window=new Materijalno.UI.Izvjestaji.NaloziZaFinansijePrintanjeNalogaIzvjestaj(vm);
        try{
            var fields=window.GetType().GetFields(BindingFlags.NonPublic|BindingFlags.Instance);
            var table=(DataTable)fields.Single(f=>f.Name=="reportDt").GetValue(window);var viewer=(ReportViewer)fields.Single(f=>f.FieldType==typeof(ReportViewer)).GetValue(window);
            Check(name+" print contains every original journal line",table.Rows.Count==expected.Count&&table.Rows.Cast<DataRow>().Select(r=>(int)r["Sintstav"]).SequenceEqual(expected.Select(l=>l.Sintstav))&&table.Rows.Cast<DataRow>().Sum(r=>(decimal)r["Dug1st"])==expected.Sum(l=>l.Dug1st));
            Check(name+" print retains counterparty codes",table.Rows.Cast<DataRow>().Select(r=>(string)r["Analst"]).SequenceEqual(expected.Select(l=>l.Analst??"")));
            foreach(var format in new[]{"PDF","EXCELOPENXML"}){string mime,encoding,extension;string[] streams;Warning[] warnings;var bytes=viewer.LocalReport.Render(format,null,out mime,out encoding,out extension,out streams,out warnings);if(warnings.Length>0)throw new Exception(string.Join(";",warnings.Select(w=>w.Message)));File.WriteAllBytes(Path.Combine(folder,name+(format=="PDF"?".pdf":".xlsx")),bytes);}
            table.WriteXml(Path.Combine(folder,name+".xml"),XmlWriteMode.WriteSchema);
            Check(name+" actual PDF and XLSX render",true);
        }finally{window.Close();}
    }
}
