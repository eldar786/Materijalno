using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Materijalno.Model.EntityModels;
using Microsoft.EntityFrameworkCore;

namespace Materijalno.Model.Services
{
    public sealed class YearCloseService
    {
        private readonly Func<materijalno_knjigovodstvoContext> _create;
        private readonly Func<DateTime> _today;
        public YearCloseService(Func<materijalno_knjigovodstvoContext> create = null, Func<DateTime> today = null)
        {
            _create = create ?? (() => new materijalno_knjigovodstvoContext());
            _today = today ?? (() => DateTime.Today);
        }

        public int CloseFromInventory()
        {
            using (var db = _create())
            using (db.AllowPosting())
            {
                // An explicitly supplied transaction remains owned by its caller.
                // This also permits full SQL verification followed by rollback.
                if (db.Database.CurrentTransaction != null) return Transfer(db);
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    int year = Transfer(db);
                    transaction.Commit();
                    return year;
                }
            }
        }

        private int Transfer(materijalno_knjigovodstvoContext db)
        {
            MaterialPostingService.LockLedger(db);
            var inventory = db.Inv.AsNoTracking().OrderBy(i => i.Kljnaz).ThenBy(i => i.Ident)
                .ThenBy(i => i.FoxRecno ?? int.MaxValue).ThenBy(i => i.Id).ToList();
            // Fox's COUNT<2 accounts for its empty placeholder record. SQL drafts
            // are not business inventory, so one genuine saved item is sufficient.
            if (inventory.Count == 0) throw new InvalidOperationException("Već je formirano početno stanje ili inventurni popis nije unesen.");

            // forpoc.scx opens MAT on Kljnazi, then GO BOTTOM; SKIP -1.
            // Neither ActiveBookYear nor MAX(datum) is the Fox year check.
            var tail = db.Mat.IgnoreQueryFilters().AsNoTracking().Where(m => !m.IsArchived)
                .OrderByDescending(m => m.Kljnaz).ThenByDescending(m => m.Ident)
                .ThenByDescending(m => m.FoxRecno ?? int.MaxValue).ThenByDescending(m => m.Id).Take(2).ToList();
            var previous = tail.Count > 1 ? tail[1] : tail.FirstOrDefault();
            if (previous?.Datun == null) throw new InvalidOperationException("Nije moguće odrediti godinu postojećeg prometa.");
            int sourceYear = previous.Datun.Value.Year;
            if (sourceYear == _today().Year) throw new InvalidOperationException("Niste završili tekuću godinu.");

            if (inventory.Any(i => !i.Datun.HasValue || !i.Kljnaz.HasValue || string.IsNullOrWhiteSpace(i.Ident) ||
                !i.Kolic.HasValue || i.Kolic < 0 || i.Nc < 0 || i.Vrijed < 0 || i.Status != "P"))
                throw new InvalidOperationException("Inventurni popis sadrži neispravnu stavku. Provjerite datum, skladište, materijal, količinu i cijenu.");

            // APPEND FROM INV keeps the source dates and amounts, including zero
            // quantities. Do not recalculate prices, re-number rows, require 31.12,
            // compare to book balances, or replace dates with calendarYear+1.
            int targetYear = inventory.Max(i => i.Datun.Value.Year);
            // Replace the physical table contents, including previously archived
            // rows. SQL does not need Fox's empty cursor-placeholder records.
            // The inventory has been read into memory; it is deleted only AFTER
            // the new MAT rows have been saved successfully in this transaction.
            db.Database.ExecuteSqlRaw("DELETE FROM dbo.mat;");
            // KOMAT/KORAC exist only as immutable fox_raw snapshots in this app.
            // Preserve export receipts/counters: this operation never touches Oracle.
            var documents = new Dictionary<string, MaterialDocument>(StringComparer.Ordinal);
            foreach (var item in inventory)
            {
                int year = item.Datun.Value.Year, warehouse = item.Kljnaz.Value;
                string number = item.Brfak ?? "", key = year + "|" + warehouse + "|" + number;
                MaterialDocument document;
                if (!documents.TryGetValue(key, out document))
                {
                    document = db.MaterialDocuments.SingleOrDefault(d => d.Status == "P" && d.BookYear == year && d.Warehouse == warehouse && d.Number == number);
                    if (document == null)
                    {
                        document = new MaterialDocument { Id = Guid.NewGuid(), Status = "P", BookYear = year, Warehouse = warehouse, Number = number, CreatedAt = DateTime.UtcNow };
                        db.MaterialDocuments.Add(document);
                    }
                    documents.Add(key, document);
                }
                db.Mat.Add(new Mat {
                    Kljnaz = item.Kljnaz, Ident = item.Ident, Datun = item.Datun, Datnar = item.Datnar,
                    Analst = item.Analst, Brdok = item.Brdok, Brnar = item.Brnar, Brfak = item.Brfak,
                    Kolic = item.Kolic, Nc = item.Nc, Vrijed = item.Vrijed, Redbr = item.Redbr, Status = item.Status,
                    Konto1 = item.Konto1, Konto2 = item.Konto2, Fcj = item.Fcj, Troskovi = item.Troskovi,
                    Cartro = item.Cartro, Zavtro = item.Zavtro, Ppp = item.Ppp, Tarifa = item.Tarifa,
                    Fvrijed = item.Fvrijed, Porppp = item.Porppp, Kontosklad = item.Kontosklad,
                    Kontosklad1 = item.Kontosklad1, Trospe = item.Trospe, Ourst = item.Ourst, Mjtst = item.Mjtst,
                    IsDraft = false, IsArchived = false, BookYear = year, DocumentId = document.Id, Medus = ""
                });
            }
            db.ApplicationSettings.Single(s => s.KeyName == "ActiveBookYear").Value = targetYear.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var audit = db.YearCloses.SingleOrDefault(c => c.BookYear == sourceYear);
            if (audit == null) { audit = new YearClose { BookYear = sourceYear }; db.YearCloses.Add(audit); }
            audit.NextYear = targetYear; audit.CreatedAt = DateTime.UtcNow; audit.Source = "FoxInventory";
            audit.Quantity = inventory.Sum(i => i.Kolic ?? 0); audit.Value = inventory.Sum(i => i.Vrijed);
            db.SaveChanges();
            db.Database.ExecuteSqlRaw("DELETE FROM dbo.inv; DELETE FROM dbo.nalmat;");
            return targetYear;
        }
    }
}
