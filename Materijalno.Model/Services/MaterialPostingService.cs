using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Materijalno.Model.EntityModels;
using Microsoft.EntityFrameworkCore;

namespace Materijalno.Model.Services
{
    public sealed class MaterialPostingService
    {
        private readonly Func<materijalno_knjigovodstvoContext> _create;
        public MaterialPostingService(Func<materijalno_knjigovodstvoContext> create = null)
        { _create = create ?? (() => new materijalno_knjigovodstvoContext()); }

        internal static void LockLedger(materijalno_knjigovodstvoContext db)
        {
            db.Database.ExecuteSqlRaw("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'Materijalno:ledger', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @result<0 THROW 51000,N'Knjiženje je zauzeto. Pokušajte ponovo.',1;");
        }

        public int ActiveYear()
        {
            using (var db = _create()) return ActiveYear(db);
        }

        internal static int ActiveYear(materijalno_knjigovodstvoContext db)
        {
            var value = db.ApplicationSettings.Where(s => s.KeyName == "ActiveBookYear").Select(s => s.Value).Single();
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        public StockBalance Balance(int material, int warehouse, int excludedId = 0, Guid? excludedPair = null, DateTime? at = null)
        {
            using (var db = _create()) return Balance(db, material, warehouse, excludedId, excludedPair, at);
        }

        internal static StockBalance Balance(materijalno_knjigovodstvoContext db, int material, int warehouse,
            int excludedId = 0, Guid? excludedPair = null, DateTime? at = null)
        {
            var query = db.Mat.Where(m => m.Ident == material && m.Kljnaz == warehouse && m.Id != excludedId);
            if (excludedPair.HasValue) query = query.Where(m => m.TransferPairId != excludedPair);
            if (at.HasValue)
            {
                DateTime end = at.Value.Date.AddDays(1);
                query = db.Mat.IgnoreQueryFilters().Where(m => !m.IsDraft && m.BookYear == at.Value.Year &&
                    m.Ident == material && m.Kljnaz == warehouse && m.Id != excludedId && m.Datun < end);
                if (excludedPair.HasValue) query = query.Where(m => m.TransferPairId != excludedPair);
            }
            return new StockBalance { Quantity = query.Sum(m => m.Kolic) ?? 0, Value = query.Sum(m => m.Vrijed) ?? 0 };
        }

        public decimal? ReturnPrice(int material, int warehouse, int excludedId = 0)
        {
            using (var db = _create()) return ReturnPrice(db, material, warehouse, excludedId);
        }

        private static decimal? ReturnPrice(materijalno_knjigovodstvoContext db, int material, int warehouse, int excludedId)
        {
            return db.Mat.Where(m => m.Ident == material && m.Kljnaz == warehouse && m.Id != excludedId && m.Nc != null)
                // izlazi1p.scx opens Mat ordered by its Brfak index, then uses
                // GO BOTTOM inside the material/warehouse filter to price a return.
                .OrderByDescending(m => m.Brfak).ThenByDescending(m => m.Id).Select(m => m.Nc).FirstOrDefault();
        }

        public string ReserveDocumentNumber(string status, int year)
        {
            using (var db = _create())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                LockLedger(db);
                string name = status == "I" || status == "V" ? "PROMET_IV" : "PROMET_" + status;
                string prefix = status == "U" ? "10-" : status == "M" ? "M-" : "61-";
                var numbers = db.Mat.IgnoreQueryFilters().Where(m => m.BookYear == year && m.Brfak.StartsWith(prefix))
                    .Select(m => m.Brfak).ToList();
                int maximum = numbers.Select(n => { int parsed; return int.TryParse(n.Substring(prefix.Length), out parsed) ? parsed : 0; }).DefaultIfEmpty(0).Max();
                int number = NextNumber(db, name, year, maximum);
                db.SaveChanges(); transaction.Commit();
                return prefix + number.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static int NextNumber(materijalno_knjigovodstvoContext db, string name, int year, int importedMaximum)
        {
            var counter = db.DocumentCounters.SingleOrDefault(c => c.Name == name && c.BookYear == year);
            if (counter == null)
            {
                counter = new DocumentCounter { Name = name, BookYear = year, LastNumber = importedMaximum };
                db.DocumentCounters.Add(counter);
            }
            counter.LastNumber = checked(Math.Max(counter.LastNumber, importedMaximum) + 1);
            return counter.LastNumber;
        }

        public Mat Save(Mat input)
        {
            if (input == null) throw new InvalidOperationException("Nema stavke za snimanje.");
            if (input.Status == "M") return SaveTransfer(input);
            using (var db = _create())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            using (db.AllowPosting())
            {
                LockLedger(db);
                ValidateIdentity(db, input);
                var stored = input.Id == 0 ? db.Mat.IgnoreQueryFilters().SingleOrDefault(m => m.LineToken == input.LineToken)
                    : db.Mat.IgnoreQueryFilters().SingleOrDefault(m => m.Id == input.Id);
                if (input.Id != 0 && stored == null) throw new InvalidOperationException("Stavka više ne postoji. Osvježite pregled.");
                bool existed = stored != null && !stored.IsDraft;
                bool changed = existed && (stored.Ident != input.Ident || stored.Kljnaz != input.Kljnaz ||
                    Math.Abs(stored.Kolic ?? 0) != Math.Abs(input.Kolic ?? 0) || stored.Status != input.Status || stored.Nc != input.Nc || stored.Vrijed != input.Vrijed);
                if (existed) EnsureMutable(db, stored, changed);
                decimal quantity = Math.Abs(input.Kolic ?? 0);
                if (quantity <= 0) throw new InvalidOperationException("Količina mora biti veća od nule.");
                var stock = Balance(db, input.Ident.Value, input.Kljnaz.Value, stored?.Id ?? 0);
                decimal price;
                decimal value;
                switch (input.Status)
                {
                    case "U":
                        value = LedgerMath.Money(LedgerMath.ParseAmount(input.Fvrijed) + LedgerMath.ParseAmount(input.Cartro) +
                            LedgerMath.ParseAmount(input.Zavtro) + (input.Porppp ?? 0) + (input.Trospe ?? 0));
                        if (value < 0 || string.IsNullOrWhiteSpace(input.Fvrijed)) throw new InvalidOperationException("Unesite valjanu nabavnu vrijednost.");
                        price = LedgerMath.Price(value / quantity);
                        decimal invoice = LedgerMath.ParseAmount(input.Fvrijed);
                        input.Fcj = LedgerMath.Price(invoice / quantity).ToString("0.#########", CultureInfo.InvariantCulture);
                        input.Troskovi = invoice == 0 ? 0 : LedgerMath.Money((LedgerMath.ParseAmount(input.Cartro) + LedgerMath.ParseAmount(input.Zavtro) + (input.Trospe ?? 0)) * 100 / invoice);
                        break;
                    case "I":
                        value = LedgerMath.IssueValue(stock, quantity);
                        price = existed && !changed ? stored.Nc ?? stock.UnitCost : stock.UnitCost;
                        if (existed && !changed) value = Math.Abs(stored.Vrijed ?? 0);
                        quantity = -quantity; value = -value;
                        break;
                    case "V":
                        var lastPrice = existed && !changed ? stored.Nc : ReturnPrice(db, input.Ident.Value, input.Kljnaz.Value, stored?.Id ?? 0);
                        if (!lastPrice.HasValue) throw new InvalidOperationException("Nema ranije nabavne cijene za povrat u odabrano skladište.");
                        price = lastPrice.Value; value = LedgerMath.Money(quantity * price);
                        break;
                    case "P":
                        if (db.Mat.Any(m => m.Status == "P" && m.Ident == input.Ident && m.Kljnaz == input.Kljnaz && m.Id != input.Id))
                            throw new InvalidOperationException("Početno stanje za taj materijal i skladište već postoji.");
                        if (!input.Nc.HasValue || input.Nc < 0) throw new InvalidOperationException("Unesite poznatu početnu cijenu ili vrijednost; cijena se ne može izmisliti.");
                        price = LedgerMath.Price(input.Nc.Value); value = LedgerMath.Money(quantity * price);
                        input.Datun = new DateTime(ActiveYear(db), 1, 1); input.Datnar = input.Datun;
                        break;
                    default: throw new InvalidOperationException("Nepoznata vrsta prometa.");
                }
                if (stock.Quantity + quantity < 0) throw new InvalidOperationException("Izmjena bi napravila negativnu zalihu.");
                input.Kolic = quantity; input.Nc = price; input.Vrijed = value;
                input.Kontosklad = Account(db, input.Ident.Value, input.Kljnaz.Value);
                input.Konto1 = db.TabelaMaterijala.Single(m => m.Ident == input.Ident).Konto1;
                input.Kljnaz1 = null; input.Kontosklad1 = null; input.Medus = "";
                input.IsDraft = false; input.IsArchived = false; input.BookYear = ActiveYear(db);
                input.DocumentId = Document(db, input.Status, input.BookYear, input.Brfak ?? "", input.Kljnaz.Value).Id;
                if (stored == null) { stored = input; db.Mat.Add(stored); }
                else Apply(db, stored, input);
                db.SaveChanges(); transaction.Commit();
                return stored;
            }
        }

        public static Mat CreateTransferDraft()
        {
            var row = new Mat { Status = "M", Medus = "", Kolic = 0, Nc = 0, Vrijed = 0,
                Analst = "", Brnar = "", Konto2 = "" };
            NormalizeTransferDefaults(row);
            return row;
        }

        private static void NormalizeTransferDefaults(Mat row)
        {
            // Fox SCATTER ... BLANK / APPEND BLANK: numeric blanks mean zero,
            // character blanks mean empty text. OURST/MJTST are integer codes
            // in SQL, where zero represents the unused blank Fox code.
            row.Fcj = string.IsNullOrWhiteSpace(row.Fcj) ? "0" : row.Fcj;
            row.Troskovi = row.Troskovi ?? 0;
            row.Cartro = string.IsNullOrWhiteSpace(row.Cartro) ? "0" : row.Cartro;
            row.Zavtro = string.IsNullOrWhiteSpace(row.Zavtro) ? "0" : row.Zavtro;
            row.Ppp = string.IsNullOrWhiteSpace(row.Ppp) ? "0" : row.Ppp;
            row.Tarifa = row.Tarifa ?? "";
            row.Fvrijed = string.IsNullOrWhiteSpace(row.Fvrijed) ? "0" : row.Fvrijed;
            row.Porppp = row.Porppp ?? 0;
            row.Trospe = row.Trospe ?? 0;
            row.Ourst = row.Ourst ?? 0;
            row.Mjtst = row.Mjtst ?? 0;
        }

        public Mat SaveTransfer(Mat input)
        {
            using (var db = _create())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            using (db.AllowPosting())
            {
                LockLedger(db);
                ValidateIdentity(db, input);
                NormalizeTransferDefaults(input);
                int sourceWarehouse = input.Medus == "1" ? input.Kljnaz1 ?? 0 : input.Kljnaz ?? 0;
                int destinationWarehouse = input.Medus == "1" ? input.Kljnaz ?? 0 : input.Kljnaz1 ?? 0;
                if (sourceWarehouse == destinationWarehouse || !db.SifarnikSkladista.Any(w => w.Kljnaz == destinationWarehouse) || sourceWarehouse <= 0)
                    throw new InvalidOperationException("Odaberite dva različita postojeća skladišta.");
                var selected = input.Id == 0 ? null : db.Mat.IgnoreQueryFilters().SingleOrDefault(m => m.Id == input.Id);
                if (input.Id != 0 && selected == null) throw new InvalidOperationException("Stavka više ne postoji.");
                var pairId = selected?.TransferPairId ?? Guid.NewGuid();
                var pair = db.Mat.IgnoreQueryFilters().Where(m => m.TransferPairId == pairId).ToList();
                var source = pair.SingleOrDefault(m => m.Kolic < 0) ?? (selected?.Medus != "1" ? selected : null);
                var destination = pair.SingleOrDefault(m => m.Medus == "1");
                decimal quantity = Math.Abs(input.Kolic ?? 0);
                if (quantity <= 0) throw new InvalidOperationException("Količina mora biti veća od nule.");
                bool existed = source != null && !source.IsDraft;
                bool changed = existed && (source.Kljnaz != sourceWarehouse || source.Kljnaz1 != destinationWarehouse || source.Ident != input.Ident || Math.Abs(source.Kolic ?? 0) != quantity);
                if (existed) EnsureMutable(db, source, changed);
                var stock = Balance(db, input.Ident.Value, sourceWarehouse, 0, selected?.TransferPairId);
                decimal amount = LedgerMath.IssueValue(stock, quantity);
                decimal price = existed && !changed ? source.Nc ?? stock.UnitCost : stock.UnitCost;
                if (existed && !changed) amount = Math.Abs(source.Vrijed ?? 0);
                if (existed && changed && destination != null)
                {
                    var targetStock = Balance(db, destination.Ident.Value, destination.Kljnaz.Value, 0, pairId);
                    if (targetStock.Quantity < 0) throw new InvalidOperationException("Odredište je već koristilo preneseni materijal.");
                }
                var document = Document(db, "M", ActiveYear(db), input.Brfak ?? "", sourceWarehouse);
                if (source == null) { source = CreateTransferDraft(); db.Mat.Add(source); }
                if (destination == null) { destination = CreateTransferDraft(); db.Mat.Add(destination); }
                var materialAccount = db.TabelaMaterijala.Single(m => m.Ident == input.Ident).Konto1;
                int sourceAccount = Account(db, input.Ident.Value, sourceWarehouse);
                int targetAccount = Account(db, input.Ident.Value, destinationWarehouse);
                foreach (var row in new[] { source, destination })
                {
                    if (!existed)
                    {
                        row.Fcj = input.Fcj; row.Troskovi = input.Troskovi;
                        row.Cartro = input.Cartro; row.Zavtro = input.Zavtro; row.Ppp = input.Ppp;
                        row.Tarifa = input.Tarifa; row.Fvrijed = input.Fvrijed;
                        row.Porppp = input.Porppp; row.Trospe = input.Trospe;
                        row.Ourst = input.Ourst; row.Mjtst = input.Mjtst;
                    }
                    NormalizeTransferDefaults(row);
                    row.Status = "M"; row.Ident = input.Ident; row.Datun = input.Datun?.Date; row.Datnar = input.Datnar?.Date ?? input.Datun?.Date;
                    row.Brdok = input.Brdok; row.Brfak = input.Brfak; row.Redbr = input.Redbr ?? 1;
                    row.Nc = price; row.Konto1 = materialAccount; row.DocumentId = document.Id; row.TransferPairId = pairId;
                    row.BookYear = document.BookYear; row.IsDraft = false; row.IsArchived = false;
                    row.Analst = input.Analst ?? ""; row.Brnar = input.Brnar ?? ""; row.Konto2 = input.Konto2 ?? "";
                }
                source.Kljnaz = sourceWarehouse; source.Kljnaz1 = destinationWarehouse; source.Kolic = -quantity; source.Vrijed = -amount;
                source.Kontosklad = sourceAccount; source.Kontosklad1 = targetAccount; source.Medus = "";
                destination.Kljnaz = destinationWarehouse; destination.Kljnaz1 = sourceWarehouse; destination.Kolic = quantity; destination.Vrijed = amount;
                destination.Kontosklad = targetAccount; destination.Kontosklad1 = sourceAccount; destination.Medus = "1";
                if (selected != null && input.RowVersion != null) db.Entry(selected).Property(m => m.RowVersion).OriginalValue = input.RowVersion;
                db.SaveChanges(); transaction.Commit();
                return destination;
            }
        }

        public void Delete(Mat input)
        {
            if (input == null) return;
            using (var db = _create())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            using (db.AllowPosting())
            {
                LockLedger(db);
                var row = db.Mat.IgnoreQueryFilters().SingleOrDefault(m => m.Id == input.Id);
                if (row == null) return;
                var rows = row.TransferPairId.HasValue ? db.Mat.IgnoreQueryFilters().Where(m => m.TransferPairId == row.TransferPairId).ToList() : new List<Mat> { row };
                foreach (var current in rows)
                {
                    if (!current.IsDraft)
                    {
                        EnsureMutable(db, current, true);
                        var remaining = Balance(db, current.Ident.Value, current.Kljnaz.Value, current.Id);
                        if (remaining.Quantity < 0) throw new InvalidOperationException("Brisanje bi napravilo negativnu zalihu.");
                    }
                    db.Mat.Remove(current);
                }
                if (input.RowVersion != null) db.Entry(row).Property(m => m.RowVersion).OriginalValue = input.RowVersion;
                db.SaveChanges(); transaction.Commit();
            }
        }

        private static void ValidateIdentity(materijalno_knjigovodstvoContext db, Mat input)
        {
            if (!input.Ident.HasValue || !db.TabelaMaterijala.Any(m => m.Ident == input.Ident)) throw new InvalidOperationException("Materijal ne postoji u šifarniku.");
            if (!input.Kljnaz.HasValue || !db.SifarnikSkladista.Any(w => w.Kljnaz == input.Kljnaz)) throw new InvalidOperationException("Skladište ne postoji u šifarniku.");
            if (input.Status != "P" && (!input.Datun.HasValue || input.Datun.Value.Year != ActiveYear(db))) throw new InvalidOperationException("Datum mora pripadati otvorenoj knjigovodstvenoj godini.");
            if (input.Status != "P" && (string.IsNullOrWhiteSpace(input.Brfak) || string.IsNullOrWhiteSpace(input.Brdok)))
                throw new InvalidOperationException("Unesite broj kalkulacije i izvornog dokumenta.");
        }

        internal static int Account(materijalno_knjigovodstvoContext db, int material, int warehouse)
        {
            var account = db.SifarnikMaterijalSkladisteKonto.SingleOrDefault(m => m.Sifmat == material && m.Sifskla == warehouse)?.Sifkonta;
            if (!account.HasValue || !db.SifarnikKonta.Any(c => c.Sifkonta == account))
                throw new InvalidOperationException("Nedostaje valjan konto veze materijal–skladište.");
            return account.Value;
        }

        private static MaterialDocument Document(materijalno_knjigovodstvoContext db, string status, int year, string number, int warehouse)
        {
            var document = db.MaterialDocuments.SingleOrDefault(d => d.Status == status && d.BookYear == year && d.Number == number && d.Warehouse == warehouse);
            if (document == null)
            {
                document = new MaterialDocument { Id = Guid.NewGuid(), Status = status, BookYear = year, Number = number, Warehouse = warehouse, CreatedAt = DateTime.UtcNow };
                db.MaterialDocuments.Add(document);
            }
            return document;
        }

        private static void Apply(materijalno_knjigovodstvoContext db, Mat stored, Mat input)
        {
            var version = input.RowVersion;
            int? sourceRecord = stored.FoxRecno;
            var token = stored.LineToken;
            db.Entry(stored).CurrentValues.SetValues(input);
            stored.FoxRecno = sourceRecord; stored.LineToken = token;
            if (version != null) db.Entry(stored).Property(m => m.RowVersion).OriginalValue = version;
        }

        private static void EnsureMutable(materijalno_knjigovodstvoContext db, Mat row, bool accountingChange)
        {
            if (row.IsArchived) throw new InvalidOperationException("Dokument pripada zaključenoj godini.");
            DateTime begin = new DateTime(row.BookYear, 1, 1), end = begin.AddYears(1);
            if (db.Nalmat.Any(n => n.Brfak == row.Brfak && n.Datnsta >= begin && n.Datnsta < end) ||
                db.FinancialBatches.Any(b => b.SourceDocumentId == row.DocumentId))
                throw new InvalidOperationException("Za dokument postoji finansijski nalog. Knjižena historija se ne prepisuje.");
            if (accountingChange && db.Mat.Any(m => m.Ident == row.Ident && m.Id > row.Id && m.DocumentId != row.DocumentId &&
                (m.Kljnaz == row.Kljnaz || m.Kljnaz == row.Kljnaz1)))
                throw new InvalidOperationException("Postoji kasniji zavisni promet. Ispravka zahtijeva kontrolisano storno, a ne promjenu ranije nabavne osnovice.");
        }
    }
}
