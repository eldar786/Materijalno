using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Materijalno.Import;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;

public static class MigrateMaterialIdentifiers
{
    static SqlConnection db;static SqlTransaction tx;static string work;static int checks;
    static string[] tables={"tabela_materijala","mat","inv","sifarnik_materijal_skladiste_konto"};
    static string Column(string table)=>table=="sifarnik_materijal_skladiste_konto"?"Sifmat":"ident";
    static string Q(string s)=>"["+s.Replace("]","]]")+"]";
    static SqlCommand Cmd(string sql)=>new SqlCommand(sql,db,tx){CommandTimeout=180};
    static DataTable Rows(string sql,string name="Rows"){using(var c=Cmd(sql))using(var r=c.ExecuteReader()){var t=new DataTable(name);t.Load(r);return t;}}
    static object Scalar(string sql){using(var c=Cmd(sql))return c.ExecuteScalar();}
    static void Exec(string sql){using(var c=Cmd(sql))c.ExecuteNonQuery();}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    sealed class Change{public string Table;public int Id;public string Code;}
    sealed class Definition{public string Drop,Create;}
    static Dictionary<string,DbfTable> fox=new Dictionary<string,DbfTable>();
    static Dictionary<int,string> canonical=new Dictionary<int,string>();
    static string Canonical(object old,DbfRecord source,string field)
    {
        if(old==DBNull.Value)return null;
        int numeric=Convert.ToInt32(old,CultureInfo.InvariantCulture);string code;
        if(source!=null&&!source.Deleted&&source.Values.TryGetValue(field,out code)&&!string.IsNullOrWhiteSpace(code)){
            int sourceNumber;if(int.TryParse(code.Trim(),out sourceNumber)&&sourceNumber==numeric)return code.Trim();
        }
        return canonical.TryGetValue(numeric,out code)?code:numeric.ToString(CultureInfo.InvariantCulture);
    }
    static List<Change> Plan()
    {
        string source=Path.Combine(work,"fox_source");
        foreach(var name in new[]{"mater","mat","inv","kontosklad"})fox[name]=DbfReader.Read(Directory.GetFiles(source).Single(p=>Path.GetFileName(p).Equals(name+".dbf",StringComparison.OrdinalIgnoreCase)));
        foreach(var r in fox["mater"].Records.Where(r=>!r.Deleted)){
            string code=r.Values["IDENT"].Trim();Check(MaterialIdentifier.IsValid(code),"Valid Fox code "+code);
            int number=int.Parse(code,CultureInfo.InvariantCulture);string previous;
            if(canonical.TryGetValue(number,out previous)&&previous!=code)throw new Exception("Fox has distinct codes previously collapsed into one integer: "+code);
            canonical[number]=code;
        }
        var result=new List<Change>();
        foreach(string table in tables){
            string src=table=="tabela_materijala"?"mater":table=="sifarnik_materijal_skladiste_konto"?"kontosklad":table;
            foreach(DataRow row in Rows("SELECT * FROM dbo."+Q(table)).Rows){
                int id=Convert.ToInt32(row["id"]);int recno=row.Table.Columns.Contains("fox_recno")&&!row.IsNull("fox_recno")?Convert.ToInt32(row["fox_recno"]):id;
                DbfRecord original=recno>0&&recno<=fox[src].Records.Count?fox[src].Records[recno-1]:null;
                string code=Canonical(row[Column(table)],original,src=="kontosklad"?"SIFMAT":"IDENT");
                if(code!=null&&!MaterialIdentifier.IsValid(code))throw new Exception("Invalid code in existing SQL row "+table+"/"+id);
                result.Add(new Change{Table=table,Id=id,Code=code});
            }
        }
        File.WriteAllText(Path.Combine(work,"identifier_plan.json"),new JavaScriptSerializer().Serialize(new{Database=db.Database,Rows=result.Count,LeadingZeroRows=result.Count(r=>r.Code!=null&&r.Code.StartsWith("0")),Tables=tables,NewDatabaseCreated=false}));
        return result;
    }
    static List<Definition> Indexes()
    {
        var result=new List<Definition>();
        foreach(string table in tables){
            var indices=Rows("SELECT i.object_id,i.index_id,i.name,i.is_unique,i.is_unique_constraint,i.is_primary_key,i.type_desc,i.filter_definition,i.is_disabled FROM sys.indexes i WHERE i.object_id=OBJECT_ID('dbo."+table+"') AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND c.name='"+Column(table)+"')");
            foreach(DataRow index in indices.Rows){
                Check(!(bool)index["is_primary_key"]&&!(bool)index["is_disabled"],"Supported dependent index "+index["name"]);
                var cols=Rows("SELECT c.name,ic.is_descending_key,ic.is_included_column FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id="+index["object_id"]+" AND ic.index_id="+index["index_id"]+" ORDER BY ic.key_ordinal,ic.index_column_id").AsEnumerable();
                string key=string.Join(",",cols.Where(c=>!(bool)c["is_included_column"]).Select(c=>Q((string)c["name"])+((bool)c["is_descending_key"]?" DESC":" ASC")));
                string name=Q((string)index["name"]),target="dbo."+Q(table),type=(string)index["type_desc"];
                if((bool)index["is_unique_constraint"])result.Add(new Definition{Drop="ALTER TABLE "+target+" DROP CONSTRAINT "+name,Create="ALTER TABLE "+target+" ADD CONSTRAINT "+name+" UNIQUE "+type+" ("+key+")"});
                else{
                    string include=string.Join(",",cols.Where(c=>(bool)c["is_included_column"]).Select(c=>Q((string)c["name"])));
                    result.Add(new Definition{Drop="DROP INDEX "+name+" ON "+target,Create="CREATE "+((bool)index["is_unique"]?"UNIQUE ":"")+type+" INDEX "+name+" ON "+target+" ("+key+")"+(include==""?"":" INCLUDE ("+include+")")+(index.IsNull("filter_definition")?"":" WHERE "+index["filter_definition"])});
                }
            }
        }
        return result;
    }
    static List<Definition> ForeignKeys()
    {
        var result=new List<Definition>();
        var rows=Rows("SELECT DISTINCT f.object_id,f.name,f.parent_object_id,f.referenced_object_id,f.delete_referential_action_desc,f.update_referential_action_desc,f.is_disabled,f.is_not_trusted FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id JOIN sys.columns pc ON pc.object_id=fc.parent_object_id AND pc.column_id=fc.parent_column_id JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id WHERE (OBJECT_NAME(pc.object_id) IN ('mat','inv','tabela_materijala') AND pc.name='ident') OR (OBJECT_NAME(pc.object_id)='sifarnik_materijal_skladiste_konto' AND pc.name='Sifmat') OR (OBJECT_NAME(rc.object_id)='tabela_materijala' AND rc.name='ident')");
        foreach(DataRow r in rows.Rows){
            Check(!(bool)r["is_disabled"]&&!(bool)r["is_not_trusted"],"Trusted material FK "+r["name"]);
            var names=Rows("SELECT OBJECT_SCHEMA_NAME(parent_object_id) ps,OBJECT_NAME(parent_object_id) pt,pc.name pcol,OBJECT_SCHEMA_NAME(referenced_object_id) rs,OBJECT_NAME(referenced_object_id) rt,rc.name rcol FROM sys.foreign_key_columns f JOIN sys.columns pc ON pc.object_id=f.parent_object_id AND pc.column_id=f.parent_column_id JOIN sys.columns rc ON rc.object_id=f.referenced_object_id AND rc.column_id=f.referenced_column_id WHERE constraint_object_id="+r["object_id"]+" ORDER BY constraint_column_id");
            var first=names.Rows[0];string parent=Q((string)first["ps"])+"."+Q((string)first["pt"]),referenced=Q((string)first["rs"])+"."+Q((string)first["rt"]),name=Q((string)r["name"]);
            result.Add(new Definition{Drop="ALTER TABLE "+parent+" DROP CONSTRAINT "+name,Create="ALTER TABLE "+parent+" WITH CHECK ADD CONSTRAINT "+name+" FOREIGN KEY ("+string.Join(",",names.AsEnumerable().Select(a=>Q((string)a["pcol"])))+") REFERENCES "+referenced+" ("+string.Join(",",names.AsEnumerable().Select(a=>Q((string)a["rcol"])))+") ON DELETE "+((string)r["delete_referential_action_desc"]).Replace('_',' ')+" ON UPDATE "+((string)r["update_referential_action_desc"]).Replace('_',' ')});
        }
        return result;
    }
    static List<Definition> Checks()
    {
        return Rows("SELECT OBJECT_NAME(parent_object_id) table_name,name,definition FROM sys.check_constraints WHERE parent_object_id IN (OBJECT_ID('dbo.mat'),OBJECT_ID('dbo.inv'),OBJECT_ID('dbo.tabela_materijala'),OBJECT_ID('dbo.sifarnik_materijal_skladiste_konto'))").AsEnumerable().Select(r=>new Definition{Drop="ALTER TABLE dbo."+Q((string)r["table_name"])+" DROP CONSTRAINT "+Q((string)r["name"]),Create="ALTER TABLE dbo."+Q((string)r["table_name"])+" WITH CHECK ADD CONSTRAINT "+Q((string)r["name"])+" CHECK "+r["definition"]}).ToList();
    }
    static string ProtectedHash(string table)
    {
        var t=Rows("SELECT * FROM dbo."+Q(table)+" ORDER BY "+(table=="document_counter"?"name,book_year":"id"));
        using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream,Encoding.UTF8)){
            foreach(DataRow r in t.Rows)foreach(DataColumn c in t.Columns){
                if(tables.Contains(table)&&(c.ColumnName.Equals(Column(table),StringComparison.OrdinalIgnoreCase)||c.ColumnName=="row_version"))continue;
                object v=r[c];w.Write(c.ColumnName);w.Write(v==DBNull.Value);if(v==DBNull.Value)continue;
                if(v is byte[]){w.Write(((byte[])v).Length);w.Write((byte[])v);}else w.Write(v is DateTime?((DateTime)v).ToString("O",CultureInfo.InvariantCulture):Convert.ToString(v,CultureInfo.InvariantCulture));
            }
            w.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","");
        }
    }
    static materijalno_knjigovodstvoContext Context()
    {
        var context=new materijalno_knjigovodstvoContext(new DbContextOptionsBuilder<materijalno_knjigovodstvoContext>().UseSqlServer(db).Options);
        context.Database.UseTransaction(tx);return context;
    }
    static void ApplicationChecks()
    {
        using(var context=Context()){
            Check(context.TabelaMaterijala.Any(m=>m.Ident=="094001"),"EF finds full code 094001");
            Check(context.Mat.Any(m=>m.Ident=="087001"),"EF filters stock by 087001");
            Check(context.SifarnikMaterijala.Any(m=>m.Ident=="094001"),"Material maintenance view retains zeros");
            Check(context.SifarnikMaterijalSkladisteKonto.Any(m=>m.Sifmat=="094001"),"Material/warehouse links retain zeros");
            var master=context.TabelaMaterijala.AsNoTracking().ToList();Check(master.Any(m=>m.Ident=="2"),"Short code 2 is not padded");
            Check(context.Mat.IgnoreQueryFilters().Count()==Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.mat")),"Application loads all existing ledger records");
        }
        // Native fixture uses negative identity values so rollback does not consume IDs.
        const string test="000000007";const int id=-1900000700;
        Check(Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.tabela_materijala WHERE ident='"+test+"' OR id="+id))==0,"Rollback fixture code is unused");
        Exec("SET IDENTITY_INSERT dbo.tabela_materijala ON; INSERT dbo.tabela_materijala(Id,ident,nazmat,jedm) VALUES("+id+",'"+test+"','PROVJERA VODECIH NULA','KOM'); SET IDENTITY_INSERT dbo.tabela_materijala OFF;");
        Exec("SET IDENTITY_INSERT dbo.mat ON; INSERT dbo.mat(id,ident,kljnaz,is_draft,is_archived,book_year) VALUES("+id+",'094001',1000,1,0,2026); SET IDENTITY_INSERT dbo.mat OFF;");
        using(var context=Context()){
            var row=context.Mat.IgnoreQueryFilters().Single(m=>m.Id==id);Check(row.Ident=="094001","EF reads saved leading zeros");
            row.Ident=test;context.SaveChanges();Check((string)Scalar("SELECT ident FROM dbo.mat WHERE id="+id)==test,"EF SaveChanges preserves arbitrary leading zeros");
            row.Ident="094001";context.SaveChanges();Check((string)Scalar("SELECT ident FROM dbo.mat WHERE id="+id)=="094001","EF edit preserves 094001");
        }
        Exec("DELETE FROM dbo.mat WHERE id="+id+"; DELETE FROM dbo.tabela_materijala WHERE Id="+id);
        Check(Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.mat WHERE id="+id))==0,"Fixture removed before commit");
    }
    [STAThread] public static int Main(string[] args)
    {
        try{
            work=args[0];string mode=args[2];var config=XDocument.Load(args[1]);var entry=config.Root.Element("connectionStrings").Elements("add").Single(e=>(string)e.Attribute("name")=="MaterijalnoDb");var builder=new SqlConnectionStringBuilder((string)entry.Attribute("connectionString"));
            if(builder.InitialCatalog!="materijalno_fox_20261105_102038")throw new Exception("Only the current selected database is permitted.");
            var backup=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(work,"backup_before.json")));Check((bool)backup["Verified"]&&(string)backup["Database"]==builder.InitialCatalog,"Verified backup of the existing database");
            builder.ApplicationName="Materijalno_Identifier_Migration";
            using(db=new SqlConnection(builder.ConnectionString)){
                db.Open();var plan=Plan();Console.WriteLine("PLAN rows="+plan.Count+"; leading-zero rows="+plan.Count(r=>r.Code!=null&&r.Code.StartsWith("0")));
                var index=Indexes();var keys=ForeignKeys();var check=Checks();
                File.WriteAllText(Path.Combine(work,"schema_dependencies.sql"),string.Join(";\r\n",keys.Concat(check).Concat(index).Select(d=>d.Drop).Concat(index.Concat(check).Concat(keys).Select(d=>d.Create)))+";");
                if(mode=="plan")return 0;
                var protectedTables=tables.Concat(new[]{"nalmat","financial_batch","financial_export_log","document_counter"}).ToArray();
                using(tx=db.BeginTransaction(IsolationLevel.Serializable)){
                    try{
                        Exec("SET XACT_ABORT ON; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Materijalno:ledger',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Ledger busy',1;");
                        Check(Convert.ToInt32(Scalar("SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND parent_id IN (OBJECT_ID('dbo.mat'),OBJECT_ID('dbo.inv'),OBJECT_ID('dbo.tabela_materijala'),OBJECT_ID('dbo.sifarnik_materijal_skladiste_konto'))"))==0,"No unexpected update triggers on target tables");
                        var hashes=protectedTables.ToDictionary(t=>t,t=>ProtectedHash(t));var archive=new DataSet("BeforeIdentifierMigration");
                        foreach(string table in protectedTables)archive.Tables.Add(Rows("SELECT * FROM dbo."+Q(table),table));string archivePath=Path.Combine(work,"data_before.xml");if(!File.Exists(archivePath))archive.WriteXml(archivePath,XmlWriteMode.WriteSchema);
                        foreach(var d in keys.Concat(check).Concat(index))Exec(d.Drop);
                        foreach(string table in tables){bool nullable=Convert.ToBoolean(Scalar("SELECT is_nullable FROM sys.columns WHERE object_id=OBJECT_ID('dbo."+table+"') AND name='"+Column(table)+"'"));Exec("ALTER TABLE dbo."+Q(table)+" ALTER COLUMN "+Q(Column(table))+" varchar(9) "+(nullable?"NULL":"NOT NULL"));}
                        foreach(string table in tables){using(var c=Cmd("UPDATE dbo."+Q(table)+" SET "+Q(Column(table))+"=@code WHERE id=@id")){
                            c.Parameters.Add("@code",SqlDbType.VarChar,9);c.Parameters.Add("@id",SqlDbType.Int);
                            foreach(var row in plan.Where(p=>p.Table==table)){c.Parameters["@code"].Value=(object)row.Code??DBNull.Value;c.Parameters["@id"].Value=row.Id;Check(c.ExecuteNonQuery()==1,"Updated "+table+"/"+row.Id);}
                        }}
                        foreach(var d in index.Concat(check).Concat(keys))Exec(d.Create);
                        Exec("EXEC sys.sp_refreshview N'dbo.sifarnik_materijala';");
                        foreach(string table in tables){var actual=Rows("SELECT id,"+Q(Column(table))+" code FROM dbo."+Q(table)).AsEnumerable().ToDictionary(r=>Convert.ToInt32(r["id"]),r=>r.IsNull("code")?null:(string)r["code"]);Check(plan.Where(p=>p.Table==table).All(p=>actual[p.Id]==p.Code),"Every identifier verified in "+table);}
                        ApplicationChecks();
                        foreach(var h in hashes)Check(ProtectedHash(h.Key)==h.Value,"Every other field unchanged in "+h.Key);
                        Check(Convert.ToInt32(Scalar("SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1"))==0,"All foreign keys enabled and trusted");
                        if(mode=="apply")tx.Commit();else tx.Rollback();tx=null;
                        var result=new{Passed=true,Applied=mode=="apply",RehearsalRolledBack=mode!="apply",Database=db.Database,Rows=plan.Count,Checks=checks,LeadingZeroRows=plan.Count(p=>p.Code!=null&&p.Code.StartsWith("0")),ProtectedHashes=hashes,NewDatabaseCreated=false,OracleConnectionsUsed=false};
                        File.WriteAllText(Path.Combine(work,mode=="apply"?"migration_result.json":"migration_rehearsal.json"),new JavaScriptSerializer().Serialize(result));Console.WriteLine((mode=="apply"?"COMMITTED":"ROLLED BACK")+"; identifiers verified; protected fields unchanged; checks="+checks);
                    }catch{try{if(tx!=null)tx.Rollback();}catch{}throw;}
                }
            }
            return 0;
        }catch(Exception e){for(var x=e;x!=null;x=x.InnerException)Console.WriteLine(x.GetType().Name+": "+x.Message);return 1;}
    }
}
