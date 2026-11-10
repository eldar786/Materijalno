using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Materijalno.Import;

public static class InspectFoxRefresh
{
    public static int Main(string[] args)
    {
        try {
            string source=args[0],config=args[1],output=args[2];
            var xml=XDocument.Load(config);var cs=(string)xml.Root.Element("connectionStrings").Elements("add").Single(e=>(string)e.Attribute("name")=="MaterijalnoDb").Attribute("connectionString");
            var metadata=new DataSet("CurrentSchema");
            using(var db=new SqlConnection(cs)) {
                db.Open();
                foreach(var query in new[]{
                    "SELECT s.name schema_name,t.name table_name,c.name column_name,ty.name data_type,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id ORDER BY s.name,t.name,c.column_id",
                    "SELECT OBJECT_SCHEMA_NAME(parent_id) schema_name,OBJECT_NAME(parent_id) table_name,name,is_disabled FROM sys.triggers WHERE parent_class=1",
                    "SELECT name,OBJECT_SCHEMA_NAME(parent_object_id) schema_name,OBJECT_NAME(parent_object_id) table_name,OBJECT_NAME(referenced_object_id) referenced_table,is_disabled,is_not_trusted FROM sys.foreign_keys",
                    "SELECT * FROM dbo.app_settings",
                    "SELECT * FROM dbo.document_counter ORDER BY name,book_year",
                    "SELECT id,source_document_id,brfak,source_status,number,activity,book_year,state FROM dbo.financial_batch",
                    "SELECT COUNT(*) rows,SUM(CASE WHEN is_draft=0 THEN 1 ELSE 0 END) posted,SUM(CASE WHEN is_draft=0 THEN kolic ELSE 0 END) quantity,SUM(CASE WHEN is_draft=0 THEN vrijed ELSE 0 END) value,MAX(datun) last_date,SUM(CASE WHEN fox_recno IS NULL THEN 1 ELSE 0 END) wpf_rows FROM dbo.mat",
                    "SELECT COUNT(*) rows,SUM(dug1st) debit,SUM(pot1st) credit,SUM(CASE WHEN fox_recno IS NULL THEN 1 ELSE 0 END) wpf_rows FROM dbo.nalmat",
                    "SELECT name,seed_value,increment_value,last_value FROM sys.identity_columns WHERE OBJECT_NAME(object_id) IN ('mat','inv','nalmat')"
                }) {using(var c=new SqlCommand(query,db))using(var r=c.ExecuteReader()){var table=new DataTable("Table"+metadata.Tables.Count);table.Load(r);metadata.Tables.Add(table);}}
                metadata.WriteXml(Path.Combine(output,"sql_before_schema.xml"),XmlWriteMode.WriteSchema);
                foreach(var index in new[]{3,4,5,6,7}) {
                    var t=metadata.Tables[index];Console.WriteLine(t.TableName);Console.WriteLine(string.Join(" | ",t.Columns.Cast<DataColumn>().Select(c=>c.ColumnName)));
                    foreach(DataRow row in t.Rows)Console.WriteLine(string.Join(" | ",row.ItemArray.Select(v=>v is DateTime?((DateTime)v).ToString("yyyy-MM-dd"):Convert.ToString(v,CultureInfo.InvariantCulture))));
                }
                Console.WriteLine("Target SQL tables="+metadata.Tables[0].AsEnumerable().Select(r=>r["schema_name"]+"."+r["table_name"]).Distinct().Count());
            }
            foreach(string name in new[]{"mat","nalmat","inv","mater","kontosklad","datnaz","skontdat","brojnal","broju","broji","brojm"}) {
                var file=Directory.GetFiles(source).Single(f=>Path.GetFileName(f).Equals(name+".dbf",StringComparison.OrdinalIgnoreCase));var t=DbfReader.Read(file);var active=t.Records.Where(r=>!r.Deleted).ToList();
                Console.WriteLine("FOX "+name+" physical="+t.Records.Count+" active="+active.Count);
                if(name=="mat") {
                    var posted=active.Where(r=>Number(r.Values["ident"])>0&&Number(r.Values["kljnaz"])>0).ToList();
                    Console.WriteLine("FOX posted="+posted.Count+" quantity="+posted.Sum(r=>Number(r.Values["kolic"]))+" value="+posted.Sum(r=>Number(r.Values["vrijed"]))+" M191="+posted.Count(r=>r.Values["brfak"].Trim()=="M-191"));
                }
                if(name=="nalmat")Console.WriteLine("FOX journal D="+active.Sum(r=>Number(r.Values["dug1st"]))+" P="+active.Sum(r=>Number(r.Values["pot1st"]))+" M191="+active.Count(r=>r.Values["brfak"].Trim()=="M-191"));
                if(name.StartsWith("broj"))foreach(var r in active)Console.WriteLine("FOX counter "+name+" "+string.Join(" | ",r.Values.Select(k=>k.Key+"="+k.Value.Trim())));
            }
            return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
    }
    static decimal Number(string s){return string.IsNullOrWhiteSpace(s)?0:decimal.Parse(s.Trim(),CultureInfo.InvariantCulture);}
}
