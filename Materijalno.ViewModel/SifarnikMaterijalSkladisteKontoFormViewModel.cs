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
    public class SifarnikMaterijalSkladisteKontoFormViewModel : INotifyPropertyChanged
    {
        private GlavniViewModel _gvm;
        private bool isSelectedUnosSifarnikMaterijalSkladisteKonto;

        SifarnikMaterijalSkladisteKonto sifarnikMaterijalSkladisteKonto = new SifarnikMaterijalSkladisteKonto();

        public ICommand SaveSifarnikMaterijalSkladisteKontoCommand { get; set; }
        public ICommand CancelSifarnikMaterijalSkladisteKontoCommand { get; set; }


        public SifarnikMaterijalSkladisteKontoFormViewModel(SifarnikMaterijalSkladisteKontoViewModel sifarnikMaterijalSkladisteKontoForm, GlavniViewModel glavniViewModel)
        {
            _gvm = glavniViewModel;
            isSelectedUnosSifarnikMaterijalSkladisteKonto = sifarnikMaterijalSkladisteKontoForm.IsSelectedUnosSifarnikMaterijalSkladisteKonto;
            SaveSifarnikMaterijalSkladisteKontoCommand = new RelayCommand(SaveSifarnikMaterijalSkladisteKonto);
            CancelSifarnikMaterijalSkladisteKontoCommand = new RelayCommand(CancelSifarnikMaterijalSkladisteKonto);
            if (sifarnikMaterijalSkladisteKontoForm.IsSelectedUnosSifarnikMaterijalSkladisteKonto == false)
            {
                sifarnikMaterijalSkladisteKonto = sifarnikMaterijalSkladisteKontoForm.SelectedSifarnikMaterijalSkladisteKonto;
            }
        }

        private bool ValidationSifarnikMaterijalSkladisteKonto()
        {
            if (!sifarnikMaterijalSkladisteKonto.Sifmat.HasValue ||
                !sifarnikMaterijalSkladisteKonto.Sifskla.HasValue ||
                !sifarnikMaterijalSkladisteKonto.Sifkonta.HasValue)
            {
                return false;
            }

            using (var context = new materijalno_knjigovodstvoContext())
            {
                bool exists = context.SifarnikMaterijalSkladisteKonto.Any(s =>
                    s.Id != sifarnikMaterijalSkladisteKonto.Id && s.Sifmat == sifarnikMaterijalSkladisteKonto.Sifmat && s.Sifskla == sifarnikMaterijalSkladisteKonto.Sifskla && s.Sifkonta == sifarnikMaterijalSkladisteKonto.Sifkonta);

                if (exists)
                {
                    System.Windows.MessageBox.Show("Već postoji zapis sa istim šifrom materijala, skladištem i kontom.", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            return true;
        }


        private void SaveSifarnikMaterijalSkladisteKonto()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Ako je odabrana izmjena Button da radi Update na bazi
                if (isSelectedUnosSifarnikMaterijalSkladisteKonto == false)
                {
                    if (ValidationSifarnikMaterijalSkladisteKonto() == false)
                    {
                        System.Windows.MessageBox.Show("Molimo unesite sva polja", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    dbContext.Update(sifarnikMaterijalSkladisteKonto);
                    dbContext.SaveChanges();

                    System.Windows.MessageBox.Show("Uspješno ste izmijenili šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                    //NAKON STO KLIKNEMO NA OK DA SE VRATI NA LISTU ŠIFARNIK SKLADIŠTA
                    _gvm.OdabraniVM = new SifarnikMaterijalSkladisteKontoViewModel(_gvm);
                }
                else
                {
                    if (ValidationSifarnikMaterijalSkladisteKonto() == false)
                    {
                        System.Windows.MessageBox.Show("Molimo unesite sva polja", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    dbContext.Add(sifarnikMaterijalSkladisteKonto);
                    dbContext.SaveChanges();

                    System.Windows.MessageBox.Show("Uspješno ste unijeli novi šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                    _gvm.OdabraniVM = new SifarnikMaterijalSkladisteKontoViewModel(_gvm);
                }
            }
        }

        private void CancelSifarnikMaterijalSkladisteKonto()
        {
            _gvm.OdabraniVM = new SifarnikMaterijalSkladisteKontoViewModel(_gvm);
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SifarnikMaterijalSkladisteKonto SifarnikMaterijalSkladisteKonto { get => sifarnikMaterijalSkladisteKonto; set { sifarnikMaterijalSkladisteKonto = value; OnPropertyChanged("SifarnikMaterijalSkladisteKonto"); } }
    }
}