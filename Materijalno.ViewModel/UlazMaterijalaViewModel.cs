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
using Newtonsoft.Json.Linq;

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
        public static Komitenti selectedKomitent;
        public static Mat selectedMat;
        string connectionString = "Server= 192.168.1.213;Trusted_Connection=False;" +
            "MultipleActiveResultSets=true;User Id=sa;Password=Lutrija1;";
        string queryStringStaraSifra_Ime = "SELECT STARA_SIFRA, IME FROM [FINANSIJE2-LINKED SERVER]..[IBS].[GR_KOMITENTI]";
        string currentNazivZaSifruKomitenta;
        public int CurrentIndex = 0;
        public static bool isNovaKalkulacijaClicked = false;
        public static bool isTraziClicked = false;
        public event PropertyChangedEventHandler PropertyChanged;

        private int? idNoveKalkulacije = null;

        private bool fakturaUToku = false;
        private bool trenutnaStavkaSnimljena = false;
        private bool novaStavkaUToku = false;

        public event Action RequestFocusSlijedecaStavka;
        public event Action RequestFocusIdent;
        public event Action RequestFocusAnalst;

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
                OnPropertyChanged(nameof(FcjFormatted));
                OnPropertyChanged(nameof(FvrijedFormatted));
                OnPropertyChanged(nameof(UkupnoTroskovi));

                UcitajNazivKonta1();
            }
        }

        public string FcjFormatted
        {
            get
            {
                if (CurrentItemMat == null ||
                    string.IsNullOrWhiteSpace(CurrentItemMat.Fcj))
                    return "";

                if (decimal.TryParse(
                    CurrentItemMat.Fcj,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal fcj))
                {
                    // Uzmi samo prve 4 decimale, bez zaokruživanja
                    decimal fcjPrikaz = Math.Truncate(fcj * 10000m) / 10000m;

                    return fcjPrikaz.ToString(
                        "N4",
                        System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                }

                return CurrentItemMat.Fcj;
            }
        }



        public string FvrijedFormatted
        {
            get
            {
                if (CurrentItemMat == null ||
                    string.IsNullOrWhiteSpace(CurrentItemMat.Fvrijed))
                    return "";

                if (decimal.TryParse(
                    CurrentItemMat.Fvrijed,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal vrijednost))
                {
                    return vrijednost.ToString(
                        "N2",
                        System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                }

                return CurrentItemMat.Fvrijed;
            }
        }

        public decimal UkupnoTroskovi
        {
            get
            {
                if (CurrentItemMat == null)
                    return 0m;

                decimal cartro = 0m;
                decimal zavtro = 0m;

                TryProcitajDecimal(CurrentItemMat.Cartro, out cartro);
                TryProcitajDecimal(CurrentItemMat.Zavtro, out zavtro);

                decimal trospe = CurrentItemMat.Trospe ?? 0m;

                return cartro + trospe + zavtro;
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
                OnPropertyChanged(nameof(CurrentItemTabMaterijala));
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


        private string _nazivKonta1;

        public string NazivKonta1
        {
            get => _nazivKonta1;
            set
            {
                _nazivKonta1 = value;
                OnPropertyChanged(nameof(NazivKonta1));
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
        public RelayCommand NabavnaCijenaCommand { get; set; }

        //Potrebno uraditi ???
        public RelayCommand PrintCommand { get; set; }
        public RelayCommand OtvoriKomitentListuCommand { get; set; }
        public RelayCommand TraziSifruMaterijalaCommand { get; set; }

        public RelayCommand OdustaniCommand { get; set; }
        public RelayCommand OsvjeziCommand { get; set; }
        public RelayCommand BrojKalkulacijeCommand { get; set; }
        public RelayCommand SlijedecaStavkaCommand { get; set; }
        public RelayCommand KrajFaktureCommand { get; set; }

        public RelayCommand NovaStavkaButtonCommand { get; set; }


        #endregion

        #region Constructor

        public UlazMaterijalaViewModel(GlavniViewModel gvm)
        {
            _gvm = gvm;
            //Prilikom otvaranja Ulaza iz nove kalkulacije, treba promijeniti u false
            isNovaKalkulacijaClicked = false;

            #region Commands
            PrviButtonCommand = new RelayCommand(PrviButton, () => !fakturaUToku);
            NextButtonCommand = new RelayCommand(NextButton, () => !fakturaUToku);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !fakturaUToku);
            ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !fakturaUToku);
            BrisanjeCommand = new RelayCommand(Brisanje, () => !fakturaUToku);
            UpdateCommand = new RelayCommand(Update, () => !fakturaUToku);
            //NovaKalkulacijaCommand = new RelayCommand(NovaKalkulacija, () => !isNovaKalkulacijaClicked);
            NovaKalkulacijaCommand = new RelayCommand(NovaKalkulacija,() => !fakturaUToku);
            //SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => isNovaKalkulacijaClicked);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => fakturaUToku && novaStavkaUToku);
            //OdustaniCommand = new RelayCommand(Odustani, () => isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani,() => fakturaUToku && novaStavkaUToku);

            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !fakturaUToku);
            PrintCommand = new RelayCommand(Print, () => !fakturaUToku);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            BrojKalkulacijeCommand = new RelayCommand(BrojKalkulacije);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            SlijedecaStavkaCommand = new RelayCommand( SlijedecaStavka, () => fakturaUToku && trenutnaStavkaSnimljena);

            KrajFaktureCommand = new RelayCommand( KrajFakture, () => fakturaUToku && trenutnaStavkaSnimljena);
            NovaStavkaButtonCommand = new RelayCommand(NovaStavkaButton,() => !fakturaUToku && CurrentItemMat != null && CurrentItemMat.Id > 0 && CurrentItemMat.Status == "U" && !string.IsNullOrWhiteSpace(CurrentItemMat.Brfak));

            #endregion

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz == 1000 && row.Status == "U")
                    .OrderBy(row => row.Datun)
                    .ToList());



                
                UpdateCurrentItemData(dbContext);
                
                

                StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }

        public UlazMaterijalaViewModel(GlavniViewModel gvm, Mat CurrentItemMat)
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
            PrviButtonCommand = new RelayCommand(PrviButton, () => !fakturaUToku);
            NextButtonCommand = new RelayCommand(NextButton, () => !fakturaUToku);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !fakturaUToku);
            ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !fakturaUToku);
            BrisanjeCommand = new RelayCommand(Brisanje, () => !fakturaUToku);
            UpdateCommand = new RelayCommand(Update, () => !fakturaUToku);
            NovaKalkulacijaCommand = new RelayCommand(NovaKalkulacija,() => !fakturaUToku);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => isNovaKalkulacijaClicked);

            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !fakturaUToku);
            PrintCommand = new RelayCommand(Print, () => !fakturaUToku);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            BrojKalkulacijeCommand = new RelayCommand(BrojKalkulacije);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            SlijedecaStavkaCommand = new RelayCommand(SlijedecaStavka, () => fakturaUToku && trenutnaStavkaSnimljena);

            KrajFaktureCommand = new RelayCommand( KrajFakture, () => fakturaUToku && trenutnaStavkaSnimljena);

            NovaStavkaButtonCommand = new RelayCommand(NovaStavkaButton, () => !fakturaUToku && CurrentItemMat != null && CurrentItemMat.Id > 0 && CurrentItemMat.Status == "U" && !string.IsNullOrWhiteSpace(CurrentItemMat.Brfak));


            #endregion

            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                    .Where(row => row.Kljnaz == 1000 && row.Status == "U")
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
                //!!! - OVDJE PADA !!!
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

                StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }

        #endregion

        #region Methods

        //public void NabavnaCijena()
        //{
        //    var dbContext = new materijalno_knjigovodstvoContext();

        //    decimal? inputValue1 = CurrentItemMat.Vrijed;
        //    //Troskovi speditera
        //    decimal? inputValue2 = CurrentItemMat.Trospe;
        //    //Iznos PDV-a (iskljucen iz kalkulacije za izracun NC)
        //    decimal? inputValue3 = CurrentItemMat.Porppp;
        //    decimal? inputValue4 = CurrentItemMat.Troskovi;
        //    decimal carinaValue = 0.00m;

        //    // Ovo radimo zato sto je "Cartro" string i vraca null kada se ne dodijeli vrijednost (trenutno rjesenje)
        //    if (CurrentItemMat.Cartro == null)
        //    {
        //        CurrentItemMat.Cartro = "0,00";
        //    }
        //    else
        //    {
        //        carinaValue = decimal.Parse(CurrentItemMat.Cartro);
        //    }

        //    var culture = new CultureInfo("de-DE");

        //        decimal? value1 = inputValue1.HasValue ? (decimal?)inputValue1.Value : 0;

        //    decimal? value2 = inputValue2.HasValue ? (decimal?)inputValue2.Value : 0;
        //    // Iznos PDV-a
        //    decimal? value3 = inputValue3.HasValue ? (decimal?)inputValue3.Value : 0;

        //    decimal? value4 = inputValue4.HasValue ? (decimal?)inputValue4.Value : 0;

        //    //Vjerovatno nam ne trebaju ostale vrijednosti za izracuj NC - Dovoljno je vrijed/kolic
        //    //decimal? sum = value1 + value2 + value4 + carinaValue;

        //    decimal? sum = value1 + value2 + value3 + value4;

        //    if (CurrentItemMat.Kolic.HasValue && CurrentItemMat.Kolic.Value != 0)
        //    {
        //        decimal? nc = (sum / (decimal)CurrentItemMat.Kolic);
        //        CurrentItemMat.Nc = (sum / (decimal)CurrentItemMat.Kolic.Value);
        //    }
        //    else
        //    {
        //        // U slucaju da je Kolic null ili nula, da bi izbjegli dijeljenje sa nulom
        //        CurrentItemMat.Nc = 0;
        //    }

        //    dbContext.Update(CurrentItemMat);
        //    dbContext.SaveChanges();

        //    //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
        //    MatList = new ObservableCollection<Mat>(dbContext.Mat
        //        .Where(row => row.Kljnaz == 1000 && row.Status == "U")
        //        .OrderBy(row => row.Datun)
        //        .ToList());

        //    UpdateCurrentItemData(dbContext);
        //}

        private void UcitajNazivKonta1()
        {
            NazivKonta1 = "";

            if (CurrentItemMat == null || !CurrentItemMat.Konto1.HasValue)
                return;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                int konto1 = CurrentItemMat.Konto1.Value;

                NazivKonta1 = dbContext.SifarnikKonta
                    .Where(x => x.Sifkonta == konto1)
                    .Select(x => x.Nazkont)
                    .FirstOrDefault() ?? "";
            }
        }

        public void NabavnaCijena()
        {
            if (CurrentItemMat == null)
                return;

            decimal kolicina = CurrentItemMat.Kolic ?? 0m;

            if (kolicina <= 0m)
            {
                CurrentItemMat.Nc = 0m;
                OnPropertyChanged(nameof(CurrentItemMat));
                return;
            }

            decimal fakturnaVrijednost;
            decimal zavisniTroskovi;
            decimal carina;

            // Fakturna vrijednost
            if (!TryProcitajDecimal(
                CurrentItemMat.Fvrijed,
                out fakturnaVrijednost))
                return;

            // Ostali troškovi
            if (!TryProcitajDecimal(
                CurrentItemMat.Zavtro,
                out zavisniTroskovi))
                return;

            // Carina
            if (!TryProcitajDecimal(
                CurrentItemMat.Cartro,
                out carina))
                return;

            decimal pdv = CurrentItemMat.Porppp ?? 0m;
            decimal troskoviSpeditera = CurrentItemMat.Trospe ?? 0m;

            // 1. UKUPNI DODATNI TROŠKOVI
            decimal ukupniTroskovi =
                carina +
                troskoviSpeditera +
                zavisniTroskovi;

            // 2. PROCENAT TROŠKOVA
            CurrentItemMat.Troskovi = fakturnaVrijednost > 0m
                ? Math.Round(
                    ukupniTroskovi / fakturnaVrijednost * 100m,
                    2,
                    MidpointRounding.AwayFromZero)
                : 0m;

            // 3. UKUPNA VRIJEDNOST
            decimal ukupnaVrijednost =
                fakturnaVrijednost +
                pdv +
                ukupniTroskovi;

            CurrentItemMat.Vrijed = ukupnaVrijednost;

            // 4. NABAVNA CIJENA
            CurrentItemMat.Nc = Math.Round(
                ukupnaVrijednost / kolicina,
                9,
                MidpointRounding.AwayFromZero);

            // Osvježi prikaz NC u XAML-u
            OnPropertyChanged(nameof(CurrentItemMat));
            OnPropertyChanged(nameof(UkupnoTroskovi));
        }


        private bool TryProcitajDecimal(string tekst, out decimal vrijednost)
        {
            vrijednost = 0m;

            if (string.IsNullOrWhiteSpace(tekst))
                return true;

            tekst = tekst.Trim();

            CultureInfo kultura = tekst.Contains(",")
                ? CultureInfo.GetCultureInfo("de-DE")
                : CultureInfo.InvariantCulture;

            return decimal.TryParse(
                tekst,
                NumberStyles.Number,
                kultura,
                out vrijednost);
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
                if (MatList == null || MatList.Count == 0)
                {
                    CurrentItemMat = null;
                    System.Windows.MessageBox.Show("Nema podataka za brisanje.", "Upozorenje",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2) Guard: invalid index -> clamp
                if (CurrentIndex < 0) CurrentIndex = 0;
                if (CurrentIndex >= MatList.Count) CurrentIndex = MatList.Count - 1;

                // 3) Get the current item safely
                var itemToDelete = MatList[CurrentIndex];

                CurrentItemMat = MatList[CurrentIndex];

                var resultMessageBox = System.Windows.MessageBox.Show("Želite li obrisati tekući podatak? ", "Upozorenje", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (resultMessageBox == MessageBoxResult.Yes)
                {
                    dbContext.Mat.Remove(CurrentItemMat);
                    dbContext.SaveChanges();

                    MatList.Remove(CurrentItemMat);

                    //currentItemMat = null;
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


        private bool IzracunajFcj()
        {
            if (CurrentItemMat == null)
                return false;

            string fv = CurrentItemMat.Fvrijed?.Trim();

            if (string.IsNullOrWhiteSpace(fv))
            {
                System.Windows.MessageBox.Show("Unesite fakturnu vrijednost.");
                return false;
            }

            decimal fakturnaVrijednost;
            bool uspjesno;

            if (fv.Contains(","))
            {
                // Primjeri: 3149,85 ili 3.149,85
                uspjesno = decimal.TryParse(
                    fv,
                    NumberStyles.Number,
                    CultureInfo.GetCultureInfo("de-DE"),
                    out fakturnaVrijednost);
            }
            else
            {
                // Primjeri: 3149.85 ili 3149
                uspjesno = decimal.TryParse(
                    fv,
                    NumberStyles.AllowDecimalPoint |
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out fakturnaVrijednost);
            }

            if (!uspjesno)
            {
                System.Windows.MessageBox.Show(
                    "Fakturna vrijednost nije ispravno unesena.",
                    "Greška",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            decimal kolicina = CurrentItemMat.Kolic ?? 0m;

            if (kolicina <= 0m)
            {
                System.Windows.MessageBox.Show(
                    "Količina mora biti veća od nule.",
                    "Greška",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            // FCJ = Fakturna vrijednost / Količina
            decimal fcj = Math.Round(
                fakturnaVrijednost / kolicina,
                8,
                MidpointRounding.AwayFromZero);

            // Obje kolone su varchar u SQL bazi
            CurrentItemMat.Fvrijed = fakturnaVrijednost.ToString(
                "0.00",
                CultureInfo.InvariantCulture);

            CurrentItemMat.Fcj = fcj.ToString(
                 "0.########",
                CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(FcjFormatted));

            return true;
        }

        //Update forme sa novim unesenim vrijednostima
        // Update forme sa novim unesenim vrijednostima
        private void Update()
        {
            if (CurrentItemMat == null)
                return;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                // Na osnovu Ident dohvati Konto1
                if (CurrentItemMat.Ident != null)
                {
                    CurrentItemMat.Konto1 = (int?)dbContext.TabelaMaterijala
                        .Where(row => row.Ident == CurrentItemMat.Ident)
                        .Select(row => row.Konto1)
                        .FirstOrDefault();
                }

                UcitajNazivKonta1();

                // NOVO: Automatski preračunaj FCJ
                if (!IzracunajFcj())
                    return;

                // Snimanje izmjena u SQL bazu
                dbContext.Update(CurrentItemMat);
                dbContext.SaveChanges();

                System.Windows.MessageBox.Show(
                    "Uspješno ste izmijenili",
                    "Potvrda",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Vrati prikaz na izmijenjeni podatak
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

        private void Osvjezi()
        {
            var dbContext = new materijalno_knjigovodstvoContext();

            // Ident kolona
            int? maxValue_SifraMat = dbContext.Mat
                .Max(row => row.Ident);

            int? minValue_SifraMat = dbContext.Mat
                .Min(row => row.Ident);

            if (CurrentItemMat.Ident < minValue_SifraMat || CurrentItemMat.Ident > maxValue_SifraMat)
            {
                System.Windows.MessageBox.Show("Materijal ne postoji!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                CurrentItemMat.Ident = 0;
            }

            dbContext.Update(CurrentItemMat);
            dbContext.SaveChanges();

            if (CurrentItemMat.Ident != null)
            {
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                               .Where(row => row.Kljnaz == 1000 && row.Status == "U")
                               .OrderBy(row => row.Datun)
                               .ToList());
            }

            UpdateCurrentItemData(dbContext);
        }

        private void BrojKalkulacije()
        {
            if (CurrentItemMat == null)
                return;

            // Ako trenutna kalkulacija već ima broj,
            // nemoj generisati novi.
            if (!string.IsNullOrWhiteSpace(CurrentItemMat.Brfak))
                return;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                var sviBrojevi = dbContext.Mat
                    .Where(x => x.Status == "U" && x.Brfak != null)
                    .Select(x => x.Brfak)
                    .ToList();

                int najveciBroj = 0;

                foreach (var brfak in sviBrojevi)
                {
                    if (string.IsNullOrWhiteSpace(brfak))
                        continue;

                    var dijelovi = brfak.Split('-');

                    if (dijelovi.Length == 2 &&
                        dijelovi[0].Trim() == "10" &&
                        int.TryParse(dijelovi[1].Trim(), out int broj))
                    {
                        if (broj > najveciBroj)
                            najveciBroj = broj;
                    }
                }

                // Dodijeli sljedeći broj kalkulacije
                CurrentItemMat.Brfak = $"10-{najveciBroj + 1}";

                // Osvježi prikaz u XAML-u
                OnPropertyChanged(nameof(CurrentItemMat));
            }
        }

        // An event that will be raised to notify the view to open the PrintWindow
        public event Action OnPrintEvent;

        //obrise sva polja i ostavlja opciju za SNIMI i ODUSTANI
        private void NovaKalkulacija()
        {
            if (fakturaUToku)
            {
                System.Windows.MessageBox.Show(
                    "Već imate započetu fakturu. Prvo završite unos ili odustanite.");
                return;
            }

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                var noviMat = new Mat
                {
                    Kljnaz = 1000,
                    Kljnaz1 = 0,
                    Status = "U",
                    Redbr = 1,
                    Brnar = "",
                    Konto2 = "",
                    Kontosklad1 = 0,
                    Tarifa = "1",
                    Medus = "",
                    Ppp = "17",
                    Ourst = 0,
                    Mjtst = 0
                };

                CurrentItemMat = noviMat;

                // Generisanje broja fakture
                BrojKalkulacije();

                // Provjera da BrojKalkulacije nije zamijenio objekat
                if (!ReferenceEquals(CurrentItemMat, noviMat))
                {
                    System.Windows.MessageBox.Show(
                        "Prilikom generisanja broja fakture promijenjena je trenutna stavka.",
                        "Upozorenje",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                // Kreiranje reda u SQL bazi
                dbContext.Mat.Add(noviMat);
                dbContext.SaveChanges();

                // Zapamti novokreiranu nesnimljenu stavku
                idNoveKalkulacije = noviMat.Id;

                if (MatList == null)
                    MatList = new ObservableCollection<Mat>();

                MatList.Add(noviMat);

                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = noviMat;

                fakturaUToku = true;
                novaStavkaUToku = true;
                trenutnaStavkaSnimljena = false;
                isNovaKalkulacijaClicked = true;

                UpdateCurrentItemData(dbContext);
                UpdateCommands();

                // Automatski fokus na šifru komitenta
                RequestFocusAnalst?.Invoke();
            }
        }

        private void NovaStavkaButton()
        {
            if (fakturaUToku ||
                CurrentItemMat == null ||
                CurrentItemMat.Id <= 0)
            {
                return;
            }

            int odabraniId = CurrentItemMat.Id;

            try
            {
                using (var dbContext = new materijalno_knjigovodstvoContext())
                {
                    // 1. Učitaj odabranu stavku iz SQL baze
                    var odabranaStavka = dbContext.Mat
                        .AsNoTracking()
                        .FirstOrDefault(x =>
                            x.Id == odabraniId &&
                            x.Status == "U");

                    if (odabranaStavka == null ||
                        string.IsNullOrWhiteSpace(odabranaStavka.Brfak))
                    {
                        System.Windows.MessageBox.Show(
                            "Odabrana faktura nije pronađena!",
                            "Upozorenje",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    // 2. Pronađi najveći Redbr za ovu fakturu
                    int posljednjiRedbr = dbContext.Mat
                        .Where(x =>
                            x.Brfak == odabranaStavka.Brfak &&
                            x.Kljnaz == odabranaStavka.Kljnaz &&
                            x.Status == "U")
                        .Max(x => (int?)x.Redbr) ?? 0;

                    // 3. Kreiraj novu stavku iste fakture
                    var novaStavka = new Mat
                    {
                        // PODACI KOJI OSTAJU ISTI
                        Kljnaz = odabranaStavka.Kljnaz,
                        Kljnaz1 = odabranaStavka.Kljnaz1,
                        Brfak = odabranaStavka.Brfak,
                        Brdok = odabranaStavka.Brdok,
                        Datun = odabranaStavka.Datun,
                        Datnar = odabranaStavka.Datnar,
                        Analst = odabranaStavka.Analst,

                        // NOVI REDNI BROJ
                        Redbr = posljednjiRedbr + 1,

                        // STATUS ULAZA
                        Status = "U",

                        // NOVI MATERIJAL
                        Ident = null,
                        Kolic = null,
                        Nc = null,
                        Vrijed = null,
                        Fvrijed = "",

                        // TROŠKOVI SE UNOSE ZA NOVU STAVKU
                        Porppp = null,
                        Trospe = null,
                        Cartro = "0",
                        Zavtro = "0",

                        // OSTALA POČETNA POLJA
                        Brnar = "",
                        Konto2 = "",
                        Kontosklad = 1010100,
                        Kontosklad1 = 0,
                        Tarifa = "1",
                        Medus = "",
                        Ppp = "17",
                        Ourst = 0,
                        Mjtst = 0
                    };

                    // 4. Kreiraj privremeni red u SQL bazi
                    // Isto kao NOVA KALKULACIJA i SLIJ. STAVKA
                    dbContext.Mat.Add(novaStavka);
                    dbContext.SaveChanges();

                    // 5. Zapamti ID nesnimljene stavke
                    idNoveKalkulacije = novaStavka.Id;

                    // 6. Dodaj stavku u lokalnu listu
                    if (MatList == null)
                        MatList = new ObservableCollection<Mat>();

                    MatList.Add(novaStavka);

                    CurrentIndex = MatList.Count - 1;
                    CurrentItemMat = novaStavka;

                    // 7. Aktiviraj unos
                    fakturaUToku = true;
                    novaStavkaUToku = true;
                    trenutnaStavkaSnimljena = false;
                    isNovaKalkulacijaClicked = true;

                    // 8. Osvježi podatke materijala i komande
                    UpdateCurrentItemData(dbContext);
                    UpdateCommands();

                    System.Windows.MessageBox.Show(
                        "Kreirana je nova stavka za fakturu " +
                        novaStavka.Brfak +
                        ".\n\nUnesite šifru materijala, količinu, " +
                        "fakturisanu vrijednost i troškove, zatim kliknite SNIMI.",
                        "Nova stavka",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Automatski fokus na Ident nakon NOVA STAVKA
                    RequestFocusIdent?.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Greška prilikom kreiranja nove stavke:\n" +
                    ex.Message,
                    "Greška",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool ValidacijaNoveKalkulacije()
        {
            if (CurrentItemMat == null)
                return false;

            if (string.IsNullOrWhiteSpace(CurrentItemMat.Analst) ||
                string.IsNullOrWhiteSpace(CurrentItemMat.Brdok) ||
                !CurrentItemMat.Ident.HasValue ||
                CurrentItemMat.Ident.Value <= 0 ||
                !CurrentItemMat.Kolic.HasValue ||
                CurrentItemMat.Kolic.Value <= 0 ||
                string.IsNullOrWhiteSpace(CurrentItemMat.Fvrijed))
            {
                System.Windows.MessageBox.Show(
                    "Molim unesite sva potrebna polja prije snimanja, ili kliknite dugme 'Odustani'.",
                    "Nepotpuni podaci",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        //Pokupi sva polja trenutna i spasi. Trebalo bi napraviti disabled SAVE button ako nije odabrana nova kalkulacija
        private void SpasiNovuKalkulaciju()
        {
            // 1. Provjera da li postoji aktivna stavka za snimanje
            if (!fakturaUToku ||
                !novaStavkaUToku ||
                CurrentItemMat == null)
            {
                return;
            }

            // 2. Provjera obaveznih polja
            if (!ValidacijaNoveKalkulacije())
                return;

            // 3. Provjera da snimamo samo novokreirani red
            if (!idNoveKalkulacije.HasValue ||
                CurrentItemMat.Id != idNoveKalkulacije.Value)
            {
                System.Windows.MessageBox.Show(
                    "Nema aktivne nove stavke za snimanje.",
                    "Upozorenje",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                // 4. Postavi podatke za ULAZ
                CurrentItemMat.Status = "U";
                CurrentItemMat.Kljnaz = 1000;
                CurrentItemMat.Kljnaz1 = 0;

                CurrentItemMat.Kontosklad = 1010100;
                CurrentItemMat.Kontosklad1 = 0;

                // 5. Pronađi konto materijala
                if (CurrentItemMat.Ident != null)
                {
                    CurrentItemMat.Konto1 = (int?)dbContext.TabelaMaterijala
                        .Where(row => row.Ident == CurrentItemMat.Ident)
                        .Select(row => row.Konto1)
                        .FirstOrDefault();

                    UcitajNazivKonta1();
                }

                // 6. Izračunaj fakturisanu cijenu
                if (!IzracunajFcj())
                    return;

                // 7. Postavi podrazumijevane vrijednosti
                CurrentItemMat.Trospe = CurrentItemMat.Trospe ?? 0m;
                CurrentItemMat.Porppp = CurrentItemMat.Porppp ?? 0m;

                if (string.IsNullOrWhiteSpace(CurrentItemMat.Cartro))
                    CurrentItemMat.Cartro = "0";

                if (string.IsNullOrWhiteSpace(CurrentItemMat.Zavtro))
                    CurrentItemMat.Zavtro = "0";

                // 8. Izračunaj nabavnu cijenu i ukupnu vrijednost
                NabavnaCijena();

                // Zapamti ID trenutne stavke
                int snimljeniId = CurrentItemMat.Id;

                // 9. UPDATE postojećeg reda - isto kao kod IZLAZA
                dbContext.Update(CurrentItemMat);
                dbContext.SaveChanges();

                // 10. Ova stavka je sada uspješno snimljena
                idNoveKalkulacije = null;

                trenutnaStavkaSnimljena = true;
                novaStavkaUToku = false;

                // Faktura ostaje aktivna
                fakturaUToku = true;
                isNovaKalkulacijaClicked = false;

                // 11. Osvježi listu
                MatList = new ObservableCollection<Mat>(
                    dbContext.Mat
                        .Where(row =>
                            row.Kljnaz == 1000 &&
                            row.Status == "U")
                        .OrderBy(row => row.Datun)
                        .ToList());

                // 12. Pronađi upravo snimljenu stavku
                var snimljenaStavka = MatList
                    .FirstOrDefault(x => x.Id == snimljeniId);

                if (snimljenaStavka != null)
                {
                    CurrentItemMat = snimljenaStavka;
                    CurrentIndex = MatList.IndexOf(snimljenaStavka);

                    UpdateCurrentItemData(dbContext);
                }

                // 13. Osvježi dostupnost dugmadi
                UpdateCommands();

                UpdateCommands();

                System.Windows.MessageBox.Show(
                    "Stavka je uspješno snimljena.",
                    "Potvrda",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Nakon zatvaranja poruke fokus na SLIJ. STAVKA
                RequestFocusSlijedecaStavka?.Invoke();
            }
        }

        private void SlijedecaStavka()
        {
            // Sljedeća stavka može se dodati tek nakon SNIMI
            if (!fakturaUToku ||
                !trenutnaStavkaSnimljena ||
                CurrentItemMat == null)
            {
                return;
            }

            // Sačuvaj podatke prethodno snimljene stavke
            var prethodna = CurrentItemMat;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                // 1. Pronađi najveći Redbr za istu fakturu
                int najveciRedbr = dbContext.Mat
                    .Where(x =>
                        x.Kljnaz == prethodna.Kljnaz &&
                        x.Brfak == prethodna.Brfak &&
                        x.Status == "U")
                    .Max(x => (int?)x.Redbr) ?? 0;

                // 2. Kreiraj novu stavku
                var nova = new Mat
                {
                    // ZADRŽI PODATKE FAKTURE
                    Kljnaz = prethodna.Kljnaz,
                    Kljnaz1 = prethodna.Kljnaz1,
                    Datun = prethodna.Datun,
                    Datnar = prethodna.Datnar,
                    Analst = prethodna.Analst,
                    Brfak = prethodna.Brfak,
                    Brdok = prethodna.Brdok,

                    // Povećaj redni broj
                    Redbr = najveciRedbr + 1,

                    // Ostala početna polja
                    Status = "U",
                    Brnar = "",
                    Konto2 = "",
                    Kontosklad1 = 0,
                    Tarifa = "1",
                    Medus = "",
                    Ppp = "17",
                    Ourst = 0,
                    Mjtst = 0
                };

                // 3. Kreiraj red u SQL bazi
                // Isto kao kod Izlaza
                dbContext.Mat.Add(nova);
                dbContext.SaveChanges();

                // 4. Zapamti ID nove, još nezavršene stavke
                idNoveKalkulacije = nova.Id;

                // 5. Dodaj novu stavku u listu
                if (MatList == null)
                    MatList = new ObservableCollection<Mat>();

                MatList.Add(nova);

                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = nova;

                // 6. Postavi stanje dugmadi
                fakturaUToku = true;
                novaStavkaUToku = true;
                trenutnaStavkaSnimljena = false;
                isNovaKalkulacijaClicked = true;

                // 7. Osvježi prikaz
                UpdateCurrentItemData(dbContext);

                // 8. Osvježi dostupnost dugmadi
                UpdateCommands();
                RequestFocusIdent?.Invoke();
            }
        }


        private void KrajFakture()
        {
            if (!fakturaUToku || !trenutnaStavkaSnimljena)
                return;

            var rezultat = System.Windows.MessageBox.Show(
                "Želite li završiti unos ove fakture?",
                "Kraj fakture",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (rezultat != MessageBoxResult.Yes)
                return;

            fakturaUToku = false;
            novaStavkaUToku = false;
            trenutnaStavkaSnimljena = false;
            isNovaKalkulacijaClicked = false;
            idNoveKalkulacije = null;

            UpdateCommands();

            System.Windows.MessageBox.Show(
                "Unos fakture je uspješno završen.",
                "Potvrda",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        private void UpdateCommands()
        {
            PrviButtonCommand?.RaiseCanExecuteChanged();
            ZadnjiButtonCommand?.RaiseCanExecuteChanged();
            NextButtonCommand?.RaiseCanExecuteChanged();
            PrethodniButtonCommand?.RaiseCanExecuteChanged();

            BrisanjeCommand?.RaiseCanExecuteChanged();
            UpdateCommand?.RaiseCanExecuteChanged();
            TraziSifruMaterijalaCommand?.RaiseCanExecuteChanged();
            PrintCommand?.RaiseCanExecuteChanged();

            NovaKalkulacijaCommand?.RaiseCanExecuteChanged();
            SpasiNovuKalkulacijuCommand?.RaiseCanExecuteChanged();
            OdustaniCommand?.RaiseCanExecuteChanged();

            SlijedecaStavkaCommand?.RaiseCanExecuteChanged();
            KrajFaktureCommand?.RaiseCanExecuteChanged();
            NovaStavkaButtonCommand?.RaiseCanExecuteChanged();
        }


        private bool CanSlijedecaStavka()
        {
            return fakturaUToku && trenutnaStavkaSnimljena;
        }

        private bool CanKrajFakture()
        {
            return fakturaUToku && trenutnaStavkaSnimljena;
        }

        private bool CanSpasiNovuKalkulaciju()
        {
            return fakturaUToku && novaStavkaUToku;
        }

        private bool CanNovaKalkulacija()
        {
            return !fakturaUToku;
        }

        private bool CanOdustani()
        {
            return novaStavkaUToku;
        }

        //Samo dostupno kada odemo na nova kalkulacija i da vrati na prethodni
        private void Odustani()
        {
            if (!fakturaUToku ||
                !novaStavkaUToku ||
                CurrentItemMat == null)
            {
                return;
            }

            var rezultat = System.Windows.MessageBox.Show(
                "Želite li odustati od trenutne nesnimljene stavke?",
                "Potvrda",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (rezultat != MessageBoxResult.Yes)
                return;

            // Zapamti podatke prije brisanja
            int idZaBrisanje = CurrentItemMat.Id;
            string brojFakture = CurrentItemMat.Brfak;
            int? skladiste = CurrentItemMat.Kljnaz;

            // Smijemo obrisati samo aktivnu nesnimljenu stavku
            if (!idNoveKalkulacije.HasValue ||
                idNoveKalkulacije.Value != idZaBrisanje)
            {
                System.Windows.MessageBox.Show(
                    "Nije pronađena nesnimljena stavka za brisanje.",
                    "Upozorenje",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                // Pronađi isključivo trenutnu nesnimljenu stavku
                var stavkaZaBrisanje = dbContext.Mat
                    .FirstOrDefault(x => x.Id == idZaBrisanje);

                if (stavkaZaBrisanje != null)
                {
                    dbContext.Mat.Remove(stavkaZaBrisanje);
                    dbContext.SaveChanges();
                }
            }

            // Ukloni obrisanu stavku iz lokalne liste
            if (MatList != null)
            {
                var stavkaUListi = MatList
                    .FirstOrDefault(x => x.Id == idZaBrisanje);

                if (stavkaUListi != null)
                    MatList.Remove(stavkaUListi);
            }

            // Nesnimljena stavka više ne postoji
            idNoveKalkulacije = null;
            novaStavkaUToku = false;
            isNovaKalkulacijaClicked = false;

            // Pronađi prethodno snimljenu stavku iste fakture
            var prethodnaSnimljena = MatList?
                .Where(x =>
                    x.Brfak == brojFakture &&
                    x.Kljnaz == skladiste &&
                    x.Status == "U")
                .OrderByDescending(x => x.Redbr)
                .FirstOrDefault();

            if (prethodnaSnimljena != null)
            {
                // Postoje prethodno snimljene stavke
                CurrentItemMat = prethodnaSnimljena;
                CurrentIndex = MatList.IndexOf(prethodnaSnimljena);

                trenutnaStavkaSnimljena = true;
                fakturaUToku = true;
            }
            else
            {
                // Odustajanje od prve stavke fakture
                fakturaUToku = false;
                trenutnaStavkaSnimljena = false;

                CurrentItemMat = MatList?.LastOrDefault();

                CurrentIndex = CurrentItemMat != null
                    ? MatList.IndexOf(CurrentItemMat)
                    : 0;
            }

            UpdateCommands();

            System.Windows.MessageBox.Show(
                "Odustali ste od trenutne stavke.",
                "Potvrda",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        private void OtvoriKomitentListu()
        {
            _gvm.OdabraniVM = new KomitentiListaViewModel(this, _gvm);
        }

        // Ova metoda radi update CurrentItem i CurrentItemTabMaterijala based on the current index
        private void UpdateCurrentItemData(materijalno_knjigovodstvoContext dbContext)
        {
            if (MatList == null)
            {
                MatList = new ObservableCollection<Mat>();
            }

            // Ako nema trenutnog materijala, učitaj listu iz baze
            if (CurrentItemMat == null)
            {
                MatList = new ObservableCollection<Mat>(
                    dbContext.Mat
                        .Where(row => row.Kljnaz == 1000 && row.Status == "U")
                        .OrderBy(row => row.Datun)
                        .ToList());

                if (MatList.Count == 0)
                {
                    CurrentIndex = 0;
                    CurrentItemMat = null;
                    CurrentItemTabMaterijala = null;

                    TebelaMaterijalaList =
                        new ObservableCollection<TabelaMaterijala>();

                    CurrentNazivZaSifruKomitenta = "";
                    selectedKomitent = null;

                    return;
                }

                // Osiguraj da je indeks validan
                if (CurrentIndex < 0 || CurrentIndex >= MatList.Count)
                {
                    CurrentIndex = 0;
                }

                CurrentItemMat = MatList[CurrentIndex];
            }
            else
            {
                // Pronađi trenutni materijal prema ID-u
                for (int i = 0; i < MatList.Count; i++)
                {
                    if (MatList[i].Id == CurrentItemMat.Id)
                    {
                        CurrentIndex = i;

                        // Ne mijenjaj objekt ako je već isti
                        if (!ReferenceEquals(CurrentItemMat, MatList[i]))
                        {
                            CurrentItemMat = MatList[i];
                        }

                        break;
                    }
                }
            }

            if (CurrentItemMat == null)
                return;

            // Učitaj podatke o materijalu
            TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(
                dbContext.TabelaMaterijala
                    .Where(row => row.Ident == CurrentItemMat.Ident)
                    .ToList());

            CurrentItemTabMaterijala = TebelaMaterijalaList.FirstOrDefault();

            // Pronađi naziv komitenta
            CurrentNazivZaSifruKomitenta = "";

            if (StaraSifra_Ime_List != null &&
                !string.IsNullOrWhiteSpace(CurrentItemMat.Analst))
            {
                CurrentNazivZaSifruKomitenta =
                    StaraSifra_Ime_List
                        .FirstOrDefault(row =>
                            row.STARA_SIFRA == CurrentItemMat.Analst)?.IME ?? "";
            }

            // Ako je korisnik odabrao novog komitenta
            if (selectedKomitent != null && novaStavkaUToku)
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