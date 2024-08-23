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

namespace Materijalno.ViewModel
{
    public class UlazMaterijalaViewModel : ObservableObject
    {
        #region Fields and Lists

        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        public ObservableCollection<Mat> MatList { get; set; }
        public int CurrentIndex { get; set; } = 0;
        public Mat CurrentItem { get; set; }
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

                CurrentItem = MatList[CurrentIndex];

                var dat = CurrentItem.Datun;

                
            }
        }

        private void NextButton()
        {
            CurrentIndex = (CurrentIndex + 1) % MatList.Count;
            CurrentItem = MatList[CurrentIndex];
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;


        public void OnpropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }
    }
}