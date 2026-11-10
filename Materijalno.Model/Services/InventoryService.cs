using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Materijalno.Model.EntityModels;
using Microsoft.EntityFrameworkCore;

namespace Materijalno.Model.Services
{
    public sealed class InventoryService
    {
        private readonly Func<materijalno_knjigovodstvoContext> _create;
        public InventoryService(Func<materijalno_knjigovodstvoContext> create = null)
        { _create = create ?? (() => new materijalno_knjigovodstvoContext()); }

        public decimal Quote(Inv input)
        {
            using (var db = _create()) return Quote(db, input);
        }

        private static decimal Quote(materijalno_knjigovodstvoContext db, Inv input)
        {
            if (input == null || !input.Ident.HasValue || input.Kljnaz.GetValueOrDefault() <= 0)
                throw new InvalidOperationException("Odaberite materijal i skladište inventure.");
            DateTime day = (input.InventoryDate ?? input.Datun ?? DateTime.Today).Date;
            var balance = MaterialPostingService.Balance(db, input.Ident.Value, input.Kljnaz.Value, at: day);
            if (balance.Quantity < 0 || balance.Value < 0)
                throw new InvalidOperationException("Knjigovodstveni saldo na datum inventure nije ispravan.");
            if (balance.Quantity > 0) return balance.UnitCost;
            if (balance.Value != 0) throw new InvalidOperationException("Nulta količina ima nenultu knjigovodstvenu vrijednost.");
            DateTime end = day.AddDays(1);
            var lastPrice = db.Mat.IgnoreQueryFilters().Where(m => !m.IsDraft && m.BookYear == day.Year &&
                m.Ident == input.Ident && m.Kljnaz == input.Kljnaz && m.Datun < end && m.Nc != null)
                .OrderByDescending(m => m.Id).Select(m => m.Nc).FirstOrDefault();
            if (lastPrice.HasValue) return lastPrice.Value;
            if ((input.Kolic ?? 0) == 0) return 0;
            if (input.Nc > 0) return LedgerMath.Price(input.Nc);
            throw new InvalidOperationException("Za inventurni višak bez historije unesite potvrđenu nabavnu cijenu.");
        }

        public Inv Save(Inv input)
        {
            if (input == null) throw new InvalidOperationException("Nema inventurne stavke.");
            using (var db = _create())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            using (db.AllowPosting())
            {
                MaterialPostingService.LockLedger(db);
                DateTime day = (input.InventoryDate ?? input.Datun ?? DateTime.Today).Date;
                if (day.Year != MaterialPostingService.ActiveYear(db)) throw new InvalidOperationException("Inventura mora pripadati otvorenoj godini.");
                if (!input.Kolic.HasValue || input.Kolic < 0) throw new InvalidOperationException("Unesite nenegativnu prebrojanu količinu.");
                if (!db.TabelaMaterijala.Any(m => m.Ident == input.Ident) || !db.SifarnikSkladista.Any(w => w.Kljnaz == input.Kljnaz))
                    throw new InvalidOperationException("Materijal i skladište moraju postojati.");
                if (!input.Kljnaz.HasValue) throw new InvalidOperationException("Odaberite skladište inventure.");
                Guid session = SessionId(input.Kljnaz.Value, day);
                if (db.Inv.Any(i => i.Id != input.Id && i.InventorySessionId == session && i.Ident == input.Ident))
                    throw new InvalidOperationException("Materijal je već unesen u taj inventurni popis. Koristite izmjenu postojeće stavke.");
                input.Nc = Quote(db, input);
                input.Vrijed = LedgerMath.Money(input.Kolic.Value * input.Nc);
                input.Datun = day; input.Datnar = day; input.InventoryDate = day; input.InventorySessionId = session;
                input.Status = "P"; input.IsDraft = false; input.IsArchived = false;
                input.Kontosklad = MaterialPostingService.Account(db, input.Ident.Value, input.Kljnaz.Value);
                input.Konto1 = db.TabelaMaterijala.Single(m => m.Ident == input.Ident).Konto1 ?? 0;
                var stored = input.Id == 0 ? null : db.Inv.IgnoreQueryFilters().SingleOrDefault(i => i.Id == input.Id);
                if (stored == null)
                {
                    if (input.Id != 0) throw new InvalidOperationException("Inventurna stavka više ne postoji.");
                    stored = input; db.Inv.Add(stored);
                }
                else
                {
                    if (stored.IsArchived) throw new InvalidOperationException("Inventura pripada zaključenoj godini.");
                    var version = input.RowVersion; var source = stored.FoxRecno;
                    db.Entry(stored).CurrentValues.SetValues(input); stored.FoxRecno = source;
                    if (version != null) db.Entry(stored).Property(i => i.RowVersion).OriginalValue = version;
                }
                db.SaveChanges(); transaction.Commit(); return stored;
            }
        }

        public void Delete(Inv input)
        {
            if (input == null) return;
            using (var db = _create())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            using (db.AllowPosting())
            {
                MaterialPostingService.LockLedger(db);
                var row = db.Inv.IgnoreQueryFilters().SingleOrDefault(i => i.Id == input.Id);
                if (row == null) return;
                if (row.IsArchived) throw new InvalidOperationException("Inventura pripada zaključenoj godini.");
                if (input.RowVersion != null) db.Entry(row).Property(i => i.RowVersion).OriginalValue = input.RowVersion;
                db.Inv.Remove(row); db.SaveChanges(); transaction.Commit();
            }
        }

        public static Guid SessionId(int warehouse, DateTime day)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(warehouse.ToString(CultureInfo.InvariantCulture) + ":" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                var bytes = new byte[16]; Array.Copy(hash, bytes, 16); return new Guid(bytes);
            }
        }
    }
}
