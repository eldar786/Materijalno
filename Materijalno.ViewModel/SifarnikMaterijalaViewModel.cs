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
        public ObservableCollection<SifarnikMaterijala> SifarnikMaterijalaList { get; set; }

        #region Commands
        public ICommand UnosCommand { get; set; }
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
                UnosCommand = new RelayCommand(UnosSifarnikMaterijala);
                IzmjenaCommand = new RelayCommand(IzmjenaSifarnikMaterijala);
            }
        }

        private void IzmjenaSifarnikMaterijala()
        {
            throw new NotImplementedException();
        }

        private void UnosSifarnikMaterijala()
        {
            throw new NotImplementedException();
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
    }
}
