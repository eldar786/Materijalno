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
using Materijalno.Model.Services;
using Microsoft.EntityFrameworkCore;

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

        decimal? KolicRazlikaVisak = 0;
        decimal? KolicRazlikaManjak = 0;

        decimal? VrijedRazlikaVisak = 0;
        decimal? VrijedRazlikaManjak = 0;


        public ObservableCollection<Mat> MatList { get; set; }
        public ObservableCollection<Inv> InvList { get; set; }

        public class DiffRow
        {
            public int Ident { get; set; }
            public int? Kljnaz { get; set; }

            public decimal MatKolic { get; set; }
            public decimal InvKolic { get; set; }
            public decimal KolicRazlika { get; set; }

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
            if (!currentKljnaz.HasValue || currentKljnaz <= 0)
                throw new InventoryReportUnavailableException("Odaberite skladište za štampanje inventurnih zaliha.");

            ukupnoduguje = new Inv();

            var dbContext = new materijalno_knjigovodstvoContext();


            var selected = stampanjeInventurnihZalihaViewModel.CurrentItemInv;
            DateTime inventoryDate;
            Guid? session = selected.InventorySessionId;
            var inventoryQuery = dbContext.Inv.IgnoreQueryFilters().Where(i => !i.IsDraft && i.Kljnaz == currentKljnaz);
            // A warehouse edit can leave the previous warehouse's session on
            // the filter. Only reuse a session that belongs to this warehouse.
            if (!session.HasValue || !inventoryQuery.Any(i => i.InventorySessionId == session))
                session = inventoryQuery.OrderByDescending(i => i.InventoryDate).ThenByDescending(i => i.Datun)
                    .Select(i => i.InventorySessionId).FirstOrDefault();
            // Original invzal.frx also prints warehouses with no saved count:
            // the inventory side is zero and the book side remains visible.
            InvList = session.HasValue
                ? new ObservableCollection<Inv>(inventoryQuery.Where(i => i.InventorySessionId == session).OrderBy(i => i.Ident).ToList())
                : new ObservableCollection<Inv>();
            inventoryDate = InvList.Count > 0
                ? (InvList[0].InventoryDate ?? InvList[0].Datun ?? DateTime.Today).Date
                : new DateTime(new MaterialPostingService().ActiveYear(), 1, 1);
            var stockQuery = InvList.Count > 0 ? ReportQueries.AtDate(dbContext, inventoryDate) : ReportQueries.ForYear(dbContext, inventoryDate);
            MatList = new ObservableCollection<Mat>(stockQuery
                .Where(m => m.Kljnaz == currentKljnaz).AsEnumerable().GroupBy(m => m.Ident)
                .Select(g => new Mat { Ident = g.Key, Kljnaz = currentKljnaz, Kolic = g.Sum(m => m.Kolic), Vrijed = g.Sum(m => m.Vrijed),
                    Nc = g.OrderBy(m => m.FoxRecno.HasValue ? 0 : 1).ThenBy(m => m.FoxRecno).ThenBy(m => m.Id).Last().Nc ?? 0 }).ToList());
            if (MatList.Count == 0 && InvList.Count == 0)
                throw new InventoryReportUnavailableException("Za skladište " + currentKljnaz + " nema materijalnih knjiženja ni inventurnog popisa.");

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
                                MatNc = i.Nc,
                                InvNc = (decimal?)i.Nc,
                                InvRedbr = i.Redbr,
                                InvKonto1 = i.Konto1
                            };

            _diffList = left.Concat(rightOnly).ToList();

            KolicRazlikaManjak = _diffList.Sum(i => Math.Max(i.MatKolic - i.InvKolic, 0));
            KolicRazlikaVisak = _diffList.Sum(i => Math.Max(i.InvKolic - i.MatKolic, 0));
            VrijedRazlikaManjak = Math.Max(_diffList.Sum(i => i.MatVrijed ?? 0) - _diffList.Sum(i => i.InvVrijed), 0);
            VrijedRazlikaVisak = Math.Max(_diffList.Sum(i => i.InvVrijed) - _diffList.Sum(i => i.MatVrijed ?? 0), 0);

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
            reportDt.Columns.Add("NazMat").DataType = typeof(string);
            reportDt.Columns.Add("Kolic").DataType = typeof(decimal);
            reportDt.Columns.Add("Vrijed").DataType = typeof(decimal);

            reportDt.Columns.Add("Datun").DataType = typeof(DateTime);
            reportDt.Columns.Add("Datnar").DataType = typeof(DateTime);

            reportDt.Columns.Add("NazivOrg").DataType = typeof(string);
            reportDt.Columns.Add("Kljnaz").DataType = typeof(int);
            reportDt.Columns.Add("Nc").DataType = typeof(decimal);
            reportDt.Columns.Add("Redbr").DataType = typeof(int);

            reportDt.Columns.Add("KolicRazlika").DataType = typeof(decimal);
            reportDt.Columns.Add("VrijedRazlika").DataType = typeof(decimal);

            reportDt.Columns.Add("KolicRazlikaVisak").DataType = typeof(decimal);
            reportDt.Columns.Add("KolicRazlikaManjak").DataType = typeof(decimal);
            reportDt.Columns.Add("VrijedRazlikaVisak").DataType = typeof(decimal);
            reportDt.Columns.Add("VrijedRazlikaManjak").DataType = typeof(decimal);



            reportDt.Columns.Add("MatKolic").DataType = typeof(decimal);
            reportDt.Columns.Add("MatVrijed").DataType = typeof(decimal);
            reportDt.Columns.Add("InvKolic").DataType = typeof(decimal);
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
                    Nc = row.MatNc == 0 ? row.InvNc ?? 0 : row.MatNc,
                    Redbr = row.InvRedbr ?? 0,

                    KolicRazlika = row.KolicRazlika,
                    VrijedRazlika = (row.MatVrijed ?? 0) - row.InvVrijed,

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

