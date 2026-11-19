using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Materijalno.ViewModel.Services;
using Materijalno.UI.Izvjestaji;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.WinForms;
using Oracle.ManagedDataAccess.Client;

public static class FinancialAllChecks
{
    static string work;static int checks;
    static readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
    static readonly List<object> snapshots=new List<object>();
    static readonly List<object> results=new List<object>();
    static readonly List<Case> cases=new List<Case>();
    sealed class Case {public string Status;public string Number;public string Screen;public Guid? BatchId;}
    static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks++;Console.WriteLine("PASS "+label);}
    static void Save(string name,object value)=>File.WriteAllText(Path.Combine(work,name),json.Serialize(value));
    static OracleConnection OpenOracle(){
        var b=new OracleConnectionStringBuilder(DatabaseConfiguration.OracleConnectionString);
        Check(b.DataSource.Equals("192.168.1.224:1521/TESTPDB.LUTRIJABIH.BA",StringComparison.OrdinalIgnoreCase)&&b.UserID=="IBS","Exact approved Oracle TESTPDB endpoint");
        b.ConnectionTimeout=8;var c=new OracleConnection(b.ConnectionString);c.Open();
        using(var q=new OracleCommand("SELECT SYS_CONTEXT('USERENV','CON_NAME')||'/'||SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL",c))Check((string)q.ExecuteScalar()=="TESTPDB/IBS","Connected Oracle identity is TESTPDB/IBS");
        return c;
    }
    static void Key(OracleCommand q,FinancialBatch b){q.BindByName=true;q.CommandTimeout=30;q.Parameters.Add("n",OracleDbType.Varchar2).Value=b.Number.ToString("D4");q.Parameters.Add("a",OracleDbType.Varchar2).Value=b.Activity.ToString();q.Parameters.Add("d",OracleDbType.Date).Value=b.DocumentDate.Date;}
    static DataTable Remote(OracleConnection c,string table,FinancialBatch b){
        using(var q=new OracleCommand("SELECT * FROM "+table+" WHERE LTRIM(TRIM(BROJNAL),'0')=LTRIM(:n,'0') AND TRIM(AKTIVNOST)=:a AND TRUNC(DATNAL)=:d"+(table=="INTERFACE_STAVKE"?" ORDER BY STAVKA":""),c)){Key(q,b);using(var r=q.ExecuteReader()){var t=new DataTable(table);t.Load(r);return t;}}
    }
    static string Text(DataRow r,string n)=>r.IsNull(n)?"":Convert.ToString(r[n],CultureInfo.InvariantCulture).Trim();
    static decimal Money(DataRow r,string n)=>r.IsNull(n)?0:Convert.ToDecimal(r[n],CultureInfo.InvariantCulture);
    static Dictionary<string,object> Fields(object o,params string[] names){return names.ToDictionary(n=>n,n=>{var v=o.GetType().GetProperty(n).GetValue(o);return v is DateTime?(object)((DateTime)v).ToString("yyyy-MM-dd"):v;});}
    static Dictionary<string,string> Stock(){
        using(var db=new materijalno_knjigovodstvoContext())return db.Mat.AsNoTracking().Where(m=>m.BookYear==2026).ToList()
            .Where(m=>m.Ident!=null&&m.Kljnaz.HasValue).GroupBy(m=>m.Kljnaz.Value+"|"+m.Ident)
            .ToDictionary(g=>g.Key,g=>g.Sum(m=>m.Kolic??0).ToString("F2",CultureInfo.InvariantCulture)+"|"+g.Sum(m=>m.Vrijed??0).ToString("F2",CultureInfo.InvariantCulture));
    }
    static string HashTable(string table,string where=""){
        using(var c=new SqlConnection(DatabaseConfiguration.MaterialConnectionString)){c.Open();using(var q=new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.["+table+"] "+where+" ORDER BY "+(table=="document_counter"?"name,book_year":"id")+" FOR XML RAW,BINARY BASE64))",c)){q.CommandTimeout=120;var raw=q.ExecuteScalar();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw==DBNull.Value?"":(string)raw))).Replace("-","");}}
    }
    static FinancialOrderViewModel Page(string status,string number){
        FinancialOrderViewModel page=status=="U"?(FinancialOrderViewModel)new CentralniMagacinViewModel(null):status=="I"?new NaloziZaduzenjeProdavniceViewModel(null):status=="V"?(FinancialOrderViewModel)new PovratUSkladisteViewModel(null):new NaloziMedjuskladisnicaViewModel(null);
        page.BrojMedjuskladisnice=number;return page;
    }
    static void CreateTransfers(Dictionary<string,string> originalStock){
        string file=Path.Combine(work,"transfers_created.json");
        if(File.Exists(file)){
            var saved=json.Deserialize<List<Dictionary<string,object>>>(File.ReadAllText(file));
            foreach(var s in saved)cases.Add(new Case{Status="M",Number=(string)s["Number"],Screen=(string)s["Screen"]});
            Check(Stock().OrderBy(p=>p.Key).SequenceEqual(originalStock.OrderBy(p=>p.Key)),"Existing transfer pair and return leave stock unchanged");return;
        }
        var plan=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(work,"transfer_plan.json")));
        string ident=(string)plan["Ident"];int source=Convert.ToInt32(plan["SourceWarehouse"]),destination=Convert.ToInt32(plan["DestinationWarehouse"]);
        decimal expected=Convert.ToDecimal(plan["Value"],CultureInfo.InvariantCulture);
        var service=new MaterialPostingService();string forward=service.ReserveDocumentNumber("M",2026),reverse=service.ReserveDocumentNumber("M",2026);
        Save("transfer_intent.json",new{Ident=ident,Source=source,Destination=destination,Quantity=1,ExpectedValue=expected,Forward=forward,Reverse=reverse});
        var made=new List<Mat>();var record=new List<object>();
        try{
            for(int i=0;i<2;i++){
                var input=MaterialPostingService.CreateTransferDraft();input.Ident=ident;input.Kljnaz=i==0?source:destination;input.Kljnaz1=i==0?destination:source;input.Kolic=1;input.Brfak=i==0?forward:reverse;
                input.Brdok=i==0?"TEST-FIN-20261119-F":"TEST-FIN-20261119-R";input.Datun=new DateTime(2026,11,19);input.Datnar=input.Datun;input.BookYear=2026;input.Redbr=1;
                var row=service.SaveTransfer(input);made.Add(row);
                record.Add(new{Number=row.Brfak,Screen=i==0?"Međuskladišnica":"Međuskladišnica - povratni testni prenos",Id=row.Id,DocumentId=row.DocumentId,Pair=row.TransferPairId,Ident=ident,Quantity=1,Value=Math.Abs(row.Vrijed.Value)});
                Save("transfer_progress.json",record);Check(Math.Abs(row.Vrijed.Value)==expected,"Test transfer value equals planned reverse value: "+row.Brfak);
            }
            Check(Stock().OrderBy(p=>p.Key).SequenceEqual(originalStock.OrderBy(p=>p.Key)),"All material/warehouse quantities and values restored exactly by reverse transfer");
            Save("transfers_created.json",record);cases.Add(new Case{Status="M",Number=forward,Screen="Međuskladišnica"});cases.Add(new Case{Status="M",Number=reverse,Screen="Međuskladišnica - povratni testni prenos"});
        }catch{
            // No finance batch exists yet: remove only unexported transfers created in this attempt.
            foreach(var row in made.AsEnumerable().Reverse())service.Delete(row);
            throw;
        }
    }
    static void Render(FinancialBatch b,IList<Nalmat> lines){
        var vm=new NaloziZaFinansijePrintanjeNalogaViewModel{Godina=b.BookYear,Brfak=b.Brfak};
        var report=new NaloziZaFinansijePrintanjeNalogaIzvjestaj(vm);
        var table=(DataTable)report.GetType().GetField("reportDt",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(report);
        Check(table.Rows.Count==lines.Count,"Printanje naloga reads all new SQL lines: "+b.Brfak);
        var viewer=(ReportViewer)report.FindName("_reportViewer");
        if(viewer==null)throw new InvalidOperationException("ReportViewer missing.");
        string mime,encoding,extension;string[] streams;Warning[] warnings;
        byte[] bytes=viewer.LocalReport.Render("IMAGE","<DeviceInfo><OutputFormat>PNG</OutputFormat><StartPage>1</StartPage><EndPage>1</EndPage><DpiX>96</DpiX><DpiY>96</DpiY></DeviceInfo>",out mime,out encoding,out extension,out streams,out warnings);
        Directory.CreateDirectory(Path.Combine(work,"reports"));File.WriteAllBytes(Path.Combine(work,"reports",b.Brfak+".png"),bytes);report.Close();
    }
    static void RunCase(Case item){
        FinancialBatch b=Page(item.Status,item.Number).PrepareLocal();item.BatchId=b.Id;Save("case_progress.json",cases);
        List<Nalmat> lines;List<Mat> source;
        using(var db=new materijalno_knjigovodstvoContext()){
            b=db.FinancialBatches.AsNoTracking().Single(x=>x.Id==b.Id);
            lines=db.Nalmat.AsNoTracking().Where(n=>n.BatchId==b.Id).OrderBy(n=>n.LineNumber).ThenBy(n=>n.Id).ToList();
            source=db.Mat.AsNoTracking().Where(m=>m.DocumentId==b.SourceDocumentId).ToList();
        }
        Check(b.SourceStatus==item.Status&&b.Brfak==item.Number,"Correct screen/status/document mapping: "+item.Screen);
        Check(FinancialPostingService.Hash(lines)==b.ContentHash&&FinancialPostingService.Hash(FinancialPostingService.BuildLines(b,source))==b.ContentHash,"SQL source, nalmat lines and batch hash agree: "+item.Number);
        Check(lines.All(n=>n.Bronsta==b.Number&&n.Sifakt==b.Activity&&n.BatchId==b.Id)&&lines.Select(n=>n.LineNumber.Value).SequenceEqual(Enumerable.Range(1,lines.Count)),"All local journal numbers, activity and sequence links correct");
        FinancialPostingService.Validate(lines);var gateway=new OracleFinancialGateway();
        using(var c=OpenOracle()){
            var h=Remote(c,"INTERFACE_NALOG",b);var s=Remote(c,"INTERFACE_STAVKE",b);
            h.WriteXml(Path.Combine(work,item.Number+"_header_before.xml"),XmlWriteMode.WriteSchema);s.WriteXml(Path.Combine(work,item.Number+"_lines_before.xml"),XmlWriteMode.WriteSchema);
            Check((b.State=="Sent"&&gateway.Inspect(b,lines)==RemoteJournalState.Matches)||(h.Rows.Count==0&&s.Rows.Count==0),"Remote key absent before new transfer, or exact completed retry");
        }
        Console.WriteLine("EXPORT "+item.Status+" "+item.Number+" -> "+b.Number.ToString("D4")+"; activity="+b.Activity+"; lines="+lines.Count);Console.Out.Flush();
        b=new FinancialPostingService().Export(b.Id,gateway);
        Check(b.State=="Sent"&&b.ExportedAt.HasValue&&b.Error==null,"SQL marks successful TESTPDB export Sent: "+item.Number);
        using(var c=OpenOracle()){
            var h=Remote(c,"INTERFACE_NALOG",b);var s=Remote(c,"INTERFACE_STAVKE",b);var mapped=FinancialPostingService.OracleLines(b,lines);
            Check(h.Rows.Count==1&&s.Rows.Count==lines.Count,"Oracle exactly one header and all expected detail rows");
            decimal internalId=Money(h.Rows[0],"NALOG");Check(internalId>0,"Oracle generated internal NALOG id");
            Check(Text(h.Rows[0],"BROJNAL")==b.Number.ToString("D4")&&Text(h.Rows[0],"AKTIVNOST")==b.Activity.ToString(),"Header number has leading zeros and correct activity");
            for(int i=0;i<s.Rows.Count;i++){
                var r=s.Rows[i];var expected=mapped[i];
                Check(Money(r,"NALOG")==internalId&&Money(r,"STAVKA")==i+1,"Header/detail NALOG and line sequence: "+(i+1));
                Check(Text(r,"AUTOR")=="MAT"&&Text(r,"KONTO")==expected.Sintstav.ToString()&&Text(r,"BROJDOK")==expected.Brdokst&&Text(r,"KOMITENT")==expected.Analst,"Oracle account/description/partner/author: "+(i+1));
                Check(Money(r,"DUGUJE")==expected.Dug1st&&Money(r,"POTRAZUJE")==expected.Pot1st&&((DateTime)r["DATDOK"]).Date==expected.Datdokst.Date,"Oracle dates and exact monetary amounts: "+(i+1));
                Check(Text(r,"MJT")==(expected.Mjtst.HasValue?expected.Mjtst.Value.ToString("D5"):""),"Oracle cost-center formatting: "+(i+1));
            }
            Check(gateway.Inspect(b,lines)==RemoteJournalState.Matches,"Independent remote inspect matches SQL payload");
            h.WriteXml(Path.Combine(work,item.Number+"_header_after.xml"),XmlWriteMode.WriteSchema);s.WriteXml(Path.Combine(work,item.Number+"_lines_after.xml"),XmlWriteMode.WriteSchema);
            int localCount;using(var db=new materijalno_knjigovodstvoContext())localCount=db.Nalmat.Count(n=>n.BatchId==b.Id);
            var repeat=Page(item.Status,item.Number).PrepareLocal();repeat=new FinancialPostingService().Export(repeat.Id,gateway);
            var h2=Remote(c,"INTERFACE_NALOG",b);var s2=Remote(c,"INTERFACE_STAVKE",b);
            Check(repeat.Id==b.Id&&h2.Rows.Count==1&&s2.Rows.Count==lines.Count,"Repeated Formiraj does not duplicate Oracle header/details");
            using(var db=new materijalno_knjigovodstvoContext()){
                var stored=db.FinancialBatches.AsNoTracking().Single(x=>x.Id==b.Id);
                Check(db.Nalmat.Count(n=>n.BatchId==b.Id)==localCount&&stored.AttemptCount==1&&db.FinancialExportLogs.Count(l=>l.BatchId==b.Id&&l.State=="Sent")==1,"Repeated form/export does not duplicate SQL rows or success logs");
            }
            snapshots.Add(new{Status=item.Status,Number=item.Number,Journal=b.Number,Activity=b.Activity,Date=b.DocumentDate.ToString("yyyy-MM-dd"),
                Source=source.OrderBy(m=>m.FoxRecno.HasValue?0:1).ThenBy(m=>m.FoxRecno).ThenBy(m=>m.Id).Select(m=>Fields(m,"Id","FoxRecno","Kljnaz","Kljnaz1","Ident","Status","Brfak","Brdok","Datun","Datnar","Kolic","Nc","Vrijed","Kontosklad","Kontosklad1","Konto1","Analst","Ourst","Mjtst","Fvrijed","Trospe","Cartro","Porppp","Zavtro")).ToArray(),
                Lines=lines.Select(n=>Fields(n,"LineNumber","Sintstav","Brdokst","Datnsta","Datdokst","Analst","Ourst","Mjtst","Dug1st","Pot1st","Sifakt","Bronsta","Brfak")).ToArray(),
                Oracle=s.Rows.Cast<DataRow>().Select(r=>new{Konto=Text(r,"KONTO"),Description=Text(r,"BROJDOK"),Date=((DateTime)r["DATDOK"]).ToString("yyyy-MM-dd"),Partner=Text(r,"KOMITENT"),CostCenter=Text(r,"MJT"),Debit=Money(r,"DUGUJE"),Credit=Money(r,"POTRAZUJE"),Author=Text(r,"AUTOR"),Line=Money(r,"STAVKA"),Nalog=Money(r,"NALOG")}).ToArray()});
            Save("cases_snapshot.json",snapshots);
            results.Add(new{Screen=item.Screen,Status=item.Status,Calculation=item.Number,Journal=b.Number.ToString("D4"),Activity=b.Activity,Date=b.DocumentDate.ToString("yyyy-MM-dd"),BatchId=b.Id,OracleNalog=internalId,HeaderRows=h.Rows.Count,DetailRows=s.Rows.Count,Debit=lines.Sum(n=>n.Dug1st??0),Credit=lines.Sum(n=>n.Pot1st??0),State=b.State,Idempotent=true});Save("results.json",results);
            Console.WriteLine("SENT "+item.Number+"; BROJNAL="+b.Number.ToString("D4")+"; Oracle NALOG="+internalId+"; rows="+s.Rows.Count+"; D=P="+lines.Sum(n=>n.Dug1st??0).ToString("F2",CultureInfo.InvariantCulture));
        }
        Render(b,lines);
    }
    [STAThread] public static int Main(string[] args){work=args[0];var failures=new List<string>();try{
        Check(DatabaseConfiguration.OracleExportEnabled,"Oracle export enabled in test process");
        Check(Process.GetProcessesByName("Materijalno.UI").Length==0,"No parallel application while test documents are created");
        var backup=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(work,"backup_before.json")));Check((bool)backup["Verified"]&&(string)backup["Database"]=="materijalno_fox_20261105_102038","Verified backup of exact current SQL database");
        using(var c=OpenOracle()){}
        int maxMat,maxNal;using(var db=new materijalno_knjigovodstvoContext()){Check(db.Database.GetDbConnection().Database=="materijalno_fox_20261105_102038","Exact current SQL catalog");maxMat=db.Mat.IgnoreQueryFilters().Max(m=>m.Id);maxNal=db.Nalmat.Max(n=>n.Id);}
        var stock=Stock();Save("stock_before.json",stock);
        var protectedHashes=new Dictionary<string,string>{{"mat",HashTable("mat","WHERE id<="+maxMat)},{"nalmat",HashTable("nalmat","WHERE id<="+maxNal)}};
        foreach(string table in new[]{"inv","tabela_materijala","sifarnik_materijal_skladiste_konto","sifarnik_skladista","sifarnik_konta"})protectedHashes[table]=HashTable(table);
        Save("protected_hashes_before.json",protectedHashes);
        cases.Add(new Case{Status="U",Number="10-36",Screen="Ulaz u centralni magacin"});cases.Add(new Case{Status="I",Number="61-241",Screen="Zaduženje prodavnice"});cases.Add(new Case{Status="V",Number="61-247",Screen="Povrat u skladište"});
        CreateTransfers(stock);Save("case_progress.json",cases);
        foreach(var item in cases){try{RunCase(item);}catch(Exception e){string message=item.Status+" "+item.Number+": "+e.GetType().Name+": "+e.Message;failures.Add(message);Console.WriteLine("FAIL "+message);Save("failures.json",failures);}}
        Check(Stock().OrderBy(p=>p.Key).SequenceEqual(stock.OrderBy(p=>p.Key)),"All stock quantities and values identical after all financial tests");
        foreach(var p in protectedHashes){string where=p.Key=="mat"?"WHERE id<="+maxMat:p.Key=="nalmat"?"WHERE id<="+maxNal:"";Check(HashTable(p.Key,where)==p.Value,"All pre-existing rows unchanged in "+p.Key);}
        Save("stock_after.json",Stock());Save("result.json",new{Passed=failures.Count==0,Checks=checks,Completed=results.Count,Attempted=cases.Count,Failures=failures,OracleDestination="TESTPDB/IBS",NewDatabaseCreated=false,StockUnchanged=true,NewJournalsRetained=true});
        if(failures.Count==0){Console.WriteLine("ALL PASSED "+checks+"; all four financial screens; five SQL/TESTPDB journals including reversal; print reports and idempotency checked; stock unchanged.");return 0;}return 1;
    }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);Save("fatal.json",new{Type=e.GetType().Name,Error=e.Message});return 1;}}
}
