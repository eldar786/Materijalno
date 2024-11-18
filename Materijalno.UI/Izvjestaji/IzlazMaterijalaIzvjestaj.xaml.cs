using Materijalno.Model.EntityModels;
using Materijalno.UI.Helpers;
using Materijalno.ViewModel;
using Microsoft.Reporting.WinForms;
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

namespace Materijalno.UI.Izvjestaji
{
    /// <summary>
    /// Interaction logic for IzlazMaterijalaIzvjestaj.xaml
    /// </summary>
    public partial class IzlazMaterijalaIzvjestaj : Window
    {
        private IzlazMaterijalaViewModel _izlazMaterijalavm;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportIzlazMaterijala _report;
        private List<Mat> _mat;
        private List<TabelaMaterijala> _tabelaMaterijala;
        public ObservableCollection<Mat> MatList { get; set; }

        public IzlazMaterijalaIzvjestaj(IzlazMaterijalaViewModel izlazMaterijalaViewModel)
        {
            InitializeComponent();

            _izlazMaterijalavm = izlazMaterijalaViewModel;

            var dbContext = new materijalno_knjigovodstvoContext();
            //MatList = dbContext.Mat.ToList();
            MatList = new ObservableCollection<Mat>(dbContext.Mat
                     .Where(row => row.Brfak == _izlazMaterijalavm.CurrentItemMat.Brfak)
                     .OrderBy(row => row.Datun)
                     .ToList());

            _tabelaMaterijala = dbContext.TabelaMaterijala.ToList();

            _mat = MatList.ToList();

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
            reportDt = new DataTable("IzlazMaterijala");
            reportDt.Columns.Add("Redbr").DataType = typeof(int);
            reportDt.Columns.Add("Kljnaz").DataType = typeof(int);
            reportDt.Columns.Add("Ident").DataType = typeof(int);
            reportDt.Columns.Add("Nazmat").DataType = typeof(string);
            reportDt.Columns.Add("Kolic").DataType = typeof(int);
            reportDt.Columns.Add("Nc").DataType = typeof(decimal);
            reportDt.Columns.Add("Vrijed").DataType = typeof(decimal);
            reportDt.Columns.Add("Brfak").DataType = typeof(string);
            reportDt.Columns.Add("Datnar").DataType = typeof(string);
            reportDt.Columns.Add("Datun").DataType = typeof(string);
            reportDt.Columns.Add("Brdok").DataType = typeof(string);

            // Dodati CurrentItemTabSkladista.NazivOrg

            List<ReportIzlazMaterijala> lista = new List<ReportIzlazMaterijala>();

            foreach (Mat mat in _mat)
            {
                TabelaMaterijala tabmat = (from TabelaMaterijala tabmaterijala in _tabelaMaterijala
                                           where tabmaterijala.Ident == mat.Ident
                                           select tabmaterijala).FirstOrDefault();
                _report = new ReportIzlazMaterijala();
                _report.Redbr = mat.Redbr;
                _report.Kljnaz = mat.Kljnaz;
                _report.Ident = mat.Ident;
                _report.NazMat = tabmat.Nazmat;
                _report.Kolic = mat.Kolic;
                _report.Nc = mat.Nc;
                _report.Vrijed = mat.Vrijed;
                _report.Brfak = mat.Brfak;
                _report.Datnar = mat.Datnar;
                _report.Datun = mat.Datun;
                _report.Brdok = mat.Brdok;
                lista.Add(_report);
            }
            List<ReportIzlazMaterijala> listaSort = lista.OrderBy(o => o.Datun).ToList();
            foreach (ReportIzlazMaterijala report in listaSort)
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
                dr[8] = report.Datnar;
                dr[9] = report.Datun;
                dr[10] = report.Brdok;
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
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\IzlazMaterijala.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "IzlazMaterijala.rdlc";
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
