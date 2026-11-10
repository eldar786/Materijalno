using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Materijalno.Import;

public static class ArchiveProductionFox
{
    static string Q(string name) { return "[" + name.Replace("]", "]]") + "]"; }
    static string Hash(Stream stream) { using (var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
    static string FileHash(string file) { using(var stream=File.OpenRead(file)) return Hash(stream); }
    static void Execute(SqlConnection connection,string sql) { using(var cmd=new SqlCommand(sql,connection)){cmd.CommandTimeout=180;cmd.ExecuteNonQuery();} }
    static bool Exists(SqlConnection connection,string name) { using(var cmd=new SqlCommand("SELECT OBJECT_ID(@name,'U')",connection)){cmd.Parameters.AddWithValue("@name","fox_raw."+Q(name));return cmd.ExecuteScalar()!=DBNull.Value;} }
    static void Setting(SqlConnection connection,string key,string value) {
        using(var cmd=new SqlCommand("UPDATE dbo.app_settings SET value=@v WHERE key_name=@k; IF @@ROWCOUNT=0 INSERT dbo.app_settings(key_name,value) VALUES(@k,@v)",connection)){
            cmd.Parameters.AddWithValue("@k",key);cmd.Parameters.AddWithValue("@v",value);cmd.ExecuteNonQuery();
        }
    }
    public static int Main(string[] args)
    {
        try {
            string database=args[0],folder=Path.GetFullPath(args[1]),configuration=args[2],resultFile=args[3];
            if(!Regex.IsMatch(database,@"^materijalno_fox_[0-9]{8}_[0-9]{6}$"))throw new Exception("New production catalog required.");
            var config=XDocument.Load(configuration);
            var entry=config.Root.Element("connectionStrings").Elements("add").Single(e=>(string)e.Attribute("name")=="MaterijalnoDb");
            var builder=new SqlConnectionStringBuilder((string)entry.Attribute("connectionString")){InitialCatalog=database,ApplicationName="Materijalno_Production_Archive"};
            var tables=new List<object>();long compared=0,totalRows=0;int binaryFiles=0;long binaryBytes=0;
            using(var connection=new SqlConnection(builder.ConnectionString)){
                connection.Open();
                if(connection.Database!=database)throw new Exception("Wrong catalog.");
                Setting(connection,"AllSourceTablesArchived","false");
                var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(f=>Path.GetExtension(f).Equals(".dbf",StringComparison.OrdinalIgnoreCase)).OrderBy(f=>f,StringComparer.OrdinalIgnoreCase)){
                    string name=Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if(name.Length>50||!names.Add(name))throw new Exception("Ambiguous DBF table name: "+name);
                    var table=DbfReader.Read(file);bool exists=Exists(connection,name);
                    if(!exists){
                        var columns=new List<string>{"fox_recno int NOT NULL PRIMARY KEY","fox_deleted bit NOT NULL","fox_record_bytes varbinary(max) NOT NULL"};
                        foreach(var field in table.Fields)columns.Add(Q(field.Name)+" nvarchar("+(field.Type=='M'?"max":Math.Max(32,field.Length).ToString(CultureInfo.InvariantCulture))+") NOT NULL");
                        Execute(connection,"CREATE TABLE fox_raw."+Q(name)+" ("+string.Join(",",columns)+")");
                        using(var data=new DataTable()){
                            data.Columns.Add("fox_recno",typeof(int));data.Columns.Add("fox_deleted",typeof(bool));data.Columns.Add("fox_record_bytes",typeof(byte[]));
                            foreach(var field in table.Fields)data.Columns.Add(field.Name,typeof(string));
                            foreach(var record in table.Records){var row=data.NewRow();row[0]=record.RecordNumber;row[1]=record.Deleted;row[2]=record.RawBytes;foreach(var field in table.Fields)row[field.Name]=record.Values[field.Name];data.Rows.Add(row);}
                            using(var bulk=new SqlBulkCopy(connection)){bulk.DestinationTableName="fox_raw."+Q(name);bulk.BulkCopyTimeout=180;bulk.BatchSize=5000;foreach(DataColumn col in data.Columns)bulk.ColumnMappings.Add(col.ColumnName,col.ColumnName);bulk.WriteToServer(data);}
                        }
                        using(var cmd=new SqlCommand("INSERT dbo.fox_import_manifest(table_name,physical_rows,active_rows,source_hash) VALUES(@n,@p,@a,@h)",connection)){
                            cmd.Parameters.AddWithValue("@n",name);cmd.Parameters.AddWithValue("@p",table.Records.Count);cmd.Parameters.AddWithValue("@a",table.Records.Count(r=>!r.Deleted));cmd.Parameters.AddWithValue("@h",FileHash(file));cmd.ExecuteNonQuery();
                        }
                    }
                    string select="SELECT fox_recno,fox_deleted,"+string.Join(",",table.Fields.Select(f=>Q(f.Name)))+(!exists?",fox_record_bytes":"")+" FROM fox_raw."+Q(name)+" ORDER BY fox_recno";
                    using(var cmd=new SqlCommand(select,connection)){
                        cmd.CommandTimeout=180;
                        using(var reader=cmd.ExecuteReader()){
                            foreach(var record in table.Records){
                                if(!reader.Read()||reader.GetInt32(0)!=record.RecordNumber||reader.GetBoolean(1)!=record.Deleted)throw new Exception("Raw record/deletion differs: "+name+"/"+record.RecordNumber);
                                for(int i=0;i<table.Fields.Count;i++){
                                    compared++;
                                    if(!string.Equals(reader.GetString(i+2),record.Values[table.Fields[i].Name],StringComparison.Ordinal))throw new Exception("Raw field differs: "+name+"/"+record.RecordNumber+"/"+table.Fields[i].Name);
                                }
                                if(!exists&&!((byte[])reader[table.Fields.Count+2]).SequenceEqual(record.RawBytes))throw new Exception("Raw bytes differ: "+name);
                            }
                            if(reader.Read())throw new Exception("Unexpected extra raw record: "+name);
                        }
                    }
                    totalRows+=table.Records.Count;
                    tables.Add(new{Table=name,PhysicalRows=table.Records.Count,ActiveRows=table.Records.Count(r=>!r.Deleted),Fields=table.Fields.Count,FieldsCompared=(long)table.Records.Count*table.Fields.Count,SHA256=FileHash(file)});
                    Console.WriteLine("VERIFIED "+name+" rows="+table.Records.Count);
                }
                Execute(connection,"IF OBJECT_ID('dbo.fox_source_files','U') IS NULL CREATE TABLE dbo.fox_source_files(relative_path nvarchar(450) NOT NULL PRIMARY KEY,byte_count bigint NOT NULL,sha256 varchar(64) NOT NULL,content varbinary(max) NOT NULL);");
                var extensions=new HashSet<string>(new[]{".dbf",".fpt",".dbt",".cdx",".idx",".dbc",".dct",".dcx"},StringComparer.OrdinalIgnoreCase);
                foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(f=>extensions.Contains(Path.GetExtension(f)))){
                    string relative=file.Substring(folder.TrimEnd(Path.DirectorySeparatorChar).Length+1),hash=FileHash(file);byte[] bytes=File.ReadAllBytes(file);
                    using(var cmd=new SqlCommand("INSERT dbo.fox_source_files(relative_path,byte_count,sha256,content) VALUES(@p,@l,@h,@b)",connection)){
                        cmd.CommandTimeout=180;cmd.Parameters.AddWithValue("@p",relative);cmd.Parameters.AddWithValue("@l",(long)bytes.Length);cmd.Parameters.AddWithValue("@h",hash);cmd.Parameters.Add("@b",SqlDbType.VarBinary,-1).Value=bytes;cmd.ExecuteNonQuery();
                    }
                    using(var cmd=new SqlCommand("SELECT content FROM dbo.fox_source_files WHERE relative_path=@p",connection)){
                        cmd.Parameters.AddWithValue("@p",relative);
                        using(var reader=cmd.ExecuteReader(CommandBehavior.SequentialAccess)){if(!reader.Read())throw new Exception("Missing source file: "+relative);using(var stream=reader.GetStream(0)){if(Hash(stream)!=hash)throw new Exception("Source file bytes differ: "+relative);}}
                    }
                    binaryFiles++;binaryBytes+=bytes.LongLength;
                }
                Setting(connection,"AllSourceTablesArchived","true");Setting(connection,"DatabaseRole","FoxProduction");Setting(connection,"Source","Production Fox DBF import");Setting(connection,"SourceSnapshot",folder);
            }
            var result=new{Database=database,Passed=true,Tables=tables,TableCount=tables.Count,PhysicalRows=totalRows,FieldsCompared=compared,ExactSourceFiles=binaryFiles,ExactSourceBytes=binaryBytes,Failures=0};
            File.WriteAllText(resultFile,new JavaScriptSerializer().Serialize(result),new UTF8Encoding(false));
            Console.WriteLine("ALL VERIFIED tables="+tables.Count+" rows="+totalRows+" fields="+compared+" exact files="+binaryFiles);return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
    }
}
