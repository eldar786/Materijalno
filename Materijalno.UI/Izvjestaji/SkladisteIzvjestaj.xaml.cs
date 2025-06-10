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
    public partial class SkladisteIzvjestaj : Window
    {
        private SifarnikSkladistaViewModel _skladistevm;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportSkladiste _report;
        private List<Materijalno.Model.EntityModels.SifarnikSkladista> _sifarnikSklad;
        

        public ObservableCollection<Materijalno.Model.EntityModels.SifarnikSkladista> SkaldistaList { get; set; }
        
        public SkladisteIzvjestaj(SifarnikSkladistaViewModel skladistevm)
        {
            InitializeComponent();

            _skladistevm = skladistevm;

            if (_skladistevm.CurrentItemSklad == null)
            {
                MessageBox.Show("Niste odabrali skladište za izvještaj.", "Greška", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dbContext = new materijalno_knjigovodstvoContext();

            SkaldistaList = new ObservableCollection<Materijalno.Model.EntityModels.SifarnikSkladista>(dbContext.SifarnikSkladista
                    .Where(row => row.Kljnaz == skladistevm.CurrentItemSklad.Kljnaz)
                    .ToList());

            _sifarnikSklad = SkaldistaList.ToList();

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
            reportDt = new DataTable("Skladista");

            reportDt.Columns.Add("Kljnaz").DataType = typeof(int);
            reportDt.Columns.Add("NazivOrg").DataType = typeof(string);
            reportDt.Columns.Add("Opstina").DataType = typeof(string);
            reportDt.Columns.Add("MjestoAdresa").DataType = typeof(string);


            List<ReportSkladiste> lista = new List<ReportSkladiste>();

            foreach (Materijalno.Model.EntityModels.SifarnikSkladista sifsklad in _sifarnikSklad)
            {
                Materijalno.Model.EntityModels.SifarnikSkladista tabsklad = (from Materijalno.Model.EntityModels.SifarnikSkladista tabskladista in _sifarnikSklad
                                                                             where tabskladista.Kljnaz == sifsklad.Kljnaz
                                                                             select tabskladista).FirstOrDefault();



                _report = new ReportSkladiste();
                _report.Kljnaz = tabsklad.Kljnaz;
                _report.NazivOrg = tabsklad.NazivOrg;
                _report.Opstina = tabsklad.Opstina;
                _report.MjestoAdresa = tabsklad.MjestoAdresa;
                
                lista.Add(_report);

            }

            List<ReportSkladiste> listaSort = lista.OrderBy(o => o.Kljnaz).ToList();

            foreach (ReportSkladiste report in listaSort)
            {
                DataRow dr = reportDt.NewRow();

               
                dr[0] = report.Kljnaz;
                dr[1] = report.NazivOrg;
                dr[2] = report.Opstina;
                dr[3] = report.MjestoAdresa;
                
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
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\ReportSkladiste.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "ReportSkladiste.rdlc";
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