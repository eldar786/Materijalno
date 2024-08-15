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
    public class SifarnikSkladistaFormViewModel : INotifyPropertyChanged
    {
        private GlavniViewModel _gvm;
        private bool isSelectedUnosSifarnik;

        SifarnikSkladista sifarnikSkladista = new SifarnikSkladista();

        public ICommand SaveSifarnikSkladistaCommand { get; set; }
        public ICommand CancelSifarnikSkladistaCommand { get; set; }

        #region Objasnjenje prelaska iz jednog UserControlera u drugi 

        //Pozivamo ovaj konstruktor iz GlavniViewModel i proslijedjujemo u parametar instancu "glavniViewModel" da bi kasnije mogli 
        //promijeniti OdabraniVM: _gvm = glavniViewModel; + _gvm.OdabraniVM = new SifarnikSkladistaViewModel(_gvm);
        //tu istu instancu "_gvm vracamo u SifarnikSkladistaViewModel" parametar

        #endregion

        public SifarnikSkladistaFormViewModel(SifarnikSkladistaViewModel sifarnikSkladistaViewModel, GlavniViewModel glavniViewModel)
        {
            _gvm = glavniViewModel;
            isSelectedUnosSifarnik = sifarnikSkladistaViewModel.IsSelectedUnosSifarnik;
            SaveSifarnikSkladistaCommand = new RelayCommand(SaveSifarnikSkladista);
            CancelSifarnikSkladistaCommand = new RelayCommand(CancelSifarnikSkladista);
            if (sifarnikSkladistaViewModel.IsSelectedUnosSifarnik == false)
            {
                sifarnikSkladista = sifarnikSkladistaViewModel.SelectedSifarnikSkladista;
            }
        }

        //private bool ValidationForEach()
        //{
        //    foreach (var item in SifarnikSkladista)
        //    {
        //        if (string.IsNullOrEmpty(item.ToString()))
        //        {
        //            return false;
        //        }
        //    }
        //    return true;
        //}

        private bool ValidationSifarnikSkladista()
        {
            if (!SifarnikSkladista.Kljnaz.HasValue ||
                string.IsNullOrWhiteSpace(sifarnikSkladista.NazivOrg) ||
                //!SifarnikSkladista.ZiroRacun.HasValue ||
                //!SifarnikSkladista.PozBr.HasValue ||
                //!SifarnikSkladista.DevizniRacun.HasValue ||
                //!SifarnikSkladista.PozBrD.HasValue ||
                string.IsNullOrEmpty(SifarnikSkladista.Opstina) ||
                string.IsNullOrEmpty(SifarnikSkladista.MjestoAdresa))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void SaveSifarnikSkladista()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Ako je odabrana izmjena Button da radi Update na bazi
                if (isSelectedUnosSifarnik == false)
                {
                    if (ValidationSifarnikSkladista() == false)
                    {
                        System.Windows.MessageBox.Show("Molimo unesite sva polja", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    dbContext.Update(SifarnikSkladista);
                    dbContext.SaveChanges();

                    System.Windows.MessageBox.Show("Uspješno ste izmijenili šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                    //NAKON STO KLIKNEMO NA OK DA SE VRATI NA LISTU ŠIFARNIK SKLADIŠTA
                    _gvm.OdabraniVM = new SifarnikSkladistaViewModel(_gvm);
                }
                else
                {
                    if (ValidationSifarnikSkladista() == false)
                    {
                        System.Windows.MessageBox.Show("Molimo unesite sva polja", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    dbContext.Add(SifarnikSkladista);
                    dbContext.SaveChanges();

                    System.Windows.MessageBox.Show("Uspješno ste unijeli novi šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                    _gvm.OdabraniVM = new SifarnikSkladistaViewModel(_gvm);
                }
            }
        }

        private void CancelSifarnikSkladista()
        {
            _gvm.OdabraniVM = new SifarnikSkladistaViewModel(_gvm);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SifarnikSkladista SifarnikSkladista { get => sifarnikSkladista; set { sifarnikSkladista = value; OnPropertyChanged("SifarnikSkladista"); } }
    }
}
