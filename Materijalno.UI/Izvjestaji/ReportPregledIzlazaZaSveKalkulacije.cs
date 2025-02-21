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
    public class ReportPregledIzlazaZaSveKalkulacije
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
        public string NazivOrg { get; set; }
        public DateTime Od { get; set; }
        public DateTime Do { get; set; }
        public int? Konto1 { get; set; }

        public ObservableCollection<Mat> MatList;

        public PregledIzlazaZaSveKalkulacijeViewModel orgViewModel = new PregledIzlazaZaSveKalkulacijeViewModel();

        public List<PregledIzlazaZaSveKalkulacijeViewModel> GetAllOrgViewModel()
        {
            var list = new List<PregledIzlazaZaSveKalkulacijeViewModel>();

            var dbContext = new materijalno_knjigovodstvoContext();
            MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz1 >= 1000 && row.Kljnaz1 <= 1012 && row.Kljnaz >= 1000 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Datun)
                    .ToList());

            
            foreach (Mat m in MatList)
            {
                Ident = MatList.Select(row => row.Ident).First();
            }
            return list;
        }

    }
}
