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
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.Data.SqlClient;
using System.Runtime.Remoting.Contexts;
using System.Diagnostics.Eventing.Reader;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Globalization;

namespace Materijalno.ViewModel
{
    public class IzlazMaterijalaViewModel : INotifyPropertyChanged
    {
        #region Fields

        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        private Mat currentItemMat;
        private Mat currentItemPovrat;
        private TabelaMaterijala currentItemTabMaterijala;
        private SifarnikSkladista currentItemTabSkladista;
        string connectionString = "Server= 192.168.1.213;Trusted_Connection=False;" +
            "MultipleActiveResultSets=true;User Id=sa;Password=Lutrija1;";

        string queryStringStaraSifra_Ime = "SELECT STARA_SIFRA, IME FROM [FINANSIJE2-LINKED SERVER]..[IBS].[GR_KOMITENTI]";
        string currentNazivZaSifruKomitenta;
        public int CurrentIndex = 0;
        public event PropertyChangedEventHandler PropertyChanged;

        private bool prethodniButtonIsExecuted = false;
        private bool nextButtonIsExecuted = false;
        private bool prvoUcitavanje = false;
        private bool spasi = false;
        public static Komitenti selectedKomitent;
        public static bool isNovaKalkulacijaClicked = false;
        public static bool isTraziClicked = false;
        public static Mat selectedMat;

        #endregion


        #region Properties and Lists

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

        public Mat CurrentItemPovrat
        {
            get { return currentItemPovrat; }
            set
            {
                currentItemPovrat = value;
                OnPropertyChanged(nameof(currentItemPovrat));
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

        public ObservableCollection<TabelaMaterijala> TebelaMaterijalaList { get; set; }
        public ObservableCollection<SifarnikSkladista> TebelaSkladistaList { get; set; }
        public ObservableCollection<Mat> MatList { get; set; }
        public List<Komitenti> StaraSifra_Ime_List { get; set; }

        #endregion

        #region Commands

        public RelayCommand NextButtonCommand { get; set; }
        public RelayCommand PrethodniButtonCommand { get; set; }

        //public RelayCommand NovoZaduzenjeCommand { get; set; }
        public RelayCommand SpasiNovuKalkulacijuCommand { get; set; }

        public RelayCommand BrisanjeCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }
        public RelayCommand NovoZaduzenjeCommand { get; set; }
        public RelayCommand NovaStavkaCommand { get; set; }

        //Potrebno uraditi ???
        //public RelayCommand StampaCommand { get; set; }
        public RelayCommand PrintCommand { get; set; }
        public RelayCommand OtvoriKomitentListuCommand { get; set; }
        public RelayCommand TraziSifruMaterijalaCommand { get; set; }

        public RelayCommand OdustaniCommand { get; set; }
        public RelayCommand NabavnaCijenaCommand { get; set; }
        public RelayCommand IzlazCommand { get; set; }





        #endregion

        #region Constructor

        public IzlazMaterijalaViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;
            //Prilikom otvaranja Ulaza iz nove kalkulacije, treba promijeniti u false
            isNovaKalkulacijaClicked = false;

            #region Commands
            //PrviButtonCommand = new RelayCommand(PrviButton, () => !isNovaKalkulacijaClicked);
            NextButtonCommand = new RelayCommand(NextButton, () => !isNovaKalkulacijaClicked);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !isNovaKalkulacijaClicked);
            //ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !isNovaKalkulacijaClicked);



