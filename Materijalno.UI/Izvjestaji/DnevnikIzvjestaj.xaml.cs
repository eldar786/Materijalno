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
   
    public partial class DnevnikIzvjestaj : Window
    {
        private DnevnikViewModel _dnevnikvm;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportDnevnik _report;
        private List<Mat> _mat;
        private List<TabelaMaterijala> _tabelaMaterijala;
        private List<Materijalno.Model.EntityModels.SifarnikSkladista> _tabelaSkladista;
        private Mat ukupnavrijednost;
        private Mat ukupnoduguje;
        
        //decimal? totalVrijednost = 0;

        public ObservableCollection<Mat> MatList { get; set; }


        public DnevnikIzvjestaj(DnevnikViewModel dnevnikvm)
        {
            InitializeComponent();
            _dnevnikvm = dnevnikvm;
            
            //ukupnavrijednost = new Mat();
            ukupnoduguje = new Mat();

            var dbContext = new materijalno_knjigovodstvoContext();

            MatList = new ObservableCollection<Mat>(dbContext.Mat
                .Where(row => row.Ident == dnevnikvm.CurrentItemMat.Ident
                            && row.Kljnaz == dnevnikvm.CurrentItemMat.Kljnaz
                            && (row.Status == "M" || row.Status == "I")
                             && row.Datun >= dnevnikvm.CurrentItemMat.Datun &&
                             row.Datnar <= dnevnikvm.CurrentItemMat.Datnar)
                .OrderBy(row => row.Datun)
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
            reportDt = new DataTable("Dnevnik");

            reportDt.Columns.Add("Redbr").DataType = typeof(int);
            reportDt.Columns.Add("Ident").DataType = typeof(int);
            reportDt.Columns.Add("Nazmat").DataType = typeof(string);
            reportDt.Columns.Add("Kolic").DataType = typeof(int);
            reportDt.Columns.Add("Nc").DataType = typeof(decimal);
            reportDt.Columns.Add("Vrijed").DataType = typeof(decimal);
            reportDt.Columns.Add("Brfak").DataType = typeof(string);
            reportDt.Columns.Add("Datun").DataType = typeof(DateTime);
            reportDt.Columns.Add("NazivOrg").DataType = typeof(string);
            //reportDt.Columns.Add("Totalvrijednost").DataType = typeof(decimal);
            reportDt.Columns.Add("Status").DataType = typeof(string);
            reportDt.Columns.Add("Kljnaz").DataType = typeof(int);
            reportDt.Columns.Add("Datnar").DataType = typeof(DateTime);
            reportDt.Columns.Add("Od").DataType = typeof(DateTime);
            reportDt.Columns.Add("Do").DataType = typeof(DateTime);


            List<ReportDnevnik> lista = new List<ReportDnevnik>();

            foreach (Mat mat in _mat)
            {
                TabelaMaterijala tabmat = (from TabelaMaterijala tabmaterijala in _tabelaMaterijala
                                           where tabmaterijala.Ident == mat.Ident
                                           select tabmaterijala).FirstOrDefault();

                Materijalno.Model.EntityModels.SifarnikSkladista tabsklad = (from Materijalno.Model.EntityModels.SifarnikSkladista tabskladista in _tabelaSkladista
                                                                             where tabskladista.Kljnaz == mat.Kljnaz
                                                                             select tabskladista).FirstOrDefault();


                _report = new ReportDnevnik();
                _report.Redbr = mat.Redbr;
                _report.Ident = mat.Ident;
                _report.NazMat = tabmat.Nazmat;
                _report.Kolic = mat.Kolic;
                _report.Nc = mat.Nc;
                _report.Vrijed = mat.Vrijed;
                _report.Brfak = mat.Brfak;
                _report.Datun = (DateTime)mat.Datun;
                _report.NazivOrg = tabsklad.NazivOrg;
                //_report.TotalVrijednost = ukupnavrijednost.Vrijed;
                _report.Status = mat.Status;
                _report.Kljnaz = mat.Kljnaz;
                _report.Datnar = (DateTime)mat.Datnar;
                _report.Od = (DateTime)_dnevnikvm.CurrentItemMat.Datun;
                _report.Do = (DateTime)_dnevnikvm.CurrentItemMat.Datnar;


                lista.Add(_report);

            }

            List<ReportDnevnik> listaSort = lista.OrderBy(o => o.Datun).ToList();

            foreach (ReportDnevnik report in listaSort)
            {
                DataRow dr = reportDt.NewRow();

                dr[0] = report.Redbr;
                dr[1] = report.Ident;
                dr[2] = report.NazMat;
                dr[3] = report.Kolic;
                dr[4] = report.Nc;
                dr[5] = report.Vrijed;
                dr[6] = report.Brfak;
                dr[7] = report.Datun;
                dr[8] = report.NazivOrg;
                //dr[9] = report.TotalVrijednost;
                dr[9] = report.Status;
                dr[10] = report.Kljnaz;
                dr[11] = report.Datnar;
                dr[12] = report.Od;
                dr[13] = report.Do;



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
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\Dnevnik.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "Dnevnik.rdlc";
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
