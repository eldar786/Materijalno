using System;
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
        { _create = create ?? (() => new materijalno_knjigovodstvoContext()); _today = today ?? (() => DateTime.Today); }

        public int CloseFromInventory()
        {
            using (var db = _create())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            using (db.AllowPosting())
            {
                MaterialPostingService.LockLedger(db);
                int year = MaterialPostingService.ActiveYear(db), next = year + 1;
                if (_today().Year <= year) throw new InvalidOperationException("Tekuća godina još nije završena.");
                if (db.YearCloses.Any(c => c.BookYear == year)) throw new InvalidOperationException("Godina je već zaključena.");
                if (db.FinancialBatches.Any(b => b.BookYear == year && b.State != "Sent"))
                    throw new InvalidOperationException("Prvo završite prijenos pripremljenih finansijskih naloga za ovu godinu.");
                DateTime closingDate = new DateTime(year, 12, 31);
                var inventory = db.Inv.Where(i => i.InventoryDate == closingDate).ToList();
                if (inventory.Count == 0) throw new InvalidOperationException("Nedostaje završna inventura na 31.12.");
                var balances = db.Mat.GroupBy(m => new { m.Kljnaz, m.Ident })
                    .Select(g => new { g.Key.Kljnaz, g.Key.Ident, Quantity = g.Sum(m => m.Kolic) ?? 0, Value = g.Sum(m => m.Vrijed) ?? 0 }).ToList();
                foreach (var balance in balances.Where(b => b.Quantity != 0 || b.Value != 0))
                    if (!inventory.Any(i => i.Kljnaz == balance.Kljnaz && i.Ident == balance.Ident))
                        throw new InvalidOperationException("Završna inventura nije potpuna za sva skladišta i materijale.");
                if (inventory.Any(i => !i.Kolic.HasValue || i.Kolic < 0 || i.Nc < 0 || i.Vrijed != LedgerMath.Money((i.Kolic ?? 0) * i.Nc)))
                    throw new InvalidOperationException("Završna inventura sadrži neispravan obračun.");
                foreach (var old in db.Mat.IgnoreQueryFilters().Where(m => !m.IsArchived && m.BookYear == year).ToList()) old.IsArchived = true;
                foreach (var old in db.Inv.IgnoreQueryFilters().Where(i => !i.IsArchived).ToList()) old.IsArchived = true;
                foreach (var warehouse in inventory.GroupBy(i => i.Kljnaz))
                {
                    var document = new MaterialDocument { Id = Guid.NewGuid(), Status = "P", BookYear = next, Number = "", Warehouse = warehouse.Key.Value, CreatedAt = DateTime.UtcNow };
                    db.MaterialDocuments.Add(document);
                    int line = 0;
                    foreach (var inv in warehouse.OrderBy(i => i.Ident))
                    {
                        if ((inv.Kolic ?? 0) == 0 && inv.Vrijed == 0) continue;
                        db.Mat.Add(new Mat { Kljnaz = inv.Kljnaz, Ident = inv.Ident, Datun = new DateTime(next, 1, 1), Datnar = new DateTime(next, 1, 1),
                            Kolic = inv.Kolic, Nc = inv.Nc, Vrijed = inv.Vrijed, Status = "P", Redbr = ++line, Brdok = "", Brfak = "",
                            Konto1 = inv.Konto1, Konto2 = inv.Konto2, Kontosklad = inv.Kontosklad, IsDraft = false, IsArchived = false,
                            BookYear = next, DocumentId = document.Id, Medus = "" });
                    }
                }
                db.ApplicationSettings.Single(s => s.KeyName == "ActiveBookYear").Value = next.ToString();
                db.YearCloses.Add(new YearClose { BookYear = year, NextYear = next, CreatedAt = DateTime.UtcNow, Source = "Inventory",
                    Quantity = inventory.Sum(i => i.Kolic ?? 0), Value = inventory.Sum(i => i.Vrijed) });
                db.SaveChanges(); transaction.Commit(); return next;
            }
        }
    }
}
