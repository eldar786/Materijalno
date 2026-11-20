using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Microsoft.EntityFrameworkCore;

namespace Materijalno.ViewModel
{
    public class UnosInventurnogStanjaViewModel : INotifyPropertyChanged, Services.IActivePrometEntry
    {
        private enum EntryMode { Browse, Add, Edit, SavedNew }
        private EntryMode _mode;
        private readonly GlavniViewModel _gvm;
        private Inv _current;
        private int? _returnId;
        private int _returnIndex;
        private bool _newWarehouse;
        private string _quotedKey;

        // Retained for the existing inventory lookup page.
        public static Inv selectedInv;
        public static bool isTraziClicked;
        public static bool isNovaKalkulacijaClicked;
        public int CurrentIndex;
        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<string> RequestFocus;
        public event Action OnPrintEvent;
        public Func<bool> CommitPendingInput { get; set; }

        public Inv CurrentItemInv
        {
            get => _current;
            set { _current = value; Changed(nameof(CurrentItemInv)); NotifyAmounts(); }
        }
        public ObservableCollection<Inv> InvList { get; private set; } = new ObservableCollection<Inv>();
        public TabelaMaterijala CurrentItemTabMaterijala { get; private set; }
        public TabelaMaterijala CurrentItemTabMaterijalaK => CurrentItemTabMaterijala;
        public TabelaMaterijala CurrentItemTabMaterijalaK2 => CurrentItemTabMaterijala;
        public SifarnikSkladista CurrentItemTabSkladista { get; private set; }
        public bool IsEntryEditable => _mode == EntryMode.Add || _mode == EntryMode.Edit;
        public bool IsEntryReadOnly => !IsEntryEditable;
        public bool IsWarehouseEditable => IsEntryEditable && (_newWarehouse || _mode == EntryMode.Edit);
        public bool HasUnsavedEntry => IsEntryEditable;
        public bool IsNovaKalkulacijaClicked => IsEntryEditable;
        public bool IsOdustaniEnabled => _mode != EntryMode.Browse;
        private bool HasCurrent => CurrentItemInv != null && CurrentItemInv.Id > 0;
        private bool CanBrowse => _mode == EntryMode.Browse;

        public decimal? Kolicina
        {
            get => CurrentItemInv?.Kolic;
            set { if (CurrentItemInv == null) return; CurrentItemInv.Kolic = value; RecalculateValue(); }
        }
        public decimal NabavnaCijenaUnos
        {
            get => CurrentItemInv?.Nc ?? 0;
            set { if (CurrentItemInv == null) return; CurrentItemInv.Nc = value; RecalculateValue(); }
        }
        public decimal Vrijednost => CurrentItemInv?.Vrijed ?? 0;
        public decimal UkupnaVrijednost
        {
            get
            {
                if (CurrentItemInv?.Kljnaz == null) return 0;
                decimal saved = InvList.Where(i => i.Kljnaz == CurrentItemInv.Kljnaz && i.Id != CurrentItemInv.Id).Sum(i => i.Vrijed);
                return LedgerMath.Money(saved + (HasCurrent || IsEntryEditable ? CurrentItemInv.Vrijed : 0));
            }
        }
        public decimal? UkupnoNc => UkupnaVrijednost;

        public RelayCommand PrviButtonCommand { get; private set; }
        public RelayCommand NextButtonCommand { get; private set; }
        public RelayCommand PrethodniButtonCommand { get; private set; }
        public RelayCommand ZadnjiButtonCommand { get; private set; }
        public RelayCommand BrisanjeCommand { get; private set; }
        public RelayCommand UpdateCommand { get; private set; }
        public RelayCommand NovoSklCommand { get; private set; }
        public RelayCommand NovaStavkaCommand { get; private set; }
        public RelayCommand SpasiNovuKalkulacijuCommand { get; private set; }
        public RelayCommand NabavnaCijenaCommand { get; private set; }
        public RelayCommand PrintCommand { get; private set; }
        public RelayCommand TraziSifruMaterijalaCommand { get; private set; }
        public RelayCommand OdustaniCommand { get; private set; }
        public RelayCommand OsvjeziCommand { get; private set; }
        public RelayCommand SlijStavkaButtonCommand { get; private set; }
        public RelayCommand IzlazCommand { get; private set; }

