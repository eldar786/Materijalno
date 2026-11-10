using System;
using System.Data;
using System.Linq;
using System.Windows;
using Materijalno.Model.EntityModels;
using Materijalno.ViewModel;
using Materijalno.UI.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.WinForms;

namespace Materijalno.UI.Izvjestaji
{
    public partial class NaloziZaFinansijePrintanjeNalogaIzvjestaj : Window
    {
        private readonly DataTable reportDt;
        private bool loaded;
        public NaloziZaFinansijePrintanjeNalogaIzvjestaj(NaloziZaFinansijePrintanjeNalogaViewModel vm)
        {
            InitializeComponent();
            if (vm.Godina < 1900 || vm.Godina > 9999) throw new InvalidOperationException("Unesite ispravnu godinu naloga.");
            if (string.IsNullOrWhiteSpace(vm.Brfak)) throw new InvalidOperationException("Unesite broj kalkulacije.");
            var number=vm.Brfak.Trim();
            using(var db=new materijalno_knjigovodstvoContext())
            {
                var lines=db.Nalmat.AsNoTracking().Where(n=>n.Brfak==number&&n.Datnsta.Year==vm.Godina)
                    .OrderBy(n=>n.FoxRecno.HasValue?0:1).ThenBy(n=>n.FoxRecno).ThenBy(n=>n.LineNumber).ThenBy(n=>n.Id).ToList();
                if(lines.Count==0) throw new InvalidOperationException("Za kalkulaciju "+number+" u "+vm.Godina+". godini nema formiranog finansijskog naloga.");
                var accounts=db.SifarnikKonta.AsNoTracking().ToDictionary(a=>a.Sifkonta,a=>a.Nazkont);
                reportDt=new DataTable("NaloziZaFinansijePrintanjeNaloga");
                foreach(var field in new[]{"Sintstav","Sifakt","Bronsta"}) reportDt.Columns.Add(field,typeof(int));
                foreach(var field in new[]{"Brdokst","Brfak","Nazkon","Analst"}) reportDt.Columns.Add(field);
                reportDt.Columns.Add("Datnsta",typeof(DateTime));
                reportDt.Columns.Add("Dug1st",typeof(decimal));reportDt.Columns.Add("Pot1st",typeof(decimal));
                foreach(var line in lines)
                {
                    string name;var row=reportDt.NewRow();
                    row["Sintstav"]=line.Sintstav;row["Sifakt"]=line.Sifakt;row["Bronsta"]=line.Bronsta;
                    row["Brdokst"]=line.Brdokst??"";row["Brfak"]=line.Brfak??"";row["Analst"]=line.Analst??"";
                    row["Nazkon"]=accounts.TryGetValue(line.Sintstav,out name)?name??"":"Konto "+line.Sintstav;
                    row["Datnsta"]=line.Datnsta;row["Dug1st"]=line.Dug1st??0;row["Pot1st"]=line.Pot1st??0;
                    reportDt.Rows.Add(row);
                }
            }
            _reportViewer.LocalReport.ReportPath=System.IO.Path.Combine(new PathHelper().MExecutableRootDirectory,"Izvjestaji","NaloziZaFinansijePrintanjeNaloga.rdlc");
            _reportViewer.LocalReport.DataSources.Clear();
            _reportViewer.LocalReport.DataSources.Add(new ReportDataSource("DataSet1",reportDt));
            _reportViewer.Load+=(sender,e)=>{if(!loaded){_reportViewer.RefreshReport();loaded=true;}};
        }
        private void btnOdustani_Click(object sender,RoutedEventArgs e){Close();}
    }
}
