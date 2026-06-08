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
        private Inv currentItemInv;
        private Mat _originalItemMat;

        public Mat CurrentItemMat
        {
            get { return currentItemMat; }
            set
            {
                currentItemMat = value;
                OnPropertyChanged(nameof(CurrentItemMat));
            }
        }
        public Inv CurrentItemInv
        {
            get { return currentItemInv; }
            set
            {
                currentItemInv = value;
                OnPropertyChanged(nameof(CurrentItemInv));
            }
        }

        public ObservableCollection<Mat> MatList { get; set; }
        public ObservableCollection<Inv> InvList { get; set; }

        private Mat selectedMat;
        private Inv selectedInv;
        public Mat SelectedMat { get => selectedMat; set { selectedMat = value; OnPropertyChanged("SelectedMat"); } }
        public Inv SelectedInv { get => selectedInv; set { selectedInv = value; OnPropertyChanged("SelectedInv"); } }

        public ICommand OdaberiMatCommand { get; set; }
        public ICommand OdustaniODdabirCommand { get; set; }

        public MatListaViewModel(UlazMaterijalaViewModel ulazMaterijalaViewModel, GlavniViewModel glavniViewModel)
        {
            MatList = ulazMaterijalaViewModel.MatList;
            _gvm = glavniViewModel;
            CurrentItemMat = ulazMaterijalaViewModel.CurrentItemMat;
            OdaberiMatCommand = new RelayCommand(OdaberiMat);
        }
        
        public MatListaViewModel(IzlazMaterijalaViewModel izlazMaterijalaViewModel, GlavniViewModel glavniViewModel)
        {
            MatList = izlazMaterijalaViewModel.MatList;
            _gvm = glavniViewModel;
            CurrentItemMat = izlazMaterijalaViewModel.CurrentItemMat;
            OdaberiMatCommand = new RelayCommand(OdaberiMatIzlaz);
        }
        
        public MatListaViewModel(MedjuskladisnicaViewModel medjuskladisnicaViewModel, GlavniViewModel glavniViewModel)
        {
            MatList = medjuskladisnicaViewModel.MatList;
            _gvm = glavniViewModel;
            CurrentItemMat = medjuskladisnicaViewModel.CurrentItemMat;
            OdaberiMatCommand = new RelayCommand(OdaberiMatMedjuskladisnica);
        }

        public MatListaViewModel(PovratMaterijalaViewModel povratMaterijalaViewModel, GlavniViewModel glavniViewModel)
        {
            MatList = povratMaterijalaViewModel.MatList;
            _gvm = glavniViewModel;
            CurrentItemMat = povratMaterijalaViewModel.CurrentItemMat;
            OdaberiMatCommand = new RelayCommand(OdaberiPovrat);
        }

        public MatListaViewModel(PocetnoStanjePrviPutViewModel pocetnoStanjePrviPutViewModel, GlavniViewModel glavniViewModel)
        {
            MatList = pocetnoStanjePrviPutViewModel.MatList;
            _gvm = glavniViewModel;
            CurrentItemMat = pocetnoStanjePrviPutViewModel.CurrentItemMat;
            OdaberiMatCommand = new RelayCommand(OdaberiPocetnoStanje);
            _originalItemMat = CurrentItemMat;
            OdustaniODdabirCommand = new RelayCommand(OdustaniPocetnoStanje);
        }

        private void OdustaniPocetnoStanje()
        {

            CurrentItemMat = _originalItemMat;

            PocetnoStanjePrviPutViewModel.isTraziClicked = false;
            PocetnoStanjePrviPutViewModel.selectedMat = null;

            _gvm.OdabraniVM = new PocetnoStanjePrviPutViewModel(_gvm, _originalItemMat);
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

        private void OdaberiPovrat()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (SelectedMat != null)
                {
                    CurrentItemMat = SelectedMat;
                }

                PovratMaterijalaViewModel.isTraziClicked = true;
                PovratMaterijalaViewModel.selectedMat = selectedMat;
                _gvm.OdabraniVM = new PovratMaterijalaViewModel(_gvm, CurrentItemMat);
            }
        }
        
        private void OdaberiPocetnoStanje()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (SelectedMat != null)
                {
                    CurrentItemMat = SelectedMat;
                }

                PocetnoStanjePrviPutViewModel.isTraziClicked = true;
                PocetnoStanjePrviPutViewModel.selectedMat = selectedMat;
                _gvm.OdabraniVM = new PocetnoStanjePrviPutViewModel(_gvm, CurrentItemMat);
            }
        }

        //private void OdaberiUnosInventurnogStanja()
        //{
        //    using (var dbContext = new materijalno_knjigovodstvoContext())
        //    {
        //        if (SelectedInv != null)
        //        {
        //            CurrentItemInv = SelectedInv;
        //        }

        //        UnosInventurnogStanjaViewModel.isTraziClicked = true;
        //        UnosInventurnogStanjaViewModel.selectedInv = selectedInv;
        //        _gvm.OdabraniVM = new UnosInventurnogStanjaViewModel(_gvm, CurrentItemInv);
        //    }
        //}

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
        private void OdaberiMatMedjuskladisnica()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (SelectedMat != null)
                {
                    CurrentItemMat = SelectedMat;
                }

                MedjuskladisnicaViewModel.isTraziClicked = true;
                MedjuskladisnicaViewModel.selectedMat = selectedMat;
                _gvm.OdabraniVM = new MedjuskladisnicaViewModel(_gvm, CurrentItemMat);
            }

        }
    }
}
