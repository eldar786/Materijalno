using Materijalno.Model;
using Materijalno.Model.EntityModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Materijalno.ViewModel
{
    public class MatListaViewModel : ObservableObject
    {
        private GlavniViewModel _gvm;
        private Mat currentItemMat;

        public Mat CurrentItemMat
        {
            get { return currentItemMat; }
            set
            {
                currentItemMat = value;
                OnPropertyChanged(nameof(CurrentItemMat));
            }
        }

        public ObservableCollection<Mat> MatList { get; set; }

        private Mat selectedMat;
        public Mat SelectedMat { get => selectedMat; set { selectedMat = value; OnPropertyChanged("SelectedMat"); } }

        public ICommand OdaberiMatCommand { get; set; }
        public ICommand OdustaniCommand { get; set; }

        public MatListaViewModel(UlazMaterijalaViewModel ulazMaterijalaViewModel, GlavniViewModel glavniViewModel)
        {
            MatList = ulazMaterijalaViewModel.MatList;
            _gvm = glavniViewModel;
            CurrentItemMat = ulazMaterijalaViewModel.CurrentItemMat;
            OdaberiMatCommand = new RelayCommand(OdaberiMat);
        }
        
        public MatListaViewModel(IzlazMaterijalaViewModel ilazMaterijalaViewModel, GlavniViewModel glavniViewModel)
        {
            MatList = ilazMaterijalaViewModel.MatList;
            _gvm = glavniViewModel;
            CurrentItemMat = ilazMaterijalaViewModel.CurrentItemMat;
            OdaberiMatCommand = new RelayCommand(OdaberiMatIzlaz);
        }



        private void OdaberiMat()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (SelectedMat != null)
                {
                    CurrentItemMat = SelectedMat;
                }

                UlazMaterijalaViewModel.isTraziClicked = true;
                UlazMaterijalaViewModel.selectedMat = selectedMat;
                _gvm.OdabraniVM = new UlazMaterijalaViewModel(_gvm, CurrentItemMat);
            }
        }

        private void OdaberiMatIzlaz()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (SelectedMat != null)
                {
                    CurrentItemMat = SelectedMat;
                }

                IzlazMaterijalaViewModel.isTraziClicked = true;
                IzlazMaterijalaViewModel.selectedMat = selectedMat;
                _gvm.OdabraniVM = new IzlazMaterijalaViewModel(_gvm, CurrentItemMat);
            }

        }
    }
}
