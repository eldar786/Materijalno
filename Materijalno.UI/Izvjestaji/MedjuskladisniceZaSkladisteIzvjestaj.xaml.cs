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
    public partial class MedjuskladisniceZaSkladisteIzvjestaj : Window
    {

        private MedjuskladisniceZaSkladisteViewModel _medjuskladisniceZaSkladistevm;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportMedjuskladisniceZaSkladiste _report;
        private List<Mat> _mat;
        private List<TabelaMaterijala> _tabelaMaterijala;
        private List<Materijalno.Model.EntityModels.SifarnikSkladista> _tabelaSkladista;
        private Mat ukupnavrijednost;
        private Mat ukupnoduguje;

        //decimal? totalVrijednost = 0;

        public ObservableCollection<Mat> MatList { get; set; }


        public MedjuskladisniceZaSkladisteIzvjestaj(MedjuskladisniceZaSkladisteViewModel medjuskladisniceZaSkladistevm)
        {
            InitializeComponent();
            _medjuskladisniceZaSkladistevm = medjuskladisniceZaSkladistevm;

            //ukupnavrijednost = new Mat();
            ukupnoduguje = new Mat();

            var dbContext = new materijalno_knjigovodstvoContext();

            MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Status == "M" // Filter by status 
                            && row.Kljnaz == medjuskladisniceZaSkladistevm.CurrentItemMat.Kljnaz // Skladište
                            && row.Datun >= medjuskladisniceZaSkladistevm.CurrentItemMat.Datun // Start date condition
                            && row.Datnar <= medjuskladisniceZaSkladistevm.CurrentItemMat.Datnar) // End date condition
                     .OrderBy(row => row.Datun) // Sort by start date
                     .ToList());



            _tabelaMaterijala = dbContext.TabelaMaterijala.ToList();

            _tabelaSkladista = dbContext.SifarnikSkladista.ToList();

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
            reportDt = new DataTable("MedjuskladisniceSve");

            reportDt.Columns.Add("Redbr").DataType = typeof(int);
            reportDt.Columns.Add("Kljnaz").DataType = typeof(int);
            reportDt.Columns.Add("Ident").DataType = typeof(int);
            reportDt.Columns.Add("Nazmat").DataType = typeof(string);
            reportDt.Columns.Add("Kolic").DataType = typeof(int);
            reportDt.Columns.Add("Nc").DataType = typeof(decimal);
            reportDt.Columns.Add("Vrijed").DataType = typeof(decimal);
            reportDt.Columns.Add("Brfak").DataType = typeof(string);
            reportDt.Columns.Add("Datun").DataType = typeof(DateTime);
            reportDt.Columns.Add("Brdok").DataType = typeof(string);
            //reportDt.Columns.Add("Totalvrijednost").DataType = typeof(decimal);
            reportDt.Columns.Add("Od").DataType = typeof(DateTime);
            reportDt.Columns.Add("Do").DataType = typeof(DateTime);
            reportDt.Columns.Add("NazivOrg").DataType = typeof(string);

            List<ReportMedjuskladisniceZaSkladiste> lista = new List<ReportMedjuskladisniceZaSkladiste>();

            foreach (Mat mat in _mat)
            {
                TabelaMaterijala tabmat = (from TabelaMaterijala tabmaterijala in _tabelaMaterijala
                                           where tabmaterijala.Ident == mat.Ident
                                           select tabmaterijala).FirstOrDefault();

                Materijalno.Model.EntityModels.SifarnikSkladista tabsklad = (from Materijalno.Model.EntityModels.SifarnikSkladista tabskladista in _tabelaSkladista
                                                                             where tabskladista.Kljnaz == mat.Kljnaz
                                                                             select tabskladista).FirstOrDefault();


                _report = new ReportMedjuskladisniceZaSkladiste();
                _report.Redbr = mat.Redbr;
                _report.Kljnaz = mat.Kljnaz;
                _report.Ident = mat.Ident;
                _report.NazMat = tabmat.Nazmat;
                _report.Kolic = mat.Kolic;
                _report.Nc = mat.Nc;
                _report.Vrijed = mat.Vrijed;
                _report.Brfak = mat.Brfak;
                _report.Datun = (DateTime)mat.Datun;
                _report.Brdok = mat.Brdok;
                //_report.TotalVrijednost = ukupnavrijednost.Vrijed;
                _report.Od = (DateTime)_medjuskladisniceZaSkladistevm.CurrentItemMat.Datun;
                _report.Do = _medjuskladisniceZaSkladistevm.CurrentItemMat.Datnar;
                _report.NazivOrg = tabsklad.NazivOrg;


                lista.Add(_report);

            }

            List<ReportMedjuskladisniceZaSkladiste> listaSort = lista.OrderBy(o => o.Datun).ToList();

            foreach (ReportMedjuskladisniceZaSkladiste report in listaSort)
            {
                DataRow dr = reportDt.NewRow();

                dr[0] = report.Redbr;
                dr[1] = report.Kljnaz;
                dr[2] = report.Ident;
                dr[3] = report.NazMat;
                dr[4] = report.Kolic;
                dr[5] = report.Nc;
                dr[6] = report.Vrijed;
                dr[7] = report.Brfak;
                dr[8] = report.Datun;
                dr[9] = report.Brdok;
                //dr[10] = report.TotalVrijednost;
                dr[10] = report.Od;
                dr[11] = report.Do;
                dr[12] = report.NazivOrg;



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
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\MedjuskladisniceZaSkladiste.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "MedjuskladisniceZaSkladiste.rdlc";
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