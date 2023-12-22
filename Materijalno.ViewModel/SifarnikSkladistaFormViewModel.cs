using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Materijalno.Model;
using Materijalno.Model.EntityModels;

namespace Materijalno.ViewModel
{
    public class SifarnikSkladistaFormViewModel : ObservableObject
    {
        private GlavniViewModel _gvm;

        SifarnikSkladista sifarnikSkladista;
        public SifarnikSkladistaFormViewModel(SifarnikSkladistaViewModel sifarnikSkladistaViewModel)
        {
            //_gvm = gvm;
            sifarnikSkladista = sifarnikSkladistaViewModel.SelectedSifarnikSkladista;

        }

        public SifarnikSkladista SifarnikSkladista { get => sifarnikSkladista; set { sifarnikSkladista = value; OnPropertyChanged("SifarnikSkladista"); } }

    }
}
