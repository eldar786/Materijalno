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
    public class ReportSkladiste
    {
        public int? Kljnaz { get; set; }
        public string NazivOrg { get; set; }
        public string Opstina { get; set; }
        public string MjestoAdresa { get; set; }

        public ObservableCollection<Materijalno.Model.EntityModels.SifarnikSkladista> SkaldistaList { get; set; }

        public PovratMaterijalaViewModel orgViewModel = new PovratMaterijalaViewModel();

        public List<PovratMaterijalaViewModel> GetAllOrgViewModel()
        {
            var list = new List<PovratMaterijalaViewModel>();

            var dbContext = new materijalno_knjigovodstvoContext();
            SkaldistaList = new ObservableCollection<Materijalno.Model.EntityModels.SifarnikSkladista>(dbContext.SifarnikSkladista
                    .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Kljnaz)
                    .ToList());

            //Ident = dbContext.Mat.Select(row => row.Ident);
            foreach (Materijalno.Model.EntityModels.SifarnikSkladista s in SkaldistaList)
            {
                Kljnaz = SkaldistaList.Select(row => row.Kljnaz).First();
                //list.Add(new Mat { Ident = MatList.Select(row => row.Ident).First() });
                //list.Add(new MedjuskladisnicaViewModel { Od = orgViewModel.MatList });
                //list.Add(new MedjuskladisnicaViewModel { Do = orgViewModel.Do });
            }
            return list;
        }

    }
}