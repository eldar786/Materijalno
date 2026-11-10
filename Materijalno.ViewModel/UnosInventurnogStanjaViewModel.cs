using Materijalno.Model.Services;
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
    public class UnosInventurnogStanjaViewModel : INotifyPropertyChanged
    {
        #region Fields

        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        private Mat currentItemMat;
        private Inv currentItemInv;
        private Mat itemMat;
        private TabelaMaterijala currentItemTabMaterijala;
        private TabelaMaterijala currentItemTabMaterijalaK;
        private TabelaMaterijala currentItemTabMaterijalaK2;
        private SifarnikSkladista currentItemTabSkladista;
        public static Komitenti selectedKomitent;
        public static Inv selectedInv;

        string currentNazivZaSifruKomitenta;
        public int CurrentIndex = 0;
        public static bool isNovaKalkulacijaClicked = false;
        public static bool isTraziClicked = false;
        public event PropertyChangedEventHandler PropertyChanged;
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
                OnPropertyChanged(nameof(CurrentItemMat));
            }
        }

        public Inv CurrentItemInv
        {
            get { return currentItemInv; }
            set
            {
                currentItemInv = value;
                OnPropertyChanged(nameof(CurrentItemInv));
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
        public TabelaMaterijala CurrentItemTabMaterijalaK
        {
            get { return currentItemTabMaterijalaK; }
            set
            {
                currentItemTabMaterijalaK = value;
                OnPropertyChanged(nameof(currentItemTabMaterijalaK));
            }
        }
        public TabelaMaterijala CurrentItemTabMaterijalaK2
        {
            get { return currentItemTabMaterijalaK2; }
            set
            {
                currentItemTabMaterijalaK2 = value;
                OnPropertyChanged(nameof(currentItemTabMaterijalaK2));
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
        public ObservableCollection<Mat> MatList { get; set; }
        public List<Komitenti> StaraSifra_Ime_List { get; set; }
        public ObservableCollection<Inv> InvList { get; set; }

        #endregion

        #region Commands

        public RelayCommand NextButtonCommand { get; set; }


        public RelayCommand PrethodniButtonCommand { get; set; }
        public RelayCommand PrviButtonCommand { get; set; }
        public RelayCommand ZadnjiButtonCommand { get; set; }
        public RelayCommand BrisanjeCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }
        public RelayCommand NovoSklCommand { get; set; }
        public RelayCommand SpasiNovuKalkulacijuCommand { get; set; }
        public RelayCommand NabavnaCijenaCommand { get; set; }

        //Potrebno uraditi ???
        public RelayCommand PrintCommand { get; set; }
        public RelayCommand OtvoriKomitentListuCommand { get; set; }
        public RelayCommand TraziSifruMaterijalaCommand { get; set; }

        public RelayCommand OdustaniCommand { get; set; }
        public RelayCommand OsvjeziCommand { get; set; }
        public RelayCommand BrojKalkulacijeCommand { get; set; }
        public RelayCommand SlijStavkaButtonCommand { get; set; }
        public RelayCommand IzlazCommand { get; set; }



        #endregion

        #region Constructor
        public UnosInventurnogStanjaViewModel()
        {
        }
        public UnosInventurnogStanjaViewModel(GlavniViewModel gvm)
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
            NovoSklCommand = new RelayCommand(NovaKalkulacija, () => !isNovaKalkulacijaClicked);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => isNovaKalkulacijaClicked);

            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !isNovaKalkulacijaClicked);
            PrintCommand = new RelayCommand(Print, () => !isNovaKalkulacijaClicked);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            //BrojKalkulacijeCommand = new RelayCommand(BrojKalkulacije);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton, () => IsNovaKalkulacijaClicked);

            #endregion

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                InvList = new ObservableCollection<Inv>(dbContext.Inv
                    .Where(row => row.Kljnaz >= 1000 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Datun)
                    .ToList());

                UpdateCurrentItemData(dbContext);

                //StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }

        public UnosInventurnogStanjaViewModel(GlavniViewModel gvm, Inv CurrentItemInv)
        {
            _gvm = gvm;
            this.CurrentItemInv = CurrentItemInv;

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
            PrviButtonCommand = new RelayCommand(PrviButton, () => !isNovaKalkulacijaClicked);
            NextButtonCommand = new RelayCommand(NextButton, () => !isNovaKalkulacijaClicked);
            PrethodniButtonCommand = new RelayCommand(PrethodniButton, () => !isNovaKalkulacijaClicked);
            ZadnjiButtonCommand = new RelayCommand(ZadnjiButton, () => !isNovaKalkulacijaClicked);
            BrisanjeCommand = new RelayCommand(Brisanje, () => !isNovaKalkulacijaClicked);
            UpdateCommand = new RelayCommand(Update, () => !isNovaKalkulacijaClicked);
            NovoSklCommand = new RelayCommand(NovaKalkulacija, () => !isNovaKalkulacijaClicked);
            SpasiNovuKalkulacijuCommand = new RelayCommand(SpasiNovuKalkulaciju, () => isNovaKalkulacijaClicked);
            OdustaniCommand = new RelayCommand(Odustani, () => isNovaKalkulacijaClicked);

            TraziSifruMaterijalaCommand = new RelayCommand(Trazi, () => !isNovaKalkulacijaClicked);
            PrintCommand = new RelayCommand(Print, () => !isNovaKalkulacijaClicked);
            OsvjeziCommand = new RelayCommand(Osvjezi);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena);
            //BrojKalkulacijeCommand = new RelayCommand(BrojKalkulacije);

            OtvoriKomitentListuCommand = new RelayCommand(OtvoriKomitentListu);
            SlijStavkaButtonCommand = new RelayCommand(SlijStavkaButton, () => IsNovaKalkulacijaClicked);

            #endregion

            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //Dodaj u listu gdje je kljnaz == 1000 i sortiraj po datumu iz kolone (datun)
                //Neki datum preskoci, treba napraviti dobar data type za kolonu (datun) u sql bazi
                InvList = new ObservableCollection<Inv>(dbContext.Inv
                    .Where(row => row.Kljnaz >= 1000 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Datun)
                    .ToList());

                #region Custom UpdateCurrentItemData
                //***Prilagodjena metoda UpdateCurrentItemData()***

                for (int i = 0; i < InvList.Count(); i++)
                {
                    if (InvList[i].Id == CurrentItemInv.Id)
                    {
                        CurrentIndex = i;
                    }
                }

                CurrentItemInv = (Inv)InvList.FirstOrDefault(row => row.Id == CurrentItemInv.Id);

                //Nadji listu svih po *Ident* iz *TabelaMaterijala* i *CurrentItem* (Mat) i stavi u listu
                TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemInv.Ident).ToList());

                //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property
                CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemInv.Ident).FirstOrDefault();
                CurrentItemTabMaterijalaK = dbContext.TabelaMaterijala.Where(row => row.Konto1 == CurrentItemInv.Konto1).FirstOrDefault();
                CurrentItemTabMaterijalaK2 = dbContext.TabelaMaterijala.Where(row => row.Konto2 == CurrentItemInv.Konto2).FirstOrDefault();
                CurrentItemTabSkladista = dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemInv.Kljnaz).FirstOrDefault();

                //Ako lista nije popunjena iz linked server (oracle baza), onda ce preskociti i pozivati u konstruktoru preko druge metode i
                //popuniti CurrentNazivZaSifruKomitenta. Ovo radimo da ne bi ponovo popunjavali listu iz oracle baze, zbog brzeg rada aplikacije
                if (StaraSifra_Ime_List != null)
                {
                    CurrentNazivZaSifruKomitenta = string.IsNullOrEmpty(CurrentItemInv.Analst) ? ""
                        : StaraSifra_Ime_List.FirstOrDefault(row => row.STARA_SIFRA == CurrentItemInv.Analst)?.IME;
                }

                if (selectedKomitent != null)
                {
                    CurrentItemInv.Analst = selectedKomitent.STARA_SIFRA;
                    CurrentNazivZaSifruKomitenta = selectedKomitent.IME;
                }

                selectedKomitent = null;

                #endregion

                //StaraSifra_Ime_List = DohvatiNazivKomitenta();
            }
        }

        #endregion

        #region Methods
        public void NabavnaCijena()
        {
            if (CurrentItemInv == null || !CurrentItemInv.Ident.HasValue || CurrentItemInv.Kljnaz <= 0) return;
            try
            {
                CurrentItemInv.Nc = new InventoryService().Quote(CurrentItemInv);
                CurrentItemInv.Vrijed = LedgerMath.Money((CurrentItemInv.Kolic ?? 0) * CurrentItemInv.Nc);
                OnPropertyChanged(nameof(CurrentItemInv));
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Inventura", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }


        private void NextButton()
        {
            if (InvList == null || InvList.Count == 0)
            {
                CurrentItemInv = null;
                System.Windows.MessageBox.Show("Inventurna lista je prazna", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                return; // or handle empty list case
            }

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                //kada se dodje do zadnjeg reda da obavijesti korisnika i vrati metodu
                if (CurrentIndex == InvList.Count - 1)
                {
                    System.Windows.MessageBox.Show("Došli ste do zadnjeg podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                    return;
                }

                CurrentIndex = (CurrentIndex + 1) % InvList.Count;
                CurrentItemInv = InvList[CurrentIndex];
                UpdateCurrentItemData(dbContext);
            }
        }
        private void PrethodniButton()
        {
            if (InvList == null || InvList.Count == 0)
            {
                CurrentItemInv = null;
                System.Windows.MessageBox.Show("Inventurna lista je prazna", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                return; // or handle empty list case
            }

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (CurrentIndex == 0)
                {
                    System.Windows.MessageBox.Show("Došli ste do prvog podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                    return;
                }

                CurrentIndex = (CurrentIndex - 1) % InvList.Count;
                CurrentItemInv = InvList[CurrentIndex];
                UpdateCurrentItemData(dbContext);
            }
        }
        private void PrviButton()
        {
            if (InvList == null || InvList.Count == 0)
            {
                CurrentItemInv = null;
                System.Windows.MessageBox.Show("Inventurna lista je prazna", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                return; // or handle empty list case
            }

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentIndex = 0;
                CurrentItemInv = InvList[CurrentIndex];
                UpdateCurrentItemData(dbContext);
                System.Windows.MessageBox.Show("Došli ste do prvog podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void ZadnjiButton()
        {
            if (InvList == null || InvList.Count == 0)
            {
                CurrentItemInv = null;
                System.Windows.MessageBox.Show("Inventurna lista je prazna", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                return; // or handle empty list case
            }

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentIndex = InvList.Count - 1;
                CurrentItemInv = InvList[CurrentIndex];
                UpdateCurrentItemData(dbContext);

                System.Windows.MessageBox.Show("Došli ste do zadnjeg podatka", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void Brisanje()
        {
            if (InvList == null || InvList.Count == 0)
            {
                CurrentItemInv = null;
                System.Windows.MessageBox.Show("Inventurna lista je prazna", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                return; // or handle empty list case
            }

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentItemInv = InvList[CurrentIndex];

                var resultMessageBox = System.Windows.MessageBox.Show("Želite li obrisati tekući podatak? ", "Upozorenje", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (resultMessageBox == MessageBoxResult.Yes)
                {
                    new InventoryService().Delete(CurrentItemInv);

                    InvList.Remove(CurrentItemInv);
                    if (InvList.Count == 0) { CurrentIndex = 0; CurrentItemInv = new Inv(); OnPropertyChanged(nameof(InvList)); return; }

                    CurrentItemInv = null;
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
            if (InvList == null || InvList.Count == 0)
            {
                CurrentItemInv = null;
                System.Windows.MessageBox.Show("Inventurna lista je prazna", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                return; // or handle empty list case
            }

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                if (CurrentItemInv.Kljnaz != null)
                {
                    CurrentItemInv.Kontosklad = dbContext.SifarnikMaterijalSkladisteKonto
                    .Where(row => row.Sifskla == CurrentItemInv.Kljnaz && row.Sifmat == CurrentItemInv.Ident)
                    .Select(row => row.Sifkonta)
                    .FirstOrDefault();
                }



                CurrentItemInv = new InventoryService().Save(CurrentItemInv);
                if (CurrentIndex >= 0 && CurrentIndex < InvList.Count) InvList[CurrentIndex] = CurrentItemInv;

                System.Windows.MessageBox.Show("Uspješno ste izmijenili", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                //NAKON STO KLIKNEMO NA OK DA SE VRATI NA IZMIJENJENI ŠIFARNIK
                UpdateCurrentItemData(dbContext);
            }
        }

        private void Trazi()
        {
            if (InvList == null || InvList.Count == 0)
            {
                CurrentItemInv = null;
                System.Windows.MessageBox.Show("Inventurna lista je prazna", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                return; // or handle empty list case
            }

            _gvm.OdabraniVM = new InvListaViewModel(this, _gvm);
        }

        private void Print()
        {
            //Pozivano ovu metodu zbog ukupnoTroskovi, da bi prilikom printa izracunao, da ne bi morali ponovo racunati
            //NabavnaCijena();
            var culture = new CultureInfo("de-DE");

            decimal? ukupnoNcValue = CurrentItemInv.Kolic * CurrentItemInv.Nc;
            //treba vidjeti kako da prebaci na DE culture???
            ukupnoNc = Math.Round((decimal)ukupnoNcValue, 9);
            decimal? formmatedNc = decimal.Parse(ukupnoNc.ToString(), NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, culture);
            ukupnoNc = formmatedNc;

            OnPrintEvent?.Invoke();
        }

        private void Osvjezi()
        {
            if (CurrentItemInv == null)
            {
                System.Windows.MessageBox.Show("Tabela je prazna / nema trenutnog zapisa!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dbContext = new materijalno_knjigovodstvoContext();

            // Kljnaz kolona
            int? maxValue_Skladiste = dbContext.SifarnikSkladista
                .Max(row => row.Kljnaz);

            int? minValue_Skladiste = dbContext.SifarnikSkladista
                .Min(row => row.Kljnaz);


            // Ident kolona
            int? maxValue_SifraMat = dbContext.TabelaMaterijala
                .Max(row => row.Ident);

            int? minValue_SifraMat = dbContext.TabelaMaterijala
                .Min(row => row.Ident);


            if (CurrentItemInv.Kljnaz < minValue_Skladiste || CurrentItemInv.Kljnaz > maxValue_Skladiste)
            {
                System.Windows.MessageBox.Show("Skladište nije prijavljeno u šifarnik!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);

                //TextBox Skladiste vratiti na prazno
                CurrentItemInv.Kljnaz = 0;
            }

            if (CurrentItemInv.Ident < minValue_SifraMat || CurrentItemInv.Ident > maxValue_SifraMat)
            {
                System.Windows.MessageBox.Show("Materijal ne postoji!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
                CurrentItemInv.Ident = 0;
            }



            UpdateCurrentItemData(dbContext);
        }
                private void Izlaz()
        {
            if (CurrentItemInv != null && CurrentItemInv.IsDraft) { SpasiNovuKalkulaciju(); if (CurrentItemInv.IsDraft) return; }
            isNovaKalkulacijaClicked = false; _isNovaKalkulacijaClicked = false; isIzlazEnable = false; IsOdustaniEnabled = false;
            UpdateCommands();
            using (var db = new materijalno_knjigovodstvoContext()) UpdateCurrentItemData(db);
        }
                private void SlijStavkaButton()
        {
            if (CurrentItemInv == null) return;
            if (CurrentItemInv.IsDraft) { SpasiNovuKalkulaciju(); if (CurrentItemInv.IsDraft) return; }
            var previous = CurrentItemInv;
            using (var db = new materijalno_knjigovodstvoContext())
            {
                var next = new Inv { Kljnaz = previous.Kljnaz, Datun = previous.InventoryDate ?? previous.Datun,
                    Datnar = previous.InventoryDate ?? previous.Datun, Redbr = (previous.Redbr ?? 0) + 1, Status = "P", IsDraft = true };
                db.Inv.Add(next); db.SaveChanges();
                InvList.Add(next); CurrentIndex = InvList.Count - 1; CurrentItemInv = next;
                isNovaKalkulacijaClicked = true; IsOdustaniEnabled = true; UpdateCommands(); UpdateCurrentItemData(db);
            }
        }



        // An event that will be raised to notify the view to open the PrintWindow
        public event Action OnPrintEvent;

        //obrise sva polja i ostavlja opciju za SNIMI i ODUSTANI
                private void NovaKalkulacija()
        {
            using (var db = new materijalno_knjigovodstvoContext())
            {
                var next = new Inv { Kljnaz = 1000, Redbr = 1, Status = "P", IsDraft = true, Datun = DateTime.Today, Datnar = DateTime.Today };
                db.Inv.Add(next); db.SaveChanges();
                InvList.Add(next); CurrentIndex = InvList.Count - 1; CurrentItemInv = next;
                isNovaKalkulacijaClicked = true; IsOdustaniEnabled = true; UpdateCommands(); UpdateCurrentItemData(db);
            }
        }

        //Pokupi sva polja trenutna i spasi. Trebalo bi napraviti disabled SAVE button ako nije odabrana nova kalkulacija
        private void SpasiNovuKalkulaciju()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentItemInv.Kontosklad1 = 0;


                if (CurrentItemInv.Kljnaz != null)
                {
                    CurrentItemInv.Kontosklad = dbContext.SifarnikMaterijalSkladisteKonto
                    .Where(row => row.Sifskla == CurrentItemInv.Kljnaz && row.Sifmat == CurrentItemInv.Ident)
                    .Select(row => row.Sifkonta)
                    .FirstOrDefault();
                }

                if (CurrentItemInv.Kljnaz != null)
                {
                    CurrentItemInv.Konto1 = dbContext.TabelaMaterijala
                     .Where(row => row.Ident == CurrentItemInv.Ident)
                     .Select(row => (int?)row.Konto1) // Convert to nullable int
                     .FirstOrDefault() ?? 0;
                }



                CurrentItemInv = new InventoryService().Save(CurrentItemInv);
                if (CurrentIndex >= 0 && CurrentIndex < InvList.Count) InvList[CurrentIndex] = CurrentItemInv;

                //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
                InvList = new ObservableCollection<Inv>(dbContext.Inv
                    .Where(row => row.Kljnaz >= 1000 && row.Kljnaz <= 1012)
                    .OrderBy(row => row.Datun)
                    .ToList());

                System.Windows.MessageBox.Show("Uspješno ste unijeli.", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                //Ovo kada je true, onda ce buttoni biti dostupni
                isNovaKalkulacijaClicked = false;

                UpdateCommands();

                if (CurrentItemInv != null)
                {
                    for (int i = 0; i < InvList.Count(); i++)
                    {
                        if (InvList[i].Id == CurrentItemInv.Id)
                        {
                            CurrentIndex = i;
                            CurrentItemInv = InvList[CurrentIndex];
                        }
                    }
                }

                UpdateCurrentItemData(dbContext);
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
            NovoSklCommand.RaiseCanExecuteChanged();
            BrisanjeCommand.RaiseCanExecuteChanged();
            UpdateCommand.RaiseCanExecuteChanged();
            TraziSifruMaterijalaCommand.RaiseCanExecuteChanged();
            PrintCommand.RaiseCanExecuteChanged();

            SpasiNovuKalkulacijuCommand.RaiseCanExecuteChanged();
            OdustaniCommand.RaiseCanExecuteChanged();
        }

        //Samo dostupno kada odemo na nova kalkulacija i da vrati na prethodni
                private void Odustani()
        {
            if (CurrentItemInv == null) return;
            if (CurrentItemInv.IsDraft) new InventoryService().Delete(CurrentItemInv);
            isNovaKalkulacijaClicked = false; _isNovaKalkulacijaClicked = false; IsOdustaniEnabled = false;
            _gvm.OdabraniVM = new UnosInventurnogStanjaViewModel(_gvm);
        }

        private void OtvoriKomitentListu()
        {
            //_gvm.OdabraniVM = new KomitentiListaViewModel(this, _gvm);
        }

        // Ova metoda radi update CurrentItem i CurrentItemTabMaterijala based on the current index
        private void UpdateCurrentItemData(materijalno_knjigovodstvoContext dbContext)
        {
            if (InvList == null || InvList.Count == 0)
            {
                CurrentIndex = 0; CurrentItemInv = new Inv(); return;
            }
            CurrentIndex = Math.Max(0, Math.Min(CurrentIndex, InvList.Count - 1));

            if (InvList == null || InvList.Count == 0)
            {
                System.Windows.MessageBox.Show("Trenutno nema inventurnih listića!", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (CurrentItemInv != null)
            {
                for (int i = 0; i < InvList.Count(); i++)
                {
                    if (InvList[i].Id == CurrentItemInv.Id)
                    {
                        CurrentIndex = i;
                        CurrentItemInv = InvList[CurrentIndex];
                    }
                }
            }
            else
            {
                CurrentItemInv = InvList[CurrentIndex];
            }

            //Nadji listu svih po *Ident* iz *TabelaMaterijala* i *CurrentItem* (Mat) i stavi u listu
            TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemInv.Ident).ToList());

            //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property
            CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemInv.Ident).FirstOrDefault();
            CurrentItemTabMaterijalaK = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItemInv.Ident).FirstOrDefault();
            CurrentItemTabMaterijalaK2 = dbContext.TabelaMaterijala.Where(row => row.Konto2 == CurrentItemInv.Konto2).FirstOrDefault();
            CurrentItemTabSkladista = dbContext.SifarnikSkladista.Where(row => row.Kljnaz == CurrentItemInv.Kljnaz).FirstOrDefault();

            //Ako lista nije popunjena iz linked server (oracle baza), onda ce preskociti i pozivati u konstruktoru preko druge metode i
            //popuniti CurrentNazivZaSifruKomitenta. Ovo radimo da ne bi ponovo popunjavali listu iz oracle baze, zbog brzeg rada aplikacije
            if (StaraSifra_Ime_List != null)
            {
                CurrentNazivZaSifruKomitenta = string.IsNullOrEmpty(CurrentItemInv.Analst) ? ""
                    : StaraSifra_Ime_List.FirstOrDefault(row => row.STARA_SIFRA == CurrentItemInv.Analst)?.IME;
            }

            if (selectedKomitent != null)
            {
                CurrentItemInv.Analst = selectedKomitent.STARA_SIFRA;
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