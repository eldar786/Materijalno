using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Materijalno.ViewModel.Services;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

// This deliberately permits only the user's M-191 / 0393 test, never a bulk export.
public static class VerifyM191OracleTransfer
{
    static int checks;
    static void Check(bool condition,string label) {if(!condition)throw new InvalidOperationException(label);checks++;Console.WriteLine("PASS "+label);}
    static string Scalar(OracleConnection db,string query) {using(var c=new OracleCommand(query,db))return Convert.ToString(c.ExecuteScalar(),CultureInfo.InvariantCulture);}
    static DataTable RemoteRows(OracleConnection db,string table)
    {
        string query="SELECT * FROM "+table+" WHERE LTRIM(TRIM(BROJNAL),'0')='393' AND TRIM(AKTIVNOST)='61' AND TRUNC(DATNAL)=DATE '2026-10-31'"+(table.EndsWith("STAVKE")?" ORDER BY STAVKA":"");
        using(var cmd=new OracleCommand(query,db))using(var r=cmd.ExecuteReader()){var t=new DataTable(table);t.Load(r);return t;}
    }
    static decimal Amount(DataRow r,string key) {return Convert.ToDecimal(r[key],CultureInfo.InvariantCulture);}
    [STAThread]
    public static int Main(string[] args)
    {
        try {
            bool send=args.Length>0 && args[0]=="send-M-191-to-TESTPDB";
            if(args.Length<2)throw new ArgumentException("Expected check or send-M-191-to-TESTPDB, and audit folder.");
            string folder=args[1];Directory.CreateDirectory(folder);
            FinancialBatch batch;System.Collections.Generic.List<Nalmat> lines;
            int matCount,nalCount;decimal stockQuantity,stockValue;
            using(var oracle=new OracleConnection(DatabaseConfiguration.OracleConnectionString)) {
                oracle.Open();
                Check(Scalar(oracle,"SELECT SYS_CONTEXT('USERENV','CON_NAME') FROM DUAL")=="TESTPDB","Oracle container is TESTPDB");
                Check(Scalar(oracle,"SELECT SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL")=="IBS","Oracle schema is IBS");
                Check(Scalar(oracle,"SELECT SYS_CONTEXT('USERENV','SERVICE_NAME') FROM DUAL").Equals("testpdb.LUTRIJABIH.BA",StringComparison.OrdinalIgnoreCase),"Confirmed test service");
                using(var db=new materijalno_knjigovodstvoContext()) {
                    Check(db.Database.GetDbConnection().Database=="materijalno_fox_20261105_102038","Expected SQL catalog");
                    batch=db.FinancialBatches.AsNoTracking().Single(b=>b.Brfak=="M-191"&&b.SourceStatus=="M"&&b.BookYear==2026);
                    Check(batch.Id==Guid.Parse("386c9e6d-8737-451e-87ba-ead0e72333fc")&&batch.Number==393&&batch.Activity==61&&batch.DocumentDate==new DateTime(2026,10,31),"Exact requested journal identity");
                    lines=db.Nalmat.AsNoTracking().Where(n=>n.BatchId==batch.Id).OrderBy(n=>n.LineNumber).ThenBy(n=>n.Id).ToList();
                    var source=db.Mat.AsNoTracking().Where(m=>m.DocumentId==batch.SourceDocumentId).ToList();
                    Check(source.Count==8&&source.All(m=>m.Status=="M"&&m.Brfak=="M-191"),"Four transfer pairs, eight posted MAT rows");
                    Check(lines.Count==8,"Exactly eight local journal lines");
                    Check(FinancialPostingService.Hash(lines)==batch.ContentHash,"Prepared local content hash matches");
                    Check(FinancialPostingService.Hash(FinancialPostingService.BuildLines(batch,source))==batch.ContentHash,"Current MAT source reproduces the prepared journal");
                    Check(lines.Sum(n=>n.Dug1st??0)==3041.76m&&lines.Sum(n=>n.Pot1st??0)==3041.76m,"Duguje = potrazuje = 3041.76");
                    var export=FinancialPostingService.OracleLines(batch,lines);
                    Check(export.All(n=>n.Analst=="1LT01"&&n.Mjtst==null),"M mapping: partner 1LT01 and empty MJT");
                    matCount=db.Mat.IgnoreQueryFilters().Count();nalCount=db.Nalmat.Count();
                    stockQuantity=db.Mat.IgnoreQueryFilters().Sum(m=>m.Kolic??0);stockValue=db.Mat.IgnoreQueryFilters().Sum(m=>m.Vrijed??0);
                    var snapshot=new DataSet("M191LocalBefore");
                    using(var c=db.Database.GetDbConnection().CreateCommand()) {
                        c.CommandText="SELECT * FROM dbo.financial_batch WHERE id='386c9e6d-8737-451e-87ba-ead0e72333fc'";
                        c.Connection.Open();using(var r=c.ExecuteReader()){var t=new DataTable("Batch");t.Load(r);snapshot.Tables.Add(t);}
                        c.CommandText="SELECT * FROM dbo.nalmat WHERE batch_id='386c9e6d-8737-451e-87ba-ead0e72333fc' ORDER BY line_number";
                        using(var r=c.ExecuteReader()){var t=new DataTable("Lines");t.Load(r);snapshot.Tables.Add(t);}
                    }
                    if(!File.Exists(Path.Combine(folder,"local_before.xml")))snapshot.WriteXml(Path.Combine(folder,"local_before.xml"),XmlWriteMode.WriteSchema);
                }
                var gateway=new OracleFinancialGateway();
                var beforeHeaders=RemoteRows(oracle,"IBS.INTERFACE_NALOG");var beforeLines=RemoteRows(oracle,"IBS.INTERFACE_STAVKE");
                if(!File.Exists(Path.Combine(folder,"oracle_headers_before.xml")))beforeHeaders.WriteXml(Path.Combine(folder,"oracle_headers_before.xml"),XmlWriteMode.WriteSchema);
                if(!File.Exists(Path.Combine(folder,"oracle_lines_before.xml")))beforeLines.WriteXml(Path.Combine(folder,"oracle_lines_before.xml"),XmlWriteMode.WriteSchema);
                var state=gateway.Inspect(batch,lines);Console.WriteLine("Oracle before = "+state+"; SQL state = "+batch.State);
                if(!send){Check(state==RemoteJournalState.Absent || state==RemoteJournalState.Matches,"No conflicting Oracle journal");Console.WriteLine("CHECK ONLY: no writes; "+checks+" checks passed.");return 0;}
                Check(state!=RemoteJournalState.Different,"Oracle key is absent or identical");
                var service=new FinancialPostingService();
                var sent=service.Export(batch.Id,gateway);
                Check(sent.State=="Sent","Real application export completed");
                Check(gateway.Inspect(sent,lines)==RemoteJournalState.Matches,"All Oracle payload fields match SQL mapping after commit");
                var headers=RemoteRows(oracle,"IBS.INTERFACE_NALOG");var remote=RemoteRows(oracle,"IBS.INTERFACE_STAVKE");
                Check(headers.Rows.Count==1&&remote.Rows.Count==8,"One Oracle header and eight committed lines");
                Check((string)headers.Rows[0]["BROJNAL"]=="0393","Header BROJNAL preserves leading zero");
                Check(!headers.Rows[0].IsNull("NALOG")&&!headers.Rows[0].IsNull("DATUM_UNOSA"),"Oracle insert trigger generated NALOG and DATUM_UNOSA");
                decimal internalId=Amount(headers.Rows[0],"NALOG");
                Check(remote.AsEnumerable().All(r=>Amount(r,"NALOG")==internalId),"All eight lines reference the generated Oracle NALOG");
                Check((string)headers.Rows[0]["PROKNJIZEN"]=="N"&&remote.AsEnumerable().All(r=>(string)r["PROKNJIZEN"]=="N"),"Header and lines await posting: PROKNJIZEN=N");
                Check(remote.AsEnumerable().All(r=>(string)r["BROJNAL"]=="0393"&&(string)r["AUTOR"]=="MAT"&&(string)r["KOMITENT"]=="1LT01"),"Line number, author and partner correct");
                Check(remote.AsEnumerable().Sum(r=>Amount(r,"DUGUJE"))==3041.76m&&remote.AsEnumerable().Sum(r=>Amount(r,"POTRAZUJE"))==3041.76m,"Oracle totals equal 3041.76 on each side");
                var again=service.Export(batch.Id,gateway);
                Check(again.State=="Sent"&&RemoteRows(oracle,"IBS.INTERFACE_NALOG").Rows.Count==1&&RemoteRows(oracle,"IBS.INTERFACE_STAVKE").Rows.Count==8,"Repeated export does not duplicate rows");
                Check(gateway.Inspect(again,lines)==RemoteJournalState.Matches,"Repeated export preserves every payload field");
                using(var db=new materijalno_knjigovodstvoContext()) {
                    var stored=db.FinancialBatches.AsNoTracking().Single(b=>b.Id==batch.Id);
                    Check(stored.State=="Sent"&&stored.ExportedAt.HasValue&&stored.Error==null,"SQL recorded Sent and exported_at without error");
                    Check(db.FinancialExportLogs.Count(l=>l.BatchId==batch.Id&&l.State=="Sent")==1,"Exactly one successful SQL export log");
                    Check(db.Mat.IgnoreQueryFilters().Count()==matCount&&db.Nalmat.Count()==nalCount,"MAT and NALMAT row counts unchanged");
                    Check(db.Mat.IgnoreQueryFilters().Sum(m=>m.Kolic??0)==stockQuantity&&db.Mat.IgnoreQueryFilters().Sum(m=>m.Vrijed??0)==stockValue,"Stock quantities and values unchanged");
                    Check(FinancialPostingService.Hash(db.Nalmat.AsNoTracking().Where(n=>n.BatchId==batch.Id).OrderBy(n=>n.LineNumber).ThenBy(n=>n.Id).ToList())==batch.ContentHash,"Original eight local journal rows unchanged");
                }
                headers.WriteXml(Path.Combine(folder,"oracle_header_after.xml"),XmlWriteMode.WriteSchema);
                remote.WriteXml(Path.Combine(folder,"oracle_lines_after.xml"),XmlWriteMode.WriteSchema);
                Console.WriteLine("SUCCESS: TESTPDB.IBS; BROJNAL=0393; AKTIVNOST=61; DATNAL=2026-10-31; NALOG="+internalId+"; headers=1; lines=8; D=P=3041.76; "+checks+" checks passed.");
            }
            return 0;
        }catch(Exception e){for(var error=e;error!=null;error=error.InnerException)Console.WriteLine(error.GetType().Name+": "+error.Message);return 1;}
    }
}
