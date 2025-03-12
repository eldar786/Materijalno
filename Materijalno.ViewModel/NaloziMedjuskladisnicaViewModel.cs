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
    public class NaloziMedjuskladisnicaViewModel : INotifyPropertyChanged
    {
        #region Fields
        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;

        private string brojMedjuskladisnice;
        #endregion

        public ObservableCollection<Nalmat> NalMatList { get; set; }
        public ObservableCollection<Mat> MatListZaFormiranje { get; set; }


        public string BrojMedjuskladisnice
        {
            get { return brojMedjuskladisnice; }
            set
            {
                brojMedjuskladisnice = value;
                OnPropertyChanged(nameof(BrojMedjuskladisnice));
            }
        }


        public RelayCommand FormirajCommand { get; set; }

        public NaloziMedjuskladisnicaViewModel(GlavniViewModel gvm)
        {
            FormirajCommand = new RelayCommand(Formiraj);
        }

        public event PropertyChangedEventHandler PropertyChanged;


        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Formiraj()
        {
            int maxBronsta = 0;
            var dbContext = new materijalno_knjigovodstvoContext();

            //Prikupimo sve medjuskladisnice iz MAT tabele
            MatListZaFormiranje = new ObservableCollection<Mat>(dbContext.Mat
                               .Where(row => row.Status == "M" && row.Brfak == BrojMedjuskladisnice)
                               .OrderBy(row => row.Datun)
                               .ToList());

            NalMatList = new ObservableCollection<Nalmat>(dbContext.Nalmat.ToList());

            //Ako lista nije prazna onda trazi najveci broj u koloni "Bronsta"
            if (NalMatList.Count != 0)
            {
                maxBronsta = NalMatList.Select(n => n.Bronsta).Max();
            }

            //U NalMat tabelu treba dodati stvari iz "MatListZaFormiranje" i jos neke stvari rucno
            foreach (Mat mat in MatListZaFormiranje)
            {
                if (mat.Vrijed > 0)
                {
                    Nalmat noviNalMat = new Nalmat
                    {
                        Rostav = 1,
                        Datnsta = mat.Datun,
                        Sifakt = 61,
                        Bronsta = maxBronsta + 1,
                        Sintstav = (int)mat.Kontosklad,
                        Brdokst = mat.Brdok,
                        Kukistav = 0,
                        Datdokst = mat.Datnar,
                        Devsta = null,
                        Kurs = 0,
                        Datvalst = "  -   -",
                        Ourst = null,
                        Mjtst = null,
                        Analst = null,
                        Dug1st = mat.Vrijed,
                        Stodug1 = null,
                        Pot1st = 0,
                        Stopot1 = null,
                        Devdugst = 0,
                        Stodevd = null,
                        Devpotst = 0,
                        Stodevp = null,
                        Bracuna = null,
                        Brfak = mat.Brfak
                    };

                    dbContext.Add(noviNalMat);
                    dbContext.SaveChanges();
                }

                else if (mat.Vrijed < 0)
                {
                    Nalmat noviNalMat = new Nalmat
                    {
                        Rostav = 1,
                        Datnsta = mat.Datun,
                        Sifakt = 61,
                        Bronsta = maxBronsta + 1,
                        Sintstav = (int)mat.Kontosklad,
                        Brdokst = mat.Brdok + " NCV U MAG",
                        Kukistav = 0,
                        Datdokst = mat.Datnar,
                        Devsta = null,
                        Kurs = 0,
                        Datvalst = "  -   -",
                        Ourst = null,
                        Mjtst = null,
                        Analst = null,
                        Dug1st = 0,
                        Stodug1 = null,
                        Pot1st = mat.Vrijed,
                        Stopot1 = null,
                        Devdugst = 0,
                        Stodevd = null,
                        Devpotst = 0,
                        Stodevp = null,
                        Bracuna = null,
                        Brfak = mat.Brfak
                    };

                    dbContext.Add(noviNalMat);
                    dbContext.SaveChanges();


                    //Dalje treba dodavati u Oracle bazu, tabelu "Interface_nalog i Interface_stavke"
                }
            }
        }
    }
}
