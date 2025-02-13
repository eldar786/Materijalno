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
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.Data.SqlClient;
using System.Runtime.Remoting.Contexts;
using System.Diagnostics.Eventing.Reader;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Materijalno.ViewModel
{
    public class StanjeZalihaZaMaterijalUSkladistuViewModel : INotifyPropertyChanged
    {
        #region Fields

        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        private Mat currentItemMat;
        private Mat currentItemMed;
        private Mat itemMat;
        public static Komitenti selectedKomitent;
        public static Mat selectedMat;
        private TabelaMaterijala currentItemTabMaterijala;

        //private TabelaMaterijala currentItemTabKonto1;
        private SifarnikSkladista currentItemTabSkladista;
        private SifarnikSkladista currentItemTabSkladistaUlaza;
        string connectionString = "Server= 192.168.1.213;Trusted_Connection=False;" + "MultipleActiveResultSets=true;User Id=sa;Password=Lutrija1;";

        string queryStringStaraSifra_Ime = "SELECT STARA_SIFRA, IME FROM [FINANSIJE2-LINKED SERVER]..[IBS].[GR_KOMITENTI]";
        string currentNazivZaSifruKomitenta;
        public int CurrentIndex = 0;
        public static bool isNovaKalkulacijaClicked = false;
        public static bool isTraziClicked = false;
        public event PropertyChangedEventHandler PropertyChanged;
        private bool prethodniButtonIsExecuted = false;
        private bool nextButtonIsExecuted = false;
        private bool prvoUcitavanje = false;
        private bool spasi = false;

        private int? kljnaz;
        private int? ident;
        private int kolic;
        private decimal nc;
        private decimal vrijed;
        private DateTime datun = DateTime.Today;

        #endregion


        #region Properties and Lists

        public int? Kljnaz
        {
            get { return kljnaz; }
            set
            {
                kljnaz = value;
                OnPropertyChanged(nameof(Kljnaz));
            }
        }

        public int? Ident
        {
            get { return ident; }
            set
            {
                ident = value;
                OnPropertyChanged(nameof(Ident));
            }
        }
        public DateTime Datun
        {
            get { return datun; }
            set
            {
                datun = value;
                OnPropertyChanged(nameof(Datun));
            }
        }
        public int Kolic
        {
            get { return kolic; }
            set
            {
                kolic = value;
                OnPropertyChanged(nameof(Kolic));
            }
        }
        public decimal Nc
        {
            get { return nc; }
            set
            {
                nc = value;
                OnPropertyChanged(nameof(Nc));
            }
        }
        public decimal Vrijed
        {
            get { return vrijed; }
            set
            {
                vrijed = value;
                OnPropertyChanged(nameof(Vrijed));
            }
        }

        //Staviti bolji naziv CurrentItemMat
        public Mat CurrentItemMat
        {
            get { return currentItemMat; }
            set
            {
                currentItemMat = value;
                OnPropertyChanged(nameof(CurrentItemMat));
            }
        }

        public Mat CurrentItemMed
        {
            get { return currentItemMed; }
            set
            {
                currentItemMed = value;
                OnPropertyChanged(nameof(CurrentItemMed));
            }
        }
        public TabelaMaterijala CurrentItemTabMaterijala
        {
            get { return currentItemTabMaterijala; }
            set
            {
                currentItemTabMaterijala = value;
                OnPropertyChanged(nameof(currentItemTabMaterijala));
            }
        }

        public SifarnikSkladista CurrentItemTabSkladista
        {
            get { return currentItemTabSkladista; }
            set
            {
                currentItemTabSkladista = value;
                OnPropertyChanged(nameof(currentItemTabSkladista));
            }
        }

        public SifarnikSkladista CurrentItemTabSkladistaUlaza
        {
            get { return currentItemTabSkladistaUlaza; }
            set
            {
                currentItemTabSkladistaUlaza = value;
                OnPropertyChanged(nameof(currentItemTabSkladistaUlaza));
            }
        }

        public string CurrentNazivZaSifruKomitenta
        {
            get { return currentNazivZaSifruKomitenta; }
            set
            {
                currentNazivZaSifruKomitenta = value;
                OnPropertyChanged(nameof(CurrentNazivZaSifruKomitenta));
            }
        }

        public Komitenti SelectedKomitent
        {
            get { return selectedKomitent; }
            set
            {
                selectedKomitent = value;
                OnPropertyChanged(nameof(SelectedKomitent));
            }
        }

        decimal? ukupnoNc;
        public decimal? UkupnoNc
        {
            get { return ukupnoNc; }
            set
            {
                ukupnoNc = value;
                OnPropertyChanged(nameof(UkupnoNc));
            }
        }

        public ObservableCollection<TabelaMaterijala> TebelaMaterijalaList { get; set; }
        public ObservableCollection<SifarnikSkladista> TebelaSkladistaList { get; set; }
        public ObservableCollection<SifarnikSkladista> TebelaSkladistaUlazaList { get; set; }
        public ObservableCollection<Mat> MatList { get; set; }
        public List<Komitenti> StaraSifra_Ime_List { get; set; }

        #endregion

        #region Commands

        
        public RelayCommand TraziSifruMaterijalaCommand { get; set; }
        public RelayCommand OsvjeziCommand { get; set; }
        public RelayCommand OdustaniCommand { get; set; }
        public RelayCommand PrikaziCommand { get; set; }

        public object SelectedDate { get; set; }

        #endregion

        #region Constructor

        public StanjeZalihaZaMaterijalUSkladistuViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;

            #region Commands

            OsvjeziCommand = new RelayCommand(Osvjezi);

            PrikaziCommand = new RelayCommand(Prikazi);
            #endregion

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                MatList = new ObservableCollection<Mat>(dbContext.Mat.ToList());
            }
        }

        #endregion

        #region Methods

        public void Prikazi()
        {
            int? a = Kljnaz;
            int? b = Ident;
            DateTime c = Datun;
            int d = Kolic;
            decimal e = Nc;
            decimal f = Vrijed;

            decimal? ukupnaVrijednost = 0;
            decimal? cijena = 0;
            int? ukupnaKolicina = 0;

            var dbContext = new materijalno_knjigovodstvoContext();

            TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == Ident).ToList());

            TebelaSkladistaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(row => row.Kljnaz == Kljnaz).ToList());

            List<Mat> list = MatList
                .Where(row => row.Kljnaz == Kljnaz && row.Ident == Ident && row.Datun <= Datun)
                     .OrderBy(row => row.Datun)
                     .ToList();

            foreach (var item in list)
            {
               ukupnaVrijednost += item.Vrijed ?? 0;
               ukupnaKolicina += item.Kolic ?? 0;
            }

            if (ukupnaKolicina != 0 && ukupnaVrijednost.HasValue)
            {
                cijena = ukupnaVrijednost / ukupnaKolicina;
            }

            else if (kljnaz == null || ident == null || kljnaz == 0 || ident == 0)
            {
                System.Windows.MessageBox.Show("Niste unijeli ispravno vrijednosti!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Nc = (decimal)cijena;
            Kolic = (int)ukupnaKolicina;
            Vrijed = (decimal)ukupnaVrijednost;
        }

        public void Osvjezi()
        {
            var dbContext = new materijalno_knjigovodstvoContext();

            // Kljnaz kolona
            int? maxValue_Skladiste = dbContext.Mat
                .Max(row => row.Kljnaz);

            int? minValue_Skladiste = dbContext.Mat
                .Min(row => row.Kljnaz);

            // Kljnaz1 kolona
            int? maxValue_SkladisteKljnaz1 = dbContext.Mat
                .Max(row => row.Kljnaz1);

            int? minValue_SkladisteKljnaz1 = dbContext.Mat
                .Min(row => row.Kljnaz1);

            // Ident kolona
            int? maxValue_SifraMat = dbContext.Mat
                .Max(row => row.Ident);

            int? minValue_SifraMat = dbContext.Mat
                .Min(row => row.Ident);

            if (Kljnaz == 999 && Kljnaz <= 1012)
            {
                System.Windows.MessageBox.Show("Ne možete praviti izlaz iz Centralnog magacina!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                Kljnaz = 0;
            }
            else if (Kljnaz < minValue_Skladiste || Kljnaz > maxValue_Skladiste)
            {
                System.Windows.MessageBox.Show("Skladište nije prijavljeno u šifarnik!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                //TextBox Skladiste vratiti na prazno
                Kljnaz = 0;
            }

            if (Ident < minValue_SifraMat || Ident > maxValue_SifraMat)
            {
                System.Windows.MessageBox.Show("Materijal ne postoji!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                Ident = 0;
            }

            UpdateCurrentItemData(dbContext);
        }

        public List<Komitenti> DohvatiNazivKomitenta()
        {
            List<Komitenti> StaraSifra_Ime_List = new List<Komitenti>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(queryStringStaraSifra_Ime, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            //Pravi za svaki red novi objekat i dodaje u listu
                            Komitenti komitent = new Komitenti()
                            {
                                STARA_SIFRA = reader["STARA_SIFRA"].ToString(),
                                IME = reader["IME"].ToString()
                            };

                            StaraSifra_Ime_List.Add(komitent);
                        }
                        //Daj mi ime na osnovu jednakosti i stavi ga u property string
                        CurrentNazivZaSifruKomitenta = string.IsNullOrEmpty(currentItemMat.Analst) ? ""
                            : StaraSifra_Ime_List.FirstOrDefault(row => row.STARA_SIFRA == currentItemMat.Analst)?.IME;
                    }
                }
            }
            return StaraSifra_Ime_List;
        }

        // An event that will be raised to notify the view to open the PrintWindow
        public event Action OnPrintEvent;

        // Ova metoda radi update CurrentItem i CurrentItemTabMaterijala based on the current index
        private void UpdateCurrentItemData(materijalno_knjigovodstvoContext dbContext)
        {
            //Nadji listu svih po *Ident* iz *TabelaMaterijala* i *CurrentItem* (Mat) i stavi u listu
            TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == Ident).ToList());

            TebelaSkladistaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(row => row.Kljnaz == Kljnaz).ToList());

            //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property
            CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Ident == Ident).FirstOrDefault();
            CurrentItemTabSkladista = dbContext.SifarnikSkladista.Where(row => row.Kljnaz == Kljnaz).FirstOrDefault();
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
}