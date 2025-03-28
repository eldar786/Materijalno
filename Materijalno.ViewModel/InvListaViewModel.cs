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
    public class InvListaViewModel : ObservableObject
    {

        private GlavniViewModel _gvm;
        private Inv currentItemInv;

        public Inv CurrentItemInv
        {
            get { return currentItemInv; }
            set
            {
                currentItemInv = value;
                OnPropertyChanged(nameof(CurrentItemInv));
            }
        }

        public ObservableCollection<Inv> InvList { get; set; }

        private Inv selectedInv;
        public Inv SelectedInv { get => selectedInv; set { selectedInv = value; OnPropertyChanged("SelectedInv"); } }

        public ICommand OdaberiMatCommand { get; set; }
        public ICommand OdustaniCommand { get; set; }

        public InvListaViewModel(UnosInventurnogStanjaViewModel unosInventurnogStanjaViewModel, GlavniViewModel glavniViewModel)
        {
            InvList = unosInventurnogStanjaViewModel.InvList;
            _gvm = glavniViewModel;
            CurrentItemInv = unosInventurnogStanjaViewModel.CurrentItemInv;
            OdaberiMatCommand = new RelayCommand(OdaberiUnosInventurnogStanja);
        }
        
        public InvListaViewModel(StampanjeInventurnihZalihaViewModel stampanjeInventurnihZalihaViewModel, GlavniViewModel glavniViewModel)
        {
            InvList = stampanjeInventurnihZalihaViewModel.InvList;
            _gvm = glavniViewModel;
            CurrentItemInv = stampanjeInventurnihZalihaViewModel.CurrentItemInv;
            OdaberiMatCommand = new RelayCommand(OdaberiStampanjeInventurnihZaliha);
        }


        private void OdaberiUnosInventurnogStanja()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (SelectedInv != null)
                {
                    CurrentItemInv = SelectedInv;
                }

                UnosInventurnogStanjaViewModel.isTraziClicked = true;
                UnosInventurnogStanjaViewModel.selectedInv = selectedInv;
                _gvm.OdabraniVM = new UnosInventurnogStanjaViewModel(_gvm, CurrentItemInv);
            }
        }
        
        private void OdaberiStampanjeInventurnihZaliha()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (SelectedInv != null)
                {
                    CurrentItemInv = SelectedInv;
                }

                StampanjeInventurnihZalihaViewModel.isTraziClicked = true;
                StampanjeInventurnihZalihaViewModel.selectedInv = selectedInv;
                _gvm.OdabraniVM = new StampanjeInventurnihZalihaViewModel(_gvm, CurrentItemInv);
            }
        }
    }
}