        public UnosInventurnogStanjaViewModel()
        {
            PrviButtonCommand = new RelayCommand(() => SelectIndex(0), () => CanBrowse && HasCurrent);
            NextButtonCommand = new RelayCommand(() => SelectIndex(CurrentIndex + 1), () => CanBrowse && HasCurrent);
            PrethodniButtonCommand = new RelayCommand(() => SelectIndex(CurrentIndex - 1), () => CanBrowse && HasCurrent);
            ZadnjiButtonCommand = new RelayCommand(() => SelectIndex(InvList.Count - 1), () => CanBrowse && HasCurrent);
            BrisanjeCommand = new RelayCommand(ConfirmDelete, () => CanBrowse && HasCurrent);
            UpdateCommand = new RelayCommand(BeginEdit, () => CanBrowse && HasCurrent);
            NovoSklCommand = new RelayCommand(BeginNewWarehouse, () => CanBrowse);
            NovaStavkaCommand = new RelayCommand(BeginNewItem, () => CanBrowse && HasCurrent);
            SpasiNovuKalkulacijuCommand = new RelayCommand(() => SaveCurrentInventory(), () => IsEntryEditable);
            NabavnaCijenaCommand = new RelayCommand(NabavnaCijena, () => IsEntryEditable);
            PrintCommand = new RelayCommand(() => OnPrintEvent?.Invoke(), () => CanBrowse && HasCurrent);
            TraziSifruMaterijalaCommand = new RelayCommand(() => {
                isTraziClicked = true;
                if (_gvm != null) _gvm.OdabraniVM = new InvListaViewModel(this, _gvm);
            }, () => CanBrowse && HasCurrent);
            OdustaniCommand = new RelayCommand(CancelCurrentInventory, () => IsOdustaniEnabled);
            OsvjeziCommand = new RelayCommand(Osvjezi, () => IsEntryEditable);
            SlijStavkaButtonCommand = new RelayCommand(BeginNewItem, () => _mode == EntryMode.SavedNew);
            IzlazCommand = new RelayCommand(() => { if (_gvm != null) _gvm.OdabraniVM = null; }, () => CanBrowse);
        }

        public UnosInventurnogStanjaViewModel(GlavniViewModel gvm) : this()
        {
            _gvm = gvm; Reload(null, 0); SetMode(EntryMode.Browse);
        }
        public UnosInventurnogStanjaViewModel(GlavniViewModel gvm, Inv selection) : this()
        {
            _gvm = gvm; Reload(selection?.Id, 0); SetMode(EntryMode.Browse);
            isTraziClicked = false; selectedInv = null;
        }

