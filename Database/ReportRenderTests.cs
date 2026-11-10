using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Reporting.WinForms;

public static class ReportRenderTests
{
    [STAThread]
    public static int Main(string[] args)
    {
        int passed=0, failed=0;Directory.CreateDirectory(args[1]);
        foreach(string file in Directory.GetFiles(args[0],"*.rdlc"))
        {
            try {
                var xml=XDocument.Load(file);XNamespace ns=xml.Root.Name.Namespace;
                XNamespace rd="http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";
                using(var report=new LocalReport()) {
                    report.ReportPath=file;
                    foreach(var dataset in xml.Descendants(ns+"DataSet")) {
                        var table=new DataTable();
                        foreach(var field in dataset.Element(ns+"Fields").Elements(ns+"Field")) {
                            string type=(string)field.Element(rd+"TypeName")??"System.String";
                            var fieldType=Type.GetType(type)??typeof(string);
                            table.Columns.Add((string)field.Attribute("Name"),Nullable.GetUnderlyingType(fieldType)??fieldType);
                        }
                        for(int index=0;index<2;index++) {
                            var row=table.NewRow();foreach(DataColumn col in table.Columns) {
                                if(col.DataType==typeof(string)) row[col]=col.ColumnName=="Status"?(index==0?"U":"V"):col.ColumnName=="Brfak"?"61-TEST":"TEST ČĆŽŠĐ";
                                else if(col.DataType==typeof(DateTime)) row[col]=new DateTime(2026,11,4);
                                else if(col.DataType==typeof(bool)) row[col]=false;
                                else row[col]=Convert.ChangeType(index==0?10.50m:2.25m,col.DataType);
                            }table.Rows.Add(row);
                        }
                        report.DataSources.Add(new ReportDataSource((string)dataset.Attribute("Name"),table));
                    }
                    var parameters=xml.Descendants(ns+"ReportParameter").Select(p=>new ReportParameter((string)p.Attribute("Name"),(string)p.Element(ns+"DataType")=="DateTime"?"2026-11-04":"TEST")).ToArray();
                    if(parameters.Length>0)report.SetParameters(parameters);
                    string mime,encoding,extension;string[] streams;Warning[] warnings;
                    byte[] pdf=report.Render("PDF",null,out mime,out encoding,out extension,out streams,out warnings);
                    if(pdf.Length<1000)throw new Exception("Empty PDF");
                    File.WriteAllBytes(Path.Combine(args[1],Path.GetFileNameWithoutExtension(file)+".pdf"),pdf);
                    passed++;Console.WriteLine("PASS "+Path.GetFileName(file)+" PDF "+pdf.Length+" bytes");
                }
            }catch(Exception e){failed++;Console.WriteLine("FAIL "+Path.GetFileName(file));for(var error=e;error!=null;error=error.InnerException)Console.WriteLine(error.Message);}
        }
        Console.WriteLine("REPORTS "+passed+" passed, "+failed+" failed");return failed==0?0:1;
    }
}
