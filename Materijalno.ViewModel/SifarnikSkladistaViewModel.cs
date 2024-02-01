using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Materijalno.Model;
using System.Windows;
using System.Windows.Forms;
using Materijalno.Model.EntityModels;

namespace Materijalno.ViewModel
{
    public class SifarnikSkladistaViewModel : ObservableObject
    {
        #region Fields and Lists

        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;

        private SifarnikSkladista selectedSifarnikSkladista;
        private bool isSelectedUnosSifarnik = false;

        public ObservableCollection<SifarnikSkladista> SifarnikSkladistaList { get; set; }

        #endregion

        #region Commands
        public ICommand DeleteSifarnikSkladistaCommand { get; set; }
        public ICommand AddSifarnikSkladistaCommand { get; set; }
        public ICommand OpenSifarnikSkladistaFormCommand { get; set; }
        public ICommand IzmjenaSifarnikSkladistaFormCommand { get; set; }

        #endregion

        #region Constructor
        public SifarnikSkladistaViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                SifarnikSkladistaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.ToList());

                DeleteSifarnikSkladistaCommand = new RelayCommand(DeleteSifarnikSkladista);
                AddSifarnikSkladistaCommand = new RelayCommand(AddSifarnikSkladista);
                OpenSifarnikSkladistaFormCommand = new RelayCommand(OpenSifarnikSkladistaForm);
                IzmjenaSifarnikSkladistaFormCommand = new RelayCommand(IzmjenaSifarnikSkladistaForm);
            }
        }
        #endregion

        #region Otvaranje Sifarnik Skladista Form (Unos & Izmjena)
        private void OpenSifarnikSkladistaForm()
        {
            isSelectedUnosSifarnik = true;
            _gvm.OdabraniVM = new SifarnikSkladistaFormViewModel(this, _gvm);
        }

        private void IzmjenaSifarnikSkladistaForm()
        {
            if (selectedSifarnikSkladista == null)
            {
                System.Windows.MessageBox.Show("Niste odabrali sifarnik", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }
            _gvm.OdabraniVM = new SifarnikSkladistaFormViewModel(this, _gvm);
        }

        #endregion

        #region Brisanje sifarnika
        private void DeleteSifarnikSkladista()
        {
            if (SelectedSifarnikSkladista != null)
            {
                using (var dbContext = new materijalno_knjigovodstvoContext())
                {
                   var resultMessageBox = System.Windows.MessageBox.Show("Da li ste sigurni da želite obrisati sifarnik 'Kljnaz': " + SelectedSifarnikSkladista.Kljnaz, "Upozorenje", MessageBoxButton.YesNo, MessageBoxImage.Question);
                   
                    if (resultMessageBox == MessageBoxResult.Yes)
                    {
                        dbContext.SifarnikSkladista.Remove(SelectedSifarnikSkladista);
                        dbContext.SaveChanges();

                        SifarnikSkladistaList.Remove(SelectedSifarnikSkladista);
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
            else if (SelectedSifarnikSkladista == null)
            {
                System.Windows.MessageBox.Show("Niste odabrali sifarnik za brisanje", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region Dodavanje sifarnika
        private void AddSifarnikSkladista()
        {
            if (SelectedSifarnikSkladista != null)
            {
                using (var dbContext = new materijalno_knjigovodstvoContext())
                {
                    dbContext.SifarnikSkladista.Add(SelectedSifarnikSkladista);
                    dbContext.SaveChanges();

                    SifarnikSkladistaList.Add(SelectedSifarnikSkladista);

                    System.Windows.MessageBox.Show("Sifra skladista uspješno unesena", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                }
            }
        }
        #endregion

        #region Update sifarnika

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnpropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        #region Properties
        public SifarnikSkladista SelectedSifarnikSkladista { get => selectedSifarnikSkladista; set { selectedSifarnikSkladista = value; OnPropertyChanged("SelectedSifarnikSkladista"); } }
        public bool IsSelectedUnosSifarnik { get => isSelectedUnosSifarnik; set { isSelectedUnosSifarnik = value; OnPropertyChanged("IsSelectedUnosSifarnik"); } }

        #endregion
    }
}
