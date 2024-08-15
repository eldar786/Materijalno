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
    public class SifarnikMaterijalSkladisteKontoViewModel : ObservableObject
    {
        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        public ObservableCollection<SifarnikMaterijalSkladisteKonto> SifarnikMaterijalSkladisteKontoList { get; set; }


        public SifarnikMaterijalSkladisteKontoViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                SifarnikMaterijalSkladisteKontoList = new ObservableCollection<SifarnikMaterijalSkladisteKonto>(dbContext.SifarnikMaterijalSkladisteKonto.ToList());

                //Dodati ostale commande
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