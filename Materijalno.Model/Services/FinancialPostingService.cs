using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Materijalno.Model.EntityModels;
using Microsoft.EntityFrameworkCore;

namespace Materijalno.Model.Services
{
    public enum RemoteJournalState { Absent, Matches, Different }

    public interface IFinancialGateway
    {
        RemoteJournalState Inspect(FinancialBatch batch, IList<Nalmat> lines);
        void SendAtomically(FinancialBatch batch, IList<Nalmat> lines);
    }

    public sealed class FinancialPostingService
    {
        private readonly Func<materijalno_knjigovodstvoContext> _create;
        public FinancialPostingService(Func<materijalno_knjigovodstvoContext> create = null)
        { _create = create ?? (() => new materijalno_knjigovodstvoContext()); }

        public FinancialBatch Prepare(string status, string number)
        {
            if (string.IsNullOrWhiteSpace(number)) throw new InvalidOperationException("Unesite broj kalkulacije.");
            if (!new[] {"U","I","V","M"}.Contains(status)) throw new InvalidOperationException("Vrsta prometa nema finansijski nalog.");
            number = number.Trim();
            using (var db = _create())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                MaterialPostingService.LockLedger(db);
                int year = MaterialPostingService.ActiveYear(db);
                var source = db.Mat.Where(m => m.Status == status && m.Brfak == number && m.BookYear == year).OrderBy(m => m.Id).ToList();
                if (source.Count == 0) throw new InvalidOperationException("Kalkulacija ne postoji ili nije završena.");
                if (source.Any(m => !m.Datun.HasValue) || source.Select(m => m.Datun.Value.Date).Distinct().Count() != 1)
                    throw new InvalidOperationException("Kalkulacija mora imati jedan datum knjiženja.");
                var documents = source.Select(m => m.DocumentId).Distinct().ToList();
                if (documents.Count != 1 || !documents[0].HasValue) throw new InvalidOperationException("Dokument nije jednoznačno povezan. Provjerite broj i skladište.");
                Guid document = documents[0].Value;
                var existing = db.FinancialBatches.SingleOrDefault(b => b.SourceDocumentId == document);
                if (existing != null) { transaction.Commit(); return existing; }
                DateTime start = new DateTime(year, 1, 1), end = start.AddYears(1);
                if (db.Nalmat.Any(n => n.Brfak == number && n.Datnsta >= start && n.Datnsta < end))
                    throw new InvalidOperationException("Za tu kalkulaciju već postoji uvezeni finansijski nalog. Nije ponovo formiran niti poslan.");
                int activity = status == "U" ? 10 : 61;
                int maximum = db.Nalmat.Where(n => n.Sifakt == activity && n.Datnsta >= start && n.Datnsta < end).Select(n => (int?)n.Bronsta).Max() ?? 0;
                int next = MaterialPostingService.NextNumber(db, "FIN_" + activity, year, maximum);
                var batch = new FinancialBatch { Id = Guid.NewGuid(), SourceDocumentId = document, SourceStatus = status, Brfak = number,
                    Activity = activity, BookYear = year, Number = next, DocumentDate = source[0].Datun.Value.Date,
                    State = "Pending", ContentHash = "", CreatedAt = DateTime.UtcNow };
                var lines = BuildLines(batch, source);
                batch.ContentHash = Hash(lines);
                db.FinancialBatches.Add(batch);
                db.Nalmat.AddRange(lines);
                db.SaveChanges(); transaction.Commit(); return batch;
            }
        }

