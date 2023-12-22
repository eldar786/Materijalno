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


        public SifarnikSkladistaFormViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;

        }
    }
}
