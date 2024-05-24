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
        public ICommand PostavkaNovogPocetnogStanjaCommand { get; set; }
        public ICommand PocetnoStanjePrviPutCommand { get; set; }
        public ICommand StampanjeInventurnihZalihaCommand { get; set; }
        public ICommand UnosInventurnogStanjaCommand { get; set; }
        public ICommand NaloziZaFinansijePrintanjeNalogaCommand { get; set; }
        public ICommand PovratMaterijalaCommand { get; set; }
        public ICommand IzlazMaterijalaCommand { get; set; }
        public ICommand MedjuskladisnicaCommand { get; set; }
        public ICommand UlazMaterijalaCommand { get; set; }
        public ICommand SifarnikMaterijalSkladisteKontoCommand { get; set; }
        public ICommand SifarnikMaterijalaCommand { get; set; }
        public ICommand SifarnikKontaCommand { get; set; }
        public ICommand DnevnikCommand { get; set; }
        public ICommand CentralniMagacinCommand { get; set; }
        public ICommand ZaduzenjeProdavniceCommand { get; set; }
        public ICommand NaloziMedjuskladisnicaCommand { get; set; }
        public ICommand PovratUSkladisteCommand { get; set; }
        public ICommand StampanjeInventurnihListicaCommand { get; set; }
        public ICommand AnalitickaKarticaCommand { get; set; }
        public ICommand RekapitulacijaZalihaCommand { get; set; }
        public ICommand RekapitulacijaTroskovaCommand { get; set; }

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
            PostavkaNovogPocetnogStanjaCommand = new RelayCommand(OtvoriPostavkaNovogPocetnogStanja);
            PocetnoStanjePrviPutCommand = new RelayCommand(OtvoriPocetnoStanjePrviPut);
            StampanjeInventurnihZalihaCommand = new RelayCommand(OtvoriStampanjeInventurnihZaliha);
            UnosInventurnogStanjaCommand = new RelayCommand(OtvoriUnosInventurnogStanja);
            NaloziZaFinansijePrintanjeNalogaCommand = new RelayCommand(OtvoriNaloziZaFinansijePrintanjeNaloga);
            StampanjeInventurnihListicaCommand = new RelayCommand(OtvoriStampanjeInventurnihListica);
            PovratUSkladisteCommand = new RelayCommand(OtvoriPovratUSkladiste);
            NaloziMedjuskladisnicaCommand = new RelayCommand(OtvoriNaloziMedjuskladisnice);
            ZaduzenjeProdavniceCommand = new RelayCommand(OtvoriZaduzenjeProdavnice);
            CentralniMagacinCommand = new RelayCommand(OtvoriCentralniMagacin);
            PovratMaterijalaCommand = new RelayCommand(OtvoriPovratMaterijala);
            IzlazMaterijalaCommand = new RelayCommand(OtvoriIzlazMaterijala);
            MedjuskladisnicaCommand = new RelayCommand(OtvoriMedjuskladisnica);
            UlazMaterijalaCommand = new RelayCommand(OtvoriUlazMaterijala);
            SifarnikMaterijalSkladisteKontoCommand = new RelayCommand(OtvoriSifarnikMaterijalSkladisteKonto);
            SifarnikMaterijalaCommand = new RelayCommand(OtvoriSifarnikMaterijala);
            SifarnikKontaCommand = new RelayCommand(OtvoriSifarnikKonta);
            AnalitickaKarticaCommand = new RelayCommand(OtvoriAnalitickaKartica);
            DnevnikCommand = new RelayCommand(OtvoriDnevnik);
            RekapitulacijaZalihaCommand = new RelayCommand(OtvoriRekapitulacijaZaliha);
            RekapitulacijaTroskovaCommand = new RelayCommand(OtvoriRekapitulacijaTroskova);

        }

        public void OtvoriSifarnikSkladista()
        {
            OdabraniVM = new SifarnikSkladistaViewModel(this);
        }

        public void OtvoriPostavkaNovogPocetnogStanja()
        {
            OdabraniVM = new PostavkaNovogPocetnogstanjaViewModel(this);
        }

        public void OtvoriStampanjeInventurnihZaliha()
        {
            OdabraniVM = new StampanjeInventurnihZalihaViewModel(this);
        }

        public void OtvoriUnosInventurnogStanja()
        {
            OdabraniVM = new UnosInventurnogStanjaViewModel(this);
        }

        public void OtvoriZaduzenje()
        {

        }

        public void OtvoriNaloziZaFinansijePrintanjeNaloga()
        {
            OdabraniVM = new NaloziZaFinansijePrintanjeNalogaViewModel(this);
        }
        public void OtvoriMedjuskladisnica()
        {
            OdabraniVM = new MedjuskladisnicaViewModel(this);
        }
        public void OtvoriUlazMaterijala()
        {
            OdabraniVM = new UlazMaterijalaViewModel(this);
        }
        public void OtvoriSifarnikMaterijalSkladisteKonto()
        {
            OdabraniVM = new SifarnikMaterijalSkladisteKontoViewModel(this);
        }

        public void OtvoriRekapitulacijaTroskova()
        {
            OdabraniVM = new RekapitulacijaTroskovaViewModel(this);
        }
        public void OtvoriSifarnikMaterijala()
        {
            OdabraniVM = new SifarnikMaterijalaViewModel(this);
        }
        public void OtvoriSifarnikKonta()
        {
            OdabraniVM = new SifarnikKontaViewModel(this);
        }

        public void OtvoriAnalitickaKartica()
        {
            OdabraniVM = new AnalitickaKarticaViewModel(this);
        }
        public void OtvoriDnevnik()
        {
            OdabraniVM = new DnevnikViewModel(this);
        }

        public void OtvoriRekapitulacijaZaliha()
        {
            OdabraniVM = new RekapitulacijaZalihaViewModel(this);
        }

        public void OtvoriIzlazMaterijala()
        {
            OdabraniVM = new IzlazMaterijalaViewModel(this);
        }

        public void OtvoriPovratMaterijala()
        {
            OdabraniVM = new PovratMaterijalaViewModel(this);
        }

        public void OtvoriCentralniMagacin()
        {
            OdabraniVM = new CentralniMagacinViewModel(this);
            //nisam uradila comit all and push
        }

        public void OtvoriNaloziMedjuskladisnice()
        {
            OdabraniVM = new NaloziMedjuskladisnicaViewModel(this);
        }

        public void OtvoriPovratUSkladiste()
        {
            OdabraniVM = new PovratUSkladisteViewModel(this);
        }

        public void OtvoriGlavni()
        {
            OdabraniVM = new GlavniViewModel(_avm);
        }

        public void OtvoriPocetnoStanjePrviPut()
        {
            OdabraniVM = new PocetnoStanjePrviPutViewModel(this);
        }

        public void OtvoriStampanjeInventurnihListica()
        {
            OdabraniVM = new StampanjeInventurnihListicaViewModel(this);
        }

        public void OtvoriZaduzenjeProdavnice()
        {
            OdabraniVM = new ZaduzenjeProdavniceViewModel(this);
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