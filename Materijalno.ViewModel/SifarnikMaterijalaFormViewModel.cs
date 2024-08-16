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
    public class SifarnikMaterijalaFormViewModel : INotifyPropertyChanged
    {
        private GlavniViewModel _gvm;
        private bool isSelectedUnosSifarnik;

        SifarnikMaterijala sifarnikMaterijala = new SifarnikMaterijala();

        public ICommand SaveSifarnikMaterijalaCommand { get; set; }
        public ICommand CancelSifarnikMaterijalaCommand { get; set; }


        public SifarnikMaterijalaFormViewModel(SifarnikMaterijalaViewModel sifarnikMaterijalaViewModel, GlavniViewModel glavniViewModel)
        {
            _gvm = glavniViewModel;
            isSelectedUnosSifarnik = sifarnikMaterijalaViewModel.IsSelectedUnosSifarnikMaterijala;
            SaveSifarnikMaterijalaCommand = new RelayCommand(SaveSifarnikMterijala);
            CancelSifarnikMaterijalaCommand = new RelayCommand(CancelSifarnikMaterijala);
            if (sifarnikMaterijalaViewModel.IsSelectedUnosSifarnikMaterijala == false)
            {
                sifarnikMaterijala = sifarnikMaterijalaViewModel.SelectedSifarnikMaterijala;
            }
        }


        private bool ValidationSifarnikMaterijala()
        {
            if (!SifarnikMaterijala.Ident.HasValue ||
                string.IsNullOrWhiteSpace(sifarnikMaterijala.Nazmat) ||
                string.IsNullOrWhiteSpace(sifarnikMaterijala.Jedm) ||
                !SifarnikMaterijala.Konto1.HasValue ||
                !SifarnikMaterijala.Konto2.HasValue )

            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void SaveSifarnikMterijala()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Ako je odabrana izmjena Button da radi Update na bazi
                if (isSelectedUnosSifarnik == false)
                {
                    if (ValidationSifarnikMaterijala() == false)
                    {
                        System.Windows.MessageBox.Show("Molimo unesite sva polja", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    dbContext.Update(SifarnikMaterijala);
                    dbContext.SaveChanges();

                    System.Windows.MessageBox.Show("Uspješno ste izmijenili šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                    //NAKON STO KLIKNEMO NA OK DA SE VRATI NA LISTU ŠIFARNIK SKLADIŠTA
                    _gvm.OdabraniVM = new SifarnikSkladistaViewModel(_gvm);
                }
                else
                {
                    if (ValidationSifarnikMaterijala() == false)
                    {
                        System.Windows.MessageBox.Show("Molimo unesite sva polja", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    dbContext.Add(SifarnikMaterijala);
                    dbContext.SaveChanges();

                    System.Windows.MessageBox.Show("Uspješno ste unijeli novi šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                    _gvm.OdabraniVM = new SifarnikSkladistaViewModel(_gvm);
                }
            }
        }

        private void CancelSifarnikMaterijala()
        {
            _gvm.OdabraniVM = new SifarnikSkladistaViewModel(_gvm);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SifarnikMaterijala SifarnikMaterijala { get => sifarnikMaterijala; set { sifarnikMaterijala = value; OnPropertyChanged("SifarnikMaterijala"); } }
    
    }
}