using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Materijalno.Model;
using Materijalno.Model.EntityModels;

namespace Materijalno.ViewModel
{
    public class SifarnikMaterijalaFormViewModel
    {
        SifarnikMaterijala sifarnikMaterijala = new SifarnikMaterijala();
        private GlavniViewModel _gvm;

        public SifarnikMaterijalaFormViewModel(SifarnikMaterijalaViewModel sifarnikMaterijalaViewModel, GlavniViewModel glavniViewModel)
        {
            _gvm = glavniViewModel;
            sifarnikMaterijala = sifarnikMaterijalaViewModel.SelectedSifarnikMaterijala;
        }
    }
}