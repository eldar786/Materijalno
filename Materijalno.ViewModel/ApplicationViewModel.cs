using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Materijalno;
using Materijalno.Model;
using System.Windows.Input;


namespace Materijalno.ViewModel
{
    public class ApplicationViewModel : ObservableObject
    {
        public ICommand GlavniCommand { get; set; }
        public ICommand SifarnikSkladistaCommand { get; set; }
        
        private object _odabraniVM;

        public object OdabraniVM
        {

            get { return _odabraniVM; }

            set
            {
                _odabraniVM = value;
                OnPropertyChanged("OdabraniVM");
            }

        }
        public ApplicationViewModel()
        {
            //SifarnikSkladistaCommand = new RelayCommand(OtvoriSifarnikSkladista);
            GlavniCommand = new RelayCommand(OtvoriGlavni);
            SifarnikSkladistaCommand = new RelayCommand(OtvoriSifarnikSkladista);
        }

        //private void OtvoriSifarnikSkladista()
        //{
        //    OdabraniVM = new SifarnikSkladistaViewModel(this);
        //} 
        
        private void OtvoriGlavni()
        {
            OdabraniVM = new GlavniViewModel(this);
        }

        public void OtvoriSifarnikSkladista()
        {
            //OdabraniVM = new SifarnikSkladistaViewModel();
        }
    }
}
