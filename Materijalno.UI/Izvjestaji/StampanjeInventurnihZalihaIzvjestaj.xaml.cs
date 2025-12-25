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
        private List<DiffRow> _diffList;
        private List<ReportStampanjeInventurnihZalihaViewModel> _reportRows;

        int? KolicRazlikaVisak = 0;
        int? KolicRazlikaManjak = 0;

        decimal? VrijedRazlikaVisak = 0;
        decimal? VrijedRazlikaManjak = 0;


        public ObservableCollection<Mat> MatList { get; set; }
        public ObservableCollection<Inv> InvList { get; set; }

        public class DiffRow
        {
            public int Ident { get; set; }
            public int? Kljnaz { get; set; }

            public int MatKolic { get; set; }
            public int InvKolic { get; set; }
            public int KolicRazlika { get; set; }

            public decimal? MatVrijed { get; set; }
            public decimal InvVrijed { get; set; }
            public decimal VrijedRazlika { get; set; }

            public DateTime? InvDatun { get; set; }
            public DateTime? InvDatnar { get; set; }
            public decimal? InvNc { get; set; }
            public int? InvRedbr { get; set; }
            public int? InvKonto1 { get; set; }
            public decimal MatNc { get; set; }

        }


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
                .Select(grouped =>
                {
                    int sumKolic = (int)grouped.Sum(x => x.Kolic);
                    decimal sumVrijed = (decimal)grouped.Sum(x => x.Vrijed);

                    return new Mat
                    {
                        Ident = grouped.Key,
                        Kljnaz = currentKljnazMat,
                        Kolic = sumKolic,
                        Vrijed = sumVrijed,

                        // 🔹 weighted average price (Nc)
                        Nc = sumKolic == 0 ? 0m : (sumVrijed / sumKolic)
                    };
                })
                .ToList());


            var mat = MatList.ToList();
            var inv = InvList.ToList();

            //var diffList = (from mat in MatList.ToList()
            //                join inv in InvList.ToList()
            //                on new { Kljnaz = mat.Kljnaz, Ident = mat.Ident }
            //                equals new { Kljnaz = (int?)inv.Kljnaz, Ident = inv.Ident } into gj
            //                from subInv in gj.DefaultIfEmpty()
            //                select new
            //                {
            //                    Ident = mat.Ident,
            //                    Kljnaz = mat.Kljnaz,
            //                    MatKolic = mat.Kolic,
            //                    InvKolic = subInv?.Kolic ?? 0,
            //                    KolicRazlika = mat.Kolic - (subInv?.Kolic ?? 0),
            //                    MatVrijed = mat.Vrijed,
            //                    InvVrijed = subInv?.Vrijed ?? 0,
            //                    VrijedRazlika = mat.Vrijed - (subInv?.Vrijed ?? 0)
            //                }).ToList();  

            // Mat -> Inv (keeps all Mat rows)
            var left = from m in mat
                       join i in inv
                       on new { m.Kljnaz, m.Ident }
                       equals new { Kljnaz = (int?)i.Kljnaz, i.Ident } into gj
                       from i in gj.DefaultIfEmpty()
                       select new DiffRow
                       {
                           Ident = m.Ident ?? 0,
                           Kljnaz = m.Kljnaz,

                           MatKolic = m.Kolic ?? 0,
                           InvKolic = i?.Kolic ?? 0,
                           KolicRazlika = (m.Kolic ?? 0) - (i?.Kolic ?? 0),

                           MatVrijed = m.Vrijed,                 // if Mat.Vrijed is decimal (not nullable)
                           InvVrijed = i?.Vrijed ?? 0m,          // if Inv.Vrijed is decimal (not nullable), use i?.Vrijed ?? 0m
                           VrijedRazlika = (decimal)(m.Vrijed - (i?.Vrijed ?? 0m)),

                           InvDatun = i?.Datun,
                           InvDatnar = i?.Datnar,
                           InvNc = i?.Nc,                        // decimal? (matches class)
                           InvRedbr = i?.Redbr,
                           InvKonto1 = i?.Konto1,
                           MatNc = (decimal)m.Nc
                       };

            var rightOnly = from i in inv
                            join m in mat
                            on new { Kljnaz = (int?)i.Kljnaz, i.Ident }
                            equals new { Kljnaz = m.Kljnaz, m.Ident } into gj
                            from m in gj.DefaultIfEmpty()
                            where m == null
                            select new DiffRow
                            {
                                Ident = i.Ident ?? 0,
                                Kljnaz = i.Kljnaz,

                                MatKolic = 0,
                                InvKolic = i.Kolic ?? 0,
                                KolicRazlika = 0 - (i.Kolic ?? 0),

                                MatVrijed = 0m,
                                InvVrijed = i.Vrijed,             // no ?? if non-nullable
                                VrijedRazlika = 0m - i.Vrijed,

                                InvDatun = i.Datun,
                                InvDatnar = i.Datnar,
                                MatNc = 0m,
                                InvNc = (decimal?)i.Nc,
                                InvRedbr = i.Redbr,
                                InvKonto1 = i.Konto1
                            };

            _diffList = left.Concat(rightOnly).ToList();

            int? totalKolicRazlika = _diffList.Sum(item => item.KolicRazlika);

            decimal? totalVrijedRazlika = _diffList.Sum(item => item.VrijedRazlika);


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



            reportDt.Columns.Add("MatKolic").DataType = typeof(int);
            reportDt.Columns.Add("MatVrijed").DataType = typeof(decimal);
            reportDt.Columns.Add("InvKolic").DataType = typeof(int);
            reportDt.Columns.Add("InvVrijed").DataType = typeof(decimal);

            var lista = new List<ReportStampanjeInventurnihZalihaViewModel>();

            // IMPORTANT: use _diffList (full outer join result), not _inv/InvList
            foreach (DiffRow row in _diffList)
            {
                var tabmat = _tabelaMaterijala.FirstOrDefault(x => x.Ident == row.Ident);
                var tabsklad = _tabelaSkladista.FirstOrDefault(x => x.Kljnaz == row.Kljnaz);

                var report = new ReportStampanjeInventurnihZalihaViewModel
                {
                    Ident = row.Ident,
                    NazMat = tabmat?.Nazmat,
                    NazivOrg = tabsklad?.NazivOrg,
                    Kljnaz = row.Kljnaz ?? 0,

                    // Choose what your report shows as "Kolic" and "Vrijed"
                    // Here: inventory (Inv) side; change to row.MatKolic/row.MatVrijed if you prefer
                    Kolic = row.InvKolic,
                    Vrijed = row.InvVrijed,

                    Datun = row.InvDatun,           // DateTime? (can be null)
                    Datnar = row.InvDatnar,         // DateTime? (can be null)
                    //Nc = row.InvNc ?? 0m,
                    Nc = row.MatNc,
                    Redbr = row.InvRedbr ?? 0,

                    KolicRazlika = row.KolicRazlika,
                    VrijedRazlika = row.VrijedRazlika,

                    KolicRazlikaVisak = this.KolicRazlikaVisak,
                    KolicRazlikaManjak = this.KolicRazlikaManjak,
                    VrijedRazlikaVisak = this.VrijedRazlikaVisak,
                    VrijedRazlikaManjak = this.VrijedRazlikaManjak,

                    // NEW:
                    MatKolic = row.MatKolic,
                    MatVrijed = (decimal)row.MatVrijed,
                    InvKolic = row.InvKolic,
                    InvVrijed = row.InvVrijed
                };

                lista.Add(report);
            }

            // Sort safely because Datun can be null
            var listaSort = lista.OrderBy(o => o.Datun ?? DateTime.MinValue).ToList();
            _reportRows = listaSort;

            foreach (var report in listaSort)
            {
                DataRow dr = reportDt.NewRow();

                dr[0] = report.Ident;
                dr[1] = (object)report.NazMat ?? DBNull.Value;
                dr[2] = report.Kolic;
                dr[3] = report.Vrijed;

                // Dates can be null -> DBNull.Value
                dr[4] = (object)report.Datun ?? DBNull.Value;
                dr[5] = (object)report.Datnar ?? DBNull.Value;

                dr[6] = (object)report.NazivOrg ?? DBNull.Value;
                dr[7] = report.Kljnaz;
                dr[8] = report.Nc;
                dr[9] = report.Redbr;

                dr[10] = report.KolicRazlika;
                dr[11] = report.VrijedRazlika;

                dr[12] = report.KolicRazlikaVisak;
                dr[13] = report.KolicRazlikaManjak;
                dr[14] = report.VrijedRazlikaVisak;
                dr[15] = report.VrijedRazlikaManjak;

                dr[16] = report.MatKolic;
                dr[17] = report.MatVrijed;
                dr[18] = report.InvKolic;
                dr[19] = report.InvVrijed;

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

        //private void PripremiPrint()
        //{
        //    ReportDataSource ds = new ReportDataSource("DataSet1", reportDt);
        //    PathHelper pathHelper = new PathHelper();
        //    this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\StampanjeInventurnihZaliha.rdlc";
        //    _reportViewer.LocalReport.DataSources.Clear();
        //    _reportViewer.LocalReport.DataSources.Add(ds);
        //    try
        //    {
        //        this._reportViewer.LocalReport.ReportEmbeddedResource = "StampanjeInventurnihZaliha.rdlc";
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception("Ne postoje stavke sa ispis.");
        //    }
        //}
        private void PripremiPrint()
        {
            //ReportDataSource ds = new ReportDataSource("DataSet1", reportDt);

            //Ovo je ako hocemo da uzimamo podatke iz _reportRows jer tu imamo iz dvije tabele INV i MAT podatke (treba vidjeti da se uzimaju podaci iz postojece liste)
            ReportDataSource ds = new ReportDataSource("DataSet1", _reportRows);

            PathHelper pathHelper = new PathHelper();
            _reportViewer.LocalReport.ReportPath =
                pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\StampanjeInventurnihZaliha.rdlc";

            _reportViewer.LocalReport.DataSources.Clear();
            _reportViewer.LocalReport.DataSources.Add(ds);
        }


        private void btnOdustani_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

