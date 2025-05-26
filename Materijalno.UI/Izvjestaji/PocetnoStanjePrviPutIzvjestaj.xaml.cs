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
    public partial class PocetnoStanjePrviPutIzvjestaj : Window
    {
        private PocetnoStanjePrviPutViewModel _pocetnoStanjePrviPutvm;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportPocetnoStanjePrviPut _report;
        private List<Mat> _mat;
        private List<TabelaMaterijala> _tabelaMaterijala;
        private List<Materijalno.Model.EntityModels.SifarnikSkladista> _tabelaSkladista;
        private List<Materijalno.Model.EntityModels.SifarnikKonta> _tabelaKonto;
        private Mat ukupnavrijednost;
        private Mat ukupnoduguje;
        

        public ObservableCollection<Mat> MatList { get; set; }

        public PocetnoStanjePrviPutIzvjestaj(PocetnoStanjePrviPutViewModel pocetnoStanjePrviPutvm)
        {
            InitializeComponent();

            _pocetnoStanjePrviPutvm = pocetnoStanjePrviPutvm;

            ukupnavrijednost = new Mat();

            var dbContext = new materijalno_knjigovodstvoContext();

            MatList = new ObservableCollection<Mat>(dbContext.Mat
                     .Where(row => row.Kljnaz == pocetnoStanjePrviPutvm.CurrentItemMat.Kljnaz && row.Status == "P")
                     .OrderBy(row => row.Ident)
                     .ToList());

            _tabelaMaterijala = dbContext.TabelaMaterijala.ToList();
            _tabelaSkladista = dbContext.SifarnikSkladista.ToList();

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
            reportDt = new DataTable("PocetnoStanjePrviPut");

            reportDt.Columns.Add("Ident").DataType = typeof(int);
            reportDt.Columns.Add("Nazmat").DataType = typeof(string);
            reportDt.Columns.Add("Kolic").DataType = typeof(int);
            reportDt.Columns.Add("Vrijed").DataType = typeof(decimal);
            reportDt.Columns.Add("Datun").DataType = typeof(DateTime);
            reportDt.Columns.Add("Datnar").DataType = typeof(DateTime);
            reportDt.Columns.Add("NazivOrg").DataType = typeof(string);
            reportDt.Columns.Add("Kljnaz").DataType = typeof(int);
            reportDt.Columns.Add("Nc").DataType = typeof(decimal);
            reportDt.Columns.Add("Redbr").DataType = typeof(int);


            List<ReportPocetnoStanjePrviPut> lista = new List<ReportPocetnoStanjePrviPut>();

            foreach (Mat mat in _mat)
            {
                TabelaMaterijala tabmat = (from TabelaMaterijala tabmaterijala in _tabelaMaterijala
                                           where tabmaterijala.Ident == mat.Ident
                                           select tabmaterijala).FirstOrDefault();

                Materijalno.Model.EntityModels.SifarnikSkladista tabsklad = (from Materijalno.Model.EntityModels.SifarnikSkladista tabskladista in _tabelaSkladista
                                                                             where tabskladista.Kljnaz == mat.Kljnaz
                                                                             select tabskladista).FirstOrDefault();

                //Materijalno.Model.EntityModels.SifarnikKonta tabkonto = (from Materijalno.Model.EntityModels.SifarnikKonta tabkonta in _tabelaKonto
                //                                                         where tabkonta.Sifkonta == mat.Konto1
                //                                                         select tabkonta).FirstOrDefault();
               
                _report = new ReportPocetnoStanjePrviPut();
                _report.Ident = mat.Ident;
                _report.NazMat = tabmat.Nazmat;
                _report.Kolic = mat.Kolic;
                _report.Vrijed = mat.Vrijed;
                _report.Datun = mat.Datun;
                _report.Datnar = mat.Datnar;
                _report.NazivOrg = tabsklad.NazivOrg;
                _report.Kljnaz = mat.Kljnaz;
                _report.Redbr = mat.Redbr;

                _report.Nc = mat.Nc;

                lista.Add(_report);
            }
            List<ReportPocetnoStanjePrviPut> listaSort = lista.OrderBy(o => o.Datun).ToList();

            foreach (ReportPocetnoStanjePrviPut report in listaSort)
            {
                DataRow dr = reportDt.NewRow();

                dr[0] = report.Ident;
                dr[1] = report.NazMat;
                dr[2] = report.Kolic;
                dr[3] = report.Vrijed;
                dr[4] = report.Datun;
                dr[5] = report.Datnar;
                dr[6] = report.NazivOrg;
                dr[7] = report.Kljnaz;
                dr[8] = report.Nc;
                dr[9] = report.Redbr;

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
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\PocetnoStanjePrviPut.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "PocetnoStanjePrviPut.rdlc";
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
