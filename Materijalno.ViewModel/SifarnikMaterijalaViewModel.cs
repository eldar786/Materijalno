using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using System.Windows.Data;
using System.Windows.Input;
using Materijalno.Model;
using System.Windows;
using System.Windows.Forms;
using Materijalno.Model.EntityModels;

namespace Materijalno.ViewModel
{
    public class SifarnikMaterijalaViewModel : ObservableObject
    {
        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        private SifarnikMaterijala selectedSifarnikMaterijala;
        private bool isSelectedUnosSifarnik = false;

        public ObservableCollection<SifarnikMaterijala> SifarnikMaterijalaList { get; set; }

        #region Commands
        public ICommand OpenSifarnikMaterijalaFormCommand { get; set; }
        public ICommand IzmjenaCommand { get; set; }
        public ICommand ObrisiCommand { get; set; }
        public ICommand StampaCommand { get; set; }
        public ICommand IzlazCommand { get; set; }

        #endregion


        public SifarnikMaterijalaViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                SifarnikMaterijalaList = new ObservableCollection<SifarnikMaterijala>(dbContext.SifarnikMaterijala.ToList());

                ObrisiCommand = new RelayCommand(ObrisiSifarnikMaterijala);
                OpenSifarnikMaterijalaFormCommand = new RelayCommand(OpenSifarnikMaterijalaForm);
                IzmjenaCommand = new RelayCommand(IzmjenaSifarnikMaterijala);
            }
        }

        private void IzmjenaSifarnikMaterijala()
        {
            throw new NotImplementedException();
        }

        private void OpenSifarnikMaterijalaForm()
        {
            isSelectedUnosSifarnik = true;
            _gvm.OdabraniVM = new SifarnikMaterijalaFormViewModel(this, _gvm);
        }

        private void ObrisiSifarnikMaterijala()
        {
            throw new NotImplementedException();
        }

        public event PropertyChangedEventHandler PropertyChanged;


        public void OnpropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        #region Properties
        public SifarnikMaterijala SelectedSifarnikMaterijala { get => selectedSifarnikMaterijala; set { selectedSifarnikMaterijala = value; OnPropertyChanged("SelectedSifarnikMaterijala"); } }
        public bool IsSelectedUnosSifarnikMaterijala { get => isSelectedUnosSifarnik; set { isSelectedUnosSifarnik = value; OnPropertyChanged("IsSelectedUnosSifarnikMaterijala"); } }

        #endregion

    }
}
