using System;
using System.Data;
using System.Windows;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Materijalno.UI.Helpers;
using Microsoft.Reporting.WinForms;

namespace Materijalno.UI.Izvjestaji
{
    public partial class PregledTroskovaPoKontimaIzvjestaj : Window
    {
        private readonly DataTable reportDt;
        private bool _isReportViewerLoaded;

        public PregledTroskovaPoKontimaIzvjestaj(PregledTroskovaPoKontimaViewModel vm)
        {
            InitializeComponent();
            var filter = vm.CurrentItemMat;
            var from = filter.Datun ?? new DateTime(new MaterialPostingService().ActiveYear(), 1, 1);
            var to = filter.Datnar ?? DateTime.Today;
            reportDt = FoxCostReports.Create(from, to, false);
            _reportViewer.LocalReport.ReportPath = new PathHelper().MExecutableRootDirectory + @"\Izvjestaji\PregledTroskovaPoKontima.rdlc";
            _reportViewer.LocalReport.DataSources.Clear();
            _reportViewer.LocalReport.DataSources.Add(new ReportDataSource("DataSet1", reportDt));
            _reportViewer.Load += ReportViewer_Load;
        }

        private void ReportViewer_Load(object sender, EventArgs e)
        {
            if (_isReportViewerLoaded) return;
            _reportViewer.RefreshReport();
            _isReportViewerLoaded = true;
        }

        private void btnOdustani_Click(object sender, RoutedEventArgs e) { Close(); }
    }
}
