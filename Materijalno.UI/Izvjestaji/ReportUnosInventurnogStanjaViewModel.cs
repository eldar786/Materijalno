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
    public class ReportUnosInventurnogStanjaViewModel
    {
        public int Id { get; set; }
        public int Kljnaz { get; set; }
        public int? Ident { get; set; }
        public DateTime? Datun { get; set; }
        public string Analst { get; set; }
        public string Brdok { get; set; }
        public string Brnar { get; set; }
        public DateTime? Datnar { get; set; }
        public string Brfak { get; set; }
        public int? Kolic { get; set; }
        public decimal Nc { get; set; }
        public decimal Vrijed { get; set; }
        public int? Redbr { get; set; }
        public string Status { get; set; }
        public int Konto1 { get; set; }
        public string Konto2 { get; set; }
        public string Fcj { get; set; }
        public decimal? Troskovi { get; set; }
        public string Cartro { get; set; }
        public string Zavtro { get; set; }
        public string Ppp { get; set; }
        public string Tarifa { get; set; }
        public string Fvrijed { get; set; }
        public decimal? Porppp { get; set; }
        public int? Kontosklad { get; set; }
        public int? Kontosklad1 { get; set; }
        public decimal? Trospe { get; set; }
        public int? Ourst { get; set; }
        public int? Mjtst { get; set; }
        public string Nazkont { get; set; }
        public string NazivOrg { get; set; }
        public string NazMat { get; set; }

        public ObservableCollection<Inv> InvList;

        public UnosInventurnogStanjaViewModel orgViewModel = new UnosInventurnogStanjaViewModel();

        public List<UnosInventurnogStanjaViewModel> GetAllOrgViewModel()
        {
            var list = new List<UnosInventurnogStanjaViewModel>();

            var dbContext = new materijalno_knjigovodstvoContext();
            InvList = new ObservableCollection<Inv>(dbContext.Inv
                    .Where(row =>  row.Kljnaz >= 1000 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Datun)
                    .ToList());


            foreach (Inv i in InvList)
            {
                Ident = InvList.Select(row => row.Ident).First();
            }
            return list;
        }

    }
}
