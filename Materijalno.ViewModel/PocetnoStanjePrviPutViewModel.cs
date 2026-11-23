using Materijalno.Model.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    public class PocetnoStanjePrviPutViewModel : INotifyPropertyChanged, Services.IActivePrometEntry
    {
        #region Fields

        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        private Mat currentItemMat;
        private string _displayedMaterial;
        private int? _displayedWarehouse;
        private bool _editingExisting;
        private int? _rowBeforeEntry;
        private int _indexBeforeEntry;
        public bool IsEntryEditable => isNovaKalkulacijaClicked;
        public bool IsEntryReadOnly => !IsEntryEditable;
        public bool HasUnsavedEntry => IsEntryEditable || (CurrentItemMat != null && CurrentItemMat.Id > 0 && CurrentItemMat.IsDraft);
        public event Action RequestFocusQuantity;
        
        private Mat itemMat;
        public static Komitenti selectedKomitent;
        public static Mat selectedMat;
        private TabelaMaterijala currentItemTabMaterijala;
        private TabelaMaterijala currentItemTabMaterijalaK;
        private TabelaMaterijala currentItemTabMaterijalaK2;
        //private TabelaMaterijala currentItemTabKonto1;
        private SifarnikSkladista currentItemTabSkladista;
        private SifarnikSkladista currentItemTabSkladistaUlaza;
        string connectionString = Materijalno.Model.Services.DatabaseConfiguration.MaterialConnectionString;

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
        public static bool isIzlazEnable = false;



        #endregion

        #region Properties and Lists

        //Staviti bolji naziv CurrentItemMat
        
        public Mat CurrentItemMat
        {
            get { return currentItemMat; }
            set
            {
                currentItemMat = value;
                _displayedMaterial = value?.Ident;
                _displayedWarehouse = value?.Kljnaz;
                OnPropertyChanged(nameof(CurrentItemMat));
                UpdateSumVrijed();
            }
        }


        public TabelaMaterijala CurrentItemTabMaterijalaK
        {
            get { return currentItemTabMaterijalaK; }
            set
            {
                currentItemTabMaterijalaK = value;
                OnPropertyChanged(nameof(CurrentItemTabMaterijalaK));
            }
        }
        public TabelaMaterijala CurrentItemTabMaterijalaK2
        {
            get { return currentItemTabMaterijalaK2; }
            set
            {
                currentItemTabMaterijalaK2 = value;
                OnPropertyChanged(nameof(CurrentItemTabMaterijalaK2));
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
                    OdustaniCommand?.RaiseCanExecuteChanged(); // Notify the command to re-evaluate CanExecute
                }
            }
        }

        public ObservableCollection<TabelaMaterijala> TebelaMaterijalaList { get; set; }
        public ObservableCollection<SifarnikSkladista> TebelaSkladistaList { get; set; }
        public ObservableCollection<SifarnikSkladista> TebelaSkladistaUlazaList { get; set; }
        private ObservableCollection<Mat> _matList;
        public ObservableCollection<Mat> MatList
        {
            get => _matList;
            set
            {
                if (_matList != null) _matList.CollectionChanged -= MatListChanged;
                _matList = value;
                if (_matList != null) _matList.CollectionChanged += MatListChanged;
                OnPropertyChanged(nameof(MatList));
                UpdateSumVrijed();
            }
        }
        private void MatListChanged(object sender, NotifyCollectionChangedEventArgs e) => UpdateSumVrijed();
        public ObservableCollection<Mat> PrintList { get; set; }
        public List<Komitenti> StaraSifra_Ime_List { get; set; }

        private decimal sumVrijed;
        public decimal SumVrijed
        {
            get => sumVrijed;
            set
            {
                if (sumVrijed != value)
                {
                    sumVrijed = value;
                    OnPropertyChanged(nameof(SumVrijed));
                }
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

        #endregion

        #region Commands
        public RelayCommand NextButtonCommand { get; set; }
        public RelayCommand PrethodniButtonCommand { get; set; }
        public RelayCommand PrviButtonCommand { get; set; }
        public RelayCommand ZadnjiButtonCommand { get; set; }
        public RelayCommand BrisanjeCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }
        public RelayCommand SpasiNovuKalkulacijuCommand { get; set; }
        public RelayCommand NabavnaCijenaCommand { get; set; }
        public RelayCommand NovoSklCommand { get; set; }

        public RelayCommand PrintCommand { get; set; }
        public RelayCommand OtvoriKomitentListuCommand { get; set; }
        public RelayCommand TraziSifruMaterijalaCommand { get; set; }
        public RelayCommand OsvjeziCommand { get; set; }
        public RelayCommand OdustaniCommand { get; set; }


        public RelayCommand IzlazCommand { get; set; }
        public RelayCommand SlijStavkaButtonCommand { get; set; }


        #endregion

        #region Constructor

        public PocetnoStanjePrviPutViewModel(GlavniViewModel gvm)
        {

            _gvm = gvm;
            //Prilikom otvaranja Ulaza iz nove kalkulacije, treba promijeniti u false
            isNovaKalkulacijaClicked = false;

            #region Commands
            PrviButtonCommand = new RelayCommand(PrviButton, () => !HasUnsavedEntry && MatList != null && MatList.Count > 0);
            NextButtonCommand = new RelayCommand(NextButton, () => !HasUnsavedEntry && MatList != null && MatList.Count > 0);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !HasUnsavedEntry && MatList != null && MatList.Count > 0);
            ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !HasUnsavedEntry && MatList != null && MatList.Count > 0);
            BrisanjeCommand = new RelayCommand(Brisanje, () => !HasUnsavedEntry && CurrentItemMat != null && CurrentItemMat.Id > 0 && !CurrentItemMat.IsDraft);
            UpdateCommand = new RelayCommand(Update, () => !HasUnsavedEntry && CurrentItemMat != null && CurrentItemMat.Id > 0 && !CurrentItemMat.IsDraft);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => IsOdustaniEnabled);
            NovoSklCommand = new RelayCommand(NovaKalkulacija, () => !isNovaKalkulacijaClicked);
            IzlazCommand = new RelayCommand(Izlaz, () => isIzlazEnable);

            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !HasUnsavedEntry && MatList != null && MatList.Count > 0);
            PrintCommand = new RelayCommand(Print, () => !HasUnsavedEntry && CurrentItemMat != null && CurrentItemMat.Id > 0 && !CurrentItemMat.IsDraft);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            //RedniBrojCommand = new RelayCommand(RedniBroj);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton, () => IsNovaKalkulacijaClicked);
            #endregion


            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //NextButtonCommand = new RelayCommand(NextButton);
                //PrethodniButtonCommand = new RelayCommand(PrethodniButton);
                //PrviButtonCommand = new RelayCommand(PrviButton);
                //ZadnjiButtonCommand = new RelayCommand(ZadnjiButton);

                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = LoadOpenings(dbContext);

                UpdateCurrentItemData(dbContext);

                StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }

        public PocetnoStanjePrviPutViewModel()
        {
        }

        public PocetnoStanjePrviPutViewModel(GlavniViewModel gvm, Mat CurrentItemMat)
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
            PrviButtonCommand = new RelayCommand(PrviButton, () => !HasUnsavedEntry && MatList != null && MatList.Count > 0);
            NextButtonCommand = new RelayCommand(NextButton, () => !HasUnsavedEntry && MatList != null && MatList.Count > 0);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !HasUnsavedEntry && MatList != null && MatList.Count > 0);
            ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !HasUnsavedEntry && MatList != null && MatList.Count > 0);
            BrisanjeCommand = new RelayCommand(Brisanje, () => !HasUnsavedEntry && CurrentItemMat != null && CurrentItemMat.Id > 0 && !CurrentItemMat.IsDraft);
            UpdateCommand = new RelayCommand(Update, () => !HasUnsavedEntry && CurrentItemMat != null && CurrentItemMat.Id > 0 && !CurrentItemMat.IsDraft);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => IsOdustaniEnabled);
            IzlazCommand = new RelayCommand(Izlaz, () => isIzlazEnable);
            NovoSklCommand = new RelayCommand(NovaKalkulacija, () => !isNovaKalkulacijaClicked);

            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !HasUnsavedEntry && MatList != null && MatList.Count > 0);
            PrintCommand = new RelayCommand(Print, () => !HasUnsavedEntry && CurrentItemMat != null && CurrentItemMat.Id > 0 && !CurrentItemMat.IsDraft);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            //RedniBrojCommand = new RelayCommand(RedniBroj);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton, () => !IsNovaKalkulacijaClicked);

            #endregion



            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                MatList = LoadOpenings(dbContext);

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
                
                CurrentItemTabMaterijalaK = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).FirstOrDefault();
                
                CurrentItemTabMaterijalaK2 = CurrentItemTabMaterijalaK;

                TebelaSkladistaUlazaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemMat.Kljnaz1).ToList());

                //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property

                CurrentItemTabMaterijala = CurrentItemTabMaterijalaK;

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
        public void NabavnaCijena()
        {
            if (CurrentItemMat == null) return;
            if (CurrentItemMat.Nc.HasValue) CurrentItemMat.Nc = LedgerMath.Price(CurrentItemMat.Nc.Value);
            CurrentItemMat.Vrijed = InitialStockRules.LineValue(CurrentItemMat.Kolic, CurrentItemMat.Nc);
            OnPropertyChanged(nameof(CurrentItemMat));
            UpdateSumVrijed();
        }

        public void Osvjezi()
        {
            if (CurrentItemMat == null) return;
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentItemMat.Ident = MaterialIdentifier.Normalize(CurrentItemMat.Ident);
                if (CurrentItemMat.Kljnaz > 0 && !dbContext.SifarnikSkladista.Any(row => row.Kljnaz == CurrentItemMat.Kljnaz))
                {
                    System.Windows.MessageBox.Show("Skladište nije prijavljeno u šifarnik!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                    CurrentItemMat.Kljnaz = null;
                }
                if (!string.IsNullOrEmpty(CurrentItemMat.Ident) && !dbContext.TabelaMaterijala.Any(row => row.Ident == CurrentItemMat.Ident))
                {
                    System.Windows.MessageBox.Show("Materijal ne postoji!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                    CurrentItemMat.Ident = null;
                }
                bool changed = _displayedMaterial != CurrentItemMat.Ident || _displayedWarehouse != CurrentItemMat.Kljnaz;
                bool suggestPrice = changed || (CurrentItemMat.IsDraft && !CurrentItemMat.Nc.HasValue);
                LoadReferenceData(dbContext);
                if (suggestPrice)
                {
                    var receipts = dbContext.Mat.AsNoTracking().Where(m => m.Status == "U" &&
                        m.Ident == CurrentItemMat.Ident && m.Kljnaz == CurrentItemMat.Kljnaz).ToList();
                    CurrentItemMat.Nc = InitialStockRules.SuggestedPrice(receipts, CurrentItemMat.Ident,
                        CurrentItemMat.Kljnaz ?? 0, CurrentItemMat.BookYear);
                    NabavnaCijena();
                }
            }
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

        private static Mat CopyOpening(Mat source)
        {
            var copy = new Mat();
            foreach (var property in typeof(Mat).GetProperties().Where(p => p.CanRead && p.CanWrite))
            {
                var value = property.GetValue(source);
                property.SetValue(copy, value is byte[] ? ((byte[])value).Clone() : value);
            }
            return copy;
        }

        private static ObservableCollection<Mat> LoadOpenings(materijalno_knjigovodstvoContext db)
        {
            // Fox Kljnazi order: warehouse + material, then physical record order.
            return new ObservableCollection<Mat>(db.Mat.AsNoTracking()
                .Where(m => m.Status == "P" && m.Kljnaz >= 1000 && m.Kljnaz <= 1012)
                .OrderBy(m => m.Kljnaz).ThenBy(m => m.Ident)
                .ThenBy(m => m.FoxRecno ?? int.MaxValue).ThenBy(m => m.Id).ToList());
        }

        private void ReloadOpenings(materijalno_knjigovodstvoContext db, int? selectedId, int preferredIndex)
        {
            MatList = LoadOpenings(db);
            if (MatList.Count == 0)
            {
                CurrentIndex = 0;
                CurrentItemMat = new Mat();
                CurrentItemTabMaterijala = null;
                CurrentItemTabMaterijalaK = null;
                CurrentItemTabMaterijalaK2 = null;
                CurrentItemTabSkladista = null;
                return;
            }
            int index = selectedId.HasValue ? MatList.ToList().FindIndex(m => m.Id == selectedId.Value) : -1;
            CurrentIndex = index >= 0 ? index : Math.Max(0, Math.Min(preferredIndex, MatList.Count - 1));
            CurrentItemMat = MatList[CurrentIndex];
            LoadReferenceData(db);
        }

        private void Brisanje()
        {
            if (CurrentItemMat == null || CurrentItemMat.IsDraft || HasUnsavedEntry) return;
            if (System.Windows.MessageBox.Show("Želite li obrisati tekući podatak?", "Upozorenje",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                DeleteCurrentOpening();
        }

        public void DeleteCurrentOpening()
        {
            if (CurrentItemMat == null || CurrentItemMat.Id <= 0 || CurrentItemMat.IsDraft || HasUnsavedEntry)
                throw new InvalidOperationException("Prvo završite unos i odaberite snimljeno početno stanje za brisanje.");
            int index = MatList.ToList().FindIndex(m => m.Id == CurrentItemMat.Id);
            new MaterialPostingService().Delete(CurrentItemMat);
            using (var db = new materijalno_knjigovodstvoContext()) ReloadOpenings(db, null, index);
            _editingExisting = false;
            isNovaKalkulacijaClicked = false;
            _isNovaKalkulacijaClicked = false;
            IsOdustaniEnabled = false;
            isIzlazEnable = false;
            UpdateCommands();
        }

        private void Update()
        {
            if (CurrentItemMat == null || CurrentItemMat.Id <= 0 || CurrentItemMat.IsDraft || HasUnsavedEntry)
                throw new InvalidOperationException("Odaberite snimljeno početno stanje za izmjenu.");
            _rowBeforeEntry = CurrentItemMat.Id;
            _indexBeforeEntry = CurrentIndex;
            _editingExisting = true;
            // Keep the saved list row untouched until Snimi; Odustani reloads SQL.
            CurrentItemMat = CopyOpening(CurrentItemMat);
            isNovaKalkulacijaClicked = true;
            _isNovaKalkulacijaClicked = false;
            IsOdustaniEnabled = true;
            isIzlazEnable = false;
            UpdateCommands();
            RequestFocusQuantity?.Invoke();
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

        private void NovaKalkulacija()
        {
            _rowBeforeEntry = CurrentItemMat?.Id;
            _indexBeforeEntry = CurrentIndex;
            _editingExisting = false;
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
                    Status = "P",
                    Kljnaz1 = 0,
                    Redbr = 1
                   
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
            SaveCurrentOpening();
            System.Windows.MessageBox.Show("Početno stanje je uspješno snimljeno.", "Potvrda",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public Mat SaveCurrentOpening()
        {
            if (!IsEntryEditable || CurrentItemMat == null)
                throw new InvalidOperationException("Prvo otvorite unos ili izmjenu početnog stanja.");
            bool edited = _editingExisting;
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                currentItemMat.Status = "P";
                currentItemMat.Kontosklad1 = 0;
                currentItemMat.Analst = "";
                currentItemMat.Brnar = "";
                currentItemMat.Brdok = "";
                currentItemMat.Brfak = "";

                // The posting service assigns the Fox calendar date on a new
                // opening and preserves the saved date when editing a row.


                if (currentItemMat.Kljnaz != null)
                {
                    currentItemMat.Kontosklad = dbContext.SifarnikMaterijalSkladisteKonto
                    .Where(row => row.Sifskla == currentItemMat.Kljnaz && row.Sifmat == CurrentItemMat.Ident)
                    .Select(row => row.Sifkonta)
                    .FirstOrDefault();
                }

                if (CurrentItemMat.Ident != null)
                {
                    CurrentItemMat.Konto1 = (int?)dbContext.TabelaMaterijala
                    .Where(row => row.Ident == CurrentItemMat.Ident)
                    .Select(row => row.Konto1)
                    .FirstOrDefault();
                }

                
                CurrentItemMat = new MaterialPostingService().Save(CurrentItemMat);
                if (MatList != null) { int savedIndex = MatList.ToList().FindIndex(m => m.Id == CurrentItemMat.Id); if (savedIndex >= 0) MatList[savedIndex] = CurrentItemMat; }

                //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
                MatList = LoadOpenings(dbContext);




                isNovaKalkulacijaClicked = false;
                _isNovaKalkulacijaClicked = !edited;
                IsOdustaniEnabled = false;
                isIzlazEnable = !edited;

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
                _editingExisting = false;
                _rowBeforeEntry = CurrentItemMat.Id;
                _indexBeforeEntry = CurrentIndex;
                UpdateCommands();
                return CurrentItemMat;
            }
        }

        private void UpdateCommands()
        {
            foreach (var command in new[] { PrviButtonCommand, ZadnjiButtonCommand, NextButtonCommand,
                PrethodniButtonCommand, SpasiNovuKalkulacijuCommand, OdustaniCommand, BrisanjeCommand,
                UpdateCommand, TraziSifruMaterijalaCommand, NovoSklCommand, PrintCommand,
                IzlazCommand, SlijStavkaButtonCommand }) command?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(IsEntryEditable));
            OnPropertyChanged(nameof(IsEntryReadOnly));
            OnPropertyChanged(nameof(HasUnsavedEntry));
        }

        //Samo dostupno kada odemo na nova kalkulacija i da vrati na prethodni
        private void Odustani()
        {
            if (!IsEntryEditable) return;
            if (System.Windows.MessageBox.Show("Želite li odustati od tekućih izmjena?", "Upozorenje",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                CancelCurrentOpening();
        }

        public void CancelCurrentOpening()
        {
            if (!IsEntryEditable) return;
            using (var db = new materijalno_knjigovodstvoContext())
            {
                if (!_editingExisting && CurrentItemMat != null)
                {
                    var draft = db.Mat.IgnoreQueryFilters().SingleOrDefault(m => m.Id == CurrentItemMat.Id &&
                        m.LineToken == CurrentItemMat.LineToken && m.IsDraft && m.Status == "P");
                    if (draft != null) { db.Mat.Remove(draft); db.SaveChanges(); }
                }
                // Existing saved rows are never deleted by Odustani.
                ReloadOpenings(db, _rowBeforeEntry, _indexBeforeEntry);
            }
            _editingExisting = false;
            isNovaKalkulacijaClicked = false;
            _isNovaKalkulacijaClicked = false;
            IsOdustaniEnabled = false;
            isIzlazEnable = false;
            UpdateCommands();
        }

        private void SlijStavkaButton()
        {
            _rowBeforeEntry = CurrentItemMat?.Id;
            _indexBeforeEntry = CurrentIndex;
            _editingExisting = false;
            //Ostavi prve stavke, dodaj redni broj, ostala polja ostavi prazno i spasi kao novi currentItemMat
            Mat snimljeniCurrentMat = currentItemMat;

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaje u listu novi CurrentItemMat
                MatList.Add(new Mat
                {
                    Redbr = snimljeniCurrentMat.Redbr + 1,
                    Kljnaz = snimljeniCurrentMat.Kljnaz,
                    Brfak = snimljeniCurrentMat.Brfak,
                    Datun = snimljeniCurrentMat.Datun,
                    Datnar = snimljeniCurrentMat.Datnar,
                    Brdok = snimljeniCurrentMat.Brdok,
                    Status = "P",
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
            if (MatList == null || MatList.Count == 0)
            {
                CurrentIndex = 0; CurrentItemMat = new Mat(); return;
            }
            CurrentIndex = Math.Max(0, Math.Min(CurrentIndex, MatList.Count - 1));

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


            LoadReferenceData(dbContext);
        }

        private void LoadReferenceData(materijalno_knjigovodstvoContext dbContext)
        {
            if (CurrentItemMat == null) return;
            //Nadji listu svih po *Ident* iz *TabelaMaterijala* i *CurrentItem* (Mat) i stavi u listu
            TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).ToList());

            //TebelaKontaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Konto1 == CurrentItemMat.Konto1).ToList());
            CurrentItemTabMaterijalaK = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemMat.Ident).FirstOrDefault();
            CurrentItemTabMaterijalaK2 = CurrentItemTabMaterijalaK;

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
            _displayedMaterial = CurrentItemMat.Ident;
            _displayedWarehouse = CurrentItemMat.Kljnaz;
            UpdateSumVrijed();
        }

        private void UpdateSumVrijed()
        {
            SumVrijed = InitialStockRules.WarehouseTotal(MatList ?? Enumerable.Empty<Mat>(), CurrentItemMat);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
}
