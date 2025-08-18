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
    public class NaloziZaduzenjeProdavniceViewModel : INotifyPropertyChanged
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

        public NaloziZaduzenjeProdavniceViewModel(GlavniViewModel gvm)
        {
            FormirajCommand = new RelayCommand(Formiraj);
        }

        public void Formiraj()
        {
            int maxBronsta = 0;
            var dbContext = new materijalno_knjigovodstvoContext();

            //Prikupimo sve medjuskladisnice iz MAT tabele
            MatListZaFormiranje = new ObservableCollection<Mat>(dbContext.Mat
                               .Where(row => row.Status == "I" && row.Brfak == BrojMedjuskladisnice)
                               .OrderBy(row => row.Datun)
                               .ToList());

            if (MatListZaFormiranje.Count == 0)
            {
                System.Windows.MessageBox.Show("Nema kalkulacije pod tim brojem!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NalMatList = new ObservableCollection<Nalmat>(dbContext.Nalmat.ToList());

            foreach (var item in NalMatList)
            {
                if (item.Brfak == BrojMedjuskladisnice)
                {
                    System.Windows.MessageBox.Show("Formiran je nalog za tu kalkulaciju!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

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
                        Brdokst = mat.Brdok,
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

                            string query = "INSERT INTO Interface_nalog (Brojnal, Aktivnost, Datnal) " +
                                            "VALUES (:Brojnal, :Aktivnost, :Datnal)";

                            using (OracleCommand cmd = new OracleCommand(query, conn))
                            {
                                // Add parameters to prevent SQL injection
                                cmd.Parameters.Add(":Brojnal", OracleDbType.Int32).Value = noviNalMat.Bronsta;
                                cmd.Parameters.Add(":Aktivnost", OracleDbType.Int32).Value = noviNalMat.Sifakt;
                                cmd.Parameters.Add(":Datnal", OracleDbType.Date).Value = noviNalMat.Datnsta;

                                int rowsAffected = cmd.ExecuteNonQuery(); // Execute query
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show(ex.Message, "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        //UNOS INTERFACE_STAVKE sa kolonom "Duguje" (kada je Pot1st == 0)
                        if (noviNalMat.Pot1st == 0)
                        {
                            try
                            {
                                //conn.Open(); // Open the database connection

                                string query = "INSERT INTO Interface_stavke (Aktivnost, Datnal, Brojnal, Konto, Brojdok, Datdok, Mjt, Komitent, Duguje, Potrazuje, Autor, Stavka) " +
                                                "VALUES (:Aktivnost, :Datnal, :Brojnal, :Konto, :Brojdok, :Datdok, :Mjt, :Komitent, :Duguje, :Potrazuje, :Autor, :Stavka)";

                                //Max stavka Interface_Stavke
                                string queryMaxStavka = "SELECT MAX(Stavka) FROM Interface_stavke";
                                OracleCommand cmdMaxStavka = new OracleCommand(queryMaxStavka, conn);
                                object result = cmdMaxStavka.ExecuteScalar();
                                int maxStavka = (result != DBNull.Value && result != null) ? Convert.ToInt32(result) : 0;

                                using (OracleCommand cmd = new OracleCommand(query, conn))
                                {
                                    // Add parameters to prevent SQL injection
                                    cmd.Parameters.Add(":Aktivnost", OracleDbType.Int32).Value = noviNalMat.Sifakt;
                                    cmd.Parameters.Add(":Datnal", OracleDbType.Date).Value = noviNalMat.Datnsta;
                                    cmd.Parameters.Add(":Brojnal", OracleDbType.Int32).Value = noviNalMat.Bronsta;
                                    cmd.Parameters.Add(":Konto", OracleDbType.Int32).Value = noviNalMat.Sintstav;
                                    cmd.Parameters.Add(":Brojdok", OracleDbType.Varchar2).Value = noviNalMat.Brdokst;
                                    cmd.Parameters.Add(":Datdok", OracleDbType.Date).Value = noviNalMat.Datdokst;
                                    cmd.Parameters.Add(":Mjt", OracleDbType.Varchar2).Value = noviNalMat.Mjtst;
                                    cmd.Parameters.Add(":Komitent", OracleDbType.Varchar2).Value = noviNalMat.Analst;
                                    cmd.Parameters.Add(":Duguje", OracleDbType.Int32).Value = noviNalMat.Dug1st;
                                    //Potrazuje = 0
                                    cmd.Parameters.Add(":Potrazuje", OracleDbType.Int32).Value = 0;
                                    cmd.Parameters.Add(":Autor", OracleDbType.Varchar2).Value = "IBS";
                                    //Ako ima vrijednosti, nadji najveci broj i dodaj + 1
                                    if (maxStavka > 0)
                                    {
                                        cmd.Parameters.Add(":Stavka", OracleDbType.Int32).Value = maxStavka + 1;
                                    }
                                    else
                                    {
                                        cmd.Parameters.Add(":Stavka", OracleDbType.Int32).Value = 1;
                                    }


                                    int rowsAffected = cmd.ExecuteNonQuery(); // Execute query
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Windows.MessageBox.Show(ex.Message, "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        //Kada "Pot1st" nije 0
                        else
                        {
                            try
                            {
                                //conn.Open(); // Open the database connection

                                string query = "INSERT INTO Interface_stavke (Aktivnost, Datnal, Brojnal, Konto, Brojdok, Datdok, Mjt, Komitent, Potrazuje, Duguje, Autor, Stavka) " +
                                                "VALUES (:Aktivnost, :Datnal, :Brojnal, :Konto, :Brojdok, :Datdok, :Mjt, :Komitent, :Potrazuje, :Duguje, :Autor, :Stavka)";

                                //Max stavka Interface_Stavke
                                string queryMaxStavka = "SELECT MAX(Stavka) FROM Interface_stavke";
                                OracleCommand cmdMaxStavka = new OracleCommand(queryMaxStavka, conn);
                                object result = cmdMaxStavka.ExecuteScalar();
                                int maxStavka = (result != DBNull.Value && result != null) ? Convert.ToInt32(result) : 0;

                                using (OracleCommand cmd = new OracleCommand(query, conn))
                                {
                                    // Add parameters to prevent SQL injection
                                    cmd.Parameters.Add(":Aktivnost", OracleDbType.Int32).Value = noviNalMat.Sifakt;
                                    cmd.Parameters.Add(":Datnal", OracleDbType.Date).Value = noviNalMat.Datnsta;
                                    cmd.Parameters.Add(":Brojnal", OracleDbType.Int32).Value = noviNalMat.Bronsta;
                                    cmd.Parameters.Add(":Konto", OracleDbType.Int32).Value = noviNalMat.Sintstav;
                                    cmd.Parameters.Add(":Brojdok", OracleDbType.Varchar2).Value = noviNalMat.Brdokst;
                                    cmd.Parameters.Add(":Datdok", OracleDbType.Date).Value = noviNalMat.Datdokst;
                                    cmd.Parameters.Add(":Mjt", OracleDbType.Varchar2).Value = noviNalMat.Mjtst;
                                    cmd.Parameters.Add(":Komitent", OracleDbType.Varchar2).Value = noviNalMat.Analst;
                                    cmd.Parameters.Add(":Potrazuje", OracleDbType.Int32).Value = noviNalMat.Pot1st;
                                    //Duguje = 0
                                    cmd.Parameters.Add(":Duguje", OracleDbType.Int32).Value = 0;
                                    cmd.Parameters.Add(":Autor", OracleDbType.Varchar2).Value = "IBS";
                                    //Ako ima vrijednosti, nadji najveci broj i dodaj + 1
                                    if (maxStavka > 0)
                                    {
                                        cmd.Parameters.Add(":Stavka", OracleDbType.Int32).Value = maxStavka + 1;
                                    }
                                    else
                                    {
                                        cmd.Parameters.Add(":Stavka", OracleDbType.Int32).Value = 1;
                                    }

                                    int rowsAffected = cmd.ExecuteNonQuery(); // Execute query
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Windows.MessageBox.Show(ex.Message, "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            System.Windows.MessageBox.Show("Uspješno formiran nalog", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
