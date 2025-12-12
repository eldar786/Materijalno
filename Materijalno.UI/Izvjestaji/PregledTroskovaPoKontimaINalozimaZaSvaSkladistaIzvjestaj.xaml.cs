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
    public partial class PregledTroskovaPoKontimaINalozimaZaSvaSkladistaIzvjestaj : Window
    {
        private PregledTroskovaPoKontimaINalozimaZaSvaSkladistaViewModel _pregledTroskovaPoKontimaINalozimaZaSvaSkladistavm;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportPregledTroskovaPoKontimaINalozimaZaSvaSkladista _report;
        private List<Mat> _mat;
        private List<TabelaMaterijala> _tabelaMaterijala;
        private List<Materijalno.Model.EntityModels.SifarnikKonta> _tabelaKonto;
        private List<Materijalno.Model.EntityModels.SifarnikSkladista> _tabelaSkladista;
        private Mat ukupnavrijednost;
        private Mat ukupnoduguje;


        public ObservableCollection<Mat> MatList { get; set; }


        public PregledTroskovaPoKontimaINalozimaZaSvaSkladistaIzvjestaj(PregledTroskovaPoKontimaINalozimaZaSvaSkladistaViewModel pregledTroskovaPoKontimaINalozimaZaSvaSkladistavm)
        {
            InitializeComponent();
            _pregledTroskovaPoKontimaINalozimaZaSvaSkladistavm = pregledTroskovaPoKontimaINalozimaZaSvaSkladistavm;

            ukupnoduguje = new Mat();

            var dbContext = new materijalno_knjigovodstvoContext();


            MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Datun >= pregledTroskovaPoKontimaINalozimaZaSvaSkladistavm.CurrentItemMat.Datun
                               && row.Datnar <= pregledTroskovaPoKontimaINalozimaZaSvaSkladistavm.CurrentItemMat.Datnar
                               && row.Status == "I")
                    .AsEnumerable() // Forces execution in-memory for GroupBy
                    .GroupBy(row => new { row.Kljnaz, row.Brfak, row.Konto1 }) // Group by key
                    .Select(grouped => new Mat
                    {
                        Konto1 = grouped.Key.Konto1,
                        Kljnaz = grouped.Key.Kljnaz,
                        Brfak = grouped.Key.Brfak,

                        // Instead of setting the same values for all, use Min, Max, or another relevant aggregation
                        Datun = grouped.Min(x => x.Datun), // Take earliest date from group
                        Datnar = grouped.Max(x => x.Datnar), // Take latest date from group

                        Vrijed = grouped.Sum(x => x.Vrijed), // Sum up Vrijed
                    })
                    .ToList()); // Convert to List before assigning to ObservableCollection



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
            reportDt = new DataTable("PregledTroskovaPoKontimaINalozimaZaSvaSkladista");

            //reportDt.Columns.Add("Ident").DataType = typeof(int);
            //reportDt.Columns.Add("Nazmat").DataType = typeof(string);
            //reportDt.Columns.Add("Kolic").DataType = typeof(int);
            reportDt.Columns.Add("Vrijed").DataType = typeof(decimal);
            reportDt.Columns.Add("Datun").DataType = typeof(DateTime);
            reportDt.Columns.Add("Datnar").DataType = typeof(DateTime);
            reportDt.Columns.Add("NazivOrg").DataType = typeof(string);
            reportDt.Columns.Add("Kljnaz").DataType = typeof(int);
            reportDt.Columns.Add("Nazkont").DataType = typeof(string);
            reportDt.Columns.Add("Konto1").DataType = typeof(int);
            reportDt.Columns.Add("Brfak").DataType = typeof(string);


            List<ReportPregledTroskovaPoKontimaINalozimaZaSvaSkladista> lista = new List<ReportPregledTroskovaPoKontimaINalozimaZaSvaSkladista>();

            foreach (Mat mat in _mat)
            {
                TabelaMaterijala tabmat = (from TabelaMaterijala tabmaterijala in _tabelaMaterijala
                                           where tabmaterijala.Ident == mat.Ident
                                           select tabmaterijala).FirstOrDefault();

                Materijalno.Model.EntityModels.SifarnikSkladista tabsklad = (from Materijalno.Model.EntityModels.SifarnikSkladista tabskladista in _tabelaSkladista
                                                                             where tabskladista.Kljnaz == mat.Kljnaz
                                                                             select tabskladista).FirstOrDefault();

                Materijalno.Model.EntityModels.SifarnikKonta tabkonto = (from Materijalno.Model.EntityModels.SifarnikKonta tabkonta in _tabelaKonto
                                                                         where tabkonta.Sifkonta == mat.Konto1
                                                                         select tabkonta).FirstOrDefault();


                _report = new ReportPregledTroskovaPoKontimaINalozimaZaSvaSkladista();

                //_report.Ident = mat.Ident;
                //_report.NazMat = tabmat.Nazmat;
                //_report.Kolic = mat.Kolic;
                _report.Vrijed = mat.Vrijed;
                _report.Datun = (DateTime)mat.Datun;
                _report.Datnar = (DateTime)mat.Datnar;
                _report.NazivOrg = tabsklad.NazivOrg;
                _report.Kljnaz = mat.Kljnaz;
                _report.Nazkont = tabkonto.Nazkont;
                _report.Konto1 = mat.Konto1;
                _report.Brfak = mat.Brfak;

                lista.Add(_report);

            }

            List<ReportPregledTroskovaPoKontimaINalozimaZaSvaSkladista> listaSort = lista.OrderBy(o => o.Datun).ToList();

            foreach (ReportPregledTroskovaPoKontimaINalozimaZaSvaSkladista report in listaSort)
            {
                DataRow dr = reportDt.NewRow();

                //dr[0] = report.Ident;
                //dr[1] = report.NazMat;
                //dr[2] = report.Kolic;
                dr[0] = report.Vrijed;
                dr[1] = report.Datun;
                dr[2] = report.Datnar;
                dr[3] = report.NazivOrg;
                dr[4] = report.Kljnaz;
                dr[5] = report.Nazkont;
                dr[6] = report.Konto1;
                dr[7] = report.Brfak;
                

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
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\PregledTroskovaPoKontimaINalozimaZaSvaSkladista.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "PregledTroskovaPoKontimaINalozimaZaSvaSkladista.rdlc";
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

