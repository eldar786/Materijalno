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
    public partial class UnosInventurnogStanjaIzvjestaj : Window
    {
        private UnosInventurnogStanjaViewModel _unosInventurnogStanjavm;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportUnosInventurnogStanjaViewModel _report;
        private List<Mat> _mat;
        private List<Inv> _inv;
        private List<TabelaMaterijala> _tabelaMaterijala;
        private List<Materijalno.Model.EntityModels.SifarnikKonta> _tabelaKonto;
        private List<Materijalno.Model.EntityModels.SifarnikSkladista> _tabelaSkladista;
        private Mat ukupnavrijednost;
        private Inv ukupnoduguje;


        public ObservableCollection<Mat> MatList { get; set; }
        public ObservableCollection<Inv> InvList { get; set; }


        public UnosInventurnogStanjaIzvjestaj(UnosInventurnogStanjaViewModel unosInventurnogStanjavm)
        {
            InitializeComponent();
            _unosInventurnogStanjavm = unosInventurnogStanjavm;

            ukupnoduguje = new Inv();

            var dbContext = new materijalno_knjigovodstvoContext();


            InvList = new ObservableCollection<Inv>(dbContext.Inv
                    .Where(row => row.Kljnaz == unosInventurnogStanjavm.CurrentItemInv.Kljnaz) // Skladište
                    .OrderBy(row => row.Ident) // Group by Ident
                    .ToList()); // Convert to List before assigning to ObservableCollection

            _tabelaMaterijala = dbContext.TabelaMaterijala.ToList();
            _tabelaSkladista = dbContext.SifarnikSkladista.ToList();
            _tabelaKonto = dbContext.SifarnikKonta.ToList();

            _inv = InvList.ToList();
            decimal? totalVrijednost = 0;

            foreach (var item in InvList)
            {
                totalVrijednost += item.Vrijed;
            }

            //ukupnavrijednost.Vrijed = totalVrijednost;
            ukupnoduguje.Vrijed = (decimal)totalVrijednost;

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
            reportDt = new DataTable("UnosInventurnogStanja");

            reportDt.Columns.Add("Ident").DataType = typeof(int);
            reportDt.Columns.Add("Nazmat").DataType = typeof(string);
            reportDt.Columns.Add("Kolic").DataType = typeof(int);
            reportDt.Columns.Add("Vrijed").DataType = typeof(decimal);
            reportDt.Columns.Add("Datun").DataType = typeof(DateTime);
            reportDt.Columns.Add("Datnar").DataType = typeof(DateTime);
            reportDt.Columns.Add("NazivOrg").DataType = typeof(string);
            reportDt.Columns.Add("Kljnaz").DataType = typeof(int);
            //reportDt.Columns.Add("Nazkont").DataType = typeof(string);
            //reportDt.Columns.Add("Konto1").DataType = typeof(int);
            //reportDt.Columns.Add("Brfak").DataType = typeof(string);
            reportDt.Columns.Add("Nc").DataType = typeof(decimal);
            reportDt.Columns.Add("Redbr").DataType = typeof(int);


            List<ReportUnosInventurnogStanjaViewModel> lista = new List<ReportUnosInventurnogStanjaViewModel>();

            foreach (Inv inv in _inv)
            {
                TabelaMaterijala tabmat = (from TabelaMaterijala tabmaterijala in _tabelaMaterijala
                                           where tabmaterijala.Ident == inv.Ident
                                           select tabmaterijala).FirstOrDefault();

                Materijalno.Model.EntityModels.SifarnikSkladista tabsklad = (from Materijalno.Model.EntityModels.SifarnikSkladista tabskladista in _tabelaSkladista
                                                                             where tabskladista.Kljnaz == inv.Kljnaz
                                                                             select tabskladista).FirstOrDefault();

                Materijalno.Model.EntityModels.SifarnikKonta tabkonto = (from Materijalno.Model.EntityModels.SifarnikKonta tabkonta in _tabelaKonto
                                                                         where tabkonta.Sifkonta == inv.Konto1
                                                                         select tabkonta).FirstOrDefault();


                _report = new ReportUnosInventurnogStanjaViewModel();

                _report.Ident = inv.Ident;
                _report.NazMat = tabmat.Nazmat;
                _report.Kolic = inv.Kolic;
                _report.Vrijed = inv.Vrijed;
                _report.Datun = inv.Datun;
                _report.Datnar = inv.Datnar;
                _report.NazivOrg = tabsklad.NazivOrg;
                _report.Kljnaz = inv.Kljnaz;
                _report.Redbr = inv.Redbr;
                //_report.Nazkont = tabkonto.Nazkont;
                //_report.Konto1 = inv.Konto1;
                //_report.Brfak = inv.Brfak;
               
                _report.Nc = inv.Nc;

                lista.Add(_report);

            }

            List<ReportUnosInventurnogStanjaViewModel> listaSort = lista.OrderBy(o => o.Datun).ToList();

            foreach (ReportUnosInventurnogStanjaViewModel report in listaSort)
            {
                DataRow dr = reportDt.NewRow();

                dr[0] = report.Ident;
                dr[1] = report.NazMat;
                dr[2] = report.Kolic;
                dr[3] = report.Vrijed;
                dr[4] = report.Datun;
                dr[5] = (object)report.Datnar ?? DBNull.Value;
                dr[6] = report.NazivOrg;
                dr[7] = report.Kljnaz;
                //dr[5] = report.Nazkont;
                //dr[6] = report.Konto1;
                //dr[7] = report.Brfak;
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
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\UnosInventurnogStanja.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "UnosInventurnogStanja.rdlc";
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

