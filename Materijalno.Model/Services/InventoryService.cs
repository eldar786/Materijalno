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
            if (input == null || string.IsNullOrWhiteSpace(input.Ident) || input.Kljnaz.GetValueOrDefault() <= 0)
                throw new InvalidOperationException("Odaberite materijal i skladište inventure.");
            // uninv.scx sums the current MAT ledger by IDENT/KLJNAZ, without a
            // date/year condition. INV can already be dated 01.01. of next year.
            var balance = MaterialPostingService.Balance(db, MaterialIdentifier.Normalize(input.Ident), input.Kljnaz.Value);
            return balance.Quantity > 0 && balance.Value > 0 ? balance.UnitCost : 0;
        }

        public Inv Save(Inv input)
        {
            if (input == null) throw new InvalidOperationException("Nema inventurne stavke.");
            input.Ident = MaterialIdentifier.Normalize(input.Ident);
            if (!MaterialIdentifier.IsValid(input.Ident)) throw new InvalidOperationException("Unesite ispravnu šifru materijala.");
            using (var db = _create())
            using (db.AllowPosting())
            {
                if (db.Database.CurrentTransaction != null) return Save(db, input);
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var saved = Save(db, input);
                    transaction.Commit();
                    return saved;
                }
            }
        }

        private static Inv Save(materijalno_knjigovodstvoContext db, Inv input)
        {
                MaterialPostingService.LockLedger(db);
                DateTime day = (input.InventoryDate ?? input.Datun ?? DateTime.Today).Date;
                // Fox permits inventory entered in the following calendar year;
                // its date need not equal the year of the still-active MAT ledger.
                // uninv.scx rejects EMPTY(m.kolic), including numeric zero.
                if (!input.Kolic.HasValue || input.Kolic <= 0) throw new InvalidOperationException("Unesite količinu veću od nule.");
                if (!db.TabelaMaterijala.Any(m => m.Ident == input.Ident) || !db.SifarnikSkladista.Any(w => w.Kljnaz == input.Kljnaz))
                    throw new InvalidOperationException("Materijal i skladište moraju postojati.");
                if (!input.Kljnaz.HasValue) throw new InvalidOperationException("Odaberite skladište inventure.");
                Guid session = SessionId(input.Kljnaz.Value, day);
                if (db.Inv.Any(i => i.Id != input.Id && i.InventorySessionId == session && i.Ident == input.Ident))
                    throw new InvalidOperationException("Materijal je već unesen u taj inventurni popis. Koristite izmjenu postojeće stavke.");
                if (input.Nc < 0) throw new InvalidOperationException("Nabavna cijena ne može biti negativna.");
                // Fox permits a confirmed manual NC; saving must not replace it.
                input.Nc = input.Nc > 0 ? LedgerMath.Price(input.Nc) : Quote(db, input);
                if (input.Kolic > 0 && input.Nc == 0)
                    throw new InvalidOperationException("Nije moguće automatski odrediti NC. Unesite potvrđenu prosječnu nabavnu cijenu.");
                input.Vrijed = LedgerMath.Money(input.Kolic.Value * input.Nc);
                input.Datun = day; input.Datnar = day; input.InventoryDate = day; input.InventorySessionId = session;
                input.Status = "P"; input.IsDraft = false; input.IsArchived = false;
                input.Kontosklad = MaterialPostingService.Account(db, input.Ident, input.Kljnaz.Value);
                var material = db.TabelaMaterijala.Single(m => m.Ident == input.Ident);
                input.Konto1 = material.Konto1 ?? 0;
                input.Konto2 = material.Konto2 ?? "";
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
                db.SaveChanges(); return stored;
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
