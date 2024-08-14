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
    public class SifarnikKontaViewModel : ObservableObject
    {
        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        public ObservableCollection<SifarnikKonta> SifarnikKontaList { get; set; }

        private SifarnikKonta selectedSifarnikKonta;
        private bool isSelectedUnosSifarnik = false;

        public ICommand AddSifarnikKontaCommand { get; set; }
        public ICommand OpenSifarnikKontaFormCommand { get; set; }
        public ICommand DeleteSifarnikKontaCommand { get; set; }
        public ICommand IzmjenaSifarnikKontaFormCommand { get; set; }

        public SifarnikKontaViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                SifarnikKontaList = new ObservableCollection<SifarnikKonta>(dbContext.SifarnikKonta.ToList());

                AddSifarnikKontaCommand = new RelayCommand(AddSifarnikKonta);
                OpenSifarnikKontaFormCommand = new RelayCommand(OpenSifarnikKontaForm);
                DeleteSifarnikKontaCommand = new RelayCommand(DeleteSifarnikKonta);
                IzmjenaSifarnikKontaFormCommand = new RelayCommand(IzmjenaSifarnikKontaForm);
                
                //ObrisiCommand = new RelayCommand(ObrisiSifarnikMaterijala);
                //UnosCommand = new RelayCommand(UnosSifarnikMaterijala);
                //IzmjenaCommand = new RelayCommand(IzmjenaSifarnikMaterijala);
            }
        }

        private void OpenSifarnikKontaForm()
        {
            isSelectedUnosSifarnik = true;
            _gvm.OdabraniVM = new SifarnikKontaFormViewModel(this, _gvm);
        }


        private void IzmjenaSifarnikKontaForm()
        {
            if (selectedSifarnikKonta == null)
            {
                System.Windows.MessageBox.Show("Niste odabrali sifarnik", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }
            _gvm.OdabraniVM = new SifarnikKontaFormViewModel(this, _gvm);    
        }


        private void DeleteSifarnikKonta()
        {
            if (SelectedSifarnikKonta != null)
            {
                using (var dbContext = new materijalno_knjigovodstvoContext())
                {
                    var resultMessageBox = System.Windows.MessageBox.Show("Da li ste sigurni da želite obrisati sifarnik 'Kljnaz' ", "Upozorenje", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (resultMessageBox == MessageBoxResult.Yes)
                    {
                        dbContext.SifarnikKonta.Remove(SelectedSifarnikKonta);
                        dbContext.SaveChanges();

                        SifarnikKontaList.Remove(SelectedSifarnikKonta);
                    }
                    else if (resultMessageBox == MessageBoxResult.No)
                    {
                        return;
                    }
                }
            }
            else if (SelectedSifarnikKonta == null)
            {
                System.Windows.MessageBox.Show("Niste odabrali sifarnik za brisanje", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddSifarnikKonta()
        {
            if (SelectedSifarnikKonta != null)
            {
                using (var dbContext = new materijalno_knjigovodstvoContext())
                {
                    dbContext.SifarnikKonta.Add(SelectedSifarnikKonta);
                    dbContext.SaveChanges();

                    SifarnikKontaList.Add(SelectedSifarnikKonta);

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


        public SifarnikKonta SelectedSifarnikKonta { get => selectedSifarnikKonta; set { selectedSifarnikKonta = value; OnPropertyChanged("SelectedSifarnikKonta"); } }
        public bool IsSelectedUnosSifarnik { get => isSelectedUnosSifarnik; set { isSelectedUnosSifarnik = value; OnPropertyChanged("IsSelectedUnosSifarnik"); } }
    }
}