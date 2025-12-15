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
            var dbContext = new materijalno_knjigovodstvoContext();

            // napr listu iz mat tab za status U
            decimal ncMat = (decimal)dbContext.Mat
                .Where(row => row.Ident == CurrentItemInv.Ident)
                .Select(row => row.Nc).FirstOrDefault();

            CurrentItemInv.Nc = ncMat;


           

            var culture = new CultureInfo("de-DE");

            if (CurrentItemInv.Kolic.HasValue && CurrentItemInv.Kolic.Value != 0)
            {
                CurrentItemInv.Vrijed = (decimal)CurrentItemInv.Kolic * CurrentItemInv.Nc;
            }
            else
            {
                // U slucaju da je Kolic null ili nula, da bi izbjegli dijeljenje sa nulom
                CurrentItemInv.Nc = 0;
            }

            dbContext.Update(CurrentItemInv);
            dbContext.SaveChanges();


            //Staviti po datumu da sortira i dodaj u listu da bi se vidjele promjene
            InvList = new ObservableCollection<Inv>(dbContext.Inv
                .Where(row => row.Kljnaz >= 1000 && row.Kljnaz <= 1012)
                .OrderBy(row => row.Datun)
                .ToList());

            UpdateCurrentItemData(dbContext);
        }


        private void NextButton()
        {

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
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentItemInv = InvList[CurrentIndex];

                var resultMessageBox = System.Windows.MessageBox.Show("Želite li obrisati tekući podatak? ", "Upozorenje", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (resultMessageBox == MessageBoxResult.Yes)
                {
                    dbContext.Inv.Remove(CurrentItemInv);
                    dbContext.SaveChanges();

                    InvList.Remove(CurrentItemInv);

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
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {


                if (CurrentItemInv.Kljnaz != null)
                {
                    CurrentItemInv.Kontosklad = dbContext.SifarnikMaterijalSkladisteKonto
                    .Where(row => row.Sifskla == CurrentItemInv.Kljnaz && row.Sifmat == CurrentItemInv.Ident)
                    .Select(row => row.Sifkonta)
                    .FirstOrDefault();
                }

                CurrentItemInv.Datun = DateTime.Now;
                CurrentItemInv.Datnar = DateTime.Now;

                dbContext.Update(CurrentItemInv);
                dbContext.SaveChanges();

                System.Windows.MessageBox.Show("Uspješno ste izmijenili", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);

                //NAKON STO KLIKNEMO NA OK DA SE VRATI NA IZMIJENJENI ŠIFARNIK
                UpdateCurrentItemData(dbContext);
            }
        }

        private void Trazi()
        {
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



        // An event that will be raised to notify the view to open the PrintWindow
        public event Action OnPrintEvent;

        //obrise sva polja i ostavlja opciju za SNIMI i ODUSTANI
        private void NovaKalkulacija()
        {
            //Prolazi ponovo provjeru CanExecute
            isNovaKalkulacijaClicked = true;

            UpdateCommands();

            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                InvList.Add(new Inv
                {
                    //Za ulaz materijala broj skladišta je uvijek 1000
                    //Kljnaz = 1000
                });

                CurrentIndex = InvList.Count - 1;
                CurrentItemInv = InvList[CurrentIndex];


                dbContext.Add(CurrentItemInv);
                dbContext.SaveChanges();

                UpdateCurrentItemData(dbContext);
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

                CurrentItemInv.Datun = DateTime.Now;
                CurrentItemInv.Datnar = DateTime.Now;

                dbContext.Update(CurrentItemInv);
                dbContext.SaveChanges();

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
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentItemInv = InvList[CurrentIndex];

                var resultMessageBox = System.Windows.MessageBox.Show("Želite li odustati tekući podatak? ", "Upozorenje", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (resultMessageBox == MessageBoxResult.Yes)
                {
                    isNovaKalkulacijaClicked = false;

                    UpdateCommands();
                    //Prije odustajanja dohvati zadnji index, da bi mogao vratiti u pogledu, da ne ide na pocetak??

                    dbContext.Inv.Remove(CurrentItemInv);
                    dbContext.SaveChanges();

                    InvList.Remove(CurrentItemInv);

                    isNovaKalkulacijaClicked = false;

                    CurrentItemInv = null;

                    _gvm.OdabraniVM = new UnosInventurnogStanjaViewModel(_gvm);
                }
                else if (resultMessageBox == MessageBoxResult.No)
                {
                    return;
                }
            }
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