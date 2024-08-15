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
    public class SifarnikKontaFormViewModel : INotifyPropertyChanged
    {
        private GlavniViewModel _gvm;
        private bool isSelectedUnosSifarnik;

        SifarnikKonta sifarnikKonta = new SifarnikKonta();

        public ICommand SaveSifarnikKontaCommand { get; set; }
        public ICommand CancelSifarnikKontaCommand { get; set; }

        public SifarnikKontaFormViewModel(SifarnikKontaViewModel sifarnikKontaViewModel, GlavniViewModel glavniViewModel)
        {
            _gvm = glavniViewModel;
            isSelectedUnosSifarnik = sifarnikKontaViewModel.IsSelectedUnosSifarnik;
            SaveSifarnikKontaCommand = new RelayCommand(SaveSifarnikKonta);
            CancelSifarnikKontaCommand = new RelayCommand(CancelSifarnikKonta);
            if (sifarnikKontaViewModel.IsSelectedUnosSifarnik == false)
            {
                sifarnikKonta = sifarnikKontaViewModel.SelectedSifarnikKonta;
            }
        }

        private bool ValidationSifarnikKonta()
        {
            if (!SifarnikKonta.Sifkonta.HasValue ||
                string.IsNullOrWhiteSpace(sifarnikKonta.Nazkont) ||
                !SifarnikKonta.Stakont.HasValue ||
                !SifarnikKonta.Stamt.HasValue)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void SaveSifarnikKonta()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Ako je odabrana izmjena Button da radi Update na bazi
                if (isSelectedUnosSifarnik == false)
                {
                    if (ValidationSifarnikKonta() == false)
                    {
                        System.Windows.MessageBox.Show("Molimo unesite sva polja", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    dbContext.Update(SifarnikKonta);
                    dbContext.SaveChanges();

                    System.Windows.MessageBox.Show("Uspješno ste izmijenili šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                    //NAKON STO KLIKNEMO NA OK DA SE VRATI NA LISTU ŠIFARNIK SKLADIŠTA
                    _gvm.OdabraniVM = new SifarnikKontaViewModel(_gvm);
                }
                else
                {
                    if (ValidationSifarnikKonta() == false)
                    {
                        System.Windows.MessageBox.Show("Molimo unesite sva polja", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    dbContext.Add(SifarnikKonta);
                    dbContext.SaveChanges();

                    System.Windows.MessageBox.Show("Uspješno ste unijeli novi šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                    _gvm.OdabraniVM = new SifarnikKontaViewModel(_gvm);
                }
            }
        }

        private void CancelSifarnikKonta()
        {
            _gvm.OdabraniVM = new SifarnikKontaViewModel(_gvm);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SifarnikKonta SifarnikKonta { get => sifarnikKonta; set { sifarnikKonta = value; OnPropertyChanged("SifarnikKonta"); } }
    }
}
