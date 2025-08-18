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
   
    public partial class NaloziZaFinansijePrintanjeNalogaIzvjestaj : Window
    {
        private NaloziZaFinansijePrintanjeNalogaViewModel _naloziZaFinansijePrintanjeNalogavm;
        private bool _isReportViewerLoaded;
        private DataTable reportDt;
        private ReportNaloziZaFinansijePrintanjeNaloga _report;
        private List<Mat> _mat;
        private List<Nalmat> _nalmat;
        private List<TabelaMaterijala> _tabelaMaterijala;
        private List<Materijalno.Model.EntityModels.SifarnikSkladista> _tabelaSkladista;
        private List<Materijalno.Model.EntityModels.SifarnikKonta> _tabelaKonta;
        private Mat ukupnavrijednost;
        private Mat ukupnoduguje;

        //decimal? totalVrijednost = 0;

        public ObservableCollection<Mat> MatList { get; set; }
        public ObservableCollection<Nalmat> NalMatList { get; set; }


        public NaloziZaFinansijePrintanjeNalogaIzvjestaj(NaloziZaFinansijePrintanjeNalogaViewModel naloziZaFinansijePrintanjeNalogavm)
        {
            InitializeComponent();
            _naloziZaFinansijePrintanjeNalogavm = naloziZaFinansijePrintanjeNalogavm;

            //ukupnavrijednost = new Mat();
            ukupnoduguje = new Mat();

            var dbContext = new materijalno_knjigovodstvoContext();

            NalMatList = new ObservableCollection<Nalmat>(dbContext.Nalmat
                     .Where(row => row.Brfak == naloziZaFinansijePrintanjeNalogavm.CurrentItemMat.Brfak )
                     .OrderBy(row => row.Datnsta)
                     .ToList());

            _tabelaMaterijala = dbContext.TabelaMaterijala.ToList();

            _tabelaSkladista = dbContext.SifarnikSkladista.ToList();
            _tabelaKonta = dbContext.SifarnikKonta.ToList();

            _nalmat = NalMatList.ToList();
            decimal? totalVrijednost = 0;

            //foreach (var item in NalMatList)
            //{
            //    totalVrijednost += item.Vrijed;
            //}

            //ukupnavrijednost.Vrijed = totalVrijednost;
            //ukupnoduguje.Vrijed = totalVrijednost;

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
            reportDt = new DataTable("NaloziZaFinansijePrintanjeNaloga");

            reportDt.Columns.Add("Sintstav").DataType = typeof(int);
            reportDt.Columns.Add("Brdokst").DataType = typeof(string);
            reportDt.Columns.Add("Datnsta").DataType = typeof(DateTime);
            reportDt.Columns.Add("Dug1st").DataType = typeof(decimal);
            reportDt.Columns.Add("Pot1st").DataType = typeof(decimal);
            reportDt.Columns.Add("Brfak").DataType = typeof(string);
            reportDt.Columns.Add("Nazkon").DataType = typeof(string);
            reportDt.Columns.Add("Sifakt").DataType = typeof(int);
            reportDt.Columns.Add("Bronsta").DataType = typeof(int);
            


        //reportDt.Columns.Add("Totalvrijednost").DataType = typeof(decimal);


        List<ReportNaloziZaFinansijePrintanjeNaloga> lista = new List<ReportNaloziZaFinansijePrintanjeNaloga>();

            foreach (Nalmat nalmat in _nalmat)
            {
                //TabelaMaterijala tabmat = (from TabelaMaterijala tabmaterijala in _tabelaMaterijala
                //                           where tabmaterijala.Ident == mat.Ident
                //                           select tabmaterijala).FirstOrDefault();

                //Materijalno.Model.EntityModels.SifarnikKonta tabsklad = (from Materijalno.Model.EntityModels.SifarnikSkladista tabskladista in _tabelaSkladista
                //                                                             where tabskladista.Kljnaz == mat.Kljnaz
                //                                                             select tabskladista).FirstOrDefault();


                Materijalno.Model.EntityModels.SifarnikKonta tabkon = (from Materijalno.Model.EntityModels.SifarnikKonta tabkonta in _tabelaKonta
                                                                         where tabkonta.Sifkonta == nalmat.Sintstav
                                                                         select tabkonta).FirstOrDefault();


                _report = new ReportNaloziZaFinansijePrintanjeNaloga();
                _report.Sintstav = nalmat.Sintstav;
                _report.Brdokst = nalmat.Brdokst;
                _report.Datnsta = nalmat.Datnsta;
                _report.Dug1st = nalmat.Dug1st;
                _report.Pot1st = nalmat.Pot1st;
                _report.Brfak = nalmat.Brfak;
                _report.Nazkont = tabkon.Nazkont;
                _report.Sifakt = nalmat.Sifakt;
                _report.Bronsta = nalmat.Bronsta;
                
                lista.Add(_report);
            }

            List<ReportNaloziZaFinansijePrintanjeNaloga> listaSort = lista.OrderBy(o => o.Datnsta).ToList();

            foreach (ReportNaloziZaFinansijePrintanjeNaloga report in listaSort)
            {
                DataRow dr = reportDt.NewRow();

                dr[0] = report.Sintstav;
                dr[1] = report.Brdokst;
                dr[2] = report.Datnsta;
                dr[3] = report.Dug1st;
                dr[4] = report.Pot1st;
                dr[5] = report.Brfak;
                dr[6] = report.Nazkont;
                dr[7] = report.Sifakt;
                dr[8] = report.Bronsta;
                
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
            this._reportViewer.LocalReport.ReportPath = pathHelper.MExecutableRootDirectory + "\\Izvjestaji\\NaloziZaFinansijePrintanjeNaloga.rdlc";
            _reportViewer.LocalReport.DataSources.Add(ds);
            try
            {
                this._reportViewer.LocalReport.ReportEmbeddedResource = "NaloziZaFinansijePrintanjeNaloga.rdlc";
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
