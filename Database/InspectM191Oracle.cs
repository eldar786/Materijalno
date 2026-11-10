using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;

// Read-only diagnosis of the user's explicitly selected test journal.
public static class InspectM191Oracle
{
    static void Print(IDataReader r)
    {
        for(int i=0;i<r.FieldCount;i++) Console.Write((i==0?"":" | ")+r.GetName(i));
        Console.WriteLine();
        while(r.Read()) {
            for(int i=0;i<r.FieldCount;i++) Console.Write((i==0?"":" | ")+(r.IsDBNull(i)?"<NULL>":r.GetValue(i) is DateTime?((DateTime)r.GetValue(i)).ToString("yyyy-MM-dd HH:mm:ss"):Convert.ToString(r.GetValue(i),CultureInfo.InvariantCulture)));
            Console.WriteLine();
        }
    }
    [STAThread]
    public static int Main()
    {
        try {
            using(var db=new SqlConnection(ConfigurationManager.ConnectionStrings["MaterijalnoDb"].ConnectionString)) {
                db.Open();
                foreach(string q in new[]{
                    "SELECT id,source_document_id,source_status,brfak,activity,book_year,number,document_date,state,attempt_count,exported_at,error,content_hash FROM dbo.financial_batch WHERE brfak='M-191' ORDER BY book_year",
                    "SELECT n.id,n.batch_id,n.line_number,n.brfak,n.bronsta,n.sifakt,n.datnsta,n.sintstav,n.brdokst,n.datdokst,n.analst,n.ourst,n.mjtst,n.dug1st,n.pot1st FROM dbo.nalmat n WHERE n.brfak='M-191' ORDER BY n.id",
                    "SELECT id,document_id,transfer_pair_id,book_year,kljnaz,kljnaz1,ident,redbr,brdok,brfak,datun,datnar,kolic,nc,vrijed,kontosklad,kontosklad1,is_draft,is_archived FROM dbo.mat WHERE brfak='M-191' ORDER BY id",
                    "SELECT l.* FROM dbo.financial_export_log l JOIN dbo.financial_batch b ON b.id=l.batch_id WHERE b.brfak='M-191' ORDER BY l.id"
                }) { using(var cmd=new SqlCommand(q,db)) using(var r=cmd.ExecuteReader()) Print(r); }
            }
            using(var db=new OracleConnection(ConfigurationManager.ConnectionStrings["OracleFinansije"].ConnectionString)) {
                db.Open();
                foreach(string q in new[]{
                    "SELECT SYS_CONTEXT('USERENV','CON_NAME') CON_NAME,SYS_CONTEXT('USERENV','SERVICE_NAME') SERVICE_NAME,SYS_CONTEXT('USERENV','SESSION_USER') SESSION_USER,SYS_CONTEXT('USERENV','CURRENT_SCHEMA') CURRENT_SCHEMA FROM DUAL",
                    "SELECT OWNER,OBJECT_NAME,OBJECT_TYPE,STATUS FROM ALL_OBJECTS WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND OBJECT_NAME IN ('INTERFACE_NALOG','INTERFACE_STAVKE') ORDER BY OWNER,OBJECT_NAME",
                    "SELECT OWNER,SYNONYM_NAME,TABLE_OWNER,TABLE_NAME,DB_LINK FROM ALL_SYNONYMS WHERE SYNONYM_NAME IN ('INTERFACE_NALOG','INTERFACE_STAVKE') ORDER BY OWNER,SYNONYM_NAME",
                    "SELECT OWNER,TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_LENGTH,DATA_PRECISION,DATA_SCALE,NULLABLE,DATA_DEFAULT FROM ALL_TAB_COLUMNS WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME IN ('INTERFACE_NALOG','INTERFACE_STAVKE') ORDER BY TABLE_NAME,COLUMN_ID",
                    "SELECT OWNER,TRIGGER_NAME,TABLE_OWNER,TABLE_NAME,STATUS,TRIGGER_TYPE,TRIGGERING_EVENT,TRIGGER_BODY FROM ALL_TRIGGERS WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME IN ('INTERFACE_NALOG','INTERFACE_STAVKE') ORDER BY OWNER,TRIGGER_NAME",
                    "SELECT OWNER,NAME,LINE,TEXT FROM ALL_SOURCE WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TYPE='TRIGGER' AND NAME IN ('BI_INTERFACE_GK_NALOG','BI_INTERFACE_STAVKE') ORDER BY NAME,LINE",
                    "SELECT * FROM INTERFACE_NALOG WHERE LTRIM(TRIM(BROJNAL),'0')='393' AND TRIM(AKTIVNOST)='61' AND TRUNC(DATNAL)=DATE '2026-10-31'",
                    "SELECT * FROM INTERFACE_STAVKE WHERE LTRIM(TRIM(BROJNAL),'0')='393' AND TRIM(AKTIVNOST)='61' AND TRUNC(DATNAL)=DATE '2026-10-31' ORDER BY STAVKA"
                }) { Console.WriteLine("ORACLE "+q);using(var cmd=new OracleCommand(q,db)) {cmd.CommandTimeout=20;cmd.InitialLONGFetchSize=-1;using(var r=cmd.ExecuteReader()) Print(r);} }
            }
            return 0;
        }catch(Exception ex){Console.WriteLine(ex.GetType().Name+": "+ex.Message);return 1;}
    }
}
