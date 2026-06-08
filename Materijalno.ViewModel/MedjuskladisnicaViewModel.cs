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
    public class MedjuskladisnicaViewModel : INotifyPropertyChanged
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
        string connectionString = "Server= 192.168.1.213;Trusted_Connection=False;" +
             "MultipleActiveResultSets=true;User Id=sa;Password=Lutrija1;";

        string queryStringStaraSifra_Ime = "SELECT STARA_SIFRA, IME FROM [FINANSIJE2-LINKED SERVER]..[IBS].[GR_KOMITENTI]";
        string currentNazivZaSifruKomitenta;
        public int CurrentIndex = 0;
        public static bool isNovaKalkulacijaClicked = false;
        public static bool isOdustaniEnabled = false;
        public static bool isIzlazEnable = false;


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

        public static bool isTraziClicked = false;
        public event PropertyChangedEventHandler PropertyChanged;
        private bool prethodniButtonIsExecuted = false;
        private bool nextButtonIsExecuted = false;
        private bool prvoUcitavanje = false;
        private bool spasi = false;


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
                OnPropertyChanged(nameof(CurrentItemTabMaterijala));
            }
        }
        
        //public TabelaMaterijala CurrentItemTabKonto1
        //{
        //    get { return currentItemTabKonto1; }
        //    set
        //    {
        //        currentItemTabKonto1 = value;
        //        OnPropertyChanged(nameof(currentItemTabKonto1));
        //    }
        //}

        public SifarnikSkladista CurrentItemTabSkladista
        {
            get { return currentItemTabSkladista; }
            set
            {
                currentItemTabSkladista = value;
                OnPropertyChanged(nameof(CurrentItemTabSkladista));
            }
        }

        public SifarnikSkladista CurrentItemTabSkladistaUlaza
        {
            get { return currentItemTabSkladistaUlaza; }
            set
            {
                currentItemTabSkladistaUlaza = value;
                OnPropertyChanged(nameof(CurrentItemTabSkladistaUlaza));
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
        //public ObservableCollection<TabelaMaterijala> TebelaKontaList { get; set; }
        public ObservableCollection<SifarnikSkladista> TebelaSkladistaList { get; set; }
        public ObservableCollection<SifarnikSkladista> TebelaSkladistaUlazaList { get; set; }
        public ObservableCollection<Mat> MatList { get; set; }
        public ObservableCollection<Mat> PrintList { get; set; }
        public List<Komitenti> StaraSifra_Ime_List { get; set; }

        #endregion

        #region Commands

        public RelayCommand NextButtonCommand { get; set; }
        public RelayCommand PrethodniButtonCommand { get; set; }
        public RelayCommand PrviButtonCommand { get; set; }
        public RelayCommand ZadnjiButtonCommand { get; set; } 
        public RelayCommand BrisanjeCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }
        public RelayCommand NovaMedjuskladisnicaCommand { get; set; }
        public RelayCommand SpasiNovuKalkulacijuCommand { get; set; }
        public RelayCommand NabavnaCijenaCommand { get; set; }
        //public RelayCommand PrintFullCommand { get; set; }

        //Potrebno uraditi ???
        public RelayCommand PrintCommand { get; set; }
        public RelayCommand OtvoriKomitentListuCommand { get; set; }
        public RelayCommand TraziSifruMaterijalaCommand { get; set; }
        public RelayCommand OsvjeziCommand { get; set; }
        public RelayCommand OdustaniCommand { get; set; }
        public RelayCommand SlijStavkaButtonCommand { get; set; }
        public RelayCommand IzlazCommand { get; set; }
        public RelayCommand BrojKalkulacijeCommand { get; set; }


        #endregion

        #region Constructor

        public MedjuskladisnicaViewModel(GlavniViewModel gvm)
        {

            _gvm = gvm;
            //Prilikom otvaranja Ulaza iz nove kalkulacije, treba promijeniti u false
            isNovaKalkulacijaClicked = false;

            #region Commands
            PrviButtonCommand = new RelayCommand(PrviButton, () => !isNovaKalkulacijaClicked);
            NextButtonCommand = new RelayCommand(NextButton, () => !isNovaKalkulacijaClicked);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !isNovaKalkulacijaClicked);
            ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !isNovaKalkulacijaClicked);
            BrisanjeCommand = new RelayCommand(Brisanje, () => !isNovaKalkulacijaClicked);
            UpdateCommand = new RelayCommand(Update, () => !isNovaKalkulacijaClicked);
            NovaMedjuskladisnicaCommand = new RelayCommand(NovaMedjuskladisnica, () => !isNovaKalkulacijaClicked);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => isNovaKalkulacijaClicked);

            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !isNovaKalkulacijaClicked);
            PrintCommand = new RelayCommand(Print, () => !isNovaKalkulacijaClicked);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            BrojKalkulacijeCommand = new RelayCommand(BrojKalkulacije);

            //PrintFullCommand = new RelayCommand(PrintFull);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton);
            IzlazCommand = new RelayCommand(Izlaz, () => isIzlazEnable);
            #endregion


            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                NextButtonCommand = new RelayCommand(NextButton);
                PrethodniButtonCommand = new RelayCommand(PrethodniButton);
                PrviButtonCommand = new RelayCommand(PrviButton);
                ZadnjiButtonCommand = new RelayCommand(ZadnjiButton);

                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                     .Where(row => row.Kljnaz1 >= 1000 && row.Kljnaz1 <= 1012 && row.Kljnaz >= 1000 && row.Kljnaz <= 1012 && row.Medus == "1")
                     .OrderBy(row => row.Datun)
                     .ToList());

                UpdateCurrentItemData(dbContext);

                StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }

        public MedjuskladisnicaViewModel()
        {
        }

            public MedjuskladisnicaViewModel(GlavniViewModel gvm, Mat CurrentItemMat)
            {
            _gvm = gvm;
            this.CurrentItemMat = CurrentItemMat;

            //Kada vraca iz Trazi Mat liste, treba da ostane isNovaKalkulacijaClicked = false; zbog buttona
            if (isTraziClicked == true)
            {
                IsNovaKalkulacijaClicked = false;
            }
            else
            {
                IsNovaKalkulacijaClicked = true;
            }
            isTraziClicked = false;

            #region Commands
            PrviButtonCommand = new RelayCommand(PrviButton, () => !isNovaKalkulacijaClicked);
            NextButtonCommand = new RelayCommand(NextButton, () => !isNovaKalkulacijaClicked);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !isNovaKalkulacijaClicked);
            ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !isNovaKalkulacijaClicked);
            BrisanjeCommand = new RelayCommand(Brisanje, () => !isNovaKalkulacijaClicked);
            UpdateCommand = new RelayCommand(Update, () => !isNovaKalkulacijaClicked);
            NovaMedjuskladisnicaCommand = new RelayCommand(NovaMedjuskladisnica, () => !isNovaKalkulacijaClicked);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => isNovaKalkulacijaClicked);

            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !isNovaKalkulacijaClicked);
            PrintCommand = new RelayCommand(Print, () => !isNovaKalkulacijaClicked);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            BrojKalkulacijeCommand = new RelayCommand(BrojKalkulacije);

            //PrintFullCommand = new RelayCommand(PrintFull);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton, () => IsNovaKalkulacijaClicked);
            IzlazCommand = new RelayCommand(Izlaz, () => isIzlazEnable);
            #endregion

            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                     .Where(row => row.Kljnaz1 >= 1000 && row.Kljnaz1 <= 1012 && row.Kljnaz >= 1000 && row.Kljnaz <= 1012 && row.Medus == "1")
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

                TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).ToList());
                
                //TebelaKontaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Konto1 == CurrentItemMat.Konto1).ToList());

                TebelaSkladistaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz).ToList());

                TebelaSkladistaUlazaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz1).ToList());

                //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property

                CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Konto1 == CurrentItemMat.Konto1).FirstOrDefault();
                
                //CurrentItemTabKonto1 = dbContext.TabelaMaterijala.Where(row => row.Konto1 == CurrentItemMat.Konto1).FirstOrDefault();
                
                CurrentItemTabSkladista = dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz).FirstOrDefault();
                CurrentItemTabSkladistaUlaza = dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz1).FirstOrDefault();


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

            if (CurrentItemMat.Kljnaz == 999 && CurrentItemMat.Kljnaz <= 1012)
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

            if (CurrentItemMat.Kljnaz1 == 999 && CurrentItemMat.Kljnaz1 <= 1012)
            {
                System.Windows.MessageBox.Show("Ne možete praviti izlaz iz Centralnog magacina!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                CurrentItemMat.Kljnaz1 = 0;
            }
            else if (CurrentItemMat.Kljnaz1 < minValue_Skladiste || CurrentItemMat.Kljnaz > maxValue_Skladiste)
            {
                System.Windows.MessageBox.Show("Skladište nije prijavljeno u šifarnik!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                //TextBox Skladiste vratiti na prazno
                CurrentItemMat.Kljnaz1 = 0;
            }

            if (CurrentItemMat.Ident < minValue_SifraMat || CurrentItemMat.Ident > maxValue_SifraMat)
            {
                System.Windows.MessageBox.Show("Materijal ne postoji!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                CurrentItemMat.Ident = 0;
            }

            UpdateCurrentItemData(dbContext);

            // Napraviti da stavi nule ako dodje do promjene Sifre Materijala?
        }

        public void NabavnaCijena( )
        {
            var dbContext = new materijalno_knjigovodstvoContext();

            //Novo
            // 3) Ako ima "I" → izračunaj Sum(Vrijed)/Sum(Kolic) iz "P" redova i "I" redova
            var sums = dbContext.Mat
            .Where(r =>
                r.Ident == CurrentItemMat.Ident &&
                r.Kljnaz == CurrentItemMat.Kljnaz &&
                r.Id != CurrentItemMat.Id && // isključi trenutni red, da ne bi uzimao kolicinu i vrijed (Vidjeti kako ovo radi za UPDATE)
                (
                    r.Status == "P" ||
                    r.Status == "U" ||
                    r.Status == "V" ||
                    r.Status == "I" ||
                    r.Status == "M"
                ))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                SumVrijed = (decimal?)g.Sum(x => x.Vrijed) ?? 0m,
                SumKolic = (decimal?)g.Sum(x => x.Kolic) ?? 0m
            })
            .FirstOrDefault();

            CurrentItemMat.Nc = 
                (sums == null || sums.SumKolic == 0m)
                    ? 0m
                    : (sums.SumVrijed / sums.SumKolic);

            CurrentItemMat.Nc = Math.Round(CurrentItemMat.Nc ?? 0m, 9, MidpointRounding.AwayFromZero);

            //////////////

            //CurrentItemMat.Nc = dbContext.Mat
            //    .Where(row => row.Ident == CurrentItemMat.Ident)
            //    .Select(row => row.Nc).FirstOrDefault();

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
                     .Where(row => row.Kljnaz1 >= 1000 && row.Kljnaz1 <= 1012 && row.Kljnaz >= 1000 && row.Kljnaz <= 1012)
                     .OrderBy(row => row.Datun)
                     .ToList());

            UpdateCurrentItemData(dbContext);
        }
        //STARO
        //decimal? inputValue1 = CurrentItemMat.Kolic;
        //    //decimal? inputValue2 = CurrentItemMat.Trospe;
        //    //decimal? inputValue3 = CurrentItemMat.Porppp;
        //    //decimal? inputValue4 = CurrentItemMat.Troskovi;
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

        //    decimal? value1 = inputValue1.HasValue ? (decimal?)inputValue1.Value : 0;
        //    //decimal? value2 = inputValue2.HasValue ? (decimal?)inputValue2.Value : 0;
        //    //decimal? value3 = inputValue3.HasValue ? (decimal?)inputValue3.Value : 0;
        //    //decimal? value4 = inputValue4.HasValue ? (decimal?)inputValue4.Value : 0;
        //    //decimal? sum = value1 + value2 + value3 + value4 + carinaValue;

        //    if (CurrentItemMat.Kolic.HasValue && CurrentItemMat.Kolic.Value != 0)
        //    {
        //        //decimal? nc = (sum / (decimal)CurrentItemMat.Kolic);
        //        //CurrentItemMat.Nc = (sum / (decimal)CurrentItemMat.Kolic.Value);
        //    }
        //    else
        //    {
        //        // U slucaju da je Kolic null ili nula, da bi izbjegli dijeljenje sa nulom
        //        CurrentItemMat.Nc = 0;
        //    }

        //    var dbContext = new materijalno_knjigovodstvoContext();
        //    dbContext.Update(CurrentItemMat);
        //    dbContext.SaveChanges();

        //    //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
        //    MatList = new ObservableCollection<Mat>(dbContext.Mat
        //        .Where(row => row.Kljnaz1 >= 1000 && row.Kljnaz1 <= 1012 && row.Kljnaz >= 1000 && row.Kljnaz <= 1012)
        //        .OrderBy(row => row.Datun)
        //        .ToList());

        //    UpdateCurrentItemData(dbContext);





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
                        CurrentNazivZaSifruKomitenta = string.IsNullOrEmpty(currentItemMat.Analst) ? ""
                            : StaraSifra_Ime_List.FirstOrDefault(row => row.STARA_SIFRA == currentItemMat.Analst)?.IME;
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
                //UpdateCurrentItemDataUlaz(dbContext);
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

                //UpdateCurrentItemDataUlaz(dbContext);
                //System.Windows.MessageBox.Show("Došli ste do prvog podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

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

                //UpdateCurrentItemDataUlaz(dbContext);
                //System.Windows.MessageBox.Show("Došli ste do zadnjeg podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);


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
                if (currentItemMat.Kljnaz != null)
                {
                    currentItemMat.Kontosklad = dbContext.SifarnikMaterijalSkladisteKonto
                    .Where(row => row.Sifskla == currentItemMat.Kljnaz && row.Sifmat == CurrentItemMat.Ident)
                    .Select(row => row.Sifkonta)
                    .FirstOrDefault();
                }

                if (currentItemMat.Kljnaz1 != null)
                {
                    currentItemMat.Kontosklad1 = dbContext.SifarnikMaterijalSkladisteKonto
                    .Where(row => row.Sifskla == currentItemMat.Kljnaz1 && row.Sifmat == CurrentItemMat.Ident)
                    .Select(row => row.Sifkonta)
                    .FirstOrDefault();
                }

                if (currentItemMat.Ident != null)
                {
                    currentItemMat.Konto1 = (int?)dbContext.TabelaMaterijala
                    .Where(row => row.Ident == CurrentItemMat.Ident)
                    .Select(row => row.Konto1)
                    .FirstOrDefault();
                }

                //Provjeriti
                if (ValidacijaZaliheMaterijala() == false)
                {
                    return;
                }

                //Ako validacija ne prodje, tj. ako fali neko polje
                if (ValidacijaSpasi() == false)
                {
                    System.Windows.MessageBox.Show("Molimo unesite sva polja!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;
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

            var dbContext = new materijalno_knjigovodstvoContext();



            PrintList = new ObservableCollection<Mat>(dbContext.Mat
                .Where(row => row.Brfak == CurrentItemMat.Brfak && CurrentItemMat.Datun == currentItemMat.Datun)
                .ToList());

            //TebelaMaterijalaList =

            var culture = new CultureInfo("de-DE");

            decimal? ukupnoNcValue = CurrentItemMat.Kolic * CurrentItemMat.Nc;
            //treba vidjeti kako da prebaci na DE culture???
            ukupnoNc = Math.Round((decimal)ukupnoNcValue, 9);
            decimal? formmatedNc = decimal.Parse(ukupnoNc.ToString(), NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, culture);
            ukupnoNc = formmatedNc;

            //foreach (var mat in PrintList)
            //{

            //    Console.WriteLine($"Mat ID: {mat.Id}, Kolicina: {mat.Kolic}, Nabavna cijena: {mat.Nc}, Status: {mat.Status}");
            //}

            OnPrintEvent?.Invoke();
        }

        // An event that will be raised to notify the view to open the PrintWindow
        public event Action OnPrintEvent;

        private void Izlaz()
        {
            _gvm.OdabraniVM = new MedjuskladisnicaViewModel(_gvm);
            //Disabled SNIMI i NOVA STAVKA, treba jos Slij.STAVKA i ODUSTANI

            isNovaKalkulacijaClicked = false;
            _isNovaKalkulacijaClicked = false;
            isIzlazEnable = false;
            IzlazCommand.RaiseCanExecuteChanged();



            //ODUSTANI COMMAND treba da bude nedostupno nakon Kraj Izlaza??
            isOdustaniEnabled = false;
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
                               .Where(row => row.Status == "M")
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

            if (posljednjiBrfak != null)
            {
                var parts = posljednjiBrfak.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out int number)) // Parsiraj drugi dio (poslije -)
                {
                    number++; // Povecaj za jedan
                    posljednjiBrfak = $"{parts[0]}-{number}"; // Dodaj dvije cjeline u posljednjiBrfak
                }
            }
            else
            {
                posljednjiBrfak = "M-1";
            }
            //Stavili smo if, jer pada kada brisemo, CurrentItemMat.Brfak bude null
            //if (CurrentItemMat.Brfak != null)
            //{
            CurrentItemMat.Brfak = posljednjiBrfak;
            //}
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
            if (currentItemMat.Nc == 0 || currentItemMat.Nc == null)
            {
                return false;
            }

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Validacija *KOLICINA VECA OD ZALIHE MATERIJALA!*
                //1. Iz pocetnog stanja za trenutnu godinu uzmemo vrijednost materijala za dato skladiste
                //2. Onda od pocetnog stanja + Ulazi(za taj magacin i taj materijal) - Izlazi(za taj magacin i taj materijal)
                //3. Ako je Izlaz Vrijednost ili količine veci od stvarne zalihe materijala tj. ako ode ispod nule onda izbaci poruku
                //4. Medjuskladisnica kako to sabirati ili oduzimati
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

                //MEĐUSKLADIŠNICA
                int ukupnoMedjuskladisnica = dbContext.Mat
                .Where(row =>
                    row.Kljnaz == currentItemMat.Kljnaz &&
                    row.Ident == currentItemMat.Ident &&
                    row.Status == "M")
                .Sum(row => (int?)row.Kolic) ?? 0;

                //Povrat
                int ukupnoPovrat = dbContext.Mat
                .Where(row =>
                    row.Kljnaz == currentItemMat.Kljnaz &&
                    row.Ident == currentItemMat.Ident &&
                    row.Status == "V")
                .Sum(row => (int?)row.Kolic) ?? 0;

                int? stvarnoStanje = pocetnoStanje + ukupnoUlaziMaterijal - ukupnoIzlaziMaterijal + ukupnoMedjuskladisnica + ukupnoPovrat;

                if (stvarnoStanje < 0)
                {
                    System.Windows.MessageBox.Show("Količina je veća od zaliha materijala! " + stvarnoStanje, "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return false;
                }
            }
            return true;
        }

        private void NovaMedjuskladisnica()
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
                    Redbr = 1
                });

                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = MatList[CurrentIndex];

                BrojKalkulacije();

                dbContext.Add(CurrentItemMat);
                dbContext.SaveChanges();

                UpdateCurrentItemData(dbContext);
                //UpdateCurrentItemDataUlaz(dbContext);
            }
        }

        //Pokupi sva polja trenutna i spasi. Trebalo bi napraviti disabled SAVE button ako nije odabrana nova kalkulacija
        private void SpasiNovuKalkulaciju()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                currentItemMat.Status = "M";
                 
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

                if (currentItemMat.Kljnaz1 != null)
                {
                    currentItemMat.Kontosklad1 = dbContext.SifarnikMaterijalSkladisteKonto
                    .Where(row => row.Sifskla == currentItemMat.Kljnaz1 && row.Sifmat == CurrentItemMat.Ident)
                    .Select(row => row.Sifkonta)
                    .FirstOrDefault();
                }
                //Provjeriti
                if (ValidacijaZaliheMaterijala() == false)
                {
                    return;
                }

                //Ako validacija ne prodje, tj. ako fali neko polje
                if (ValidacijaSpasi() == false)
                {
                    System.Windows.MessageBox.Show("Molimo unesite sva polja!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;
                }

                NabavnaCijena();

                currentItemMat.Kolic = -CurrentItemMat.Kolic;
                currentItemMat.Vrijed = -CurrentItemMat.Vrijed;
                

                dbContext.Update(CurrentItemMat);
                dbContext.SaveChanges();

                //Ovo spasavamo duplo, da bi se poslije mogli praviti izvjestaji, da pokazuje stvarno stanje magacina
                Mat dupliMat = new Mat
                {
                    Kljnaz = CurrentItemMat.Kljnaz1,
                    Kljnaz1 = CurrentItemMat.Kljnaz,
                    Kolic = CurrentItemMat.Kolic * -1,
                    Vrijed = CurrentItemMat.Vrijed * -1,
                    Kontosklad = CurrentItemMat.Kontosklad1,
                    Kontosklad1 = currentItemMat.Kontosklad,
                    Medus = "1",

                    Ident = CurrentItemMat.Ident,
                    Datun = CurrentItemMat.Datun,
                    Brdok = CurrentItemMat.Brdok,
                    Datnar = CurrentItemMat.Datnar,
                    Brfak = CurrentItemMat.Brfak,
                    Nc = CurrentItemMat.Nc,
                    Redbr = CurrentItemMat.Redbr,
                    Status = CurrentItemMat.Status,
                    Konto1 = CurrentItemMat.Konto1
                };

                dbContext.Add(dupliMat);
                dbContext.SaveChanges();

                //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
                MatList = new ObservableCollection<Mat>(dbContext.Mat
                     .Where(row => row.Kljnaz1 >= 1000 && row.Kljnaz1 <= 1012 && row.Kljnaz >= 1000 && row.Kljnaz <= 1012 && row.Medus == "1")
                     .OrderBy(row => row.Datun)
                     .ToList());

                System.Windows.MessageBox.Show("Uspješno ste unijeli novi šifarnik", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                //Ovo kada je true, onda ce buttoni biti dostupni
                isNovaKalkulacijaClicked = false;
                IsNovaKalkulacijaClicked = true;
                IsOdustaniEnabled = true;
                isIzlazEnable = true;
                IzlazCommand.RaiseCanExecuteChanged();

                SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();
                SlijStavkaButtonCommand.RaiseCanExecuteChanged();

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
                //UpdateCurrentItemDataUlaz(dbContext);
            }
        }

        private void UpdateCommands()
        {
            PrviButtonCommand.RaiseCanExecuteChanged();
            ZadnjiButtonCommand.RaiseCanExecuteChanged();
            NextButtonCommand.RaiseCanExecuteChanged();
            PrethodniButtonCommand.RaiseCanExecuteChanged();
            SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();
            OdustaniCommand.RaiseCanExecuteChanged();
            NovaMedjuskladisnicaCommand.RaiseCanExecuteChanged();
            BrisanjeCommand.RaiseCanExecuteChanged();
            UpdateCommand.RaiseCanExecuteChanged();
            TraziSifruMaterijalaCommand.RaiseCanExecuteChanged();
            PrintCommand.RaiseCanExecuteChanged();

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

                    _gvm.OdabraniVM = new MedjuskladisnicaViewModel(_gvm);
                }
                else if (resultMessageBox == MessageBoxResult.No)
                {
                    return;
                }
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
                    Kljnaz1 = snimljeniCurrentMat.Kljnaz1,
                    Brfak = snimljeniCurrentMat.Brfak,
                    Datun = snimljeniCurrentMat.Datun,
                    Datnar = snimljeniCurrentMat.Datnar,
                    Brdok = snimljeniCurrentMat.Brdok,
                    Status = "I"
                });

                CurrentIndex = MatList.Count - 1;
                CurrentItemMat = MatList[CurrentIndex];



                //BrojKalkulacije();

                dbContext.Add(CurrentItemMat);
                dbContext.SaveChanges();

                //kad uradi SlijedStavka, treba da ostane SNIMI i ODUSTANI
                //Trenutno Slij.stavka je Enabled i ODUSTANI


                //Ovo kada je true, onda ce buttoni biti dostupni
                _isNovaKalkulacijaClicked = false;
                isNovaKalkulacijaClicked = true;
                isIzlazEnable = false;
                IsOdustaniEnabled = true;
                SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();

                //Ova commanda zavisi od "_isNovaKalkulacijaClicked"
                SlijStavkaButtonCommand.RaiseCanExecuteChanged();
                IzlazCommand.RaiseCanExecuteChanged();
                OdustaniCommand.RaiseCanExecuteChanged();

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

            //TebelaKontaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Konto1 == CurrentItemMat.Konto1).ToList());

            TebelaSkladistaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz).ToList());

            TebelaSkladistaUlazaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz1).ToList());

            //TebelaSkladistaUlazaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(row => row.Kljnaz== CurrentItemMat.Kljnaz1).ToList());
            //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property

            CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).FirstOrDefault();

            //CurrentItemTabKonto1 = dbContext.TabelaMaterijala.Where(row => row.Konto1 == CurrentItemMat.Konto1).FirstOrDefault();

            CurrentItemTabSkladista = dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz).FirstOrDefault();
            CurrentItemTabSkladistaUlaza = dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz1).FirstOrDefault();

            //CurrentItemTabSkladistaUlaza = dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz1).FirstOrDefault();

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