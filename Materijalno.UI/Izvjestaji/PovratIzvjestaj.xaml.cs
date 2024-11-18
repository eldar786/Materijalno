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
    public partial class PovratIzvjestaj : Window
    {

        private PovratMaterijalaViewModel _povratavm;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportPovrat _report;
        private List<Mat> _mat;
        private List<TabelaMaterijala> _tabelaMaterijala;
        private List<Materijalno.Model.EntityModels.SifarnikSkladista> _tabelaSkladista;
        private Mat ukupnavrijednost;
        decimal? totalVrijednost = 0;

        public ObservableCollection<Mat> MatList { get; set; }


        public PovratIzvjestaj(PovratMaterijalaViewModel povratvm)
        {
            InitializeComponent();

            _povratavm = povratvm;
            ukupnavrijednost = new Mat();

            var dbContext = new materijalno_knjigovodstvoContext();
            

            MatList = new ObservableCollection<Mat>(dbContext.Mat
                     .Where(row => row.Brfak == povratvm.CurrentItemMat.Brfak)
                     .OrderBy(row => row.Datun)
                     .ToList());
            _tabelaMaterijala = dbContext.TabelaMaterijala.ToList();
            _tabelaSkladista = dbContext.SifarnikSkladista.ToList();


            _mat = MatList.ToList();
            

            foreach (var item in MatList)
            {
                totalVrijednost += item.Vrijed;
            }

            //ukupnavrijednost.Vrijed = totalVrijednost;

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
            reportDt = new DataTable("Povrat");

            reportDt.Columns.Add("Redbr").DataType = typeof(int);
            reportDt.Columns.Add("Kljnaz").DataType = typeof(int);
            reportDt.Columns.Add("Ident").DataType = typeof(int);
            reportDt.Columns.Add("Nazmat").DataType = typeof(string);
            reportDt.Columns.Add("Kolic").DataType = typeof(int);
            reportDt.Columns.Add("Nc").DataType = typeof(decimal);
            reportDt.Columns.Add("Vrijed").DataType = typeof(decimal);
            reportDt.Columns.Add("Brfak").DataType = typeof(string);
            reportDt.Columns.Add("Datun").DataType = typeof(string);
            reportDt.Columns.Add("Datnar").DataType = typeof(string);
            reportDt.Columns.Add("Brdok").DataType = typeof(string);
            reportDt.Columns.Add("Totalvrijednost").DataType = typeof(decimal);
            reportDt.Columns.Add("Konto1").DataType = typeof(int);
            reportDt.Columns.Add("NazivOrg").DataType = typeof(string);
            reportDt.Columns.Add("Kontosklad").DataType = typeof(int);
            


        List<ReportPovrat> lista = new List<ReportPovrat>();

            foreach (Mat mat in _mat)
            {
                TabelaMaterijala tabmat = (from TabelaMaterijala tabmaterijala in _tabelaMaterijala
                                           where tabmaterijala.Ident == mat.Ident
                                           select tabmaterijala).FirstOrDefault();

                Materijalno.Model.EntityModels.SifarnikSkladista tabsklad = (from Materijalno.Model.EntityModels.SifarnikSkladista tabskladista in _tabelaSkladista
                                                                             where tabskladista.Kljnaz == mat.Kljnaz
                                                                             select tabskladista).FirstOrDefault();




                _report = new ReportPovrat();
                _report.Redbr = mat.Redbr;
                _report.Kljnaz = mat.Kljnaz;
                _report.Ident = mat.Ident;
                _report.NazMat = tabmat.Nazmat;
                _report.Kolic = mat.Kolic;
                _report.Nc = mat.Nc;
                _report.Vrijed = mat.Vrijed;
                _report.Brfak = mat.Brfak;
                _report.Datun = mat.Datun;
                _report.Datnar = mat.Datnar;
                _report.Brdok = mat.Brdok;
                _report.TotalVrijednost = totalVrijednost;
                _report.Konto1 = mat.Konto1;
                _report.NazivOrg = tabsklad.NazivOrg;
                _report.Kontosklad = mat.Kontosklad;


                lista.Add(_report);

            }

            List<ReportPovrat> listaSort = lista.OrderBy(o => o.Datun).ToList();

            foreach (ReportPovrat report in listaSort)
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
                dr[9] = report.Datnar;
                dr[10] = report.Brdok;
                dr[11] = report.TotalVrijednost;
                dr[12] = report.Konto1;
                dr[13] = report.NazivOrg;
                dr[14] = report.Kontosklad;


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
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\ReportPovrat.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "ReportPovrat.rdlc";
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
