using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Materijalno.Model;
using System.Windows;
using System.Windows.Forms;
using Materijalno.Model.EntityModels;

namespace Materijalno.ViewModel
{
    public class SifarnikMaterijalSkladisteKontoViewModel : ObservableObject
    {
        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        public ObservableCollection<SifarnikMaterijalSkladisteKonto> SifarnikMaterijalSkladisteKontoList { get; set; }

        private SifarnikMaterijalSkladisteKonto selectedSifarnikMaterijalSkladisteKonto;
        private bool isSelectedUnosSifarnikMaterijalSkladisteKonto = false;

        public ICommand DeleteSifarnikMaterijalSkladisteKontoCommand { get; set; }
        public ICommand AddSifarnikMaterijalSkladisteKontoCommand { get; set; }
        public ICommand OpenSifarnikMaterijalSkladisteKontoFormCommand { get; set; }
        public ICommand IzmjenaSifarnikMaterijalSkladisteKontoFormCommand { get; set; }

        public SifarnikMaterijalSkladisteKontoViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                SifarnikMaterijalSkladisteKontoList = new ObservableCollection<SifarnikMaterijalSkladisteKonto>(dbContext.SifarnikMaterijalSkladisteKonto.ToList());

                DeleteSifarnikMaterijalSkladisteKontoCommand = new RelayCommand(DeleteSifarnikMaterijalSkladisteKonto);
                AddSifarnikMaterijalSkladisteKontoCommand = new RelayCommand(AddSifarnikMaterijalSkladisteKonto);
                OpenSifarnikMaterijalSkladisteKontoFormCommand = new RelayCommand(OpenSifarnikMaterijalSkladisteKontoForm);
                IzmjenaSifarnikMaterijalSkladisteKontoFormCommand = new RelayCommand(IzmjenaSifarnikMaterijalSkladisteKontoForm);
                //Dodati ostale commande
            }
        }

        private void OpenSifarnikMaterijalSkladisteKontoForm()
        {
            isSelectedUnosSifarnikMaterijalSkladisteKonto = true;
            _gvm.OdabraniVM = new SifarnikMaterijalSkladisteKontoFormViewModel(this, _gvm);
        }

        private void IzmjenaSifarnikMaterijalSkladisteKontoForm()
        {
            if (selectedSifarnikMaterijalSkladisteKonto == null)
            {
                System.Windows.MessageBox.Show("Niste odabrali sifarnik", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }
            _gvm.OdabraniVM = new SifarnikMaterijalSkladisteKontoFormViewModel(this, _gvm);
        }

        private void DeleteSifarnikMaterijalSkladisteKonto()
        {
            if (SelectedSifarnikMaterijalSkladisteKonto != null)
            {
                using (var dbContext = new materijalno_knjigovodstvoContext())
                {
                    var resultMessageBox = System.Windows.MessageBox.Show("Da li ste sigurni da želite obrisati sifarnik ", "Upozorenje", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (resultMessageBox == MessageBoxResult.Yes)
                    {
                        dbContext.SifarnikMaterijalSkladisteKonto.Remove(SelectedSifarnikMaterijalSkladisteKonto);
                        dbContext.SaveChanges();

                        SifarnikMaterijalSkladisteKontoList.Remove(SelectedSifarnikMaterijalSkladisteKonto);
                    }
                    else if (resultMessageBox == MessageBoxResult.No)
                    {
                        return;
                    }

                    // Uraditi message box provjeru da li zelimo da obrisemo
                    //SifarnikSkladistaList.Remove(SelectedSifarnikSkladista);

                    //SelectedSifarnikSkladista = null;
                }
            }
            else if (SelectedSifarnikMaterijalSkladisteKonto == null)
            {
                System.Windows.MessageBox.Show("Niste odabrali sifarnik za brisanje", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddSifarnikMaterijalSkladisteKonto()
        {
            if (SelectedSifarnikMaterijalSkladisteKonto != null)
            {
                using (var dbContext = new materijalno_knjigovodstvoContext())
                {
                    dbContext.SifarnikMaterijalSkladisteKonto.Add(SelectedSifarnikMaterijalSkladisteKonto);
                    dbContext.SaveChanges();

                    SifarnikMaterijalSkladisteKontoList.Add(SelectedSifarnikMaterijalSkladisteKonto);

                    System.Windows.MessageBox.Show("Sifra skladista uspješno unesena", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnpropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }


        public SifarnikMaterijalSkladisteKonto SelectedSifarnikMaterijalSkladisteKonto { get => selectedSifarnikMaterijalSkladisteKonto; set { selectedSifarnikMaterijalSkladisteKonto = value; OnPropertyChanged("SelectedSifarnikMaterijalSkladisteKonto"); } }
        public bool IsSelectedUnosSifarnikMaterijalSkladisteKonto { get => isSelectedUnosSifarnikMaterijalSkladisteKonto; set { isSelectedUnosSifarnikMaterijalSkladisteKonto = value; OnPropertyChanged("IsSelectedUnosSifarnikMaterijalSkladisteKonto"); } }
    }
}