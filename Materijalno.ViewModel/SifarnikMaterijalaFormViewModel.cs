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
            if (string.IsNullOrWhiteSpace(sifarnikMaterijala.Nazmat) ||
                string.IsNullOrWhiteSpace(sifarnikMaterijala.Jedm) ||
                !SifarnikMaterijala.Ident.HasValue ||
                !SifarnikMaterijala.Konto1.HasValue ||
                !SifarnikMaterijala.Konto2.HasValue )

            {
                return false;
            }
            using (var context = new materijalno_knjigovodstvoContext())
            {
                // Check if the Ident value already exists in the database
                bool exists = context.SifarnikMaterijala
                    .Any(s => s.Ident == sifarnikMaterijala.Ident && s.Id != sifarnikMaterijala.Id);

                if (exists)
                {
                    // Show an error message or throw an exception depending on your setup
                    System.Windows.MessageBox.Show("Šifra materijala već postoji", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            return true;
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

                    //NAKON STO KLIKNEMO NA OK DA SE VRATI NA LISTU ŠIFARNIK MATERIJALA
                    _gvm.OdabraniVM = new SifarnikMaterijalaViewModel(_gvm);
                }
                else
                {
                    if (ValidationSifarnikMaterijala() == false)
                    {
                        System.Windows.MessageBox.Show("Molimo unesite sva polja", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    SifarnikMaterijala.Siftar = 1;
                    dbContext.Add(SifarnikMaterijala);
                    dbContext.SaveChanges();

                    System.Windows.MessageBox.Show("Uspješno ste unijeli novi šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                    _gvm.OdabraniVM = new SifarnikMaterijalaViewModel(_gvm);
                }
            }
        }

        private void CancelSifarnikMaterijala()
        {
            _gvm.OdabraniVM = new SifarnikMaterijalaViewModel(_gvm);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SifarnikMaterijala SifarnikMaterijala { get => sifarnikMaterijala; set { sifarnikMaterijala = value; OnPropertyChanged("SifarnikMaterijala"); } }
    
    }
}