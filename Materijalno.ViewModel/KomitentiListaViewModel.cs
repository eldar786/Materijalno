using Materijalno.Model;
using Materijalno.Model.EntityModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Materijalno.ViewModel
{
    public class KomitentiListaViewModel : ObservableObject
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

        public List<Komitenti> StaraSifra_Ime_List { get; set; }

        private Komitenti selectedKomitent;

        public ICommand OdaberiKomitentCommand { get; set; }
        public ICommand OdustaniCommand { get; set; }



        public KomitentiListaViewModel(UlazMaterijalaViewModel ulazMaterijalaViewModel, GlavniViewModel glavniViewModel)
        {
            StaraSifra_Ime_List = ulazMaterijalaViewModel.StaraSifra_Ime_List;
            CurrentItemMat = ulazMaterijalaViewModel.CurrentItemMat;
            _gvm = glavniViewModel;

            OdaberiKomitentCommand = new RelayCommand(OdaberiKomitent);
            OdustaniCommand = new RelayCommand(Odustani);
        }

        public KomitentiListaViewModel(IzlazMaterijalaViewModel izlazMaterijalaViewModel, GlavniViewModel glavniViewModel)
        {
            StaraSifra_Ime_List = izlazMaterijalaViewModel.StaraSifra_Ime_List;
            CurrentItemMat = izlazMaterijalaViewModel.CurrentItemMat;
            _gvm = glavniViewModel;



            OdaberiKomitentCommand = new RelayCommand(OdaberiKomitentIzlaz);
            OdustaniCommand = new RelayCommand(Odustani);
        }

        private void OdaberiKomitent()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (SelectedKomitent != null)
                {
                    CurrentItemMat.Analst = selectedKomitent.STARA_SIFRA;
                    dbContext.Update(this.currentItemMat);
                    dbContext.SaveChanges();
                }

                UlazMaterijalaViewModel.selectedKomitent = selectedKomitent;
                _gvm.OdabraniVM = new UlazMaterijalaViewModel(_gvm, CurrentItemMat);
            }
        }

        private void OdaberiKomitentIzlaz()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (SelectedKomitent != null)
                {
                    CurrentItemMat.Analst = selectedKomitent.STARA_SIFRA;
                    dbContext.Update(this.currentItemMat);
                    dbContext.SaveChanges();
                }

                IzlazMaterijalaViewModel.selectedKomitent = selectedKomitent;
                _gvm.OdabraniVM = new IzlazMaterijalaViewModel(_gvm, CurrentItemMat);
            }
        }

        private void Odustani()
        {
            _gvm.OdabraniVM = new UlazMaterijalaViewModel(_gvm, CurrentItemMat);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnpropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        #region Properties
        public Komitenti SelectedKomitent { get => selectedKomitent; set { selectedKomitent = value; OnPropertyChanged("SelectedKomitent"); } }

        #endregion
    }
}
