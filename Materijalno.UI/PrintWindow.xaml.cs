using System;
using System.IO;
using System.Windows;
using Materijalno.Model.Services;
using Microsoft.Reporting.WinForms;

namespace Materijalno.UI
{
    public partial class PrintWindow : Window
    {
        private bool loaded;
        public PrintWindow() { InitializeComponent(); }

        public PrintWindow(ReceiptPrintReport report) : this()
        {
            ConfigureReport(_reportViewer.LocalReport, report);
            _reportViewer.SetDisplayMode(DisplayMode.PrintLayout);
            _reportViewer.ZoomMode = ZoomMode.PageWidth;
            _reportViewer.Load += (s, e) => { if (!loaded) { _reportViewer.RefreshReport(); loaded = true; } };
            Closed += (s, e) => _reportViewer.Dispose();
        }

        public static void ConfigureReport(LocalReport target, ReceiptPrintReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            target.ReportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Izvjestaji", "UlazMaterijala.rdlc");
            target.DataSources.Clear();
            target.DataSources.Add(new ReportDataSource("DataSet1", report.Rows));
            target.SetParameters(new[] {
                new ReportParameter("Organization", report.Organization), new ReportParameter("Number", report.Number),
                new ReportParameter("DocumentNumber", report.DocumentNumber), new ReportParameter("DocumentDate", report.DocumentDate),
                new ReportParameter("SupplierCode", report.SupplierCode), new ReportParameter("SupplierName", report.SupplierName),
                new ReportParameter("Account", report.Account), new ReportParameter("AccountName", report.AccountName)
            });
        }

        private void Close_Click(object sender, RoutedEventArgs e) { Close(); }
    }
}
