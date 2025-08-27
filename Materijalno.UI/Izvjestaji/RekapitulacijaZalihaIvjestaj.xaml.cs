using Microsoft.Reporting.WinForms;
using Materijalno.ViewModel;
using Materijalno.UI;
using Materijalno.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Globalization;
using Materijalno.Model.EntityModels;
using Materijalno.UI.Helpers;

namespace Materijalno.UI.Izvjestaji
{
    public partial class RekapitulacijaZalihaIvjestaj : Window
    {
        private RekapitulacijaZalihaViewModel _rekapitulacijaZalihaViewModel;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportRekapitulacijaZaliha _report;
        private List<Mat> _mat;
        private List<TabelaMaterijala> _tabelaMaterijala;
        private List<Materijalno.Model.EntityModels.SifarnikKonta> _tabelaKonto;
        private List<Materijalno.Model.EntityModels.SifarnikSkladista> _tabelaSkladista;
        private Mat ukupnavrijednost;
        private Mat ukupnoduguje;


        public ObservableCollection<Mat> MatList { get; set; }


        public RekapitulacijaZalihaIvjestaj(RekapitulacijaZalihaViewModel rekapitulacijaZalihaViewModel)
        {
            InitializeComponent();
            _rekapitulacijaZalihaViewModel = rekapitulacijaZalihaViewModel;

            ukupnoduguje = new Mat();

            var dbContext = new materijalno_knjigovodstvoContext();

            MatList = new ObservableCollection<Mat>(dbContext.Mat
                     .Where(row => row.Datnar <= rekapitulacijaZalihaViewModel.CurrentItemMat.Datnar && row.Ident != 0) 
                     .AsEnumerable() // Forces execution in-memory to enable GroupBy & OrderBy
                     .GroupBy(row => new { row.Kljnaz, row.Kontosklad }) // Group by BOTH Kljnaz and Kontosklad
                     .Select(grouped => new Mat
                     {
                         Kontosklad = grouped.Key.Kontosklad, // Kontosklad remains the same
                         Kljnaz = grouped.Key.Kljnaz, 
                         Datnar = rekapitulacijaZalihaViewModel.CurrentItemMat.Datnar,

                         Vrijed = grouped.Sum(x => x.Vrijed), // Sum up Vrijed
                     })
                     .ToList()); 



            _tabelaMaterijala = dbContext.TabelaMaterijala.ToList();
            _tabelaSkladista = dbContext.SifarnikSkladista.ToList();
            _tabelaKonto = dbContext.SifarnikKonta.ToList();

            _mat = MatList.ToList();
            decimal? totalVrijednost = 0;

            foreach (var item in MatList)
            {
                totalVrijednost += item.Vrijed;
            }

            //ukupnavrijednost.Vrijed = totalVrijednost;
            ukupnoduguje.Vrijed = totalVrijednost;

            try
            {
                NapuniPodatke();
                PripremiPrint();
                _reportViewer.Load += ReportViewer_Load;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void NapuniPodatke()
        {
            reportDt = new DataTable("RekapitulacijaZaliha");

            reportDt.Columns.Add("Vrijed").DataType = typeof(decimal);
            reportDt.Columns.Add("Datun").DataType = typeof(DateTime);
            reportDt.Columns.Add("Datnar").DataType = typeof(DateTime);
            reportDt.Columns.Add("NazivOrg").DataType = typeof(string);
            reportDt.Columns.Add("Kljnaz").DataType = typeof(int);
            reportDt.Columns.Add("Nazkont").DataType = typeof(string);
            reportDt.Columns.Add("Kontosklad").DataType = typeof(int);


            List<ReportRekapitulacijaZaliha> lista = new List<ReportRekapitulacijaZaliha>();

            foreach (Mat mat in _mat)
            {
                TabelaMaterijala tabmat = (from TabelaMaterijala tabmaterijala in _tabelaMaterijala
                                           where tabmaterijala.Ident == mat.Ident
                                           select tabmaterijala).FirstOrDefault();

                Materijalno.Model.EntityModels.SifarnikSkladista tabsklad = (from Materijalno.Model.EntityModels.SifarnikSkladista tabskladista in _tabelaSkladista
                                                                             where tabskladista.Kljnaz == mat.Kljnaz
                                                                             select tabskladista).FirstOrDefault();

                Materijalno.Model.EntityModels.SifarnikKonta tabkonto = (from Materijalno.Model.EntityModels.SifarnikKonta tabkonta in _tabelaKonto
                                                                         where tabkonta.Sifkonta == mat.Kontosklad
                                                                         select tabkonta).FirstOrDefault();


                _report = new ReportRekapitulacijaZaliha();

                _report.Vrijed = mat.Vrijed;
                _report.Datun = mat.Datun;
                _report.Datnar = mat.Datnar;
                _report.NazivOrg = tabsklad.NazivOrg;
                _report.Kljnaz = mat.Kljnaz;
                _report.Nazkont = tabkonto.Nazkont;
                _report.Kontosklad = mat.Kontosklad;

                lista.Add(_report);

            }

            List<ReportRekapitulacijaZaliha> listaSort = lista.OrderBy(o => o.Datun).ToList();

            foreach (ReportRekapitulacijaZaliha report in listaSort)
            {
                DataRow dr = reportDt.NewRow();

                dr[0] = report.Vrijed;
                dr[1] = report.Datun;
                dr[2] = report.Datnar;
                dr[3] = report.NazivOrg;
                dr[4] = report.Kljnaz;
                dr[5] = report.Nazkont;
                dr[6] = report.Kontosklad;


                reportDt.Rows.Add(dr);
            }
        }

        private void ReportViewer_Load(object sender, EventArgs e)
        {
            if (!_isReportViewerLoaded)
            {
                try
                {
                    PripremiPrint();

                    _reportViewer.RefreshReport();
                    _isReportViewerLoaded = true;

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void PripremiPrint()
        {
            ReportDataSource ds = new ReportDataSource("DataSet1", reportDt);
            PathHelper pathHelper = new PathHelper();
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\RekapitulacijaZaliha.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "RekapitulacijaZaliha.rdlc";
            }
            catch (Exception e)
            {
                throw new Exception("Ne postoje stavke sa ispis.");
            }
        }

        private void btnOdustani_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

