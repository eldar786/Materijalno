using Materijalno.Model.Services;
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
        string connectionString = Materijalno.Model.Services.DatabaseConfiguration.MaterialConnectionString;

        string queryStringStaraSifra_Ime = "SELECT STARA_SIFRA, IME FROM [FINANSIJE2-LINKED SERVER]..[IBS].[GR_KOMITENTI]";
        string currentNazivZaSifruKomitenta;
        public int CurrentIndex = 0;
        public static bool isNovaKalkulacijaClicked = false;
        public static bool isOdustaniEnabled = false;
        public static bool isIzlazEnable = false;
        public event Action RequestFocusIdent;

        private bool _unosMedjuskladisniceAktivan = false;

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

        public bool UnosMedjuskladisniceAktivan
        {
            get => _unosMedjuskladisniceAktivan;
            set
            {
                if (_unosMedjuskladisniceAktivan == value)
                    return;

                _unosMedjuskladisniceAktivan = value;
                OnPropertyChanged(nameof(UnosMedjuskladisniceAktivan));
                OnPropertyChanged(nameof(NijeAktivanUnosMedjuskladisnice));
            }
        }

        public bool NijeAktivanUnosMedjuskladisnice =>
            !UnosMedjuskladisniceAktivan;

        public static bool isTraziClicked = false;
        public event PropertyChangedEventHandler PropertyChanged;
        private bool prethodniButtonIsExecuted = false;
        private bool nextButtonIsExecuted = false;
        private bool prvoUcitavanje = false;
        private bool spasi = false;


        #endregion


        #region Properties and Lists

        //Staviti bolji naziv CurrentItemMat
        public int? SourceWarehouse
        {
            get => CurrentItemMat?.Medus == "1" ? CurrentItemMat.Kljnaz1 : CurrentItemMat?.Kljnaz;
            set { if (CurrentItemMat == null) return; if (CurrentItemMat.Medus == "1") CurrentItemMat.Kljnaz1 = value; else CurrentItemMat.Kljnaz = value; OnPropertyChanged(nameof(SourceWarehouse)); }
        }
        public int? DestinationWarehouse
        {
            get => CurrentItemMat?.Medus == "1" ? CurrentItemMat.Kljnaz : CurrentItemMat?.Kljnaz1;
            set { if (CurrentItemMat == null) return; if (CurrentItemMat.Medus == "1") CurrentItemMat.Kljnaz = value; else CurrentItemMat.Kljnaz1 = value; OnPropertyChanged(nameof(DestinationWarehouse)); }
        }
        public string SourceWarehouseName => (CurrentItemMat?.Medus == "1" ? CurrentItemTabSkladistaUlaza : CurrentItemTabSkladista)?.NazivOrg;
        public string DestinationWarehouseName => (CurrentItemMat?.Medus == "1" ? CurrentItemTabSkladista : CurrentItemTabSkladistaUlaza)?.NazivOrg;

        public Mat CurrentItemMat
        {
            get { return currentItemMat; }
            set
            {
                currentItemMat = value;
                OnPropertyChanged(nameof(SourceWarehouse)); OnPropertyChanged(nameof(DestinationWarehouse));
                OnPropertyChanged(nameof(SourceWarehouseName)); OnPropertyChanged(nameof(DestinationWarehouseName));
                OnPropertyChanged(nameof(CurrentItemMat));
                OnPropertyChanged(nameof(CanPrintTransfer));
                UpdateCommands();
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
                OnPropertyChanged(nameof(SourceWarehouseName)); OnPropertyChanged(nameof(DestinationWarehouseName));
            }
        }

        public SifarnikSkladista CurrentItemTabSkladistaUlaza
        {
            get { return currentItemTabSkladistaUlaza; }
            set
            {
                currentItemTabSkladistaUlaza = value;
                OnPropertyChanged(nameof(CurrentItemTabSkladistaUlaza));
                OnPropertyChanged(nameof(SourceWarehouseName)); OnPropertyChanged(nameof(DestinationWarehouseName));
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
        //public ObservableCollection<TabelaMaterijala> TebelaKontaList { get; set; }
        public ObservableCollection<SifarnikSkladista> TebelaSkladistaList { get; set; }
        public ObservableCollection<SifarnikSkladista> TebelaSkladistaUlazaList { get; set; }
        public ObservableCollection<Mat> MatList { get; set; }
        public ObservableCollection<Mat> PrintList { get; set; }
        public List<Komitenti> StaraSifra_Ime_List { get; set; }

        #endregion

        private enum TransferWorkflow { Browsing, Editing, SavedLine }
        private TransferWorkflow _workflow;
        private int? _rowBeforeEditing;
        private int _indexBeforeEditing;
        public bool CanBrowseTransfers => _workflow == TransferWorkflow.Browsing;
        public bool CanPrintTransfer => CanBrowseTransfers && CurrentItemMat != null && CurrentItemMat.Id > 0 && !CurrentItemMat.IsDraft;
        public event Action RequestFocusSlijedecaStavka;

        private void InitializeCommands()
        {
            PrviButtonCommand = new RelayCommand(PrviButton, () => CanBrowseTransfers && MatList.Count > 0);
            ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => CanBrowseTransfers && MatList.Count > 0);
            NextButtonCommand = new RelayCommand(NextButton, () => CanBrowseTransfers && MatList.Count > 0);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => CanBrowseTransfers && MatList.Count > 0);
            BrisanjeCommand = new RelayCommand(Brisanje, () => CanPrintTransfer);
            UpdateCommand = new RelayCommand(Update, () => CanPrintTransfer);
            NovaMedjuskladisnicaCommand = new RelayCommand(NovaMedjuskladisnica, () => CanBrowseTransfers);
            NovaStavkaButtonCommand = new RelayCommand(NovaStavkaButton, () => CanPrintTransfer);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => _workflow == TransferWorkflow.Editing);
            OdustaniCommand = new RelayCommand(Odustani, () => _workflow == TransferWorkflow.Editing);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton, () => _workflow == TransferWorkflow.SavedLine);
            IzlazCommand = new RelayCommand(Izlaz, () => _workflow == TransferWorkflow.SavedLine);
            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => CanBrowseTransfers && MatList.Count > 0);
            PrintCommand = new RelayCommand(Print, () => CanPrintTransfer);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            BrojKalkulacijeCommand = new RelayCommand(BrojKalkulacije);
            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            SetWorkflow(TransferWorkflow.Browsing);
        }

        private void SetWorkflow(TransferWorkflow state)
        {
            _workflow = state;
            isNovaKalkulacijaClicked = state == TransferWorkflow.Editing;
            isIzlazEnable = state == TransferWorkflow.SavedLine;
            IsOdustaniEnabled = state == TransferWorkflow.Editing;
            IsNovaKalkulacijaClicked = state == TransferWorkflow.SavedLine;
            UnosMedjuskladisniceAktivan = state != TransferWorkflow.Browsing;
            OnPropertyChanged(nameof(CanBrowseTransfers));
            OnPropertyChanged(nameof(CanPrintTransfer));
            UpdateCommands();
        }

        private void ReloadTransfers(materijalno_knjigovodstvoContext db, int? preferredId = null, int? preferredIndex = null)
        {
            MatList = new ObservableCollection<Mat>(db.Mat.AsNoTracking()
                .Where(m => m.Status == "M" && m.Medus == "1").OrderBy(m => m.Datun).ThenBy(m => m.Id).ToList());
            int found = preferredId.HasValue ? MatList.ToList().FindIndex(m => m.Id == preferredId.Value) : -1;
            CurrentIndex = found >= 0 ? found : preferredIndex ?? 0;
            CurrentItemMat = null;
            UpdateCurrentItemData(db);
            OnPropertyChanged(nameof(MatList));
            OnPropertyChanged(nameof(CanPrintTransfer));
            UpdateCommands();
        }

        #region Commands

        public RelayCommand NextButtonCommand { get; set; }
        public RelayCommand PrethodniButtonCommand { get; set; }
        public RelayCommand PrviButtonCommand { get; set; }
        public RelayCommand ZadnjiButtonCommand { get; set; } 
        public RelayCommand BrisanjeCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }
        public RelayCommand NovaMedjuskladisnicaCommand { get; set; }

        public RelayCommand NovaStavkaButtonCommand { get; set; }

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

        public MedjuskladisnicaViewModel(GlavniViewModel gvm) : this()
        {
            _gvm = gvm;
            using (var db = new materijalno_knjigovodstvoContext()) ReloadTransfers(db);
            StaraSifra_Ime_List = DohvatiNazivKomitenta();
        }

        public MedjuskladisnicaViewModel()
        {
            MatList = new ObservableCollection<Mat>();
            InitializeCommands();
        }

            public MedjuskladisnicaViewModel(GlavniViewModel gvm, Mat selected) : this()
        {
            _gvm = gvm;
            isTraziClicked = false;
            using (var db = new materijalno_knjigovodstvoContext())
            {
                ReloadTransfers(db, selected?.Id);
                if (selected != null && selected.IsDraft)
                {
                    MatList.Add(selected); CurrentIndex = MatList.Count - 1;
                    CurrentItemMat = selected; UpdateCurrentItemData(db);
                    SetWorkflow(TransferWorkflow.Editing);
                }
            }
            StaraSifra_Ime_List = DohvatiNazivKomitenta();
        }
        #endregion

        #region Methods

        public void Osvjezi()
        {
            if (CurrentItemMat == null) return;
            var dbContext = new materijalno_knjigovodstvoContext();

            // Kljnaz kolona
            int? maxValue_Skladiste = dbContext.SifarnikSkladista
                .Max(row => row.Kljnaz);

            int? minValue_Skladiste = dbContext.SifarnikSkladista
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
        public void NabavnaCijena()
        {
            if (CurrentItemMat == null || !CurrentItemMat.Ident.HasValue || !CurrentItemMat.Kolic.HasValue) return;
            int warehouse = CurrentItemMat.Medus == "1" ? CurrentItemMat.Kljnaz1 ?? 0 : CurrentItemMat.Kljnaz ?? 0;
            if (warehouse <= 0) return;
            try
            {
                var stock = new MaterialPostingService().Balance(CurrentItemMat.Ident.Value, warehouse, CurrentItemMat.Id, CurrentItemMat.TransferPairId);
                CurrentItemMat.Nc = stock.UnitCost;
                CurrentItemMat.Vrijed = LedgerMath.IssueValue(stock, Math.Abs(CurrentItemMat.Kolic.Value));
                OnPropertyChanged(nameof(CurrentItemMat));
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Količina", MessageBoxButton.OK, MessageBoxImage.Warning); }
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
                        CurrentNazivZaSifruKomitenta = string.IsNullOrEmpty(currentItemMat?.Analst) ? ""
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
            if (!CanPrintTransfer) return;
            if (System.Windows.MessageBox.Show("Obrisati oba reda odabrane međuskladišnice?", "Brisanje", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { DeleteCurrentLine(); }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Brisanje", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        public void DeleteCurrentLine()
        {
            if (!CanPrintTransfer) throw new InvalidOperationException("Prvo odaberite snimljenu stavku za brisanje.");
            int index = CurrentIndex;
            new MaterialPostingService().Delete(CurrentItemMat);
            using (var db = new materijalno_knjigovodstvoContext()) ReloadTransfers(db, preferredIndex: index);
            SetWorkflow(TransferWorkflow.Browsing);
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

                CurrentItemMat = new MaterialPostingService().SaveTransfer(CurrentItemMat);
                if (CurrentIndex >= 0 && CurrentIndex < MatList.Count) MatList[CurrentIndex] = CurrentItemMat;

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
            int? selectedId = CurrentItemMat?.Id;
            SetWorkflow(TransferWorkflow.Browsing);
            using (var db = new materijalno_knjigovodstvoContext()) ReloadTransfers(db, selectedId);
        }
        private void BrojKalkulacije()
        {
            if (CurrentItemMat != null) CurrentItemMat.Brfak = new MaterialPostingService().ReserveDocumentNumber("M", new MaterialPostingService().ActiveYear());
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

            else if (CurrentItemMat.Vrijed == null || CurrentItemMat.Nc == null)
            {
                CurrentItemMat.Vrijed = 0;
                CurrentItemMat.Nc = 0;
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
            if (CurrentItemMat == null || !CurrentItemMat.Ident.HasValue || !CurrentItemMat.Kolic.HasValue) return false;
            int warehouse = CurrentItemMat.Status == "M" && CurrentItemMat.Medus == "1" ? CurrentItemMat.Kljnaz1 ?? 0 : CurrentItemMat.Kljnaz ?? 0;
            try
            {
                var stock = new MaterialPostingService().Balance(CurrentItemMat.Ident.Value, warehouse, CurrentItemMat.Id, CurrentItemMat.TransferPairId);
                LedgerMath.IssueValue(stock, Math.Abs(CurrentItemMat.Kolic.Value));
                return true;
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Količina", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        }

        private void NovaMedjuskladisnica()
        {
            _rowBeforeEditing = CurrentItemMat?.Id;
            _indexBeforeEditing = CurrentIndex;
            var posting = new MaterialPostingService();
            int year = posting.ActiveYear();
            var draft = MaterialPostingService.CreateTransferDraft();
            draft.BookYear = year; draft.Redbr = 1;
            draft.Brfak = posting.ReserveDocumentNumber("M", year);
            using (var db = new materijalno_knjigovodstvoContext())
            {
                db.Mat.Add(draft); db.SaveChanges();
                MatList.Add(draft); CurrentIndex = MatList.Count - 1; CurrentItemMat = draft;
                UpdateCurrentItemData(db);
            }
            SetWorkflow(TransferWorkflow.Editing);
        }
        private void NovaStavkaButton()
        {
            if (CanPrintTransfer) CreateNextLine();
        }

        private void CreateNextLine()
        {
            var previous = CurrentItemMat;
            if (previous == null || previous.IsDraft) return;
            _rowBeforeEditing = previous.Id; _indexBeforeEditing = CurrentIndex;
            int? source = SourceWarehouse, destination = DestinationWarehouse;
            using (var db = new materijalno_knjigovodstvoContext())
            {
                int last = db.Mat.IgnoreQueryFilters().Where(m => m.Status == "M" && !m.IsArchived &&
                    m.BookYear == previous.BookYear && m.Brfak == previous.Brfak &&
                    ((m.Kljnaz == source && m.Kljnaz1 == destination) || (m.Kljnaz == destination && m.Kljnaz1 == source)))
                    .Max(m => m.Redbr) ?? 0;
                var next = MaterialPostingService.CreateTransferDraft();
                next.BookYear = previous.BookYear; next.Kljnaz = source; next.Kljnaz1 = destination;
                next.Datun = previous.Datun; next.Datnar = previous.Datnar; next.Brdok = previous.Brdok; next.Brfak = previous.Brfak;
                next.Ourst = previous.Ourst ?? 0; next.Mjtst = previous.Mjtst ?? 0;
                next.Redbr = last + 1;
                db.Mat.Add(next); db.SaveChanges();
                MatList.Add(next); CurrentIndex = MatList.Count - 1; CurrentItemMat = next;
                UpdateCurrentItemData(db);
            }
            SetWorkflow(TransferWorkflow.Editing);
            RequestFocusIdent?.Invoke();
        }

        //Pokupi sva polja trenutna i spasi. Trebalo bi napraviti disabled SAVE button ako nije odabrana nova kalkulacija
        private void SpasiNovuKalkulaciju()
        {
            try { SaveCurrentLine(); }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Međuskladišnica", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        public Mat SaveCurrentLine()
        {
            if (_workflow != TransferWorkflow.Editing || CurrentItemMat == null)
                throw new InvalidOperationException("Prvo otvorite unos stavke međuskladišnice.");
            CurrentItemMat.Status = "M";
            var saved = new MaterialPostingService().SaveTransfer(CurrentItemMat);
            using (var db = new materijalno_knjigovodstvoContext()) ReloadTransfers(db, saved.Id);
            SetWorkflow(TransferWorkflow.SavedLine);
            RequestFocusSlijedecaStavka?.Invoke();
            return CurrentItemMat;
        }

        private void UpdateCommands()
        {
            foreach (var command in new[] { PrviButtonCommand, ZadnjiButtonCommand, NextButtonCommand, PrethodniButtonCommand,
                SpasiNovuKalkulacijuCommand, OdustaniCommand, NovaMedjuskladisnicaCommand, BrisanjeCommand,
                UpdateCommand, TraziSifruMaterijalaCommand, PrintCommand, SlijStavkaButtonCommand, NovaStavkaButtonCommand, IzlazCommand })
                command?.RaiseCanExecuteChanged();
        }

        //Samo dostupno kada odemo na nova kalkulacija i da vrati na prethodni
        private void Odustani()
        {
            if (_workflow != TransferWorkflow.Editing) return;
            if (System.Windows.MessageBox.Show("Želite li odustati od tekućeg unosa?", "Upozorenje", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { CancelCurrentLine(); }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Odustajanje", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        public void CancelCurrentLine()
        {
            if (_workflow != TransferWorkflow.Editing) return;
            if (CurrentItemMat != null && CurrentItemMat.IsDraft) new MaterialPostingService().Delete(CurrentItemMat);
            using (var db = new materijalno_knjigovodstvoContext()) ReloadTransfers(db, _rowBeforeEditing, _indexBeforeEditing);
            SetWorkflow(TransferWorkflow.Browsing);
        }
        private void SlijStavkaButton()
        {
            if (_workflow == TransferWorkflow.SavedLine) CreateNextLine();
        }


        private void OtvoriKomitentListu()
        {
            _gvm.OdabraniVM = new KomitentiListaViewModel(this, _gvm);
        }


        // Ova metoda radi update CurrentItem i CurrentItemTabMaterijala based on the current index
        private void UpdateCurrentItemData(materijalno_knjigovodstvoContext dbContext)
        {
            if (MatList == null || MatList.Count == 0)
            {
                CurrentIndex = 0; CurrentItemMat = null;
                CurrentItemTabMaterijala = null; CurrentItemTabSkladista = null; CurrentItemTabSkladistaUlaza = null;
                TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>();
                TebelaSkladistaList = new ObservableCollection<SifarnikSkladista>();
                TebelaSkladistaUlazaList = new ObservableCollection<SifarnikSkladista>();
                CurrentNazivZaSifruKomitenta = ""; return;
            }
            int found = CurrentItemMat == null ? -1 : MatList.ToList().FindIndex(m => m.Id == CurrentItemMat.Id);
            CurrentIndex = found >= 0 ? found : Math.Max(0, Math.Min(CurrentIndex, MatList.Count - 1));
            CurrentItemMat = MatList[CurrentIndex];
            TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(m => m.Ident == CurrentItemMat.Ident).ToList());
            TebelaSkladistaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(m => m.Kljnaz == CurrentItemMat.Kljnaz).ToList());
            TebelaSkladistaUlazaList = new ObservableCollection<SifarnikSkladista>(dbContext.SifarnikSkladista.Where(m => m.Kljnaz == CurrentItemMat.Kljnaz1).ToList());
            CurrentItemTabMaterijala = TebelaMaterijalaList.FirstOrDefault();
            CurrentItemTabSkladista = TebelaSkladistaList.FirstOrDefault();
            CurrentItemTabSkladistaUlaza = TebelaSkladistaUlazaList.FirstOrDefault();
            CurrentNazivZaSifruKomitenta = StaraSifra_Ime_List?.FirstOrDefault(m => m.STARA_SIFRA == CurrentItemMat.Analst)?.IME ?? "";
            if (selectedKomitent != null)
            {
                CurrentItemMat.Analst = selectedKomitent.STARA_SIFRA;
                CurrentNazivZaSifruKomitenta = selectedKomitent.IME;
                selectedKomitent = null;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
}
