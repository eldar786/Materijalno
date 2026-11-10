using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Materijalno.Import;

// Replaces the Fox-derived data in an EXISTING catalog. No CREATE/ALTER/DROP,
// no Oracle client or connection, and every data change uses one transaction.
public static class RefreshExistingFoxData
{
    static SqlConnection db;
    static SqlTransaction tx;
    static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    static readonly Dictionary<string,DbfTable> fox=new Dictionary<string,DbfTable>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string,DataTable> data=new Dictionary<string,DataTable>(StringComparer.OrdinalIgnoreCase);
    static string work,source;
    static long fieldsChecked;
    static string Q(string s){return "["+s.Replace("]","]]")+"]";}
    static string Text(string s){return (s??"").Trim();}
    static decimal? Num(string s){return string.IsNullOrWhiteSpace(s)?(decimal?)null:decimal.Parse(s.Trim(),NumberStyles.Number,Inv);}
    static int? Code(string s){var n=Num(s);return n.HasValue?(int?)checked((int)n.Value):null;}
    static DateTime? Day(string s){return string.IsNullOrWhiteSpace(s)?(DateTime?)null:DateTime.ParseExact(s.Trim(),"yyyyMMdd",Inv);}
    static Guid Stable(string s){using(var h=SHA256.Create()){var a=h.ComputeHash(Encoding.UTF8.GetBytes(s));var b=new byte[16];Array.Copy(a,b,16);return new Guid(b);}}
    static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","");}
    static SqlCommand Command(string sql){return new SqlCommand(sql,db,tx){CommandTimeout=240};}
    static void Exec(string sql){using(var c=Command(sql))c.ExecuteNonQuery();}
    static object Scalar(string sql){using(var c=Command(sql))return c.ExecuteScalar();}
    static DataTable Query(string sql,string name){using(var c=Command(sql))using(var r=c.ExecuteReader()){var t=new DataTable(name);t.Load(r);return t;}}
    static DataTable Empty(string name)
    {
        using(var c=Command("SELECT TOP(0) * FROM "+name))using(var r=c.ExecuteReader()){
            var t=new DataTable(name);for(int i=0;i<r.FieldCount;i++)if(r.GetName(i)!="row_version")t.Columns.Add(r.GetName(i),r.GetFieldType(i));return t;
        }
    }
    static DataRow Add(DataTable t,params object[] pairs)
    {
        var r=t.NewRow();for(int i=0;i<pairs.Length;i+=2)r[(string)pairs[i]]=pairs[i+1]??DBNull.Value;t.Rows.Add(r);return r;
    }
    static void Set(DataRow row,string col,object value){row[col]=value??DBNull.Value;}
    static IEnumerable<DbfRecord> Active(string table){return fox[table].Records.Where(r=>!r.Deleted);}
    static string SchemaHash()
    {
        string q="SELECT s.name,t.name,c.column_id,c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed,ISNULL(dc.definition,'') def FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id ORDER BY s.name,t.name,c.column_id";
        var t=Query(q,"Schema");return Hash(Encoding.UTF8.GetBytes(string.Join("\n",t.AsEnumerable().Select(r=>string.Join("|",r.ItemArray.Select(v=>Convert.ToString(v,Inv)))))));
    }
    static void CheckSourceStable()
    {
        var manifest=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(source,"snapshot_manifest.json")));
        string live=(string)manifest["Source"];
        foreach(Dictionary<string,object> f in (System.Collections.IEnumerable)manifest["Files"]){
            string rel=(string)f["File"],expected=(string)f["SHA256"];
            if(Hash(File.ReadAllBytes(Path.Combine(source,rel)))!=expected)throw new Exception("Snapshot changed: "+rel);
            using(var stream=new FileStream(Path.Combine(live,rel),FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            using(var h=SHA256.Create())if(BitConverter.ToString(h.ComputeHash(stream)).Replace("-","")!=expected)throw new Exception("Fox source changed since snapshot: "+rel);
        }
    }
    static void BuildRaw()
    {
        var existing=Query("SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='fox_raw'","RawNames").AsEnumerable().Select(r=>(string)r[0]).ToList();
        foreach(string file in Directory.GetFiles(source,"*",SearchOption.AllDirectories).Where(p=>Path.GetExtension(p).Equals(".dbf",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase)){
            string name=Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            if(fox.ContainsKey(name)||!existing.Contains(name,StringComparer.OrdinalIgnoreCase))throw new Exception("Missing or ambiguous existing Fox raw table: "+name);
            var src=DbfReader.Read(file);fox.Add(name,src);
            string target="fox_raw."+Q(name);var t=Empty(target);
            foreach(var r in src.Records){
                var row=Add(t,"fox_recno",r.RecordNumber,"fox_deleted",r.Deleted);
                if(t.Columns.Contains("fox_record_bytes"))Set(row,"fox_record_bytes",r.RawBytes);
                foreach(var field in src.Fields){if(!t.Columns.Contains(field.Name))throw new Exception("Existing schema missing Fox field "+name+"."+field.Name);Set(row,field.Name,r.Values[field.Name]);}
            }
            data.Add(target,t);
            Console.WriteLine("MAPPED raw "+name+" physical="+t.Rows.Count+" active="+src.Records.Count(r=>!r.Deleted));
        }
        if(existing.Count!=fox.Count)throw new Exception("Raw table membership differs; no table schema changes are allowed.");
        var files=Empty("dbo.fox_source_files");
        foreach(string file in Directory.GetFiles(source,"*",SearchOption.AllDirectories).Where(p=>new[]{".dbf",".fpt",".dbt",".cdx",".idx",".dbc",".dct",".dcx"}.Contains(Path.GetExtension(p).ToLowerInvariant()))){
            var b=File.ReadAllBytes(file);Add(files,"relative_path",file.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length+1),"byte_count",(long)b.Length,"sha256",Hash(b),"content",b);
        }
        data.Add("dbo.fox_source_files",files);
        var imports=Empty("dbo.fox_import_manifest");
        foreach(var f in fox){string file=Directory.GetFiles(source,"*",SearchOption.AllDirectories).Single(p=>Path.GetFileName(p).Equals(f.Key+".dbf",StringComparison.OrdinalIgnoreCase));Add(imports,"table_name",f.Key,"physical_rows",f.Value.Records.Count,"active_rows",f.Value.Records.Count(r=>!r.Deleted),"source_hash",Hash(File.ReadAllBytes(file)),"imported_at",DateTime.UtcNow);}
        data.Add("dbo.fox_import_manifest",imports);
    }
    static void BuildMasters()
    {
        var materials=Empty("dbo.tabela_materijala");
        foreach(var r in Active("mater")){var v=r.Values;Add(materials,"Id",r.RecordNumber,"ident",Code(v["IDENT"]),"nazmat",Text(v["NAZMAT"]),"jedm",Text(v["JEDM"]),"siftar",Code(v["SIFTAR"]),"konto1",Code(v["KONTO1"]),"konto2",Text(v["KONTO2"]));}
        data.Add(materials.TableName,materials);
        var warehouses=Empty("dbo.sifarnik_skladista");
        foreach(var r in Active("datnaz")){var v=r.Values;Add(warehouses,"id",r.RecordNumber,"kljnaz",Code(v["KLJNAZ"]),"naziv_org",Text(v["NAZIVORG"]),"ziro_racun",Text(v["ZIRORCUN"]),"poz_br",Text(v["POZBR"]),"devizni_racun",Text(v["DEVRAC"]),"poz_br_d",Text(v["POZBRD"]),"opstina",Text(v["OPSTINA"]),"naziv_sdk",Text(v["NAZIVSDK"]),"mjesto_adresa",Text(v["MJEADR"]));}
        data.Add(warehouses.TableName,warehouses);
        var accounts=Empty("dbo.sifarnik_konta");
        foreach(var r in Active("skontdat")){var v=r.Values;Add(accounts,"id",r.RecordNumber,"sifkonta",Code(v["SIFKONTA"]),"nazkont",Text(v["NAZKONT"]),"stakont",Code(v["STAKONT"]),"stamt",Code(v["STAMT"]));}
        data.Add(accounts.TableName,accounts);
        var links=Empty("dbo.sifarnik_materijal_skladiste_konto");var seen=new Dictionary<string,int?>();
        foreach(var r in Active("kontosklad")){var v=r.Values;string key=Code(v["SIFMAT"])+"|"+Code(v["SIFSKLA"]);int? account=Code(v["SIFKONTA"]);
            if(seen.ContainsKey(key)){if(seen[key]!=account)throw new Exception("Ambiguous material/warehouse account: "+key);continue;}
            seen.Add(key,account);Add(links,"Id",r.RecordNumber,"Sifmat",Code(v["SIFMAT"]),"Sifskla",Code(v["SIFSKLA"]),"Sifkonta",account);
        }
        data.Add(links.TableName,links);
    }
    static void CopyFields(DataRow row,DbfRecord r,string table)
    {
        string[] integers={"kljnaz","kljnaz1","ident","konto1","kontosklad","kontosklad1","ourst","mjtst","redbr","rostav","sifakt","bronsta","sintstav","kukistav"};
        string[] decimals={"kolic","nc","vrijed","troskovi","porppp","trospe","kurs","dug1st","pot1st","stodug1","stopot1","devdugst","devpotst","stodevd","stodevp"};
        foreach(var field in fox[table].Fields){
            string col=field.Name.Equals("KONTOSKLA2",StringComparison.OrdinalIgnoreCase)?"kontosklad1":field.Name.ToLowerInvariant();
            if(!row.Table.Columns.Contains(col))throw new Exception("Unknown operational Fox field: "+table+"."+col);
            string val=r.Values[field.Name];object mapped;
            if(row.Table.Columns[col].DataType==typeof(DateTime))mapped=Day(val);
            else if(integers.Contains(col)){int? n=Code(val);mapped=(table=="nalmat"&&new[]{"rostav","sifakt","bronsta","sintstav","kukistav"}.Contains(col))?(object)(n??0):n;}
            else if(decimals.Contains(col))mapped=Num(val)??0;
            else mapped=Text(val);
            Set(row,col,mapped);
        }
    }
    static int BuildLedger()
    {
        int year=Active("mat").Select(r=>Day(r.Values["DATUN"])?.Year??0).Max();
        if(year<2000)throw new Exception("No valid active book year.");
        var docs=Empty("dbo.material_document");var docKeys=new HashSet<Guid>();var mat=Empty("dbo.mat");
        var pairs=new Dictionary<string,List<DataRow>>();
        foreach(var r in Active("mat")){
            var v=r.Values;bool posted=Code(v["IDENT"])>0&&Code(v["KLJNAZ"])>0;DateTime? date=Day(v["DATUN"]);int bookYear=date?.Year??year;string status=Text(v["STATUS"]),number=Text(v["BRFAK"]);
            var row=Add(mat,"id",r.RecordNumber,"is_draft",!posted,"is_archived",false,"book_year",bookYear,"line_token",Stable("mat:"+r.RecordNumber),"fox_recno",r.RecordNumber);
            CopyFields(row,r,"mat");
            if(!posted){foreach(string col in new[]{"ident","kljnaz","kljnaz1"})if(!row.IsNull(col)&&Convert.ToInt32(row[col])<=0)row[col]=DBNull.Value;continue;}
            int warehouse=status=="M"?0:Code(v["KLJNAZ"]).Value;Guid doc=Stable(status+"|"+bookYear+"|"+number+"|"+warehouse);Set(row,"document_id",doc);
            if(docKeys.Add(doc))Add(docs,"id",doc,"status",status,"book_year",bookYear,"number",number,"warehouse",status=="M"&&(Num(v["KOLIC"])??0)>0?Code(v["KLJNAZ1"]):Code(v["KLJNAZ"]),"created_at",DateTime.UtcNow);
            if(status=="M"){
                var wh=new[]{Code(v["KLJNAZ"]).Value,Code(v["KLJNAZ1"]).Value}.OrderBy(n=>n).ToArray();
                string key=number+"|"+Text(v["IDENT"])+"|"+Text(v["DATUN"])+"|"+wh[0]+":"+wh[1]+"|"+Math.Abs(Num(v["KOLIC"])??0).ToString(Inv)+"|"+Math.Abs(Num(v["VRIJED"])??0).ToString(Inv);
                if(!pairs.ContainsKey(key))pairs.Add(key,new List<DataRow>());pairs[key].Add(row);
            }
        }
        foreach(var pair in pairs){
            var minus=pair.Value.Where(r=>(decimal)r["kolic"]<0).OrderBy(r=>(int)r["id"]).ToList();
            var plus=pair.Value.Where(r=>(decimal)r["kolic"]>0).OrderBy(r=>(int)r["id"]).ToList();
            if(minus.Count==0||minus.Count!=plus.Count||minus.Count+plus.Count!=pair.Value.Count)throw new Exception("Unbalanced Fox transfer group "+pair.Key);
            for(int i=0;i<minus.Count;i++){
                if((decimal)minus[i]["vrijed"]+(decimal)plus[i]["vrijed"]!=0||(int)minus[i]["kljnaz"]!=(int)plus[i]["kljnaz1"]||(int)minus[i]["kljnaz1"]!=(int)plus[i]["kljnaz"])throw new Exception("Invalid transfer pair "+pair.Key);
                Guid id=Stable(pair.Key+(minus.Count==1?"":"|pair:"+i));minus[i]["transfer_pair_id"]=id;plus[i]["transfer_pair_id"]=id;
            }
        }
        data.Add(docs.TableName,docs);data.Add(mat.TableName,mat);
        var inventory=Empty("dbo.inv");
        foreach(var r in Active("inv")){
            bool draft=Code(r.Values["IDENT"])<=0||!Code(r.Values["IDENT"]).HasValue;
            var row=Add(inventory,"id",r.RecordNumber,"is_draft",draft,"is_archived",false,"inventory_date",Day(r.Values["DATUN"]),"fox_recno",r.RecordNumber);CopyFields(row,r,"inv");
            if(draft)foreach(string col in new[]{"ident","kljnaz"})if(!row.IsNull(col)&&Convert.ToInt32(row[col])<=0)row[col]=DBNull.Value;
            if(!draft)row["inventory_session_id"]=Stable("inv:"+Text(r.Values["KLJNAZ"])+"|"+Text(r.Values["DATUN"]));
        }
        data.Add(inventory.TableName,inventory);
        var journals=Empty("dbo.nalmat");foreach(var r in Active("nalmat")){var row=Add(journals,"id",r.RecordNumber,"fox_recno",r.RecordNumber);CopyFields(row,r,"nalmat");}data.Add(journals.TableName,journals);
        return year;
    }
    static void BuildCountersAndSettings(int year)
    {
        var counter=Empty("dbo.document_counter");var numbers=new Dictionary<string,int>();
        Action<string,int,int> set=(name,y,value)=>{string key=name+"|"+y;if(!numbers.ContainsKey(key)||numbers[key]<value)numbers[key]=value;};
        foreach(var map in new[]{new[]{"broju","PROMET_U"},new[]{"broji","PROMET_IV"},new[]{"brojm","PROMET_M"}})
            foreach(var r in Active(map[0])){int? y=Code(r.Values["GODBRNA"]),n=Code(r.Values["BRNAL"]);if(!y.HasValue||!n.HasValue)continue;set(map[1],y.Value<100?y.Value+2000:y.Value,n.Value);}
        foreach(var r in Active("brojnal")){int? y=Code(r.Values["GODBRNA"]),n=Code(r.Values["BRNAL"]),a=Code(r.Values["AKTBRNA"]);if(!y.HasValue||!n.HasValue||!a.HasValue)continue;set("FIN_"+a,y.Value<100?y.Value+2000:y.Value,n.Value);}
        foreach(var r in Active("nalmat")){DateTime? d=Day(r.Values["DATNSTA"]);int? a=Code(r.Values["SIFAKT"]),n=Code(r.Values["BRONSTA"]);if(d.HasValue&&a.HasValue&&n.HasValue)set("FIN_"+a,d.Value.Year,n.Value);}
        foreach(var item in numbers){var a=item.Key.Split('|');Add(counter,"name",a[0],"book_year",int.Parse(a[1],Inv),"last_number",item.Value);}data.Add(counter.TableName,counter);
        var settings=Empty("dbo.app_settings");
        foreach(var s in new[]{new[]{"Source","Production Fox DBF import"},new[]{"ActiveBookYear",year.ToString(Inv)},new[]{"ImportComplete","true"},new[]{"DatabaseRole","FoxProduction"},new[]{"AllSourceTablesArchived","true"},new[]{"SourceSnapshot",source}})Add(settings,"key_name",s[0],"value",s[1]);
        data.Add(settings.TableName,settings);
    }
    static void Bulk(DataTable table)
    {
        if(table.Rows.Count==0)return;
        using(var b=new SqlBulkCopy(db,SqlBulkCopyOptions.KeepIdentity|SqlBulkCopyOptions.CheckConstraints|SqlBulkCopyOptions.KeepNulls|SqlBulkCopyOptions.TableLock,tx)){
            b.DestinationTableName=table.TableName;b.BulkCopyTimeout=240;b.BatchSize=5000;
            foreach(DataColumn c in table.Columns)b.ColumnMappings.Add(c.ColumnName,c.ColumnName);b.WriteToServer(table);
        }
    }
    static void Verify(DataTable expected)
    {
        string key=expected.Columns.Contains("fox_recno")&&expected.TableName.StartsWith("fox_raw.")?"fox_recno":expected.Columns.Contains("id")?"id":expected.Columns.Contains("relative_path")?"relative_path":expected.Columns.Contains("table_name")?"table_name":expected.Columns.Contains("key_name")?"key_name":"name";
        string order=Q(key)+(expected.TableName=="dbo.document_counter"?",book_year":"");
        var actual=Query("SELECT "+string.Join(",",expected.Columns.Cast<DataColumn>().Select(c=>Q(c.ColumnName)))+" FROM "+expected.TableName+" ORDER BY "+order,"Verify");
        var wanted=expected.AsEnumerable().OrderBy(r=>Convert.ToString(r[key],Inv),StringComparer.Ordinal).ToList();
        Func<DataRow,string> rowKey=r=>Convert.ToString(r[key],Inv)+(expected.TableName=="dbo.document_counter"?"|"+r["book_year"]:"");
        var lookup=wanted.ToDictionary(rowKey,StringComparer.Ordinal);
        if(actual.Rows.Count!=expected.Rows.Count)throw new Exception("Row count differs: "+expected.TableName);
        foreach(DataRow a in actual.Rows){DataRow e;if(!lookup.TryGetValue(rowKey(a),out e))throw new Exception("Unexpected row: "+expected.TableName);
            foreach(DataColumn col in expected.Columns){object x=e[col.ColumnName],y=a[col.ColumnName];fieldsChecked++;
                bool same=x is byte[]&&y is byte[]?((byte[])x).SequenceEqual((byte[])y):Equals(x,y);
                if(!same&&x is DateTime&&y is DateTime)same=Math.Abs((((DateTime)x)-((DateTime)y)).TotalMilliseconds)<4;
                if(!same)throw new Exception("Field differs: "+expected.TableName+" key="+rowKey(a)+" column="+col.ColumnName);
            }
        }
        Console.WriteLine("VERIFIED "+expected.TableName+" rows="+actual.Rows.Count);
    }
    static void SaveOriginalApplicationData()
    {
        var old=new DataSet("SQLBeforeRefresh");
        foreach(string table in new[]{"financial_batch","financial_export_log","material_document","document_counter","app_settings","year_close"})old.Tables.Add(Query("SELECT * FROM dbo."+Q(table),table));
        old.Tables.Add(Query("SELECT * FROM dbo.mat WHERE fox_recno IS NULL","wpf_mat"));old.Tables.Add(Query("SELECT * FROM dbo.nalmat WHERE fox_recno IS NULL","wpf_nalmat"));
        string file=Path.Combine(work,"application_state_before.xml");if(!File.Exists(file))old.WriteXml(file,XmlWriteMode.WriteSchema);
    }
    public static int Main(string[] args)
    {
        try{
            source=Path.GetFullPath(args[0]);work=Path.GetFullPath(args[2]);bool apply=args.Length>3&&args[3]=="apply-existing-Fox-refresh";
            var cfg=XDocument.Load(args[1]);var entry=cfg.Root.Element("connectionStrings").Elements("add").Single(e=>(string)e.Attribute("name")=="MaterijalnoDb");var cs=new SqlConnectionStringBuilder((string)entry.Attribute("connectionString"));
            if(cs.InitialCatalog!="materijalno_fox_20261105_102038")throw new Exception("Only the explicitly selected existing catalog is allowed.");
            var backup=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(work,"backup_before.json")));
            if(!(bool)backup["Verified"]||(string)backup["Database"]!=cs.InitialCatalog)throw new Exception("Verified backup required before even the transaction rehearsal.");
            CheckSourceStable();cs.ApplicationName="Materijalno_Existing_Fox_Data_Refresh";
            using(db=new SqlConnection(cs.ConnectionString)){
                db.Open();string originalSchema=SchemaHash();BuildRaw();BuildMasters();int year=BuildLedger();BuildCountersAndSettings(year);SaveOriginalApplicationData();
                var plan=new{Database=db.Database,Source=source,ExistingTablesOnly=true,RawTables=fox.Count,SourceFiles=data["dbo.fox_source_files"].Rows.Count,ActiveYear=year,Mat=data["dbo.mat"].Rows.Count,Posted=data["dbo.mat"].AsEnumerable().Count(r=>!(bool)r["is_draft"]),Nalmat=data["dbo.nalmat"].Rows.Count,OracleConnectionsUsed=false,FinancialMetadataArchivedAndReset=true};
                File.WriteAllText(Path.Combine(work,"refresh_plan.json"),new JavaScriptSerializer().Serialize(plan),new UTF8Encoding(false));
                using(tx=db.BeginTransaction(IsolationLevel.Serializable)){
                    try{
                        Exec("SET XACT_ABORT ON; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Materijalno:ledger',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Ledger is busy',1;");
                        // Child-first deletes; constraints stay enabled throughout.
                        foreach(string table in new[]{"financial_export_log","nalmat","financial_batch","mat","inv","material_document","year_close","sifarnik_materijal_skladiste_konto","tabela_materijala","sifarnik_skladista","sifarnik_konta","document_counter","app_settings","fox_import_manifest","fox_source_files"})Exec("DELETE FROM dbo."+Q(table));
                        foreach(var item in data.Where(p=>p.Key.StartsWith("fox_raw."))){Exec("DELETE FROM "+item.Key);Bulk(item.Value);}
                        foreach(string table in new[]{"tabela_materijala","sifarnik_skladista","sifarnik_konta","sifarnik_materijal_skladiste_konto","material_document","mat","inv","nalmat","document_counter","app_settings","fox_import_manifest","fox_source_files"})Bulk(data["dbo."+table]);
                        foreach(var item in data)Verify(item.Value);
                        if(Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.financial_batch"))!=0||Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.financial_export_log"))!=0)throw new Exception("Old application test metadata remains.");
                        if(Convert.ToInt32(Scalar("SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1"))!=0)throw new Exception("Untrusted foreign key.");
                        foreach(var table in new[]{"mat","inv","nalmat"})Exec("DBCC CHECKIDENT ('dbo."+table+"',RESEED,"+fox[table].Records.Count+") WITH NO_INFOMSGS");
                        foreach(var map in new[]{new[]{"tabela_materijala","mater"},new[]{"sifarnik_skladista","datnaz"},new[]{"sifarnik_konta","skontdat"},new[]{"sifarnik_materijal_skladiste_konto","kontosklad"}})Exec("DBCC CHECKIDENT ('dbo."+map[0]+"',RESEED,"+fox[map[1]].Records.Count+") WITH NO_INFOMSGS");
                        if(SchemaHash()!=originalSchema)throw new Exception("Schema unexpectedly changed.");
                        CheckSourceStable();
                        if(apply)tx.Commit();else tx.Rollback();tx=null;
                        var result=new{Database=db.Database,Applied=apply,RehearsalRolledBack=!apply,Passed=true,RawTables=fox.Count,FieldsChecked=fieldsChecked,SchemaUnchanged=true,SourceUnchanged=true,OracleConnectionsUsed=false,Plan=plan};
                        File.WriteAllText(Path.Combine(work,apply?"refresh_result.json":"rehearsal_result.json"),new JavaScriptSerializer().Serialize(result),new UTF8Encoding(false));
                        Console.WriteLine((apply?"COMMITTED":"REHEARSAL ROLLED BACK")+"; tables="+data.Count+"; fields checked="+fieldsChecked+"; existing schema unchanged; Oracle unused.");
                    }catch{try{if(tx!=null)tx.Rollback();}catch{}throw;}
                }
            }
            return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
    }
}
