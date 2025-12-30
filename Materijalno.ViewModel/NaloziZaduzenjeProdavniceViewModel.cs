using Materijalno.Model.EntityModels;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

            // Prikupimo sve Zaduzenje prodavnice (status= "I") iz MAT tabele
            MatListZaFormiranje = new ObservableCollection<Mat>(dbContext.Mat
                .Where(row => row.Status == "I" && row.Brfak == BrojMedjuskladisnice)
                .OrderBy(row => row.Datun)
                .ToList());

            if (MatListZaFormiranje.Count == 0)
            {
                MessageBox.Show("Nema kalkulacije pod tim brojem!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NalMatList = new ObservableCollection<Nalmat>(dbContext.Nalmat.ToList());

            foreach (var item in NalMatList)
            {
                if (item.Brfak == BrojMedjuskladisnice)
                {
                    MessageBox.Show("Formiran je nalog za tu kalkulaciju!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Ako lista nije prazna onda trazi najveci broj u koloni "Bronsta"
            if (NalMatList.Count != 0)
                maxBronsta = NalMatList.Select(n => n.Bronsta).Max();

            int bronsta = maxBronsta + 1;

            // Oracle vrijednosti
            string brojnalOracle = bronsta.ToString().PadLeft(4, '0'); // npr. "0422"
            string aktivnostOracle = "61";                              // isti kao Sifakt

            // Datnal isti za sve stavke
            DateTime datnalOracle = (DateTime)MatListZaFormiranje.First().Datun;

            using (OracleConnection conn = new OracleConnection(connectionString))
            {
                conn.Open();

                // 1) INTERFACE_NALOG upisi samo jednom (MERGE da ne puca ako vec postoji)
                try
                {
                    string qNalog = @"
MERGE INTO Interface_nalog t
USING (SELECT :Brojnal AS Brojnal, :Aktivnost AS Aktivnost, :Datnal AS Datnal FROM dual) s
ON (t.Brojnal = s.Brojnal AND t.Aktivnost = s.Aktivnost AND t.Datnal = s.Datnal)
WHEN NOT MATCHED THEN
  INSERT (Brojnal, Aktivnost, Datnal)
  VALUES (s.Brojnal, s.Aktivnost, s.Datnal)";

                    using (OracleCommand cmdNalog = new OracleCommand(qNalog, conn))
                    {
                        cmdNalog.Parameters.Add(":Brojnal", OracleDbType.Varchar2).Value = brojnalOracle;
                        cmdNalog.Parameters.Add(":Aktivnost", OracleDbType.Varchar2).Value = aktivnostOracle;
                        cmdNalog.Parameters.Add(":Datnal", OracleDbType.Date).Value = datnalOracle;
                        cmdNalog.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 2) Uzmemo pocetni max STAVKA za taj nalog samo jednom
                int nextStavka = 1;
                try
                {
                    string qMaxStavka = @"
SELECT NVL(MAX(STAVKA), 0)
FROM INTERFACE_STAVKE
WHERE AKTIVNOST = :AKTIVNOST
  AND DATNAL    = :DATNAL
  AND BROJNAL   = :BROJNAL";

                    using (OracleCommand cmdMax = new OracleCommand(qMaxStavka, conn))
                    {
                        cmdMax.Parameters.Add(":AKTIVNOST", OracleDbType.Varchar2).Value = aktivnostOracle;
                        cmdMax.Parameters.Add(":DATNAL", OracleDbType.Date).Value = datnalOracle;
                        cmdMax.Parameters.Add(":BROJNAL", OracleDbType.Varchar2).Value = brojnalOracle;

                        int maxStavka = Convert.ToInt32(cmdMax.ExecuteScalar());
                        nextStavka = maxStavka + 1;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 3) Prolaz kroz MAT stavke: upis u SQL Server Nalmat + upis u Oracle Interface_stavke (2 reda po MAT redu)
                foreach (Mat mat in MatListZaFormiranje)
                {
                    decimal iznos = Math.Abs(Convert.ToDecimal(mat.Vrijed));

                    // Ako su Konto1/Kontosklad nullable u modelu, ovo sprijecava exception
                    int konto1Nalmat = Convert.ToInt32(mat.Konto1);
                    int kontoskladNalmat = Convert.ToInt32(mat.Kontosklad);

                    // =====================
                    // NALMAT – ROW 1 (konto1 → duguje)
                    // =====================
                    Nalmat nalmat1 = new Nalmat
                    {
                        Rostav = 1,
                        Datnsta = (DateTime)mat.Datun,
                        Sifakt = 61,
                        Bronsta = bronsta,
                        Sintstav = konto1Nalmat,            // <-- konto1
                        Brdokst = mat.Brdok,
                        Kukistav = 0,
                        Datdokst = (DateTime)mat.Datnar,
                        Devsta = null,
                        Kurs = 0,
                        Datvalst = "  -   -",
                        Ourst = null,
                        Mjtst = null,
                        Analst = null,
                        Dug1st = iznos,
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

                    // =====================
                    // NALMAT – ROW 2 (kontosklad → potrazuje)
                    // =====================
                    Nalmat nalmat2 = new Nalmat
                    {
                        Rostav = 1,
                        Datnsta = (DateTime)mat.Datun,
                        Sifakt = 61,
                        Bronsta = bronsta,
                        Sintstav = kontoskladNalmat,        // <-- kontosklad
                        Brdokst = mat.Brdok + " NCV U MAG",
                        Kukistav = 0,
                        Datdokst = (DateTime)mat.Datnar,
                        Devsta = null,
                        Kurs = 0,
                        Datvalst = "  -   -",
                        Ourst = null,
                        Mjtst = null,
                        Analst = null,
                        Dug1st = 0,
                        Stodug1 = null,
                        Pot1st = iznos,
                        Stopot1 = null,
                        Devdugst = 0,
                        Stodevd = null,
                        Devpotst = 0,
                        Stodevp = null,
                        Bracuna = null,
                        Brfak = mat.Brfak
                    };

                    dbContext.Add(nalmat1);
                    dbContext.Add(nalmat2);
                    dbContext.SaveChanges();

                    // --- Oracle: Interface_stavke (DVA REDA po MAT redu) ---
                    try
                    {
                        const string qStavka = @"
INSERT INTO Interface_stavke
(Aktivnost, Datnal, Brojnal, Konto, Brojdok, Datdok, Mjt, Komitent, Duguje, Potrazuje, Autor, Stavka)
VALUES
(:Aktivnost, :Datnal, :Brojnal, :Konto, :Brojdok, :Datdok, :Mjt, :Komitent, :Duguje, :Potrazuje, :Autor, :Stavka)";

                        // RED 1: KONTO = MAT.konto1, DUGUJE = iznos, POTRAZUJE = 0
                        using (OracleCommand cmd1 = new OracleCommand(qStavka, conn))
                        {
                            cmd1.Parameters.Add(":Aktivnost", OracleDbType.Varchar2).Value = aktivnostOracle;
                            cmd1.Parameters.Add(":Datnal", OracleDbType.Date).Value = datnalOracle;
                            cmd1.Parameters.Add(":Brojnal", OracleDbType.Varchar2).Value = brojnalOracle;

                            cmd1.Parameters.Add(":Konto", OracleDbType.Varchar2).Value = mat.Konto1?.ToString() ?? "0";
                            cmd1.Parameters.Add(":Brojdok", OracleDbType.Varchar2).Value = mat.Brdok;
                            cmd1.Parameters.Add(":Datdok", OracleDbType.Date).Value = (object)mat.Datnar ?? DBNull.Value;

                            cmd1.Parameters.Add(":Mjt", OracleDbType.Varchar2).Value = (object)mat.Mjtst ?? DBNull.Value;
                            cmd1.Parameters.Add(":Komitent", OracleDbType.Varchar2).Value = (object)mat.Analst ?? DBNull.Value;

                            cmd1.Parameters.Add(":Duguje", OracleDbType.Decimal).Value = iznos;
                            cmd1.Parameters.Add(":Potrazuje", OracleDbType.Decimal).Value = 0m;

                            cmd1.Parameters.Add(":Autor", OracleDbType.Varchar2).Value = "MAT";
                            cmd1.Parameters.Add(":Stavka", OracleDbType.Int32).Value = nextStavka++;
                            cmd1.ExecuteNonQuery();
                        }

                        // RED 2: KONTO = MAT.kontosklad, DUGUJE = 0, POTRAZUJE = iznos
                        using (OracleCommand cmd2 = new OracleCommand(qStavka, conn))
                        {
                            cmd2.Parameters.Add(":Aktivnost", OracleDbType.Varchar2).Value = aktivnostOracle;
                            cmd2.Parameters.Add(":Datnal", OracleDbType.Date).Value = datnalOracle;
                            cmd2.Parameters.Add(":Brojnal", OracleDbType.Varchar2).Value = brojnalOracle;

                            cmd2.Parameters.Add(":Konto", OracleDbType.Varchar2).Value = mat.Kontosklad?.ToString() ?? "0";
                            cmd2.Parameters.Add(":Brojdok", OracleDbType.Varchar2).Value = mat.Brdok + " NCV U MAG";
                            cmd2.Parameters.Add(":Datdok", OracleDbType.Date).Value = (object)mat.Datnar ?? DBNull.Value;

                            cmd2.Parameters.Add(":Mjt", OracleDbType.Varchar2).Value = (object)mat.Mjtst ?? DBNull.Value;
                            cmd2.Parameters.Add(":Komitent", OracleDbType.Varchar2).Value = (object)mat.Analst ?? DBNull.Value;

                            cmd2.Parameters.Add(":Duguje", OracleDbType.Decimal).Value = 0m;
                            cmd2.Parameters.Add(":Potrazuje", OracleDbType.Decimal).Value = iznos;

                            cmd2.Parameters.Add(":Autor", OracleDbType.Varchar2).Value = "MAT";
                            cmd2.Parameters.Add(":Stavka", OracleDbType.Int32).Value = nextStavka++;
                            cmd2.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }

            MessageBox.Show("Uspješno formiran nalog", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
