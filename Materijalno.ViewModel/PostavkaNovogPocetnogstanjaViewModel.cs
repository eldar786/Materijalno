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
using Microsoft.EntityFrameworkCore;

namespace Materijalno.ViewModel
{
    public class PostavkaNovogPocetnogstanjaViewModel : ObservableObject
    {
        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;

        public ObservableCollection<Mat> MatList { get; set; }

        public RelayCommand PocetnoStanjeCommand { get; set; }

        public PostavkaNovogPocetnogstanjaViewModel(GlavniViewModel gvm)
        {
            PocetnoStanjeCommand = new RelayCommand(PocetnoStanje);
        }
        public void PocetnoStanje()
        {
            if (System.Windows.MessageBox.Show("Zaključiti završenu godinu i formirati početno stanje iz potpune završne inventure?", "Početno stanje", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                int year = new Materijalno.Model.Services.YearCloseService().CloseFromInventory();
                System.Windows.MessageBox.Show("Otvorena je godina " + year + ". Prethodni promet i nalozi su sačuvani u arhivi.", "Početno stanje", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Početno stanje", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        public event PropertyChangedEventHandler PropertyChanged;


        public void OnpropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }
    }
}