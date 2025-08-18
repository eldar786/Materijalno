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
            MessageBoxResult result = System.Windows.MessageBox.Show(
    "Postavka novog početnog stanja?",              // Poruka
    "Potvrda",                         // Naslov
    MessageBoxButton.YesNo,                 // Buttons
    MessageBoxImage.Question                // Icon
);
            if (result == MessageBoxResult.Yes)
            {
                //Izbrisi sve iz tabela Mat, ostavi status "P"
                var dbContext = new materijalno_knjigovodstvoContext();
                //MAT
                MatList = new ObservableCollection<Mat>(dbContext.Mat.Where(row => row.Status != "P"));
                foreach (Mat mat in MatList)
                {
                    dbContext.Mat.Remove(mat);
                }
                dbContext.Database.ExecuteSqlRaw("TRUNCATE TABLE mat");

                //NALMAT - moze i koristenjem querya
                var nalMatList = dbContext.Nalmat.ToList();
                dbContext.Nalmat.RemoveRange(nalMatList);
                //Query za brisanje koji radi brze a moze i LINQ
                //dbContext.Database.ExecuteSqlRaw("DELETE FROM nalmat");
                dbContext.Database.ExecuteSqlRaw("TRUNCATE TABLE nalmat");

                //INV
                dbContext.Database.ExecuteSqlRaw("DELETE FROM inv");
                dbContext.Database.ExecuteSqlRaw("TRUNCATE TABLE inv");

                //Spasi bazu
                dbContext.SaveChanges();

                System.Windows.MessageBox.Show("Uspješno ste obavili postavku početnog stanja!", "Informacija", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                return;
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