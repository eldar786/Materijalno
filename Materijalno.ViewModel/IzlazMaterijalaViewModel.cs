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
        public static bool isIzlazEnable = false;
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

        private bool _isOdustaniEnabled;

        //Ovo je uslov koji je vezan za Odustani button i ako za property kazemo false, automatski ce biti button disabled, ne treba pozivati dodatno 
        //OdustaniCommand.RaiseCanExecuteChanged();, jer je dodato u setter da to radi kada dodje do promjene vrijednosti
        public bool IsOdustaniEnabled
        {
            get => _isOdustaniEnabled;
            set
            {
                //ako je doslo do promjene vrijednosti, tj. ako vrijednost nije ista, ako je npr preslo iz true u false
                if (_isOdustaniEnabled != value)
                {
                    _isOdustaniEnabled = value;
                    OnPropertyChanged(nameof(IsOdustaniEnabled));
                    OdustaniCommand.RaiseCanExecuteChanged(); // Notify the command to re-evaluate CanExecute
                }
            }
        }

        public ObservableCollection<TabelaMaterijala> TebelaMaterijalaList { get; set; }
        public ObservableCollection<SifarnikSkladista> TebelaSkladistaList { get; set; }
        public ObservableCollection<Mat> MatList { get; set; }
        public List<Komitenti> StaraSifra_Ime_List { get; set; }

        #endregion

        private bool _isNovaKalkulacijaClicked;
        public bool IsNovaKalkulacijaClicked
        {
            get => _isNovaKalkulacijaClicked;
            set
            {
                if (_isNovaKalkulacijaClicked != value)
                {
                    _isNovaKalkulacijaClicked = value;
                    OnPropertyChanged(nameof(IsNovaKalkulacijaClicked));
                    SlijStavkaButtonCommand.RaiseCanExecuteChanged(); // Notify the command to re-evaluate CanExecute
                }
            }
        }

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
        public RelayCommand OsvjeziCommand { get; set; }
        public RelayCommand BrojKalkulacijeCommand { get; set; }
        public RelayCommand SlijStavkaButtonCommand { get; set; }


        #endregion

        #region Constructor

        public IzlazMaterijalaViewModel()
        {

        }
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
            OdustaniCommand = new RelayCommand(Odustani, () => IsOdustaniEnabled);
            IzlazCommand = new RelayCommand(Izlaz, () => isIzlazEnable);
            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !isNovaKalkulacijaClicked);
            //StampaCommand = new RelayCommand(Stampa, () => !isNovaKalkulacijaClicked);

            PrintCommand = new RelayCommand(Print, () => !isNovaKalkulacijaClicked);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            BrojKalkulacijeCommand = new RelayCommand(BrojKalkulacije);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton, () => IsNovaKalkulacijaClicked);
            #endregion

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaj u listu gdje je kljnaz između 1000 i 1012 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012 && row.Status == "I")
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
            OdustaniCommand = new RelayCommand(Odustani, () => IsOdustaniEnabled);
            IzlazCommand = new RelayCommand(Izlaz, () => isIzlazEnable);
            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !isNovaKalkulacijaClicked);
            PrintCommand = new RelayCommand(Print, () => !isNovaKalkulacijaClicked);

            BrojKalkulacijeCommand = new RelayCommand(BrojKalkulacije);
            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton, () => !IsNovaKalkulacijaClicked);
            #endregion

            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012 && row.Status == "I")
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
            var dbContext = new materijalno_knjigovodstvoContext();

            CurrentItemMat.Nc = dbContext.Mat
                .Where(row => row.Ident == CurrentItemMat.Ident)
                .Select(row => row.Nc).FirstOrDefault();

            var culture = new CultureInfo("de-DE");

            if (CurrentItemMat.Kolic.HasValue && CurrentItemMat.Kolic.Value != 0)
            {
                CurrentItemMat.Vrijed = (decimal)CurrentItemMat.Kolic.Value * CurrentItemMat.Nc;
            }
            else
            {
                // U slucaju da je Kolic null ili nula, da bi izbjegli dijeljenje sa nulom
                CurrentItemMat.Nc = 0;
            }

            dbContext.Update(CurrentItemMat);
            dbContext.SaveChanges();

            //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
            MatList = new ObservableCollection<Mat>(dbContext.Mat
                .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012 && row.Status == "I")
                .OrderBy(row => row.Datun)
                .ToList());

            UpdateCurrentItemData(dbContext);
        }

        public void Osvjezi()
        {
            var dbContext = new materijalno_knjigovodstvoContext();

            // Kljnaz kolona
            int? maxValue_Skladiste = dbContext.Mat
                .Max(row => row.Kljnaz);

            int? minValue_Skladiste = dbContext.Mat
                .Min(row => row.Kljnaz);

            // Ident kolona
            int? maxValue_SifraMat = dbContext.Mat
                .Max(row => row.Ident);

            int? minValue_SifraMat = dbContext.Mat
                .Min(row => row.Ident);

            if (CurrentItemMat.Kljnaz == 1000)
            {
                System.Windows.MessageBox.Show("Ne možete praviti izlaz iz Centralnog magacina!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                CurrentItemMat.Kljnaz = 0;
            }
            else if (CurrentItemMat.Kljnaz < minValue_Skladiste || CurrentItemMat.Kljnaz > maxValue_Skladiste)
            {
                System.Windows.MessageBox.Show("Skladište nije prijavljeno u šifarnik!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                //TextBox Skladiste vratiti na prazno
                CurrentItemMat.Kljnaz = 0;
            }
            if (CurrentItemMat.Ident < minValue_SifraMat || CurrentItemMat.Ident > maxValue_SifraMat)
            {
                System.Windows.MessageBox.Show("Materijal ne postoji!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                CurrentItemMat.Ident = 0;
            }
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

                }
                else if (resultMessageBox == MessageBoxResult.No)
                {
                    return;
                }

                //Kada zadnji brise, ne vraca index
                //Jedna opcija jeste da ponovo ocitamo MatListu
                //Ako je CurrentIndex zadnji broj kao kod MatList.Count onda nakon brisanja da ide CurrentIdex - 1
                if (MatList.Count == CurrentIndex)
                {
                    CurrentItemMat = MatList[CurrentIndex - 1];
                }
                else
                {
                    CurrentItemMat = MatList[CurrentIndex];
                }

                UpdateCurrentItemData(dbContext);
            }
        }

        private void Update()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                // Na osnovu Ident od CurrentItemMat, daj mi Konto1 iz tabele TabelaMaterijala i dodijeli u CurrentItemMat u Mat tabeli
                // Trenutno u If, ali treba uraditi validaciju
                if (CurrentItemMat.Ident != null)
                {
                    CurrentItemMat.Konto1 = (int?)dbContext.TabelaMaterijala
                    .Where(row => row.Ident == CurrentItemMat.Ident)
                    .Select(row => row.Konto1)
                    .FirstOrDefault();
                }

                if (currentItemMat.Kljnaz != null)
                {
                    currentItemMat.Kontosklad = dbContext.SifarnikMaterijalSkladisteKonto
                    .Where(row => row.Sifskla == currentItemMat.Kljnaz && row.Sifmat == CurrentItemMat.Ident)
                    .Select(row => row.Sifkonta)
                    .FirstOrDefault();
                }

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
            //Pozivano ovu metodu zbog ukupnoTroskovi, da bi prilikom printa izracunao, da ne bi morali ponovo racunati
            NabavnaCijena();
            var culture = new CultureInfo("de-DE");

            decimal? ukupnoNcValue = CurrentItemMat.Kolic * CurrentItemMat.Nc;
            //treba vidjeti kako da prebaci na DE culture???
            ukupnoNc = Math.Round((decimal)ukupnoNcValue, 9);
            decimal? formmatedNc = decimal.Parse(ukupnoNc.ToString(), NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, culture);
            ukupnoNc = formmatedNc;

            OnPrintEvent?.Invoke();
        }

        // An event that will be raised to notify the view to open the PrintWindow
        public event Action OnPrintEvent;

        private void Izlaz()
        {
            //Disabled SNIMI i NOVA STAVKA, treba jos Slij.STAVKA i ODUSTANI

            isNovaKalkulacijaClicked = false;
            _isNovaKalkulacijaClicked = false;
            isIzlazEnable = false;
            IzlazCommand.RaiseCanExecuteChanged();



            //ODUSTANI COMMAND treba da bude nedostupno nakon Kraj Izlaza??
            IsOdustaniEnabled = false;
            OdustaniCommand.RaiseCanExecuteChanged();

            UpdateCommands();
            SlijStavkaButtonCommand.RaiseCanExecuteChanged();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {


                //Samo vrati buttone i zadnji kreiran
                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = MatList[CurrentIndex];

                //_gvm.OdabraniVM = new IzlazMaterijalaViewModel(_gvm);

                UpdateCurrentItemData(dbContext);
            }
        }

        private void BrojKalkulacije()
        {
            var dbContext = new materijalno_knjigovodstvoContext();

            MatList = new ObservableCollection<Mat>(dbContext.Mat
                               .Where(row => row.Status == "I")
                               .OrderBy(row => row.Datun)
                               .ToList());

            //Proci kroz MatListu i naci posljednji "brfak" i dodati +1
            var posljednjiBrfak = MatList
            .OrderByDescending(x =>
            x.Brfak != null && x.Brfak.Contains('-') // Provjeravamo da li Brfak is not null and sadrzi '-'
            ? int.Parse(
                x.Brfak.Substring(
                    x.Brfak.LastIndexOf('-') + 1
                )
              )
            : int.MinValue // Koristi defaultnu vrijednost za null ili ako je invalide
            ).Select(x => x.Brfak)
            .FirstOrDefault();

            var parts = posljednjiBrfak.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out int number)) // Parsiraj drugi dio (poslije -)
            {
                number++; // Povecaj za jedan
                posljednjiBrfak = $"{parts[0]}-{number}"; // Dodaj dvije cjeline u posljednjiBrfak
            }
            //Stavili smo if, jer pada kada brisemo, CurrentItemMat.Brfak bude null
            //if (CurrentItemMat.Brfak != null)
            //{
            CurrentItemMat.Brfak = posljednjiBrfak;
            //}
        }

        //obrise sva polja i ostavlja opciju za SNIMI i ODUSTANI
        private void NovaKalkulacija()
        {
            //Prolazi ponovo provjeru CanExecute
            isNovaKalkulacijaClicked = true;
            IsOdustaniEnabled = true;

            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                MatList.Add(new Mat
                {
                    //Za ulaz materijala broj skladišta je uvijek 1000
                    //Kljnaz = 1000
                    Status = "I",
                    Kljnaz1 = 0,
                    Redbr = 1
                });

                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = MatList[CurrentIndex];

                //CurrentItemMat.Status = "I";
                //CurrentItemMat.Kljnaz1 = 0;
                //CurrentItemMat.Redbr = 1;

                BrojKalkulacije();

                dbContext.Add(CurrentItemMat);
                dbContext.SaveChanges();

                UpdateCurrentItemData(dbContext);
            }
        }

        private bool ValidacijaSpasi()
         {
            var property = typeof(Mat).GetProperties();
            bool allNull = property.All(prop => prop.GetValue(CurrentItemMat) == null);

            if (CurrentItemMat.Ourst == null || CurrentItemMat.Mjtst == null)
            {
                CurrentItemMat.Ourst = 000;
                CurrentItemMat.Mjtst = 000000;
            }

            //Ovo smo morali rucno za sada, dok se ne uradi validacija u xamlu
            if (CurrentItemMat.Kljnaz == null || CurrentItemMat.Datun == null || CurrentItemMat.Brfak == null ||
                CurrentItemMat.Brdok == null || CurrentItemMat.Datnar == null || CurrentItemMat.Redbr == null ||
                CurrentItemMat.Ident == null || CurrentItemMat.Kolic == null || CurrentItemMat.Nc == null ||
                CurrentItemMat.Vrijed == null)
            {
                return false;
            }

            return true;
        }

        private bool ValidacijaZaliheMaterijala()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Validacija *KOLICINA VECA OD ZALIHE MATERIJALA!*
                //1. Iz pocetnog stanja za trenutnu godinu uzmemo vrijednost materijala za dato skladiste
                //2. Onda od pocetnog stanja + Ulazi(za taj magacin i taj materijal) - Izlazi(za taj magacin i taj materijal)
                //3. Ako je Izlaz Vrijednost ili količine veci od stvarne zalihe materijala tj. ako ode ispod nule onda izbaci poruku

                int? ukupnoUlaziMaterijal = new int();
                int? ukupnoIzlaziMaterijal = new int();

                //POCETNO STANJE
                int pocetnoStanje = (int)dbContext.Mat
                    .Where(row => row.Kljnaz == currentItemMat.Kljnaz && row.Ident == currentItemMat.Ident && row.Status == "P").Select(row => row.Kolic).FirstOrDefault();

                //ULAZI ZA MATERIJAL I SKLADISTE
                ObservableCollection<Mat> ulaziMaterijal = new ObservableCollection<Mat>(dbContext.Mat.Where(row => row.Kljnaz == currentItemMat.Kljnaz && row.Ident == currentItemMat.Ident
                && row.Status == "U").ToList());

                //Idi kroz listu i saberi sve Vrijednosti
                foreach (var item in ulaziMaterijal)
                {
                    ukupnoUlaziMaterijal += item.Kolic;
                }

                //IZLAZI ZA MATERIJAL I SKLADISTE
                ObservableCollection<Mat> izlaziMaterijal = new ObservableCollection<Mat>(dbContext.Mat.Where(row => row.Kljnaz == currentItemMat.Kljnaz && row.Ident == currentItemMat.Ident
                && row.Status == "I").ToList());

                foreach (var item in izlaziMaterijal)
                {
                    ukupnoIzlaziMaterijal += item.Kolic;
                }

                int? stvarnoStanje = pocetnoStanje + ukupnoUlaziMaterijal - (-ukupnoIzlaziMaterijal);

                if (stvarnoStanje < 0)
                {
                    System.Windows.MessageBox.Show("Količina je veća od zaliha materijala!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                    
                    return false;
                }
            }
            return true;
        }

        //Pokupi sva polja trenutna i spasi. Trebalo bi napraviti disabled SAVE button ako nije odabrana nova kalkulacija
        private void SpasiNovuKalkulaciju()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                // Na osnovu Ident od CurrentItemMat, daj mi Konto1 iz tabele TabelaMaterijala i dodijeli u CurrentItemMat u Mat tabeli
                // Trenutno u If, ali treba uraditi validaciju

                currentItemMat.Status = "I";
                currentItemMat.Kontosklad1 = 0;

                if (CurrentItemMat.Ident != null)
                {
                    currentItemMat.Konto1 = (int?)dbContext.TabelaMaterijala
                    .Where(row => row.Ident == CurrentItemMat.Ident)
                    .Select(row => row.Konto1)
                    .FirstOrDefault();
                }

                if (currentItemMat.Kljnaz != null)
                {
                    currentItemMat.Kontosklad = dbContext.SifarnikMaterijalSkladisteKonto
                    .Where(row => row.Sifskla == currentItemMat.Kljnaz && row.Sifmat == CurrentItemMat.Ident)
                    .Select(row => row.Sifkonta)
                    .FirstOrDefault();
                }

                if (ValidacijaZaliheMaterijala() == false)
                {
                    return;
                }

                //Pc stanje + Ulazi + Izlazi (to je stvarno stanje) ako je novi izlaz veci od stavrnog stanja onda treba da izbaci gresku poruku

                //Ako validacija ne prodje, tj. ako fali neko polje
                if (ValidacijaSpasi() == false)
                {
                    System.Windows.MessageBox.Show("Molimo unesite sva polja!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;
                }

                dbContext.Update(CurrentItemMat);
                dbContext.SaveChanges();

                //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012 && row.Status == "I")
                    .OrderBy(row => row.Datun)
                    .ToList());

                System.Windows.MessageBox.Show("Uspješno ste unijeli novi šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                //Ovo kada je true, onda ce buttoni biti dostupni
                isNovaKalkulacijaClicked = false;
                _isNovaKalkulacijaClicked = true;
                IsOdustaniEnabled = true;
                isIzlazEnable = true;

                SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();
                SlijStavkaButtonCommand.RaiseCanExecuteChanged();

                //Treba da je dostupan
                IzlazCommand.RaiseCanExecuteChanged();

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
            //IzlazCommand.RaiseCanExecuteChanged();
            SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();
            OdustaniCommand.RaiseCanExecuteChanged();
            SlijStavkaButtonCommand.RaiseCanExecuteChanged();
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

        private void SlijStavkaButton()
        {
            //Ostavi prve stavke, dodaj redni broj, ostala polja ostavi prazno i spasi kao novi currentItemMat
            Mat snimljeniCurrentMat = currentItemMat;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaje u listu novi CurrentItemMat
                MatList.Add(new Mat
                {
                    //Za ulaz materijala broj skladišta je uvijek 1000
                    //Kljnaz = 1000
                    Redbr = snimljeniCurrentMat.Redbr + 1,
                    Kljnaz = snimljeniCurrentMat.Kljnaz,
                    Brfak = snimljeniCurrentMat.Brfak,
                    Datun = snimljeniCurrentMat.Datun,
                    Datnar = snimljeniCurrentMat.Datnar,
                    Brdok = snimljeniCurrentMat.Brdok,
                    Status = "I",
                    Kljnaz1 = 0
                });

                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = MatList[CurrentIndex];

                //Dodaje po jedan a treba isti
                //BrojKalkulacije();

                dbContext.Add(CurrentItemMat);
                dbContext.SaveChanges();

                //kad uradi SlijedStavka, treba da ostane SNIMI i ODUSTANI


                //Ovo kada je true, onda ce buttoni biti dostupni
                _isNovaKalkulacijaClicked = false;
                isNovaKalkulacijaClicked = true;
                isIzlazEnable = false;
                IsOdustaniEnabled = true;
                SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();

                //Ova commanda zavisi od "_isNovaKalkulacijaClicked"
                SlijStavkaButtonCommand.RaiseCanExecuteChanged();
                IzlazCommand.RaiseCanExecuteChanged();

                UpdateCurrentItemData(dbContext);
            }
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