        public static List<Nalmat> BuildLines(FinancialBatch batch, IList<Mat> source)
        {
            var lines = new List<Nalmat>();
            foreach (var row in source.OrderBy(m => m.FoxRecno.HasValue ? 0 : 1).ThenBy(m => m.FoxRecno).ThenBy(m => m.Id))
            {
                if (!row.Datun.HasValue || !row.Vrijed.HasValue || !row.Kontosklad.HasValue)
                    throw new InvalidOperationException("Dokument nema potpun datum, procjenu ili konto skladišta.");
                string document = (row.Brdok ?? "").Trim();
                decimal value = row.Vrijed.Value, amount = -value;
                switch (batch.SourceStatus)
                {
                    case "I":
                    case "V":
                        if ((batch.SourceStatus == "I" && amount <= 0) || (batch.SourceStatus == "V" && amount >= 0)) continue;
                        if (!row.Konto1.HasValue) throw new InvalidOperationException("Nedostaje konto troška.");
                        lines.Add(Line(batch,row,row.Konto1.Value,amount,0,document,null,row.Ourst,row.Mjtst));
                        lines.Add(Line(batch,row,row.Kontosklad.Value,0,amount,document+" NCV U MAG",null,0,0));
                        break;
                    case "M":
                        if (row.Kolic >= 0 || amount <= 0) continue;
                        if (!row.TransferPairId.HasValue || source.Count(m => m.TransferPairId == row.TransferPairId) != 2 ||
                            source.Where(m => m.TransferPairId == row.TransferPairId).Sum(m => m.Vrijed) != 0 ||
                            source.Where(m => m.TransferPairId == row.TransferPairId).Sum(m => m.Kolic) != 0)
                            throw new InvalidOperationException("Međuskladišnica nema potpun uravnotežen par.");
                        if (!row.Kontosklad1.HasValue) throw new InvalidOperationException("Nedostaje konto odredišta.");
                        lines.Add(Line(batch,row,row.Kontosklad1.Value,amount,0,document));
                        lines.Add(Line(batch,row,row.Kontosklad.Value,0,amount,document+" NCV U MAG"));
                        break;
                    case "U":
                        string supplier = (row.Analst ?? "").Trim();
                        string description = ("FAKT.DOBAV."+document+"  "+supplier).TrimEnd();
                        if (value != 0)
                        {
                            lines.Add(Line(batch,row,4320000,0,value,description,supplier));
                            lines.Add(Line(batch,row,row.Kontosklad.Value,value,0,"MAGACIN "+batch.Brfak));
                        }
                        Component(lines,batch,row,1000000,LedgerMath.ParseAmount(row.Fvrijed),description);
                        Component(lines,batch,row,1001400,row.Trospe??0,description);
                        Component(lines,batch,row,1002000,LedgerMath.ParseAmount(row.Cartro),description);
                        Component(lines,batch,row,1003000,row.Porppp??0,description);
                        Component(lines,batch,row,1004000,LedgerMath.ParseAmount(row.Zavtro),description);
                        break;
                    default: throw new InvalidOperationException("Vrsta prometa nema finansijski nalog.");
                }
            }
            for (int index=0;index<lines.Count;index++) lines[index].LineNumber=index+1;
            Validate(lines);
            return lines;
        }

        private static void Component(List<Nalmat> lines, FinancialBatch batch, Mat row, int account, decimal amount, string description)
        {
            amount=LedgerMath.Money(amount);
            if (amount==0) return;
            lines.Add(Line(batch,row,account,amount,0,description));
            lines.Add(Line(batch,row,account,0,amount,description));
        }

        private static Nalmat Line(FinancialBatch batch, Mat row, int account, decimal debit, decimal credit,
            string description, string partner=null, int? unit=null, int? costCenter=null)
        {
            // All four original web forms use ADATUN for DATDOKST.
            return new Nalmat {BatchId=batch.Id,Rostav=1,Datnsta=row.Datun.Value.Date,Sifakt=batch.Activity,Bronsta=batch.Number,
                Sintstav=account,Brdokst=description,Kukistav=0,Datdokst=row.Datun.Value.Date,Datvalst="",Analst=partner??"",
                Ourst=unit,Mjtst=costCenter,Dug1st=LedgerMath.Money(debit),Pot1st=LedgerMath.Money(credit),Brfak=batch.Brfak,
                Kurs=0,Devdugst=0,Devpotst=0};
        }

        public static IList<Nalmat> OracleLines(FinancialBatch batch, IList<Nalmat> source)
        {
            Validate(source);
            return source.Select(line => new Nalmat {
                Sintstav=line.Sintstav,Brdokst=(line.Brdokst??"").Trim(),Datdokst=line.Datdokst,
                Analst=batch.SourceStatus=="U"?(line.Analst??"").Trim():"1LT01",
                // nalogiweb / nalogiwebp: SUBSTR(mjtst,2,5), preserving zero padding.
                Mjtst=(batch.SourceStatus=="I"||batch.SourceStatus=="V") && line.Mjtst.HasValue
                    ? (int?)int.Parse(line.Mjtst.Value.ToString("D6",CultureInfo.InvariantCulture).Substring(1,5),CultureInfo.InvariantCulture)
                    : line.Mjtst,
                Dug1st=line.Dug1st,Pot1st=line.Pot1st,LineNumber=line.LineNumber
            }).ToList();
        }

