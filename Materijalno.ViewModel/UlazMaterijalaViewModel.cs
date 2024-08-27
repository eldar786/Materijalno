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

namespace Materijalno.ViewModel
{
    public class UlazMaterijalaViewModel : INotifyPropertyChanged
    {
        #region Fields

        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        private Mat currentItem;
        private TabelaMaterijala currentItemTabMaterijala;

        public int CurrentIndex = 0;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Properties and Lists

        public Mat CurrentItem
        {
            get { return currentItem; }
            set { 
                currentItem = value;
                OnPropertyChanged(nameof(CurrentItem));
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

        public ObservableCollection<TabelaMaterijala> TebelaMaterijalaList { get; set; }
        public ObservableCollection<Mat> MatList { get; set; }


        #endregion

        #region Commands

        public ICommand NextButtonCommand { get; set; }

        #endregion

        #region Constructor

        public UlazMaterijalaViewModel(GlavniViewModel gvm)
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                NextButtonCommand = new RelayCommand(NextButton);

                //Dodaj u listu gdje je kljnaz == 1000
                MatList = new ObservableCollection<Mat>(dbContext.Mat.Where(row => row.Kljnaz == 1000).ToList());

                UpdateCurrentItemData(dbContext);
            }
        }

        #endregion

        #region Methods

        private void NextButton()
        {
            using (var dbContext = new materijalno_knjigovodstvoContext())
            {
                CurrentIndex = (CurrentIndex + 1) % MatList.Count;

                UpdateCurrentItemData(dbContext);
            }
        }

        // Ova metoda radi update CurrentItem i CurrentItemTabMaterijala based on the current index
        private void UpdateCurrentItemData(materijalno_knjigovodstvoContext dbContext)
        {
            CurrentItem = MatList[CurrentIndex];

            //Nadji listu svih po *Ident* iz *TabelaMaterijala* i *CurrentItem* (Mat) i stavi u listu
            TebelaMaterijalaList = new ObservableCollection<TabelaMaterijala>(dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItem.Ident).ToList());
            
            //Nadji jednu vrijednost po *Ident* iz *TabelaMaterijala* i po Sifri materijala iz tabele *Mat*(col:*Ident*) i stavi u jedan property
            CurrentItemTabMaterijala = dbContext.TabelaMaterijala.Where(row => row.Ident == CurrentItem.Ident).FirstOrDefault();
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
}