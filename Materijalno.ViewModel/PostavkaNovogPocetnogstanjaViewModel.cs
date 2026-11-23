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
            if (System.Windows.MessageBox.Show("Formirati novo početno stanje iz unesenog inventurnog popisa?\n\nPostojeći materijalni promet bit će obrisan i zamijenjen inventurnim stavkama. Nakon uspješnog prenosa brišu se inventurni popis i lokalni finansijski nalozi. Datumi, količine, NC i vrijednosti preuzimaju se iz inventure.", "Početno stanje", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            try
            {
                var service = new Materijalno.Model.Services.YearCloseService();
                int year = service.CloseFromInventory();
                System.Windows.MessageBox.Show("Formirano je početno stanje za " + year + ". Prethodni promet je zamijenjen inventurnim stavkama. Inventurni popis i lokalni finansijski nalozi su obrisani.", "Početno stanje", MessageBoxButton.OK, MessageBoxImage.Information);
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
