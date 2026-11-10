using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Materijalno.Model;
using Materijalno.Model.EntityModels;
using Microsoft.EntityFrameworkCore;

namespace Materijalno.ViewModel
{
    // A report selection is independent of saved inventory rows.
    public class StampanjeInventurnihZalihaViewModel : INotifyPropertyChanged
    {
        private Inv currentItemInv = new Inv();
        private SifarnikSkladista currentItemTabSkladista;
        private int? resolvedWarehouse;
        public static bool isTraziClicked;
        public static Inv selectedInv;
        public event PropertyChangedEventHandler PropertyChanged;

        public Inv CurrentItemInv
        {
            get { return currentItemInv; }
            set { currentItemInv = value ?? new Inv(); Notify(nameof(CurrentItemInv)); }
        }
        public SifarnikSkladista CurrentItemTabSkladista
        {
            get { return currentItemTabSkladista; }
            private set { currentItemTabSkladista = value; Notify(nameof(CurrentItemTabSkladista)); }
        }
        public ObservableCollection<Inv> InvList { get; private set; } = new ObservableCollection<Inv>();
        public RelayCommand OsvjeziCommand { get; private set; }

        public StampanjeInventurnihZalihaViewModel()
        {
            OsvjeziCommand = new RelayCommand(RefreshWarehouse);
        }
        public StampanjeInventurnihZalihaViewModel(GlavniViewModel main) : this()
        {
            using (var db = new materijalno_knjigovodstvoContext())
                InvList = new ObservableCollection<Inv>(db.Inv.AsNoTracking().OrderBy(i => i.Kljnaz).ThenBy(i => i.Datun).ToList());
        }
        public StampanjeInventurnihZalihaViewModel(GlavniViewModel main, Inv selected) : this(main)
        {
            if (selected == null) return;
            CurrentItemInv = new Inv {Kljnaz = selected.Kljnaz, InventorySessionId = selected.InventorySessionId, InventoryDate = selected.InventoryDate};
            resolvedWarehouse = selected.Kljnaz;
            RefreshWarehouse();
        }

        public void RefreshWarehouse()
        {
            int? warehouse = CurrentItemInv.Kljnaz;
            if (resolvedWarehouse != warehouse)
            {
                CurrentItemInv.InventorySessionId = null;
                CurrentItemInv.InventoryDate = null;
            }
            resolvedWarehouse = warehouse;
            using (var db = new materijalno_knjigovodstvoContext())
                CurrentItemTabSkladista = warehouse.HasValue && warehouse > 0
                    ? db.SifarnikSkladista.AsNoTracking().FirstOrDefault(w => w.Kljnaz == warehouse) : null;
        }
        private void Notify(string name) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    }
}
