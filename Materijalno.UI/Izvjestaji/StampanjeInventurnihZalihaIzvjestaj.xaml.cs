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
    public partial class StampanjeInventurnihZalihaIzvjestaj : Window
    {
        private StampanjeInventurnihZalihaViewModel _stampanjeInventurnihZalihaViewModel;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportStampanjeInventurnihZalihaViewModel _report;
        private List<Mat> _mat;
        private List<Inv> _inv;
        private List<TabelaMaterijala> _tabelaMaterijala;
        private List<Materijalno.Model.EntityModels.SifarnikKonta> _tabelaKonto;
        private List<Materijalno.Model.EntityModels.SifarnikSkladista> _tabelaSkladista;
        private Mat ukupnavrijednost;
        private Inv ukupnoduguje;

        int? KolicRazlikaVisak = 0;
        int? KolicRazlikaManjak = 0;

        decimal? VrijedRazlikaVisak = 0;
        decimal? VrijedRazlikaManjak = 0;


        public ObservableCollection<Mat> MatList { get; set; }
        public ObservableCollection<Inv> InvList { get; set; }


        public StampanjeInventurnihZalihaIzvjestaj(StampanjeInventurnihZalihaViewModel stampanjeInventurnihZalihaViewModel)
        {
            InitializeComponent();
            _stampanjeInventurnihZalihaViewModel = stampanjeInventurnihZalihaViewModel;
            int? currentKljnaz = stampanjeInventurnihZalihaViewModel.CurrentItemInv?.Kljnaz;
            int? currentKljnazMat = stampanjeInventurnihZalihaViewModel.CurrentItemInv?.Kljnaz;

            ukupnoduguje = new Inv();

            var dbContext = new materijalno_knjigovodstvoContext();


            InvList = new ObservableCollection<Inv>(dbContext.Inv
                    .Where(row => row.Kljnaz == stampanjeInventurnihZalihaViewModel.CurrentItemInv.Kljnaz) // Skladište
                    .OrderBy(row => row.Ident) // Group by Ident
                    .ToList()); // Convert to List before assigning to ObservableCollection

            MatList = new ObservableCollection<Mat>(dbContext.Mat
                .Where(row => row.Kljnaz == currentKljnazMat)
                .AsEnumerable() // switch to LINQ-to-Objects
                .GroupBy(row => row.Ident)
                .Select(grouped => new Mat
                {
                    Ident = grouped.Key,
                    Kljnaz = currentKljnazMat,
                    Kolic = grouped.Sum(x => x.Kolic),
                    Vrijed = grouped.Sum(x => x.Vrijed)
                })
                    .ToList());

            var diffList = (from mat in MatList.ToList()
                            join inv in InvList.ToList()
                            on new { Kljnaz = mat.Kljnaz, Ident = mat.Ident }
                            equals new { Kljnaz = (int?)inv.Kljnaz, Ident = inv.Ident } into gj
                            from subInv in gj.DefaultIfEmpty()
                            select new
                            {
                                Ident = mat.Ident,
                                Kljnaz = mat.Kljnaz,
                                MatKolic = mat.Kolic,
                                InvKolic = subInv?.Kolic ?? 0,
                                KolicRazlika = mat.Kolic - (subInv?.Kolic ?? 0),
                                MatVrijed = mat.Vrijed,
                                InvVrijed = subInv?.Vrijed ?? 0,
                                VrijedRazlika = mat.Vrijed - (subInv?.Vrijed ?? 0)
                            }).ToList();  

            int? totalKolicRazlika = diffList.Sum(item => item.KolicRazlika);

            decimal? totalVrijedRazlika = diffList.Sum(item => item.VrijedRazlika);


            //KolicRazlika
            if (totalKolicRazlika > 0)
            {
                KolicRazlikaVisak = totalKolicRazlika;
            }

            else if (totalKolicRazlika < 0)
            {
                KolicRazlikaManjak = totalKolicRazlika;
            }

            //VrijedRazlika
            if (totalVrijedRazlika > 0)
            {
                VrijedRazlikaVisak = totalVrijedRazlika;
            }
            else if (totalVrijedRazlika < 0)
            {
                VrijedRazlikaManjak = totalVrijedRazlika;
            }


            _tabelaMaterijala = dbContext.TabelaMaterijala.ToList();
            _tabelaSkladista = dbContext.SifarnikSkladista.ToList();
            _tabelaKonto = dbContext.SifarnikKonta.ToList();

            _inv = InvList.ToList();
            decimal? totalVrijednost = 0;

            foreach (var item in InvList)
            {
                totalVrijednost += item.Vrijed;
            }

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
            reportDt = new DataTable("StampanjeInventurnihZaliha");

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
            reportDt.Columns.Add("KolicRazlika").DataType = typeof(int);
            reportDt.Columns.Add("VrijedRazlika").DataType = typeof(decimal);
            reportDt.Columns.Add("KolicRazlikaVisak").DataType = typeof(int);
            reportDt.Columns.Add("KolicRazlikaManjak").DataType = typeof(int);
            reportDt.Columns.Add("VrijedRazlikaVisak").DataType = typeof(decimal);
            reportDt.Columns.Add("VrijedRazlikaManjak").DataType = typeof(decimal);


            List<ReportStampanjeInventurnihZalihaViewModel> lista = new List<ReportStampanjeInventurnihZalihaViewModel>();

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

                var matchedMat = MatList.FirstOrDefault(m => m.Ident == inv.Ident && m.Kljnaz == inv.Kljnaz);

                _report = new ReportStampanjeInventurnihZalihaViewModel();

                _report.Ident = inv.Ident;
                _report.NazMat = tabmat.Nazmat;
                _report.Kolic = inv.Kolic;
                _report.Vrijed = inv.Vrijed;
                _report.Datun = inv.Datun;
                _report.Datnar = inv.Datnar;
                _report.NazivOrg = tabsklad.NazivOrg;
                _report.Kljnaz = inv.Kljnaz;
                _report.Redbr = inv.Redbr;
                _report.Nc = inv.Nc;
                _report.KolicRazlika = (matchedMat?.Kolic ?? 0) - (inv.Kolic ?? 0);
                _report.VrijedRazlika = (decimal)((matchedMat?.Vrijed ?? 0m) - ((decimal?)(inv.Vrijed) ?? 0));

                _report.KolicRazlikaVisak = this.KolicRazlikaVisak;
                _report.KolicRazlikaManjak = this.KolicRazlikaManjak;

                _report.VrijedRazlikaVisak = this.VrijedRazlikaVisak;
                _report.VrijedRazlikaManjak = this.VrijedRazlikaManjak;
               
                lista.Add(_report);

            }

            List<ReportStampanjeInventurnihZalihaViewModel> listaSort = lista.OrderBy(o => o.Datun).ToList();

            foreach (ReportStampanjeInventurnihZalihaViewModel report in listaSort)
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
                dr[8] = report.Nc;
                dr[9] = report.Redbr;
                dr[10] = report.KolicRazlika; 
                dr[11] = report.VrijedRazlika; 
                dr[12] = report.KolicRazlikaVisak; 
                dr[13] = report.KolicRazlikaManjak; 
                dr[14] = report.VrijedRazlikaVisak; 
                dr[15] = report.VrijedRazlikaManjak; 

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
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\StampanjeInventurnihZaliha.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "StampanjeInventurnihZaliha.rdlc";
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

