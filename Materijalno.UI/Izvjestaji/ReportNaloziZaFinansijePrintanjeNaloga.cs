using Materijalno.Model.EntityModels;
using Materijalno.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Materijalno.UI.Izvjestaji
{
    public class ReportNaloziZaFinansijePrintanjeNaloga
    {
        public int Id { get; set; }
        public int Rostav { get; set; }
        public DateTime Datnsta { get; set; }
        public int Sifakt { get; set; }
        public int Bronsta { get; set; }
        public int Sintstav { get; set; }
        public string Brdokst { get; set; }
        public int Kukistav { get; set; }
        public DateTime Datdokst { get; set; }
        public int? Devsta { get; set; }
        public int? Kurs { get; set; }
        public string Datvalst { get; set; }
        public int? Ourst { get; set; }
        public int? Mjtst { get; set; }
        public int? Analst { get; set; }
        public decimal? Dug1st { get; set; }
        public int? Stodug1 { get; set; }
        public decimal? Pot1st { get; set; }
        public int? Stopot1 { get; set; }
        public int? Devdugst { get; set; }
        public int? Stodevd { get; set; }
        public int? Devpotst { get; set; }
        public int? Stodevp { get; set; }
        public int? Bracuna { get; set; }
        public string Brfak { get; set; }
        public string Nazkont { get; set; }
        

        public ObservableCollection<Nalmat> NalMatList;

        public NaloziZaFinansijePrintanjeNalogaViewModel orgViewModel = new NaloziZaFinansijePrintanjeNalogaViewModel();

        public List<NaloziZaFinansijePrintanjeNalogaViewModel> GetAllOrgViewModel()
        {
            var list = new List<NaloziZaFinansijePrintanjeNalogaViewModel>();

            var dbContext = new materijalno_knjigovodstvoContext();
            NalMatList = new ObservableCollection<Nalmat>(dbContext.Nalmat
                    .OrderBy(row => row.Datnsta)
                    .ToList());

            
            foreach (Nalmat m in NalMatList)
            {
                Id = NalMatList.Select(row => row.Id).First();
            }
            return list;
        }
    }
}
