using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

public static class InspectFinancialAll
{
    static DataTable Read(OracleConnection c,string name,string sql){using(var cmd=new OracleCommand(sql,c)){cmd.CommandTimeout=20;using(var r=cmd.ExecuteReader()){var t=new DataTable(name);t.Load(r);return t;}}}
    public static int Main(string[] args){try{
        string work=args[0];var b=new OracleConnectionStringBuilder(DatabaseConfiguration.OracleConnectionString);
        if(!b.DataSource.Equals("192.168.1.224:1521/TESTPDB.LUTRIJABIH.BA",StringComparison.OrdinalIgnoreCase)||!b.UserID.Equals("IBS",StringComparison.OrdinalIgnoreCase))throw new Exception("Only authorized TESTPDB/IBS destination allowed.");
        b.ConnectionTimeout=8;b.Pooling=false;
        Console.WriteLine("CONNECTING approved TESTPDB endpoint using .NET Framework client");Console.Out.Flush();
        using(var c=new OracleConnection(b.ConnectionString)){
            c.Open();var data=new DataSet("OraclePreflight");
            var identity=Read(c,"Identity","SELECT SYS_CONTEXT('USERENV','CON_NAME') CON_NAME,SYS_CONTEXT('USERENV','SERVICE_NAME') SERVICE_NAME,SYS_CONTEXT('USERENV','SESSION_USER') SESSION_USER,SYS_CONTEXT('USERENV','CURRENT_SCHEMA') CURRENT_SCHEMA FROM DUAL");
            if((string)identity.Rows[0]["CON_NAME"]!="TESTPDB"||(string)identity.Rows[0]["CURRENT_SCHEMA"]!="IBS")throw new Exception("Oracle identity does not match TESTPDB/IBS.");
            data.Tables.Add(identity);Console.WriteLine("CONNECTED TESTPDB / IBS; confirmed by SYS_CONTEXT");
            data.Tables.Add(Read(c,"Columns","SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_LENGTH,DATA_PRECISION,DATA_SCALE,NULLABLE,DATA_DEFAULT FROM USER_TAB_COLUMNS WHERE TABLE_NAME IN ('INTERFACE_NALOG','INTERFACE_STAVKE') ORDER BY TABLE_NAME,COLUMN_ID"));
            data.Tables.Add(Read(c,"Triggers","SELECT TRIGGER_NAME,TABLE_NAME,STATUS,TRIGGERING_EVENT FROM USER_TRIGGERS WHERE TABLE_NAME IN ('INTERFACE_NALOG','INTERFACE_STAVKE') ORDER BY TABLE_NAME,TRIGGER_NAME"));
            data.Tables.Add(Read(c,"Numbers","SELECT TRIM(AKTIVNOST) AKTIVNOST,MAX(TO_NUMBER(TRIM(BROJNAL))) MAX_NUMBER,COUNT(*) HEADER_ROWS FROM INTERFACE_NALOG WHERE EXTRACT(YEAR FROM DATNAL)=2026 AND REGEXP_LIKE(TRIM(BROJNAL),'^[0-9]+$') GROUP BY TRIM(AKTIVNOST) ORDER BY TRIM(AKTIVNOST)"));
            data.Tables.Add(Read(c,"Recent","SELECT * FROM (SELECT NALOG,BROJNAL,AKTIVNOST,DATNAL FROM INTERFACE_NALOG WHERE TRIM(AKTIVNOST) IN ('10','61') AND EXTRACT(YEAR FROM DATNAL)=2026 ORDER BY NALOG DESC) WHERE ROWNUM<=12"));
            data.WriteXml(Path.Combine(work,"oracle_preflight.xml"),XmlWriteMode.WriteSchema);
            foreach(DataRow r in data.Tables["Numbers"].Rows)Console.WriteLine("Oracle activity "+r["AKTIVNOST"]+"; max number="+r["MAX_NUMBER"]+"; headers="+r["HEADER_ROWS"]);
        }
        using(var db=new materijalno_knjigovodstvoContext()){
            if(db.Database.GetDbConnection().Database!="materijalno_fox_20261105_102038")throw new Exception("Unexpected SQL database.");
            var all=db.Mat.AsNoTracking().Where(m=>m.BookYear==2026).ToList();var links=db.SifarnikMaterijalSkladisteKonto.AsNoTracking().ToList();
            var stock=all.Where(m=>m.Ident!=null&&m.Kljnaz.HasValue).GroupBy(m=>new{m.Ident,Warehouse=m.Kljnaz.Value}).Select(g=>new{g.Key.Ident,g.Key.Warehouse,Quantity=g.Sum(m=>m.Kolic??0),Value=g.Sum(m=>m.Vrijed??0)}).ToList();
            object plan=null;
            foreach(var source in stock.Where(s=>s.Warehouse==1001&&s.Quantity>=1&&s.Value>0).OrderBy(s=>s.Ident)){
                var destination=stock.SingleOrDefault(s=>s.Warehouse==1002&&s.Ident==source.Ident);if(destination==null||destination.Quantity<0||destination.Value<0)continue;
                if(!links.Any(l=>l.Sifmat==source.Ident&&l.Sifskla==1001)||!links.Any(l=>l.Sifmat==source.Ident&&l.Sifskla==1002))continue;
                decimal value=LedgerMath.IssueValue(new StockBalance{Quantity=source.Quantity,Value=source.Value},1);
                decimal reverse=LedgerMath.IssueValue(new StockBalance{Quantity=destination.Quantity+1,Value=destination.Value+value},1);
                if(value<=0||reverse!=value)continue;
                plan=new{Ident=source.Ident,SourceWarehouse=1001,DestinationWarehouse=1002,Quantity=1m,Value=value,ReverseValue=reverse,SourceQuantity=source.Quantity,SourceValue=source.Value,DestinationQuantity=destination.Quantity,DestinationValue=destination.Value};
                Console.WriteLine("Transfer/reversal plan: "+source.Ident+"; 1001 -> 1002 -> 1001; quantity=1; each value="+value.ToString("F2",CultureInfo.InvariantCulture)+"; final balances unchanged");break;
            }
            if(plan==null)throw new Exception("No exact reversible one-unit transfer found; no changes made.");
            File.WriteAllText(Path.Combine(work,"transfer_plan.json"),new JavaScriptSerializer().Serialize(plan));
            foreach(int activity in new[]{10,61}){int max=db.Nalmat.Where(n=>n.Sifakt==activity&&n.Datnsta>=new DateTime(2026,1,1)&&n.Datnsta<new DateTime(2027,1,1)).Select(n=>(int?)n.Bronsta).Max()??0;var counter=db.DocumentCounters.SingleOrDefault(d=>d.Name=="FIN_"+activity&&d.BookYear==2026);Console.WriteLine("SQL next activity "+activity+" number="+(Math.Max(max,counter?.LastNumber??0)+1));}
        }
        Console.WriteLine("PREFLIGHT PASSED; read only; no SQL/Oracle journal inserts.");return 0;
    }catch(Exception e){for(var x=e;x!=null;x=x.InnerException)Console.WriteLine(x.GetType().Name+": "+x.Message);return 1;}}
}
