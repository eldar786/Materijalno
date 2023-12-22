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

        private SifarnikSkladista sifarnikSkladista;

        public ObservableCollection<SifarnikSkladista> SifarnikSkladistaList { get; set; }

        #endregion

        #region Commands
        public ICommand DeleteSifarnikSkladistaCommand { get; set; }
        public ICommand AddSifarnikSkladistaCommand { get; set; }
        public ICommand OpenSifarnikSkladistaFormCommand { get; set; }

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
            }

        }
        #endregion

        #region Otvaranje Sifarnik Skladista Form
        private void OpenSifarnikSkladistaForm()
        {
            _gvm.OdabraniVM = new SifarnikSkladistaFormViewModel(_gvm);
        }

        #endregion

        #region Brisanje sifarnika
        private void DeleteSifarnikSkladista()
        {
            if (SelectedSifarnikSkladista != null)
            {
                using (var dbContext = new materijalno_knjigovodstvoContext())
                {
                    dbContext.SifarnikSkladista.Remove(SelectedSifarnikSkladista);
                    dbContext.SaveChanges();

                    // Vidjeti drugi naci za refresh listu, da radi INotPropChanged
                    SifarnikSkladistaList.Remove(SelectedSifarnikSkladista);

                    SelectedSifarnikSkladista = null;
                }
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
        public SifarnikSkladista SelectedSifarnikSkladista { get => sifarnikSkladista; set { sifarnikSkladista = value; OnPropertyChanged("SelectedSifarnikSkladista"); } }
        #endregion
    }
}
