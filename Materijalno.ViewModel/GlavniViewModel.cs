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
        public ICommand SifarnikMaterijalSkladisteKontoCommand { get; set; }
        public ICommand SifarnikMaterijalaCommand { get; set; }
        public ICommand SifarnikKontaCommand { get; set; }
        public ICommand SifarnikMaterijalSkladisteKontoCommand { get; set; }
        public ICommand UlazMaterijalaCommand { get; set; }
        public ICommand IzlazMaterijalaCommand { get; set; }
        public ICommand MedjuskladisnicaCommand { get; set; }
        public ICommand PovratMaterijalaCommand { get; set; }
        public ICommand CentralniMagacinCommand { get; set; }
        public ICommand ZaduzenjeProdavniceCommand { get; set; }
        public ICommand NaloziMedjuskladisnicaCommand { get; set; }
        public ICommand PovratUSkladisteCommand { get; set; }
        public ICommand StampanjeInventurnihListicaCommand { get; set; }
        public ICommand SifarnikKontaCommand { get; set; }

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
            SifarnikMaterijalSkladisteKontoCommand = new RelayCommand(OtvoriSifarnikMaterijalSkladisteKonto);
            SifarnikMaterijalaCommand = new RelayCommand(OtvoriSifarnikMaterijala);

            SifarnikKontaCommand = new RelayCommand(OtvoriSifarnikKonta);
        }

        public void OtvoriSifarnikSkladista()
        {
            OdabraniVM = new SifarnikSkladistaViewModel(this); 
        }
        public void OtvoriZaduzenje()
        {
            
        }

        public void OtvoriSifarnikMaterijalSkladisteKonto()
        {
            OdabraniVM = new SifarnikMaterijalSkladisteKontoViewModel(this);
        public void OtvoriSifarnikMaterijala()
        {
            OdabraniVM = new SifarnikMatrijalaViewModel(this);

        public void OtvoriSifarnikKonta()
        {
            OdabraniVM = new SifarnikKontaViewModel(this);
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