        private void Reload(int? id, int fallbackIndex)
        {
            using (var db = new materijalno_knjigovodstvoContext())
            {
                // Fox INV is opened on Kljnazi (warehouse + character IDENT).
                InvList = new ObservableCollection<Inv>(db.Inv.AsNoTracking().Where(i => i.Status == "P")
                    .OrderBy(i => i.Kljnaz).ThenBy(i => i.Ident).ThenBy(i => i.FoxRecno ?? int.MaxValue).ThenBy(i => i.Id).ToList());
                Changed(nameof(InvList));
                int index = id.HasValue ? InvList.ToList().FindIndex(i => i.Id == id.Value) : -1;
                CurrentIndex = index >= 0 ? index : Math.Max(0, Math.Min(fallbackIndex, InvList.Count - 1));
                CurrentItemInv = InvList.Count == 0 ? new Inv() : InvList[CurrentIndex];
                LoadReferences(db);
            }
        }
        private void SelectIndex(int index)
        {
            if (!CanBrowse || InvList.Count == 0) return;
            CurrentIndex = Math.Max(0, Math.Min(index, InvList.Count - 1));
            CurrentItemInv = InvList[CurrentIndex];
            using (var db = new materijalno_knjigovodstvoContext()) LoadReferences(db);
        }
        private void LoadReferences(materijalno_knjigovodstvoContext db)
        {
            string ident = CurrentItemInv?.Ident;
            int? warehouse = CurrentItemInv?.Kljnaz;
            CurrentItemTabMaterijala = string.IsNullOrWhiteSpace(ident) ? null : db.TabelaMaterijala.AsNoTracking().SingleOrDefault(m => m.Ident == ident);
            CurrentItemTabSkladista = warehouse.HasValue ? db.SifarnikSkladista.AsNoTracking().SingleOrDefault(w => w.Kljnaz == warehouse) : null;
            foreach (var name in new[] { nameof(CurrentItemTabMaterijala), nameof(CurrentItemTabMaterijalaK), nameof(CurrentItemTabMaterijalaK2), nameof(CurrentItemTabSkladista), nameof(UkupnaVrijednost), nameof(UkupnoNc) }) Changed(name);
        }
        public void Osvjezi()
        {
            if (!IsEntryEditable || CurrentItemInv == null) return;
            CurrentItemInv.Ident = MaterialIdentifier.Normalize(CurrentItemInv.Ident);
            using (var db = new materijalno_knjigovodstvoContext())
            {
                LoadReferences(db);
                if (CurrentItemInv.Kljnaz.HasValue && CurrentItemTabSkladista == null) throw new InvalidOperationException("Skladište nije prijavljeno u šifarnik.");
                if (!string.IsNullOrWhiteSpace(CurrentItemInv.Ident) && CurrentItemTabMaterijala == null) throw new InvalidOperationException("Materijal ne postoji.");
                string key = CurrentItemInv.Kljnaz + "|" + CurrentItemInv.Ident;
                if (key != _quotedKey)
                {
                    CurrentItemInv.Nc = 0;
                    if (CurrentItemTabMaterijala != null && CurrentItemTabSkladista != null) {
                        CurrentItemInv.Konto1 = CurrentItemTabMaterijala.Konto1;
                        CurrentItemInv.Konto2 = CurrentItemTabMaterijala.Konto2;
                        CurrentItemInv.Nc = new InventoryService().Quote(CurrentItemInv);
                    }
                    _quotedKey = key; RecalculateValue(); Changed(nameof(NabavnaCijenaUnos));
                }
            }
        }
        public void NabavnaCijena()
        {
            if (!IsEntryEditable) return;
            Osvjezi();
            if (CurrentItemTabMaterijala == null || CurrentItemTabSkladista == null) return;
            CurrentItemInv.Nc = new InventoryService().Quote(CurrentItemInv);
            RecalculateValue(); Changed(nameof(NabavnaCijenaUnos));
        }
        public void RecalculateValue()
        {
            if (CurrentItemInv != null) CurrentItemInv.Vrijed = LedgerMath.Money((CurrentItemInv.Kolic ?? 0) * CurrentItemInv.Nc);
            Changed(nameof(Vrijednost)); Changed(nameof(UkupnaVrijednost)); Changed(nameof(UkupnoNc));
        }
        private void NotifyAmounts()
        {
            foreach (var name in new[] { nameof(Kolicina), nameof(NabavnaCijenaUnos), nameof(Vrijednost), nameof(UkupnaVrijednost), nameof(UkupnoNc) }) Changed(name);
        }
        public void BeginNewWarehouse() { BeginNew(true); }
        public void BeginNewItem() { BeginNew(false); }
        private void BeginNew(bool newWarehouse)
        {
            if (IsEntryEditable) throw new InvalidOperationException("Prvo snimite tekući unos ili kliknite Odustani.");
            if (!newWarehouse && !HasCurrent) throw new InvalidOperationException("Prvo odaberite skladište inventure.");
            _returnId = HasCurrent ? (int?)CurrentItemInv.Id : null; _returnIndex = CurrentIndex;
            var previous = CurrentItemInv;
            int? warehouse = newWarehouse ? null : previous.Kljnaz;
            int line = newWarehouse ? 1 : _mode == EntryMode.SavedNew ? (previous.Redbr ?? 0) + 1 : InvList.Count(i => i.Kljnaz == warehouse) + 1;
            DateTime day = newWarehouse ? new DateTime(new MaterialPostingService().ActiveYear(), 1, 1) : (previous.InventoryDate ?? previous.Datun ?? new DateTime(new MaterialPostingService().ActiveYear(), 1, 1)).Date;
            _newWarehouse = newWarehouse; _quotedKey = null;
            // Like Fox's MEMVAR entry, no empty row is inserted before Snimi.
            CurrentItemInv = new Inv { Kljnaz = warehouse, Datun = day, Datnar = day, InventoryDate = day, Redbr = line, Status = "P", Kolic = 0, Nc = 0, Vrijed = 0, IsDraft = true };
            SetMode(EntryMode.Add);
            using (var db = new materijalno_knjigovodstvoContext()) LoadReferences(db);
            RequestFocus?.Invoke(newWarehouse ? "Warehouse" : "Material");
        }
        public void BeginEdit()
        {
            if (!CanBrowse || !HasCurrent) return;
            _returnId = CurrentItemInv.Id; _returnIndex = CurrentIndex;
            using (var db = new materijalno_knjigovodstvoContext()) CurrentItemInv = db.Inv.AsNoTracking().Single(i => i.Id == _returnId);
            _quotedKey = CurrentItemInv.Kljnaz + "|" + CurrentItemInv.Ident;
            SetMode(EntryMode.Edit); RequestFocus?.Invoke("Quantity");
        }
        public Inv SaveCurrentInventory()
        {
            if (!IsEntryEditable) throw new InvalidOperationException("Prvo otvorite unos ili izmjenu inventurne stavke.");
            if (CommitPendingInput != null && !CommitPendingInput()) throw new InvalidOperationException("Provjerite označena polja prije snimanja.");
            Osvjezi();
            bool edited = _mode == EntryMode.Edit;
            var saved = new InventoryService().Save(CurrentItemInv);
            SetMode(edited ? EntryMode.Browse : EntryMode.SavedNew);
            Reload(saved.Id, CurrentIndex); _returnId = saved.Id; _returnIndex = CurrentIndex;
            RequestFocus?.Invoke(edited ? "Edit" : "NextItem");
            return CurrentItemInv;
        }
        public void CancelCurrentInventory()
        {
            if (_mode == EntryMode.Browse) return;
            SetMode(EntryMode.Browse); Reload(_returnId, _returnIndex); _quotedKey = null;
            UpdateCommands(); RequestFocus?.Invoke(HasCurrent ? "Edit" : "NewWarehouse");
        }
        private void ConfirmDelete()
        {
            if (MessageBox.Show("Želite li obrisati tekući podatak?", "Inventurni popis", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                DeleteCurrentInventory();
        }
        public void DeleteCurrentInventory()
        {
            if (!CanBrowse || !HasCurrent) return;
            int index = CurrentIndex; new InventoryService().Delete(CurrentItemInv);
            Reload(null, index); UpdateCommands(); RequestFocus?.Invoke(HasCurrent ? "Edit" : "NewWarehouse");
        }
        private void SetMode(EntryMode mode)
        {
            _mode = mode; isNovaKalkulacijaClicked = IsEntryEditable;
            foreach (var name in new[] { nameof(IsEntryEditable), nameof(IsEntryReadOnly), nameof(IsWarehouseEditable), nameof(HasUnsavedEntry), nameof(IsNovaKalkulacijaClicked), nameof(IsOdustaniEnabled) }) Changed(name);
            UpdateCommands();
        }
        private void UpdateCommands()
        {
            foreach (var command in new[] { PrviButtonCommand, NextButtonCommand, PrethodniButtonCommand, ZadnjiButtonCommand, BrisanjeCommand, UpdateCommand, NovoSklCommand, NovaStavkaCommand, SpasiNovuKalkulacijuCommand, NabavnaCijenaCommand, PrintCommand, TraziSifruMaterijalaCommand, OdustaniCommand, OsvjeziCommand, SlijStavkaButtonCommand, IzlazCommand }) command?.RaiseCanExecuteChanged();
        }
        private void Changed(string name) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    }
}
