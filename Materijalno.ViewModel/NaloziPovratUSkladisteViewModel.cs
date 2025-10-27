using Materijalno.Model.EntityModels;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Materijalno.ViewModel
{
   public class NaloziPovratUSkladisteViewModel : INotifyPropertyChanged
    {
        #region Fields
        private ApplicationViewModel _avm;
        private GlavniViewModel _gvm;
        string connectionString = "User Id=IBS;Password=IBS11g$;Data Source=192.168.1.224:1521/TESTPDB.LUTRIJABIH.BA;";

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

        public NaloziPovratUSkladisteViewModel(GlavniViewModel gvm)
        {
            FormirajCommand = new RelayCommand(Formiraj);
        }

        public void Formiraj()
        {
            int maxBronsta = 0;
            var dbContext = new materijalno_knjigovodstvoContext();

            //Prikupimo sve medjuskladisnice iz MAT tabele
            MatListZaFormiranje = new ObservableCollection<Mat>(dbContext.Mat
                               .Where(row => row.Status == "V" && row.Brfak == BrojMedjuskladisnice)
                               .OrderBy(row => row.Datun)
                               .ToList());

            if (MatListZaFormiranje.Count == 0)
            {
                System.Windows.MessageBox.Show("Ulaz ne postoji", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

                    using (OracleConnection conn = new OracleConnection(connectionString))
                    {
                        //UNOS INTERFACE_NALOG
                        try
                        {
                            conn.Open(); // Open the database connection

                            string query = "INSERT INTO Interface_nalog (Brojnal, Aktivnost, Godina, Nalog, Datnal, Proknjizen, Datum_Unosa, Radnik, Radnik_Kontrolisao, Pocetno_stanje, Modul, Created_By, Date_Created, Modified_By, Date_Modified, Nalog_U_Gk) " +
                                            "VALUES (:Brojnal, :Aktivnost, :Godina, :Nalog, :Datnal, :Proknjizen, :Datum_Unosa, :Radnik, :Radnik_Kontrolisao, :Pocetno_stanje, :Modul, :Created_By, :Date_Created, :Modified_By, :Date_Modified, :Nalog_U_Gk)";

                            using (OracleCommand cmd = new OracleCommand(query, conn))
                            {
                                // Add parameters to prevent SQL injection
                                cmd.Parameters.Add(":Brojnal", OracleDbType.Int32).Value = 1;
                                cmd.Parameters.Add(":Aktivnost", OracleDbType.Int32).Value = 61;
                                cmd.Parameters.Add(":Godina", OracleDbType.Int32).Value = DBNull.Value;
                                cmd.Parameters.Add(":Nalog", OracleDbType.Int32).Value = 1234567;
                                //cmd.Parameters.Add(":Datnal", OracleDbType.Date).Value = DateTime.ParseExact("13.3.2025.", "d.M.yyyy.", null);
                                cmd.Parameters.Add(":Datnal", OracleDbType.Date).Value = noviNalMat.Datnsta;
                                cmd.Parameters.Add(":Proknjizen", OracleDbType.Varchar2).Value = "D";
                                cmd.Parameters.Add(":Datum_Unosa", OracleDbType.Date).Value = DateTime.ParseExact("13.3.2025.", "d.M.yyyy.", null);
                                cmd.Parameters.Add(":Radnik", OracleDbType.Int32).Value = null;
                                cmd.Parameters.Add(":Radnik_Kontrolisao", OracleDbType.Int32).Value = DBNull.Value;
                                cmd.Parameters.Add(":Pocetno_stanje", OracleDbType.Varchar2).Value = "N";
                                cmd.Parameters.Add(":Modul", OracleDbType.Varchar2).Value = "FIN";
                                cmd.Parameters.Add(":Created_By", OracleDbType.Varchar2).Value = "IBS";
                                cmd.Parameters.Add(":Date_Created", OracleDbType.Date).Value = DateTime.ParseExact("13.3.2025.", "d.M.yyyy.", null);
                                cmd.Parameters.Add(":Modified_By", OracleDbType.Varchar2).Value = "IBS";
                                cmd.Parameters.Add(":Date_Modified", OracleDbType.Date).Value = DateTime.ParseExact("13.3.2025.", "d.M.yyyy.", null);
                                cmd.Parameters.Add(":Nalog_U_Gk", OracleDbType.Int32).Value = 111;

                                int rowsAffected = cmd.ExecuteNonQuery(); // Execute query
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show(ex.Message, "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        //UNOS INTERFACE_STAVKE
                        try
                        {
                            conn.Open(); // Open the database connection

                            string query = "INSERT INTO Interface_stavke (Aktivnost, Datnal, Brojnal, Konto, Brojdok, Datdok, Mjt, Komitent, Duguje, Potrazuje, Autor, Stavka, Proknjizen, Nalog, Id_import_analitika) " +
                                            "VALUES (:Aktivnost, :Datnal, :Brojnal, :Konto, :Brojdok, :Datdok, :Mjt, :Komitent, :Duguje, :Potrazuje, :Autor, :Stavka, :Proknjizen, :Nalog, :Id_import_analitika)";

                            using (OracleCommand cmd = new OracleCommand(query, conn))
                            {
                                // Add parameters to prevent SQL injection
                                cmd.Parameters.Add(":Aktivnost", OracleDbType.Int32).Value = 1;
                                cmd.Parameters.Add(":Datnal", OracleDbType.Date).Value = DateTime.ParseExact("14.3.2025.", "d.M.yyyy.", null);
                                cmd.Parameters.Add(":Brojnal", OracleDbType.Int32).Value = 123;
                                cmd.Parameters.Add(":Konto", OracleDbType.Int32).Value = 1234567;
                                cmd.Parameters.Add(":Brojdok", OracleDbType.Varchar2).Value = "Porez valute + NESTO";
                                cmd.Parameters.Add(":Datdok", OracleDbType.Date).Value = DateTime.ParseExact("14.3.2025.", "d.M.yyyy.", null);
                                cmd.Parameters.Add(":Mjt", OracleDbType.Varchar2).Value = "00000";
                                cmd.Parameters.Add(":Komitent", OracleDbType.Varchar2).Value = "00001";
                                cmd.Parameters.Add(":Duguje", OracleDbType.Int32).Value = 0;
                                cmd.Parameters.Add(":Potrazuje", OracleDbType.Int32).Value = 0;
                                cmd.Parameters.Add(":Autor", OracleDbType.Varchar2).Value = "IBS";
                                cmd.Parameters.Add(":Stavka", OracleDbType.Int32).Value = 1;
                                cmd.Parameters.Add(":Proknjizen", OracleDbType.Varchar2).Value = "N";
                                cmd.Parameters.Add(":Nalog", OracleDbType.Int32).Value = 12345678;
                                cmd.Parameters.Add(":Id_import_analitika", OracleDbType.Int32).Value = 87654321;

                                int rowsAffected = cmd.ExecuteNonQuery(); // Execute query
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show(ex.Message, "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }

                    //#region Citanje "Interface_nalog"
                    //using (OracleConnection conn = new OracleConnection(connectionString))
                    //{

                    //    conn.Open(); // Open the connection

                    //    // Parameterized Query to Read Data
                    //    string query = "SELECT Brojnal, Aktivnost, Godina, Nalog, Datnal, Proknjizen, Datum_Unosa, Radnik, Radnik_Kontrolisao, Pocetno_stanje, Modul, Created_By, Date_Created, Modified_By, Date_Modified, Nalog_U_Gk FROM Interface_nalog";

                    //    using (OracleCommand cmd = new OracleCommand(query, conn))
                    //    {
                    //        using (OracleDataReader reader = cmd.ExecuteReader()) // Execute query
                    //        {
                    //            while (reader.Read()) // Read each row
                    //            {
                    //                int brojnal = reader.GetInt32(0);
                    //                string aktivnost = reader.GetString(1);
                    //                int godina = reader.GetInt32(2);

                    //                //Console.WriteLine($"Brojnal: {brojnal}, Aktivnost: {aktivnost}, Godina: {godina}");
                    //            }
                    //        }
                    //    }
                    //}
                    //#endregion
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
