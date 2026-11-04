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
    public class PovratMaterijalaViewModel : INotifyPropertyChanged
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
        private bool isNovaKalkulacijaClicked = false;
        private bool isIzlazEnable = false;
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

        public SifarnikSkladista CurrentItemTabSkladista
        {
            get { return currentItemTabSkladista; }
            set
            {
                currentItemTabSkladista = value;
                OnPropertyChanged(nameof(currentItemTabSkladista));
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
                    SlijStavkaButtonCommand?.RaiseCanExecuteChanged(); // Notify the command to re-evaluate CanExecute
                }
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
                    OdustaniCommand?.RaiseCanExecuteChanged(); // Notify the command to re-evaluate CanExecute
                }
            }
        }

        // Tok rada: pregled -> unos -> snimljeno -> sljedeća stavka / kraj povrata.
        private bool _povratAktivan;
        public bool PovratAktivan
        {
            get => _povratAktivan;
            private set
            {
                if (_povratAktivan == value) return;
                _povratAktivan = value;
                OnPropertyChanged(nameof(PovratAktivan));
                OnPropertyChanged(nameof(NijeAktivanPovrat));
                UpdateCommands();
            }
        }
        public bool NijeAktivanPovrat => !PovratAktivan;

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
        public RelayCommand NoviPovratCommand { get; set; }
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
        public RelayCommand SlijStavkaButtonCommand { get; set; }

        #endregion

        #region Constructor

        public PovratMaterijalaViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;

            isNovaKalkulacijaClicked = false;

            #region Commands
            //PrviButtonCommand = new RelayCommand(PrviButton, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            NextButtonCommand = new RelayCommand(NextButton, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            //ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            BrisanjeCommand = new RelayCommand(Brisanje, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            UpdateCommand = new RelayCommand(Update, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            NoviPovratCommand = new RelayCommand(NoviPovrat, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => PovratAktivan && isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => PovratAktivan && isNovaKalkulacijaClicked && IsOdustaniEnabled);
            IzlazCommand = new RelayCommand(Izlaz, () => PovratAktivan && isIzlazEnable);
            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            //StampaCommand = new RelayCommand(Stampa, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton, () => PovratAktivan && !isNovaKalkulacijaClicked);

            PrintCommand = new RelayCommand(Print, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            OsvjeziCommand = new RelayCommand(Osvjezi);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            NovaStavkaCommand = new RelayCommand(NovaStavka, () => !PovratAktivan && CurrentItemMat != null && CurrentItemMat.Id > 0 && !string.IsNullOrWhiteSpace(CurrentItemMat.Brfak));

            #endregion

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //NextButtonCommand = new RelayCommand(NextButton);
                //PrethodniButtonCommand = new RelayCommand(PrethodniButton);
                //PrviButtonCommand = new RelayCommand(PrviButton);
                //ZadnjiButtonCommand = new RelayCommand(ZadnjiButton);

                //Dodaj u listu gdje je kljnaz između 1000 i 1012 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                // Include at most one row with Kljnaz == 0
                var baseList = dbContext.Mat
                    .Where(row => row.Status == "V" && row.Kljnaz >= 1001 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Datun)
                    .ToList();

                var zeroRow = dbContext.Mat
                    .Where(row => row.Status == "V" && row.Kljnaz == 0)
                    .OrderBy(row => row.Datun)
                    .FirstOrDefault();

                if (zeroRow != null && !baseList.Any(r => r.Id == zeroRow.Id))
                {
                    // Insert zero-row at the beginning (change to Add() to append instead)
                    baseList.Insert(0, zeroRow);
                }

                MatList = new ObservableCollection<Mat>(baseList);

                UpdateCurrentItemData(dbContext);
                //UpdateCurrentItemDataPovrat(dbContext);

                StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }

        public PovratMaterijalaViewModel()
        {

        }

        #endregion
        public PovratMaterijalaViewModel(GlavniViewModel gvm, Mat CurrentItemMat)
        {
            _gvm = gvm;
            this.CurrentItemMat = CurrentItemMat;

            isNovaKalkulacijaClicked = false;
            isIzlazEnable = false;
            PovratAktivan = false;

            #region Commands
            //PrviButtonCommand = new RelayCommand(PrviButton, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            NextButtonCommand = new RelayCommand(NextButton, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            //ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            BrisanjeCommand = new RelayCommand(Brisanje, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            UpdateCommand = new RelayCommand(Update, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            NoviPovratCommand = new RelayCommand(NoviPovrat, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => PovratAktivan && isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => PovratAktivan && isNovaKalkulacijaClicked && IsOdustaniEnabled);
            IzlazCommand = new RelayCommand(Izlaz, () => PovratAktivan && isIzlazEnable);
            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !PovratAktivan && !isNovaKalkulacijaClicked);
            PrintCommand = new RelayCommand(Print, () => !PovratAktivan && !isNovaKalkulacijaClicked);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            NovaStavkaCommand = new RelayCommand(NovaStavka, () => !PovratAktivan && CurrentItemMat != null && CurrentItemMat.Id > 0 && !string.IsNullOrWhiteSpace(CurrentItemMat.Brfak));
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton, () => PovratAktivan && !isNovaKalkulacijaClicked);
            #endregion

            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012 && row.Status == "V")
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
                    CurrentNazivZaSifruKomitenta = CurrentItemMat == null || string.IsNullOrEmpty(CurrentItemMat.Analst) ? ""
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
                .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012 && row.Status == "V")
                .OrderBy(row => row.Datun)
                .ToList());

            UpdateCurrentItemData(dbContext);
        }

        public void Osvjezi()
        {
            var dbContext = new materijalno_knjigovodstvoContext();

            // Kljnaz kolona
            int? maxValue_Skladiste = dbContext.SifarnikSkladista
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
                        CurrentNazivZaSifruKomitenta = CurrentItemMat == null || string.IsNullOrEmpty(CurrentItemMat.Analst) ? ""
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

                if (CurrentIndex == MatList.Count)
                {
                    CurrentIndex = MatList.Count - 1;
                }

                UpdateCurrentItemData(dbContext);
            }
        }

        private void Update()
        {

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (currentItemMat.Ident != null)
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

                dbContext.Update(CurrentItemMat);
                dbContext.SaveChanges();

                System.Windows.MessageBox.Show("Uspješno steizmijenili", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

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
            PovratAktivan = false;
            isNovaKalkulacijaClicked = false;
            _isNovaKalkulacijaClicked = false;
            IsOdustaniEnabled = false;
            isIzlazEnable = false;
            UpdateCommands();
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (MatList == null || MatList.Count == 0) { CurrentItemMat = null; return; }
                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = MatList[CurrentIndex];
                UpdateCurrentItemData(dbContext);
            }
        }

        private void BrojKalkulacije()
        {
            var dbContext = new materijalno_knjigovodstvoContext();

            MatList = new ObservableCollection<Mat>(dbContext.Mat
                               .Where(row => row.Status == "V")
                               .OrderBy(row => row.Datun)
                               .ToList());

            var posljednjiBrfak = MatList
            .OrderByDescending(x =>
            x.Brfak != null && x.Brfak.Contains('-')
            ? int.Parse(x.Brfak.Substring(x.Brfak.LastIndexOf('-') + 1))
            : int.MinValue
            ).Select(x => x.Brfak)
            .FirstOrDefault();

            var parts = posljednjiBrfak.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out int number))
            {
                number++;
                posljednjiBrfak = $"{parts[0]}-{number}";
            }
            CurrentItemMat.Brfak = posljednjiBrfak;
        }

        private void NoviPovrat()
        {
            //Prolazi ponovo provjeru CanExecute
            PovratAktivan = true;
            isNovaKalkulacijaClicked = true;
            IsOdustaniEnabled = true;
            isIzlazEnable = false;
            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                MatList.Add(new Mat
                {
                    //Za ulaz materijala broj skladišta je uvijek 1000
                    //Kljnaz = 1000
                    Status = "V",
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

            if (CurrentItemMat.Ourst == null)
            {
                CurrentItemMat.Ourst = 0;
            }

            if (CurrentItemMat.Mjtst == null)
            {
                CurrentItemMat.Mjtst = 0;
            }

            // Polja koja trebaju biti prazna umjesto NULL
            if (CurrentItemMat.Analst == null)
            {
                CurrentItemMat.Analst = "";
            }

            if (CurrentItemMat.Brnar == null)
            {
                CurrentItemMat.Brnar = "";
            }

            if (CurrentItemMat.Konto2 == null)
            {
                CurrentItemMat.Konto2 = "";
            }

            if (CurrentItemMat.Medus == null)
            {
                CurrentItemMat.Medus = "";
            }

            // VARCHAR polja koja trebaju imati vrijednost "0"
            if (string.IsNullOrWhiteSpace(CurrentItemMat.Fcj))
            {
                CurrentItemMat.Fcj = "0";
            }

            if (string.IsNullOrWhiteSpace(CurrentItemMat.Cartro))
            {
                CurrentItemMat.Cartro = "0";
            }

            if (string.IsNullOrWhiteSpace(CurrentItemMat.Zavtro))
            {
                CurrentItemMat.Zavtro = "0";
            }

            if (string.IsNullOrWhiteSpace(CurrentItemMat.Ppp))
            {
                CurrentItemMat.Ppp = "0";
            }

            if (string.IsNullOrWhiteSpace(CurrentItemMat.Fvrijed))
            {
                CurrentItemMat.Fvrijed = "0";
            }

            // Tarifa treba biti "1"
            if (string.IsNullOrWhiteSpace(CurrentItemMat.Tarifa))
            {
                CurrentItemMat.Tarifa = "1";
            }

            // DECIMAL polja
            if (CurrentItemMat.Troskovi == null)
            {
                CurrentItemMat.Troskovi = 0.00m;
            }

            if (CurrentItemMat.Porppp == null)
            {
                CurrentItemMat.Porppp = 0.00m;
            }

            if (CurrentItemMat.Trospe == null)
            {
                CurrentItemMat.Trospe = 0.00m;
            }


            //Ovo smo morali rucno za sada, dok se ne uradi validacija u xamlu
            if (CurrentItemMat.Kljnaz == null || CurrentItemMat.Datun == null || CurrentItemMat.Brfak == null ||
                CurrentItemMat.Brdok == null || CurrentItemMat.Datnar == null || CurrentItemMat.Redbr == null ||
                CurrentItemMat.Ident == null || CurrentItemMat.Kolic == null)
            {
                return false;
            }

            return true;
        }

        private void SpasiNovuKalkulaciju()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                currentItemMat.Status = "V";
                currentItemMat.Kontosklad1 = 0;

                if (CurrentItemMat.Ident != null)
                {
                    currentItemMat.Konto1 = (int?)dbContext.TabelaMaterijala
                    .Where(row => row.Ident == CurrentItemMat.Ident)
                    .Select(row => row.Konto1)
                    .FirstOrDefault();
                }

                if (CurrentItemMat.Kljnaz != null)
                {
                    var pronadjeniKontosklad = dbContext.SifarnikMaterijalSkladisteKonto
                        .Where(row => row.Sifskla == CurrentItemMat.Kljnaz)
                        .Select(row => row.Sifkonta)
                        .FirstOrDefault();

                    if (pronadjeniKontosklad == null)
                    {
                        System.Windows.MessageBox.Show(
                            $"Nije pronađen konto za skladište {CurrentItemMat.Kljnaz}!",
                            "Upozorenje",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    CurrentItemMat.Kontosklad = pronadjeniKontosklad;
                    CurrentItemMat.Kontosklad1 = 0;
                }

                //Ako validacija ne prodje, tj. ako fali neko polje
                if (ValidacijaSpasi() == false)
                {
                    System.Windows.MessageBox.Show("Molimo unesite sva polja!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;
                }

                //STAVITI 

                dbContext.Update(CurrentItemMat);
                dbContext.SaveChanges();

                //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz >= 1001 && row.Kljnaz <= 1012 && row.Status == "V")
                    .OrderBy(row => row.Datun)
                    .ToList());

                System.Windows.MessageBox.Show("Uspješno ste unijeli novi šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                // Snimljeno: samo Slij. stavka i Kraj povrata.
                isNovaKalkulacijaClicked = false;
                _isNovaKalkulacijaClicked = false;
                IsOdustaniEnabled = false;
                isIzlazEnable = true;
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
            NextButtonCommand?.RaiseCanExecuteChanged();
            PrethodniButtonCommand?.RaiseCanExecuteChanged();
            SpasiNovuKalkulacijuCommand?.RaiseCanExecuteChanged();
            OdustaniCommand?.RaiseCanExecuteChanged();
            NoviPovratCommand?.RaiseCanExecuteChanged();
            BrisanjeCommand?.RaiseCanExecuteChanged();
            UpdateCommand?.RaiseCanExecuteChanged();
            TraziSifruMaterijalaCommand?.RaiseCanExecuteChanged();
            PrintCommand?.RaiseCanExecuteChanged();
            SlijStavkaButtonCommand?.RaiseCanExecuteChanged();
            IzlazCommand?.RaiseCanExecuteChanged();
            NovaStavkaCommand?.RaiseCanExecuteChanged();
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
                    PovratAktivan = false;
                    IsOdustaniEnabled = false;
                    isIzlazEnable = false;
                    UpdateCommands();
                    //Prije odustajanja dohvati zadnji index, da bi mogao vratiti u pogledu, da ne ide na pocetak??

                    var matForDelete = dbContext.Mat
                .FirstOrDefault(x => x.Id == CurrentItemMat.Id);

                    if (matForDelete != null)
                    {
                        dbContext.Mat.Remove(matForDelete);
                        dbContext.SaveChanges();
                    }

                    MatList.Remove(CurrentItemMat);

                    isNovaKalkulacijaClicked = false;

                    CurrentItemMat = null;

                    _gvm.OdabraniVM = new PovratMaterijalaViewModel(_gvm);
                }
                else if (resultMessageBox == MessageBoxResult.No)
                {
                    return;
                }
            }
        }

        private void NovaStavka()
        {
            if (CurrentItemMat == null ||
                CurrentItemMat.Id <= 0 ||
                string.IsNullOrWhiteSpace(CurrentItemMat.Brfak))
            {
                System.Windows.MessageBox.Show(
                    "Prvo odaberite postojeći povrat preko Traži!");
                return;
            }

            try
            {
                using (var dbContext = new materijalno_knjigovodstvoContext())
                {
                    var odabrana = dbContext.Mat.AsNoTracking()
                        .FirstOrDefault(x =>
                            x.Id == CurrentItemMat.Id &&
                            x.Status == "V");

                    if (odabrana == null)
                    {
                        System.Windows.MessageBox.Show(
                            "Povrat nije pronađen u bazi.");
                        return;
                    }

                    int redbr = dbContext.Mat
                        .Where(x =>
                            x.Brfak == odabrana.Brfak &&
                            x.Kljnaz == odabrana.Kljnaz &&
                            x.Status == "V")
                        .Max(x => (int?)x.Redbr) ?? 0;

                    var nova = new Mat
                    {
                        // Podaci postojećeg povrata
                        Brfak = odabrana.Brfak,
                        Kljnaz = odabrana.Kljnaz,
                        Kljnaz1 = 0,

                        Datun = odabrana.Datun,
                        Datnar = odabrana.Datnar,
                        Brdok = odabrana.Brdok,

                        // Konto skladišta iz odabranog povrata
                        Kontosklad = odabrana.Kontosklad,
                        Kontosklad1 = 0,

                        // Nova stavka
                        Redbr = redbr + 1,
                        Status = "V",
                        Ident = 0,
                        Kolic = 0,
                        Nc = 0m,
                        Vrijed = 0m
                    };

                    dbContext.Mat.Add(nova);
                    dbContext.SaveChanges();

                    MatList.Add(nova);
                    CurrentIndex = MatList.Count - 1;
                    CurrentItemMat = nova;

                    // Nakon NOVA STAVKA dostupni su SNIMI i ODUSTANI
                    PovratAktivan = true;
                    isNovaKalkulacijaClicked = true;
                    isIzlazEnable = false;
                    IsOdustaniEnabled = true;

                    UpdateCommands();
                    UpdateCurrentItemData(dbContext);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Greška prilikom dodavanja stavke:\n" + ex.Message);
            }
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

                    Kontosklad = snimljeniCurrentMat.Kontosklad,
                    Kontosklad1 = 0,


                    Brfak = snimljeniCurrentMat.Brfak,
                    Datun = snimljeniCurrentMat.Datun,
                    Datnar = snimljeniCurrentMat.Datnar,
                    Brdok = snimljeniCurrentMat.Brdok,
                    Status = "V",
                    Kljnaz1 = 0
                });

                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = MatList[CurrentIndex];

                //Dodaje po jedan a treba isti
                //BrojKalkulacije();

                dbContext.Add(CurrentItemMat);
                dbContext.SaveChanges();

                // Sljedeća stavka: ponovo samo Snimi i Odustani.
                _isNovaKalkulacijaClicked = false;
                isNovaKalkulacijaClicked = true;
                isIzlazEnable = false;
                IsOdustaniEnabled = true;
                UpdateCommands();

                UpdateCurrentItemData(dbContext);
            }
        }

        private void OtvoriKomitentListu()
        {
            _gvm.OdabraniVM = new KomitentiListaViewModel(this, _gvm);
        }

        private void UpdateCurrentItemData(materijalno_knjigovodstvoContext dbContext)
        {
            if (MatList == null || MatList.Count == 0)
            {
                CurrentItemMat = null;
                CurrentItemTabMaterijala = null;
                CurrentItemTabSkladista = null;
                return;
            }
            if (CurrentIndex < 0 || CurrentIndex >= MatList.Count) CurrentIndex = 0;
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
                CurrentNazivZaSifruKomitenta = CurrentItemMat == null || string.IsNullOrEmpty(CurrentItemMat.Analst) ? ""
                    : StaraSifra_Ime_List.FirstOrDefault(row => row.STARA_SIFRA == CurrentItemMat.Analst)?.IME;
            }

            if (selectedKomitent != null)
            {
                CurrentItemMat.Analst = selectedKomitent.STARA_SIFRA;
                CurrentNazivZaSifruKomitenta = selectedKomitent.IME;
            }

            selectedKomitent = null;
            NovaStavkaCommand?.RaiseCanExecuteChanged();
        }

        //private void UpdateCurrentItemDataPovrat(materijalno_knjigovodstvoContext dbContext)
        //{
        //    CurrentItemMat = MatList[CurrentIndex];

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
        //}

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        #endregion

    }
}
