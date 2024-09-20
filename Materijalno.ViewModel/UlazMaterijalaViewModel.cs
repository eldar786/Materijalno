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

namespace Materijalno.ViewModel
{
    public class UlazMaterijalaViewModel : INotifyPropertyChanged
    {
        #region Fields

        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        private Mat currentItemMat;
        private Mat itemMat;
        private TabelaMaterijala currentItemTabMaterijala;
        private bool prethodniButtonIsExecuted = false;
        private bool nextButtonIsExecuted = false;
        private bool prvoUcitavanje = false;
        private bool spasi = false;
        public static Komitenti selectedKomitent;
        string connectionString = "Server= 192.168.1.213;Trusted_Connection=False;" +
            "MultipleActiveResultSets=true;User Id=sa;Password=Lutrija1;";

        string queryStringStaraSifra_Ime = "SELECT STARA_SIFRA, IME FROM [FINANSIJE2-LINKED SERVER]..[IBS].[GR_KOMITENTI]";
        string currentNazivZaSifruKomitenta;
        public int CurrentIndex = 0;
        public static bool isNovaKalkulacijaClicked = false;
        public event PropertyChangedEventHandler PropertyChanged;

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

        public Mat ItemMat
        {
            get { return itemMat; }
            set
            {
                itemMat = value;
                OnPropertyChanged(nameof(ItemMat));
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
        public ObservableCollection<Mat> MatList { get; set; }
        public List<Komitenti> StaraSifra_Ime_List { get; set; }

        #endregion

        #region Commands

        public RelayCommand NextButtonCommand { get; set; }
        public RelayCommand PrethodniButtonCommand { get; set; }
        public RelayCommand PrviButtonCommand { get; set; }
        public RelayCommand ZadnjiButtonCommand { get; set; }
        public RelayCommand BrisanjeCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }
        public RelayCommand NovaKalkulacijaCommand { get; set; }
        public RelayCommand SpasiNovuKalkulacijuCommand { get; set; }


        //Potrebno uraditi ???
        public RelayCommand StampaCommand { get; set; }
        public RelayCommand OtvoriKomitentListuCommand { get; set; }
        public RelayCommand TraziSifruMaterijalaCommand { get; set; }

        //Možemo iskoristiti ovaj button prilikom kreiranja nove stavke da bude dostupno?
        public RelayCommand OdustaniCommand { get; set; }

        #endregion

        #region Constructor

        public UlazMaterijalaViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                NextButtonCommand = new RelayCommand(NextButton, CanExecuteTrue);
                PrethodniButtonCommand = new RelayCommand(PrethodniButton, CanExecuteTrue);
                PrviButtonCommand = new RelayCommand(PrviButton, CanExecuteTrue);
                ZadnjiButtonCommand = new RelayCommand(ZadnjiButton);
                BrisanjeCommand = new RelayCommand(Brisanje);
                UpdateCommand = new RelayCommand(Update);
                NovaKalkulacijaCommand = new RelayCommand(NovaKalkulacija);
                SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, CanExecute);
                OdustaniCommand = new RelayCommand(Odustani, CanExecute);

                OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);

                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz == 1000)
                    .OrderBy(row => row.Datun)
                    .ToList());

                prvoUcitavanje = true;

                UpdateCurrentItemData(dbContext);


                isNovaKalkulacijaClicked = false;
                StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }


        //Vidjeti ima li neki drugi nacin za prebacivanje podataka za selectedKomitent(da ne ide preko konstruktora?)
        //jer mora da se ponovo isto implementira kao u prethodnom konstruktoru i onda imamo duplikaciju koda
        //trenutno je rijeseno preko static polja
        public UlazMaterijalaViewModel(GlavniViewModel gvm, Mat CurrentItemMat)
        {
            _gvm = gvm;
            this.CurrentItemMat = CurrentItemMat;
            isNovaKalkulacijaClicked = true;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Prilikom vracanja iz Komitent liste, samo SAVE i ODUSTANI button treba da su dostupni
                NextButtonCommand = new RelayCommand(NextButton, CanExecute2);
                PrviButtonCommand = new RelayCommand(PrviButton, CanExecute2);
                PrethodniButtonCommand = new RelayCommand(PrethodniButton, CanExecute2);
                ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, CanExecute2);
                BrisanjeCommand = new RelayCommand(Brisanje, CanExecute2);
                UpdateCommand = new RelayCommand(Update, CanExecute2);
                NovaKalkulacijaCommand = new RelayCommand(NovaKalkulacija, CanExecute2);
                //Vidjeti kako staviti true vrijednost? iz nekog razloga ne odrazi se na UI?
                SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, CanExecute);
                OdustaniCommand = new RelayCommand(Odustani, CanExecute);

                OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);

                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz == 1000)
                    .OrderBy(row => row.Datun)
                    .ToList());

                #region Custom UpdateCurrentItemData
                //***Prilagodjena metoda UpdateCurrentItemData()***

                //Kod prethodnog buttona treba da ovo preskoci???
                //Ovo se moze iskoristiti mozda i za metodu UpdateCurrentItemData()??
                for (int i = 0; i < MatList.Count(); i++)
                {
                    if (MatList[i].Id == CurrentItemMat.Id)
                    {
                        CurrentIndex = i;
                    }
                }

                //CurrentIndex = MatList.Count - 1;
                //CurrentIndex = MatList.ElementAt(0).Id == CurrentItemMat.Id ? 0 : 1;
                    CurrentItemMat = (Mat)MatList.FirstOrDefault(row => row.Id == CurrentItemMat.Id);
                

                //CurrentItemMat = MatList[CurrentIndex];

                //Nadji listu svih po *Ident* iz *TabelaMaterijala* i *CurrentItem* (Mat) i stavi u listu
                TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).ToList());

                //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property
                CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).FirstOrDefault();

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
                PrviButtonCommand.RaiseCanExecuteChanged();

                StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }

        #endregion

        #region Methods

        //1. Scenario Ako je NovaKalkulacija false, onda iskljuci button i obrnuto
        private bool CanExecute()
        {
            if (isNovaKalkulacijaClicked == false)
            {
                return false;
            }
            else if (isNovaKalkulacijaClicked == true)
            {
                return true;
            }
            return false;
        }

        //2. Scenario Ako je NovaKalkulacija true, onda iskljuci button i obrnuto
        private bool CanExecute2()
        {
            if (isNovaKalkulacijaClicked == true)
            {
                return false;
            }
            else if (isNovaKalkulacijaClicked == false)
            {
                return true;
            }
            return true;
            // Button je dostupan samo kada isNovaKalkulacijaClicked = false
            //return !isNovaKalkulacijaClicked;
        }

        private bool CanExecuteTrue()
        {
            
            return true;
            // Button je dostupan samo kada isNovaKalkulacijaClicked = false
            //return !isNovaKalkulacijaClicked;
        }

        private bool CanExecuteFalse()
        {

            return false;
            // Button je dostupan samo kada isNovaKalkulacijaClicked = false
            //return !isNovaKalkulacijaClicked;
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
                    System.Windows.MessageBox.Show("Došli ste do zadnjeg podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    return;
                }

                CurrentIndex = (CurrentIndex + 1) % MatList.Count;
                CurrentItemMat = MatList[CurrentIndex];
                UpdateCurrentItemData(dbContext);
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
                }
                else if (resultMessageBox == MessageBoxResult.No)
                {
                    return;
                }
                UpdateCurrentItemData(dbContext);
            }
        }

        //Update forme sa novim unesenim vrijednostima
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

        //obrise sva polja i ostavlja opciju za SNIMI i ODUSTANI
        private void NovaKalkulacija()
        {
            //Ovo nam treba zbog ODUSTANI button (u ovom slučaju će biti button enable)
            isNovaKalkulacijaClicked = true;

            //Prolazi ponovo provjeru CanExecute
            SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();
            OdustaniCommand.RaiseCanExecuteChanged();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                MatList.Add(new Mat
                {
                    //Za ulaz materijala broj skladišta je uvijek 1000
                    Kljnaz = 1000
                }) ;

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

                //Staviti po datumu da sortira i da li nam ovo ucitavanje ponovno treba??
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz == 1000)
                    .OrderBy(row => row.Datun)
                    .ToList());

                //CurrentIndex = MatList.Count - 1;

                System.Windows.MessageBox.Show("Uspješno ste unijeli novi šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                //Ovo kada je true, onda ce buttoni biti dostupni
                isNovaKalkulacijaClicked = false;

                PrviButtonCommand.RaiseCanExecuteChanged();
                ZadnjiButtonCommand.RaiseCanExecuteChanged();
                NextButtonCommand.RaiseCanExecuteChanged();
                PrethodniButtonCommand.RaiseCanExecuteChanged();
                SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();
                OdustaniCommand.RaiseCanExecuteChanged();
                NovaKalkulacijaCommand.RaiseCanExecuteChanged();
                BrisanjeCommand.RaiseCanExecuteChanged();
                UpdateCommand.RaiseCanExecuteChanged();

                //spasi = true; 
                UpdateCurrentItemData(dbContext);

                //NextButtonCommand = new RelayCommand(NextButton, CanExecute2);
                //PrviButtonCommand = new RelayCommand(PrviButton, CanExecute2);
                //PrethodniButtonCommand = new RelayCommand(PrethodniButton, CanExecute2);
                //ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, CanExecute2);
                //BrisanjeCommand = new RelayCommand(Brisanje, CanExecute2);
                //UpdateCommand = new RelayCommand(Update, CanExecute2);
                //NovaKalkulacijaCommand = new RelayCommand(NovaKalkulacija, CanExecute2);
                ////Vidjeti kako staviti true vrijednost? iz nekog razloga ne odrazi se na UI?
                //SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, CanExecuteTrue);
                //OdustaniCommand = new RelayCommand(Odustani, CanExecuteTrue);

            }
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
                    dbContext.Mat.Remove(CurrentItemMat);
                    dbContext.SaveChanges();

                    MatList.Remove(CurrentItemMat);
                }
                else if (resultMessageBox == MessageBoxResult.No)
                {
                    return;
                }

                //Treba napraviti da vrati na prethodni koji je bio??
                UpdateCurrentItemData(dbContext);
            }
        }

        private void OtvoriKomitentListu()
        {
            _gvm.OdabraniVM = new KomitentiListaViewModel(this, _gvm);
        }

        // Ova metoda radi update CurrentItem i CurrentItemTabMaterijala based on the current index
        private void UpdateCurrentItemData(materijalno_knjigovodstvoContext dbContext)
        {
            //Kod prethodnog buttona treba da ovo preskoci???
            //if (selectedKomitent == null && prethodniButtonIsExecuted == false && nextButtonIsExecuted == false
            //    && prvoUcitavanje == false && spasi == false)
            //{
            //    CurrentIndex = MatList.Count - 1;
            //    CurrentItemMat = MatList[CurrentIndex];
            //}

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

            //Nadji listu svih po *Ident* iz *TabelaMaterijala* i *CurrentItem* (Mat) i stavi u listu
            TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).ToList());

            //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property
            CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).FirstOrDefault();

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

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
}