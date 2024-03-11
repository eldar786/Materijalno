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
    public class SifarnikKontaViewModel : ObservableObject
    {
        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        public ObservableCollection<SifarnikKonta> SifarnikKontaList { get; set; }


        public SifarnikKontaViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                SifarnikKontaList = new ObservableCollection<SifarnikKonta>(dbContext.SifarnikKonta.ToList());

                //ObrisiCommand = new RelayCommand(ObrisiSifarnikMaterijala);
                //UnosCommand = new RelayCommand(UnosSifarnikMaterijala);
                //IzmjenaCommand = new RelayCommand(IzmjenaSifarnikMaterijala);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;


        public void OnpropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }
    }
}