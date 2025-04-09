using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Materijalno;
using Materijalno.Model;
using Microsoft.Win32;

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
        public ICommand PoKontimaIKalkulacijamaCommand { get; set; }
        public ICommand ZaJednuKalkulacijuPregledUlazaCommand { get; set; }
        public ICommand ZaSveKalkulacijePregledUlazaCommand { get; set; }
        public ICommand ZaSveKalkulacijePregledIzlazaCommand { get; set; }
        public ICommand PoKontimaINalozimaZaSvaSkladistaCommand { get; set; }
        public ICommand PoKontimaIRadnimNalozimaCommand { get; set; }
        public ICommand PoKontimaCommand { get; set; }
        public ICommand ZaSvaSkladistaCommand { get; set; }
        public ICommand ZaSkladisteStanjeZalihaCommand { get; set; }
        public ICommand ZaMaterijalUSkladistuCommand { get; set; }
        public ICommand ZaSkladisteMedjuskladisniceCommand { get; set; }
        public ICommand MedjuskladisniceSveCommand { get; set; }
        public ICommand MedjuskladisniceJednaCommand { get; set; }
        public ICommand ZaJednuKalkulacijuPregledIzlazaCommand { get; set; }
        public ICommand ArhivaCommand { get; set; }

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
            PoKontimaIKalkulacijamaCommand = new RelayCommand(OtvoriPoKontimaIKalkulacijama);
            ZaJednuKalkulacijuPregledUlazaCommand = new RelayCommand(OtvoriZaJednuKalkulacijuPregledUlaza);
            ZaSveKalkulacijePregledUlazaCommand = new RelayCommand(OtvoriZaSveKalkulacijePregledUlaza);
            ZaSveKalkulacijePregledIzlazaCommand = new RelayCommand(OtvoriZaSveKalkulacijePregledIzlaza);
            PoKontimaINalozimaZaSvaSkladistaCommand = new RelayCommand(OtvoriPoKontimaINalozimaZaSvaSkladista);
            PoKontimaIRadnimNalozimaCommand = new RelayCommand(OtvoriPoKontimaIRadnimNalozima);
            PoKontimaCommand = new RelayCommand(OtvoriPoKontima);
            ZaSvaSkladistaCommand = new RelayCommand(OtvoriZaSvaSkladista);
            ZaSkladisteStanjeZalihaCommand = new RelayCommand(OtvoriZaSkladisteStanjeZaliha);
            ZaMaterijalUSkladistuCommand = new RelayCommand(OtvoriZaMaterijalUSkladistu);
            ZaSkladisteMedjuskladisniceCommand = new RelayCommand(OtvoriZaSkladisteMedjuskladisnice);
            MedjuskladisniceSveCommand = new RelayCommand(OtvoriMedjuskladisniceSve);
            MedjuskladisniceJednaCommand = new RelayCommand(OtvoriMedjuskladisniceJedna);
            ZaJednuKalkulacijuPregledIzlazaCommand = new RelayCommand(OtvoriZaJednuKalkulacijuPregledIzlaza);
            ArhivaCommand = new RelayCommand(Arhiva);

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
            OdabraniVM = new NaloziZaduzenjeProdavniceViewModel(this);
        } 
        
        public void OtvoriZaJednuKalkulacijuPregledUlaza()
        {
            OdabraniVM = new PregledUlazaZaJednuKalkulacijuViewModel(this);
        }

        public void OtvoriZaSveKalkulacijePregledUlaza()
        {
            OdabraniVM = new PregledUlazaZaSveKalkulacijeViewModel(this);
        }
        
        public void OtvoriPoKontimaIKalkulacijama()
        {
            OdabraniVM = new PregledUlazaPoKontimaiKalkulacijamaViewModel(this);
        }
        
        public void OtvoriZaSveKalkulacijePregledIzlaza()
        {
            OdabraniVM = new PregledIzlazaZaSveKalkulacijeViewModel(this);
        }
        
        public void OtvoriPoKontimaINalozimaZaSvaSkladista()
        {
            OdabraniVM = new PregledTroskovaPoKontimaINalozimaZaSvaSkladistaViewModel(this);
        }
        public void OtvoriPoKontimaIRadnimNalozima()
        {
            OdabraniVM = new PregledTroskovaPoKontimaIRadnimNalozimaViewModel(this);
        }
        public void OtvoriPoKontima()
        {
            OdabraniVM = new PregledTroskovaPoKontimaViewModel(this);
        }
        public void OtvoriZaSvaSkladista()
        {
            OdabraniVM = new StanjeZalihaZaSvaSkladistaViewModel(this);
        }
        
        public void OtvoriZaSkladisteStanjeZaliha()
        {
            OdabraniVM = new StanjeZalihaZaSkladisteViewModel(this);
        }
        
        public void OtvoriZaMaterijalUSkladistu()
        {
            OdabraniVM = new StanjeZalihaZaMaterijalUSkladistuViewModel(this);
        }
        
        public void OtvoriZaSkladisteMedjuskladisnice()
        {
            OdabraniVM = new MedjuskladisniceZaSkladisteViewModel(this);
        }
        
        public void OtvoriMedjuskladisniceSve()
        {
            OdabraniVM = new MedjuskladisniceSveViewModel(this);
        }

        public void OtvoriMedjuskladisniceJedna()
        {
            OdabraniVM = new MedjuskladisniceJednaViewModel(this);
        }
            
        public void OtvoriZaJednuKalkulacijuPregledIzlaza()
        {
            OdabraniVM = new PregledIzlazaZaJednuKalkulacijuViewModel(this);
        }

        public void Arhiva()
        {
            ////string conn = "Server= 192.168.1.213;Trusted_Connection=False;" +
            ////             "MultipleActiveResultSets=true;User Id=sa;Password=Lutrija1;";

            //string connectionString = "Data Source=192.168.1.213;Initial Catalog=materijalno_knjigovodstvo;Integrated Security=True";

            string connectionString = "Server=192.168.1.213;Database=materijalno_knjigovodstvo;Trusted_Connection=True;";

            string serverBackupPath = @"C:\Program Files\Microsoft SQL Server\MSSQL15.LUTRIJASQL\MSSQL\Backup\materijalno_knjigovodstvo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";

            if (Directory.Exists(Path.GetDirectoryName(serverBackupPath)))
            {
                string backupQuery = $@"
            BACKUP DATABASE [materijalno_knjigovodstvo]
            TO DISK = N'{serverBackupPath}'
            WITH FORMAT, INIT, 
            NAME = 'Full Backup of materijalno_knjigovodstvo';";

                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(backupQuery, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Optional: Success message
                    System.Windows.MessageBox.Show("Uspješno ste obavili arhiviranje!", "Potvrda", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    // Handle error
                    System.Windows.MessageBox.Show(ex.Message, "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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