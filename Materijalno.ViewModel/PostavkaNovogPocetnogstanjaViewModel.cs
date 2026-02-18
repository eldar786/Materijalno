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
            //Napraviti da stavlja trenutnu novu godinu kod pocetnog stanja za sve tabele koje prolazi, za sve magacine i materijale

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

                if (!dbContext.Inv.Any())
                {
                    System.Windows.MessageBox.Show(
                "Inventurna tabela je prazna. Nije moguće formirati početno stanje.",
                "Upozorenje",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
                     );
                    return;
                }

                // FIRST DAY OF CURRENT YEAR
                DateTime firstDayOfYear = new DateTime(DateTime.Now.Year, 1, 1);

                dbContext.Database.ExecuteSqlRaw("TRUNCATE TABLE mat");
                //Nakon brisanja tabele dodajemo po jednu praznu vrijednost za svaki status, da ne bi poslije pravio problem prilikom otvaranja
                //Dodati za status "M" Medus = 1 (Provjeriti kako radi u praksi)
                dbContext.Database.ExecuteSqlRaw(@"
                INSERT INTO mat
                (
                    kljnaz,
                    kljnaz1,
                    kolic,
                    nc,
                    vrijed,
                    status,
                    kontosklad,
                    medus
                )
                VALUES
                (1001, NULL, 0, 0, 0.00, 'I', NULL, ''),
                (1000, 1000, 0, 0, 0.00, 'M', 1, '1'),
                (1001, NULL, 0, 0, 0.00, 'V', NULL, ''),
                (1000, NULL, 0, 0, 0.00, 'U', NULL, '');
                ");

                // Transfer INV -> MAT (PRIJE TRUNCATE INV)
                var invRows = dbContext.Inv.AsNoTracking().ToList(); // snapshot

                var matRowsToInsert = invRows.Select(inv => new Mat
                {
                    Kljnaz = inv.Kljnaz,
                    Kljnaz1 = 0,
                    Ident = inv.Ident,
                    Datun = firstDayOfYear,
                    Analst = inv.Analst,
                    Brdok = inv.Brdok,
                    Brnar = inv.Brnar,
                    Datnar = firstDayOfYear,
                    Brfak = inv.Brfak,
                    Kolic = inv.Kolic,
                    Nc = inv.Nc,
                    Vrijed = inv.Vrijed,
                    Redbr = inv.Redbr,
                    Status = "P",
                    Konto1 = inv.Konto1,
                    Konto2 = inv.Konto2,
                    Fcj = inv.Fcj,
                    Troskovi = inv.Troskovi,
                    Cartro = inv.Cartro,
                    Zavtro = inv.Zavtro,
                    Ppp = inv.Ppp,
                    Tarifa = inv.Tarifa,
                    Fvrijed = inv.Fvrijed,
                    Porppp = inv.Porppp,
                    Kontosklad = inv.Kontosklad,
                    Kontosklad1 = inv.Kontosklad1,
                    Trospe = inv.Trospe,
                    Ourst = inv.Ourst,
                    Mjtst = inv.Mjtst
                }).ToList();

                dbContext.Mat.AddRange(matRowsToInsert);
                dbContext.SaveChanges();

                //Nakon prebacivanja u Mat tabelu, ide Brisanje INV tabele - Truncate
                dbContext.Database.ExecuteSqlRaw("TRUNCATE TABLE inv");

                //Nalmat Brisanje - Truncate
                dbContext.Database.ExecuteSqlRaw("TRUNCATE TABLE nalmat");

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