            BrisanjeCommand = new RelayCommand(Brisanje, () => !isNovaKalkulacijaClicked);
            UpdateCommand = new RelayCommand(Update, () => !isNovaKalkulacijaClicked);
            NovoZaduzenjeCommand = new RelayCommand(NovaKalkulacija, () => !isNovaKalkulacijaClicked);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => isNovaKalkulacijaClicked);
            IzlazCommand = new RelayCommand(Izlaz, () => isNovaKalkulacijaClicked);
            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !isNovaKalkulacijaClicked);
            //StampaCommand = new RelayCommand(Stampa, () => !isNovaKalkulacijaClicked);

            PrintCommand = new RelayCommand(Print, () => !isNovaKalkulacijaClicked);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            #endregion

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //NextButtonCommand = new RelayCommand(NextButton);
                //PrethodniButtonCommand = new RelayCommand(PrethodniButton);
                //PrviButtonCommand = new RelayCommand(PrviButton);
                //ZadnjiButtonCommand = new RelayCommand(ZadnjiButton);

                //Dodaj u listu gdje je kljnaz između 1000 i 1012 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Datun)
                    .ToList());

                UpdateCurrentItemData(dbContext);
                //UpdateCurrentItemDataPovrat(dbContext);

                StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }

        #endregion

        public IzlazMaterijalaViewModel(GlavniViewModel gvm, Mat CurrentItemMat)
        {
            _gvm = gvm;
            this.CurrentItemMat = CurrentItemMat;

            //Kada vraca iz Trazi Mat liste, treba da ostane isNovaKalkulacijaClicked = false; zbog buttona
            if (isTraziClicked == true)
            {
                isNovaKalkulacijaClicked = false;
            }
            else
            {
                isNovaKalkulacijaClicked = true;
            }


            #region Commands
            //PrviButtonCommand = new RelayCommand(PrviButton, () => !isNovaKalkulacijaClicked);
            NextButtonCommand = new RelayCommand(NextButton, () => !isNovaKalkulacijaClicked);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !isNovaKalkulacijaClicked);
            //ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !isNovaKalkulacijaClicked);
            BrisanjeCommand = new RelayCommand(Brisanje, () => !isNovaKalkulacijaClicked);
            UpdateCommand = new RelayCommand(Update, () => !isNovaKalkulacijaClicked);
            NovoZaduzenjeCommand = new RelayCommand(NovaKalkulacija, () => !isNovaKalkulacijaClicked);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => isNovaKalkulacijaClicked);
            IzlazCommand = new RelayCommand(Izlaz, () => isNovaKalkulacijaClicked);
            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !isNovaKalkulacijaClicked);
            PrintCommand = new RelayCommand(Print, () => !isNovaKalkulacijaClicked);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            #endregion

            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Datun)
                    .ToList());

                #region Custom UpdateCurrentItemData
                //***Prilagodjena metoda UpdateCurrentItemData()***

                for (int i = 0; i < MatList.Count(); i++)
                {
                    if (MatList[i].Id == CurrentItemMat.Id)
                    {
                        CurrentIndex = i;
                    }
                }

                CurrentItemMat = (Mat)MatList.FirstOrDefault(row => row.Id == CurrentItemMat.Id);

                //Nadji listu svih po *Ident* iz *TabelaMaterijala* i *CurrentItem* (Mat) i stavi u listu
                TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).ToList());
                TebelaSkladistaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz).ToList());

                //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property
                CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).FirstOrDefault();
                CurrentItemTabSkladista = dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz).FirstOrDefault();
                //Ako lista nije popunjena iz linked server (oracle baza), onda ce preskociti i pozivati u konstruktoru preko druge metode i
                //popuniti CurrentNazivZaSifruKomitenta. Ovo radimo da ne bi ponovo popunjavali listu iz oracle baze, zbog brzeg rada aplikacije
                if (StaraSifra_Ime_List != null)
                {
                    CurrentNazivZaSifruKomitenta = string.IsNullOrEmpty(CurrentItemMat.Analst) ? ""
                        : StaraSifra_Ime_List.FirstOrDefault(row => row.STARA_SIFRA == CurrentItemMat.Analst)?.IME;
                }

                if (selectedKomitent != null)
                {
                    CurrentItemMat.Analst = selectedKomitent.STARA_SIFRA;
                    CurrentNazivZaSifruKomitenta = selectedKomitent.IME;
                }

                selectedKomitent = null;

                #endregion

                StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }


        public void NabavnaCijena()
        {
            decimal? inputValue1 = CurrentItemMat.Vrijed;
            decimal? inputValue2 = CurrentItemMat.Trospe;
            decimal? inputValue3 = CurrentItemMat.Porppp;
            decimal? inputValue4 = CurrentItemMat.Troskovi;

            var culture = new CultureInfo("de-DE");

            decimal? value1 = inputValue1.HasValue ? (decimal?)inputValue1.Value : 0;

            //decimal? value1 = decimal.Parse(inputValue1, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, culture);
            decimal? value2 = inputValue2.HasValue ? (decimal?)inputValue2.Value : 0;

            decimal? value3 = inputValue3.HasValue ? (decimal?)inputValue3.Value : 0;

            decimal? value4 = inputValue4.HasValue ? (decimal?)inputValue4.Value : 0;

            decimal? sum = value1 + value2 + value3 + value4;
            //CurrentItemMat.Nc = (sum / (decimal)CurrentItemMat.Kolic).ToString();
            if (CurrentItemMat.Kolic.HasValue && CurrentItemMat.Kolic.Value != 0)
            {
                decimal? nc = (sum / (decimal)CurrentItemMat.Kolic);
                CurrentItemMat.Nc = (sum / (decimal)CurrentItemMat.Kolic.Value);
            }
            else
            {
                // U slucaju da je Kolic null ili nula, da bi izbjegli dijeljenje sa nulom
                CurrentItemMat.Nc = 0;
            }

            var dbContext = new materijalno_knjigovodstvoContext();
            dbContext.Update(CurrentItemMat);
            dbContext.SaveChanges();

            //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
            MatList = new ObservableCollection<Mat>(dbContext.Mat
                .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012)
                .OrderBy(row => row.Datun)
                .ToList());

            UpdateCurrentItemData(dbContext);
        }


        #region Methods

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
                        //List<Komitenti> StaraSifra_Ime_List = new List<Komitenti>();

                        while (reader.Read())
                        {
                            //nazivZaSifruKomitenta = (reader[1].ToString());

                            //Pravi za svaki red novi objekat i dodaje u listu
                            Komitenti komitent = new Komitenti()
                            {
                                STARA_SIFRA = reader["STARA_SIFRA"].ToString(),
                                IME = reader["IME"].ToString()
                            };

                            StaraSifra_Ime_List.Add(komitent);
                        }
                        //Daj mi ime na osnovu jednakosti i stavi ga u property string
                        CurrentNazivZaSifruKomitenta = string.IsNullOrEmpty(CurrentItemMat.Analst) ? ""
                            : StaraSifra_Ime_List.FirstOrDefault(row => row.STARA_SIFRA == CurrentItemMat.Analst)?.IME;
                    }
                }
            }
            return StaraSifra_Ime_List;
        }

        private void NextButton()
        {

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //kada se dodje do zadnjeg reda da obavijesti korisnika i vrati metodu
                if (CurrentIndex == MatList.Count - 1)
                {
                    UpdateCurrentItemData(dbContext);
                    System.Windows.MessageBox.Show("Došli ste do zadnjeg podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                    //UpdateCurrentItemDataPovrat(dbContext);
                    System.Windows.MessageBox.Show("Došli ste do zadnjeg podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                    return;
                }

                CurrentIndex = (CurrentIndex + 1) % MatList.Count;
                CurrentItemMat = MatList[CurrentIndex];
                //CurrentItemPovrat = MatList[CurrentIndex];
                UpdateCurrentItemData(dbContext);
                //UpdateCurrentItemDataPovrat(dbContext);

            }
        }
        private void PrethodniButton()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (CurrentIndex == 0)
                {
                    System.Windows.MessageBox.Show("Došli ste do prvog podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                CurrentIndex = (CurrentIndex - 1) % MatList.Count;
                CurrentItemMat = MatList[CurrentIndex];
                UpdateCurrentItemData(dbContext);
                //UpdateCurrentItemDataPovrat(dbContext);
            }
        }
        private void PrviButton()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentIndex = 0;
                CurrentItemMat = MatList[CurrentIndex];
                UpdateCurrentItemData(dbContext);
                System.Windows.MessageBox.Show("Došli ste do prvog podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                //UpdateCurrentItemDataPovrat(dbContext);
                System.Windows.MessageBox.Show("Došli ste do prvog podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void ZadnjiButton()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = MatList[CurrentIndex];
                UpdateCurrentItemData(dbContext);

                System.Windows.MessageBox.Show("Došli ste do zadnjeg podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                //UpdateCurrentItemDataPovrat(dbContext);
                System.Windows.MessageBox.Show("Došli ste do prvog podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Brisanje()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentItemMat = MatList[CurrentIndex];

                var resultMessageBox = System.Windows.MessageBox.Show("Želite li obrisati tekući podatak? ", "Upozorenje", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (resultMessageBox == MessageBoxResult.Yes)
                {
                    dbContext.Mat.Remove(CurrentItemMat);
                    dbContext.SaveChanges();

                    MatList.Remove(CurrentItemMat);

                    currentItemMat = null;
                }
                else if (resultMessageBox == MessageBoxResult.No)
                {
                    return;
                }
                UpdateCurrentItemData(dbContext);
            }
        }

        private void Update()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                dbContext.Update(CurrentItemMat);
                dbContext.SaveChanges();

                System.Windows.MessageBox.Show("Uspješno ste izmijenili", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                //NAKON STO KLIKNEMO NA OK DA SE VRATI NA IZMIJENJENI ŠIFARNIK
                UpdateCurrentItemData(dbContext);
            }
        }

        private void Trazi()
        {
            _gvm.OdabraniVM = new MatListaViewModel(this, _gvm);
        }

        private void Print()
        {
            OnPrintEvent?.Invoke();
        }

        // An event that will be raised to notify the view to open the PrintWindow
        public event Action OnPrintEvent;

        private void Izlaz()
        {
            _gvm.OdabraniVM = new IzlazMaterijalaViewModel(_gvm);
        }

        //obrise sva polja i ostavlja opciju za SNIMI i ODUSTANI
        private void NovaKalkulacija()
        {
            //Prolazi ponovo provjeru CanExecute
            isNovaKalkulacijaClicked = true;

            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                MatList.Add(new Mat
                {
                    //Za ulaz materijala broj skladišta je uvijek 1000
                    //Kljnaz = 1000
                });

                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = MatList[CurrentIndex];

                dbContext.Add(CurrentItemMat);
                dbContext.SaveChanges();

                UpdateCurrentItemData(dbContext);
            }
        }

        //Pokupi sva polja trenutna i spasi. Trebalo bi napraviti disabled SAVE button ako nije odabrana nova kalkulacija
        private void SpasiNovuKalkulaciju()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                dbContext.Update(CurrentItemMat);
                dbContext.SaveChanges();

                //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Datun)
                    .ToList());

                System.Windows.MessageBox.Show("Uspješno ste unijeli novi šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                //Ovo kada je true, onda ce buttoni biti dostupni
                isNovaKalkulacijaClicked = false;

                UpdateCommands();

                if (CurrentItemMat != null)
                {
                    for (int i = 0; i < MatList.Count(); i++)
                    {
                        if (MatList[i].Id == CurrentItemMat.Id)
                        {
                            CurrentIndex = i;
                            CurrentItemMat = MatList[CurrentIndex];
                        }
                    }
                }

                //_gvm.OdabraniVM = new UlazMaterijalaViewModel(_gvm);
                UpdateCurrentItemData(dbContext);
            }
        }

        private void UpdateCommands()
        {
            //PrviButtonCommand.RaiseCanExecuteChanged();
            //ZadnjiButtonCommand.RaiseCanExecuteChanged();
            NextButtonCommand.RaiseCanExecuteChanged();
            PrethodniButtonCommand.RaiseCanExecuteChanged();
            SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();
            OdustaniCommand.RaiseCanExecuteChanged();
            NovoZaduzenjeCommand.RaiseCanExecuteChanged();
            BrisanjeCommand.RaiseCanExecuteChanged();
            UpdateCommand.RaiseCanExecuteChanged();
            TraziSifruMaterijalaCommand.RaiseCanExecuteChanged();
            PrintCommand.RaiseCanExecuteChanged();
            IzlazCommand.RaiseCanExecuteChanged();
            SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();
            OdustaniCommand.RaiseCanExecuteChanged();
        }

        //Samo dostupno kada odemo na nova kalkulacija i da vrati na prethodni
        private void Odustani()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentItemMat = MatList[CurrentIndex];

                var resultMessageBox = System.Windows.MessageBox.Show("Želite li odustati tekući podatak? ", "Upozorenje", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (resultMessageBox == MessageBoxResult.Yes)
                {
                    isNovaKalkulacijaClicked = false;

                    UpdateCommands();
                    //Prije odustajanja dohvati zadnji index, da bi mogao vratiti u pogledu, da ne ide na pocetak??

                    dbContext.Mat.Remove(CurrentItemMat);
                    dbContext.SaveChanges();

                    MatList.Remove(CurrentItemMat);

                    isNovaKalkulacijaClicked = false;

                    CurrentItemMat = null;

                    _gvm.OdabraniVM = new IzlazMaterijalaViewModel(_gvm);
                }
                else if (resultMessageBox == MessageBoxResult.No)
                {
                    return;
                }
            }
        }

        private void OtvoriKomitentListu()
        {
            _gvm.OdabraniVM = new KomitentiListaViewModel(this, _gvm);
        }



        // Ova metoda radi update CurrentItem i CurrentItemTabMaterijala based on the current index
        private void UpdateCurrentItemData(materijalno_knjigovodstvoContext dbContext)
        {
            if (CurrentItemMat != null)
            {
                for (int i = 0; i < MatList.Count(); i++)
                {
                    if (MatList[i].Id == CurrentItemMat.Id)
                    {
                        CurrentIndex = i;
                        CurrentItemMat = MatList[CurrentIndex];
                    }
                }
            }
            else
            {
                CurrentItemMat = MatList[CurrentIndex];
            }


            //CurrentItemMat = MatList[CurrentIndex];

            //Nadji listu svih po *Ident* iz *TabelaMaterijala* i *CurrentItem* (Mat) i stavi u listu
            TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).ToList());
            
            TebelaSkladistaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz).ToList());

            //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property
            CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).FirstOrDefault();
            CurrentItemTabSkladista = dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz).FirstOrDefault();
            //Ako lista nije popunjena iz linked server (oracle baza), onda ce preskociti i pozivati u konstruktoru preko druge metode i
            //popuniti CurrentNazivZaSifruKomitenta. Ovo radimo da ne bi ponovo popunjavali listu iz oracle baze, zbog brzeg rada aplikacije
            if (StaraSifra_Ime_List != null)
            {
                CurrentNazivZaSifruKomitenta = string.IsNullOrEmpty(CurrentItemMat.Analst) ? ""
                    : StaraSifra_Ime_List.FirstOrDefault(row => row.STARA_SIFRA == CurrentItemMat.Analst)?.IME;
            }

            if (selectedKomitent != null)
            {
                CurrentItemMat.Analst = selectedKomitent.STARA_SIFRA;
                CurrentNazivZaSifruKomitenta = selectedKomitent.IME;
            }

            selectedKomitent = null;
        }

        //private void UpdateCurrentItemDataPovrat(materijalno_knjigovodstvoContext dbContext)
        //{

        //    if (CurrentItemMat != null)
        //    {
        //        for (int i = 0; i < MatList.Count(); i++)
        //        {
        //            if (MatList[i].Id == CurrentItemMat.Id)
        //            {
        //                CurrentIndex = i;
        //                CurrentItemMat = MatList[CurrentIndex];
        //            }
        //        }
        //    }
        //    else
        //    {
        //        CurrentItemMat = MatList[CurrentIndex];
        //    }


        //    //CurrentItemMat = MatList[CurrentIndex];

        //    //Nadji listu svih po *Ident* iz *TabelaMaterijala* i *CurrentItem* (Mat) i stavi u listu
        //    TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).ToList());

        //    //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property
        //    CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).FirstOrDefault();

        //    //Ako lista nije popunjena iz linked server (oracle baza), onda ce preskociti i pozivati u konstruktoru preko druge metode i
        //    //popuniti CurrentNazivZaSifruKomitenta. Ovo radimo da ne bi ponovo popunjavali listu iz oracle baze, zbog brzeg rada aplikacije
        //    if (StaraSifra_Ime_List != null)
        //    {
        //        CurrentNazivZaSifruKomitenta = string.IsNullOrEmpty(CurrentItemMat.Analst) ? ""
        //            : StaraSifra_Ime_List.FirstOrDefault(row => row.STARA_SIFRA == CurrentItemMat.Analst)?.IME;
        //    }

        //    if (selectedKomitent != null)
        //    {
        //        CurrentItemMat.Analst = selectedKomitent.STARA_SIFRA;
        //        CurrentNazivZaSifruKomitenta = selectedKomitent.IME;
        //    }

        //    selectedKomitent = null;
        //}

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
}
