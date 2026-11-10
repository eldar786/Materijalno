using System;
using System.Collections.Generic;
using System.Globalization;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Oracle.ManagedDataAccess.Client;

namespace Materijalno.ViewModel.Services
{
    public sealed class OracleFinancialGateway : IFinancialGateway
    {
        private OracleConnection Open()
        {
            if (!DatabaseConfiguration.OracleExportEnabled)
                throw new InvalidOperationException("Prijenos u Oracle nije uključen.");
            var connection = new OracleConnection(DatabaseConfiguration.OracleConnectionString);
            connection.Open(); return connection;
        }

        private static void Key(OracleCommand command, FinancialBatch batch)
        {
            command.BindByName = true;
            command.Parameters.Add("brojnal", OracleDbType.Varchar2).Value = batch.Number.ToString("D4", CultureInfo.InvariantCulture);
            command.Parameters.Add("aktivnost", OracleDbType.Varchar2).Value = batch.Activity.ToString(CultureInfo.InvariantCulture);
            command.Parameters.Add("datnal", OracleDbType.Date).Value = batch.DocumentDate.Date;
        }

        public RemoteJournalState Inspect(FinancialBatch batch, IList<Nalmat> lines)
        {
            using (var connection = Open())
            {
                int headers;
                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM INTERFACE_NALOG WHERE TRIM(LEADING '0' FROM TRIM(BROJNAL))=TRIM(LEADING '0' FROM :brojnal) AND TRIM(AKTIVNOST)=:aktivnost AND TRUNC(DATNAL)=:datnal", connection))
                { Key(cmd, batch); headers = Convert.ToInt32(cmd.ExecuteScalar()); }
                var actual = new List<Nalmat>();
                using (var cmd = new OracleCommand("SELECT KONTO,BROJDOK,DATDOK,KOMITENT,DUGUJE,POTRAZUJE,MJT,STAVKA FROM INTERFACE_STAVKE WHERE TRIM(LEADING '0' FROM TRIM(BROJNAL))=TRIM(LEADING '0' FROM :brojnal) AND TRIM(AKTIVNOST)=:aktivnost AND TRUNC(DATNAL)=:datnal ORDER BY STAVKA", connection))
                {
                    Key(cmd, batch);
                    using (var reader = cmd.ExecuteReader())
                    {
                        int index = 0;
                        while (reader.Read())
                        {
                            if (reader.IsDBNull(7) || Convert.ToInt32(reader.GetValue(7)) != ++index) return RemoteJournalState.Different;
                            actual.Add(new Nalmat { Sintstav = Convert.ToInt32(reader.GetValue(0)), Brdokst = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Datdokst = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2), Analst = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Mjtst = reader.IsDBNull(6) || string.IsNullOrWhiteSpace(Convert.ToString(reader.GetValue(6))) ? (int?)null : Convert.ToInt32(reader.GetValue(6)),
                                Dug1st = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4), Pot1st = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5) });
                        }
                    }
                }
                if (headers == 0 && actual.Count == 0) return RemoteJournalState.Absent;
                if (headers != 1 || actual.Count != lines.Count) return RemoteJournalState.Different;
                return FinancialPostingService.Hash(actual) == FinancialPostingService.Hash(FinancialPostingService.OracleLines(batch, lines)) ? RemoteJournalState.Matches : RemoteJournalState.Different;
            }
        }

        public void SendAtomically(FinancialBatch batch, IList<Nalmat> lines)
        {
            FinancialPostingService.Validate(lines);
            var mapped = FinancialPostingService.OracleLines(batch, lines);
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction())
            {
                using (var cmd = new OracleCommand("INSERT INTO INTERFACE_NALOG(BROJNAL,AKTIVNOST,DATNAL) VALUES(:brojnal,:aktivnost,:datnal)", connection))
                { cmd.Transaction = transaction; Key(cmd, batch); cmd.ExecuteNonQuery(); }
                int index = 0;
                foreach (var line in mapped)
                {
                    using (var cmd = new OracleCommand("INSERT INTO INTERFACE_STAVKE(BROJNAL,AKTIVNOST,DATNAL,KONTO,BROJDOK,DATDOK,MJT,KOMITENT,DUGUJE,POTRAZUJE,AUTOR,STAVKA) VALUES(:brojnal,:aktivnost,:datnal,:konto,:brojdok,:datdok,:mjt,:komitent,:duguje,:potrazuje,:autor,:stavka)", connection))
                    {
                        cmd.Transaction = transaction; Key(cmd, batch);
                        cmd.Parameters.Add("konto", OracleDbType.Varchar2).Value = line.Sintstav.ToString(CultureInfo.InvariantCulture);
                        cmd.Parameters.Add("brojdok", OracleDbType.Varchar2).Value = line.Brdokst;
                        cmd.Parameters.Add("datdok", OracleDbType.Date).Value = line.Datdokst;
                        cmd.Parameters.Add("mjt", OracleDbType.Varchar2).Value = line.Mjtst.HasValue ? (object)line.Mjtst.Value.ToString("D5", CultureInfo.InvariantCulture) : DBNull.Value;
                        cmd.Parameters.Add("komitent", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(line.Analst) ? (object)DBNull.Value : line.Analst;
                        var debit = cmd.Parameters.Add("duguje", OracleDbType.Decimal); debit.Precision = 13; debit.Scale = 2; debit.Value = line.Dug1st ?? 0;
                        var credit = cmd.Parameters.Add("potrazuje", OracleDbType.Decimal); credit.Precision = 13; credit.Scale = 2; credit.Value = line.Pot1st ?? 0;
                        cmd.Parameters.Add("autor", OracleDbType.Varchar2).Value = "MAT";
                        cmd.Parameters.Add("stavka", OracleDbType.Int32).Value = ++index;
                        cmd.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }
    }
}
