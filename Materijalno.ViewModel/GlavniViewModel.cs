using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Materijalno;
using Materijalno.Model;

namespace Materijalno.ViewModel
{
    public class GlavniViewModel : ObservableObject
    {
        ApplicationViewModel _avm;
        private object _odabraniVM;

        private object _odabraniVMW;

        public ICommand SifarnikSkladistaCommand { get; set; }
        public ICommand GlavniCommand { get; set; }
        public ICommand ZaduzenjeCommand { get; set; }
        public ICommand StampanjeInventurnihZalihaCommand { get; set; }

        public GlavniViewModel(ApplicationViewModel avm)
        {
            _avm = avm;
            _avm.OdabraniVM = this;

            SifarnikSkladistaCommand = new RelayCommand(OtvoriSifarnikSkladista);
            GlavniCommand = new RelayCommand(OtvoriGlavni);
        }

        public GlavniViewModel()
        {
            SifarnikSkladistaCommand = new RelayCommand(OtvoriSifarnikSkladista);
            ZaduzenjeCommand = new RelayCommand(OtvoriZaduzenje);
            StampanjeInventurnihZalihaCommand = new RelayCommand(OtvoriStampanjeInventurnihZaliha);
        }

        public void OtvoriSifarnikSkladista()
        {
            OdabraniVM = new SifarnikSkladistaViewModel(this); 
        }
        
        public void OtvoriStampanjeInventurnihZaliha()
        {
            OdabraniVM = new StampanjeInventurnihZalihaViewModel(this); 
        }

        public void OtvoriZaduzenje()
        {
            
        }

        public void OtvoriGlavni()
        {
            OdabraniVM = new GlavniViewModel(_avm);
        }

        public object OdabraniVM
        {

            get { return _odabraniVM; }

            set
            {
                _odabraniVM = value;
                OnPropertyChanged("OdabraniVM");
            }

        }

        public object OdabraniVMW
        {

            get { return _odabraniVMW; }

            set
            {
                _odabraniVMW = value;
                OnPropertyChanged("OdabraniVMW");
            }

        }

        public ApplicationViewModel AVM
        {

            get { return _avm; }

            set
            {
                _avm = value;
                OnPropertyChanged("AVM");
            }

        }
    }
}
