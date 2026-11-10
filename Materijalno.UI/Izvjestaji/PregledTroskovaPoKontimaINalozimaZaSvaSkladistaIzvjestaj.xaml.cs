using System;
using System.Data;
using System.Windows;
using Materijalno.Model.Services;
using Materijalno.ViewModel;
using Materijalno.UI.Helpers;
using Microsoft.Reporting.WinForms;

namespace Materijalno.UI.Izvjestaji
{
    public partial class PregledTroskovaPoKontimaINalozimaZaSvaSkladistaIzvjestaj : Window
    {
        private readonly DataTable reportDt;
        private bool _isReportViewerLoaded;

        public PregledTroskovaPoKontimaINalozimaZaSvaSkladistaIzvjestaj(PregledTroskovaPoKontimaINalozimaZaSvaSkladistaViewModel vm)
        {
            InitializeComponent();
            var filter = vm.CurrentItemMat;
            var from = filter.Datun ?? new DateTime(new MaterialPostingService().ActiveYear(), 1, 1);
            var to = filter.Datnar ?? DateTime.Today;
            reportDt = FoxCostReports.Create(from, to, true);
            _reportViewer.LocalReport.ReportPath = new PathHelper().MExecutableRootDirectory + @"\Izvjestaji\PregledTroskovaPoKontimaINalozimaZaSvaSkladista.rdlc";
            _reportViewer.LocalReport.DataSources.Clear();
            _reportViewer.LocalReport.DataSources.Add(new ReportDataSource("DataSet1", reportDt));
            _reportViewer.LocalReport.SetParameters(new[] {
                new ReportParameter("DatumOd", from.ToString("dd.MM.yyyy")),
                new ReportParameter("DatumDo", to.ToString("dd.MM.yyyy")) });
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
