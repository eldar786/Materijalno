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
        public int? Kljnaz { get; set; }
        public int? Kljnaz1 { get; set; }
        public int? Ident { get; set; }
        public DateTime Datun { get; set; }
        public string NazMat { get; set; }
        public string Brdok { get; set; }
        public string Brnar { get; set; }
        public DateTime Datnar { get; set; }
        public string Brfak { get; set; }
        public int? Kolic { get; set; }
        public decimal? Nc { get; set; }
        public decimal? Vrijed { get; set; }
        public int? Redbr { get; set; }
        public string Status { get; set; }
        public string Cartro { get; set; }
        public string Medus { get; set; }
        public decimal? TotalVrijednost { get; set; }

        public ObservableCollection<Mat> MatList;

        public NaloziZaFinansijePrintanjeNalogaViewModel orgViewModel = new NaloziZaFinansijePrintanjeNalogaViewModel();

        public List<NaloziZaFinansijePrintanjeNalogaViewModel> GetAllOrgViewModel()
        {
            var list = new List<NaloziZaFinansijePrintanjeNalogaViewModel>();

            var dbContext = new materijalno_knjigovodstvoContext();
            MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz1 >= 1000 && row.Kljnaz1 <= 1012 && row.Kljnaz >= 1000 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Datun)
                    .ToList());

            //Ident = dbContext.Mat.Select(row => row.Ident);
            foreach (Mat m in MatList)
            {
                Ident = MatList.Select(row => row.Ident).First();
            }
            return list;
        }
    }
}