        public static void Validate(IList<Nalmat> lines)
        {
            if (lines == null || lines.Count < 2) throw new InvalidOperationException("Nalog nema obje strane.");
            if (lines.Any(n => n.Sintstav <= 0 || n.Dug1st == null || n.Pot1st == null)) throw new InvalidOperationException("Nalog nema potpuna konta ili iznose.");
            if (lines.Any(n => ((n.Dug1st ?? 0) == 0 && (n.Pot1st ?? 0) == 0) || ((n.Dug1st ?? 0) != 0 && (n.Pot1st ?? 0) != 0)))
                throw new InvalidOperationException("Stavka naloga mora imati tačno jednu stranu knjiženja.");
            if (lines.Sum(n => n.Dug1st ?? 0) != lines.Sum(n => n.Pot1st ?? 0)) throw new InvalidOperationException("Nalog nije uravnotežen: duguje nije jednako potražuje.");
        }

        public static string Hash(IList<Nalmat> lines)
        {
            string content = string.Join("\n", lines.Select(n => string.Join("|", n.Sintstav.ToString(CultureInfo.InvariantCulture),
                (n.Brdokst ?? "").TrimEnd(), n.Datdokst.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), (n.Analst ?? "").Trim(), n.Ourst?.ToString(CultureInfo.InvariantCulture) ?? "", n.Mjtst?.ToString(CultureInfo.InvariantCulture) ?? "",
                (n.Dug1st ?? 0).ToString("F2", CultureInfo.InvariantCulture), (n.Pot1st ?? 0).ToString("F2", CultureInfo.InvariantCulture))));
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "");
        }

        public FinancialBatch Export(Guid batchId, IFinancialGateway gateway)
        {
            if (gateway == null) throw new ArgumentNullException(nameof(gateway));
            using (var db = _create())
            {
                db.Database.OpenConnection();
                string resource = "Materijalno:export:" + batchId.ToString("N");
                try
                {
                    db.Database.ExecuteSqlRaw("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={0},@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=15000; IF @r<0 THROW 51001,N'Prijenos naloga je već u toku.',1;", resource);
                    var batch = db.FinancialBatches.Single(b => b.Id == batchId);
                    var lines = db.Nalmat.Where(n => n.BatchId == batchId).OrderBy(n => n.LineNumber).ThenBy(n => n.Id).ToList();
                    if (batch.State == "Sent") return batch;
                    Validate(lines);
                    if (Hash(lines) != batch.ContentHash) throw new InvalidOperationException("Sadržaj pripremljenog naloga je promijenjen.");
                    batch.AttemptCount++; batch.State = "Exporting"; batch.Error = null; db.SaveChanges();
                    try
                    {
                        var remote = gateway.Inspect(batch, lines);
                        if (remote == RemoteJournalState.Different) throw new InvalidOperationException("Oracle sadrži drugačiji ili nepotpun nalog pod istim ključem. Prijenos je zaustavljen.");
                        if (remote == RemoteJournalState.Absent) gateway.SendAtomically(batch, lines);
                        batch.State = "Sent"; batch.ExportedAt = DateTime.UtcNow;
                        db.FinancialExportLogs.Add(new FinancialExportLog { BatchId = batch.Id, State = "Sent", CreatedAt = DateTime.UtcNow });
                        db.SaveChanges(); return batch;
                    }
                    catch (Exception exception)
                    {
                        batch.State = "Failed"; batch.Error = exception.Message.Length > 1900 ? exception.Message.Substring(0, 1900) : exception.Message;
                        db.FinancialExportLogs.Add(new FinancialExportLog { BatchId = batch.Id, State = "Failed", Error = batch.Error, CreatedAt = DateTime.UtcNow });
                        db.SaveChanges(); throw;
                    }
                }
                finally
                {
                    try { db.Database.ExecuteSqlRaw("EXEC sys.sp_releaseapplock @Resource={0},@LockOwner='Session';", resource); }
                    finally { db.Database.CloseConnection(); }
                }
            }
        }
    }
}
