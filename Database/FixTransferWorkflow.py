"""One-time migration of transfer form presentation state; guarded against reapplication."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
path=root/'Materijalno.ViewModel/MedjuskladisnicaViewModel.cs'
s=path.read_text(encoding='utf-8-sig')
if 'enum TransferWorkflow' in s:raise SystemExit('Workflow already applied.')
def replace_method(signature,new):
    global s
    start=s.index(signature);left=s.index('{',start);depth=1;end=left+1
    while depth:
        if s[end]=='{':depth+=1
        elif s[end]=='}':depth-=1
        end+=1
    s=s[:start]+new+s[end:]

start=s.index('        #region Constructor') if '        #region Constructor' in s else s.index('        public MedjuskladisnicaViewModel(GlavniViewModel gvm)')
# Constructors share one set of commands, including when returning from Trazi.
replace_method('public MedjuskladisnicaViewModel(GlavniViewModel gvm)', '''public MedjuskladisnicaViewModel(GlavniViewModel gvm) : this()
        {
            _gvm = gvm;
            using (var db = new materijalno_knjigovodstvoContext()) ReloadTransfers(db);
            StaraSifra_Ime_List = DohvatiNazivKomitenta();
        }''')
replace_method('public MedjuskladisnicaViewModel()', '''public MedjuskladisnicaViewModel()
        {
            MatList = new ObservableCollection<Mat>();
            InitializeCommands();
        }''')
replace_method('public MedjuskladisnicaViewModel(GlavniViewModel gvm, Mat CurrentItemMat)', '''public MedjuskladisnicaViewModel(GlavniViewModel gvm, Mat selected) : this()
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
        }''')
anchor='        #region Commands'
extra='''        private enum TransferWorkflow { Browsing, Editing, SavedLine }
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

'''
s=s.replace(anchor,extra+anchor,1)
replace_method('private void NovaMedjuskladisnica()', '''private void NovaMedjuskladisnica()
        {
            _rowBeforeEditing = CurrentItemMat?.Id;
            _indexBeforeEditing = CurrentIndex;
            var posting = new MaterialPostingService();
            int year = posting.ActiveYear();
            var draft = new Mat { Status = "M", BookYear = year, Redbr = 1, Analst = "", Brnar = "", Konto2 = "",
                Tarifa = "", Fcj = "0", Cartro = "0", Zavtro = "0", Ppp = "0", Fvrijed = "0",
                Troskovi = 0, Porppp = 0, Trospe = 0, Ourst = 0, Mjtst = 0, Vrijed = 0, Nc = 0,
                Brfak = posting.ReserveDocumentNumber("M", year) };
            using (var db = new materijalno_knjigovodstvoContext())
            {
                db.Mat.Add(draft); db.SaveChanges();
                MatList.Add(draft); CurrentIndex = MatList.Count - 1; CurrentItemMat = draft;
                UpdateCurrentItemData(db);
            }
            SetWorkflow(TransferWorkflow.Editing);
        }''')
replace_method('private void NovaStavkaButton()', '''private void NovaStavkaButton()
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
                var next = new Mat { Status = "M", BookYear = previous.BookYear, Kljnaz = source, Kljnaz1 = destination,
                    Datun = previous.Datun, Datnar = previous.Datnar, Brdok = previous.Brdok, Brfak = previous.Brfak,
                    Ourst = previous.Ourst, Mjtst = previous.Mjtst, Medus = "", Redbr = last + 1, Nc = 0, Vrijed = 0 };
                db.Mat.Add(next); db.SaveChanges();
                MatList.Add(next); CurrentIndex = MatList.Count - 1; CurrentItemMat = next;
                UpdateCurrentItemData(db);
            }
            SetWorkflow(TransferWorkflow.Editing);
            RequestFocusIdent?.Invoke();
        }''')
replace_method('private void SpasiNovuKalkulaciju()', '''private void SpasiNovuKalkulaciju()
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
        }''')
replace_method('private void SlijStavkaButton()', '''private void SlijStavkaButton()
        {
            if (_workflow == TransferWorkflow.SavedLine) CreateNextLine();
        }''')
replace_method('private void Izlaz()', '''private void Izlaz()
        {
            int? selectedId = CurrentItemMat?.Id;
            SetWorkflow(TransferWorkflow.Browsing);
            using (var db = new materijalno_knjigovodstvoContext()) ReloadTransfers(db, selectedId);
        }''')
replace_method('private void Brisanje()', '''private void Brisanje()
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
        }''')
replace_method('private void Odustani()', '''private void Odustani()
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
        }''')
replace_method('private void UpdateCommands()', '''private void UpdateCommands()
        {
            foreach (var command in new[] { PrviButtonCommand, ZadnjiButtonCommand, NextButtonCommand, PrethodniButtonCommand,
                SpasiNovuKalkulacijuCommand, OdustaniCommand, NovaMedjuskladisnicaCommand, BrisanjeCommand,
                UpdateCommand, TraziSifruMaterijalaCommand, PrintCommand, SlijStavkaButtonCommand, NovaStavkaButtonCommand, IzlazCommand })
                command?.RaiseCanExecuteChanged();
        }''')
replace_method('private void UpdateCurrentItemData(materijalno_knjigovodstvoContext dbContext)', '''private void UpdateCurrentItemData(materijalno_knjigovodstvoContext dbContext)
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
        }''')
s=s.replace('SlijStavkaButtonCommand.RaiseCanExecuteChanged(); // Notify','SlijStavkaButtonCommand?.RaiseCanExecuteChanged(); // Notify')
s=s.replace('OdustaniCommand.RaiseCanExecuteChanged(); // Notify','OdustaniCommand?.RaiseCanExecuteChanged(); // Notify')
path.write_text(s,encoding='utf-8-sig')
print('Transfer workflow applied.')
