using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Materijalno.Model.EntityModels;
using Microsoft.EntityFrameworkCore;

namespace Materijalno.Model.Services
{
    public static class FoxCostReports
    {
        public static DataTable Create(DateTime from, DateTime to, bool byDocument)
        {
            using (var db = new materijalno_knjigovodstvoContext())
                return Build(ReportQueries.Period(db, from, to).Where(m => m.Status == "I").ToList(),
                    db.TabelaMaterijala.AsNoTracking().ToList(), db.SifarnikKonta.AsNoTracking().ToList(),
                    db.SifarnikSkladista.AsNoTracking().ToList(), from, to, byDocument);
        }

        public static DataTable Build(IEnumerable<Mat> movements, IEnumerable<TabelaMaterijala> materials,
            IEnumerable<SifarnikKonta> accounts, IEnumerable<SifarnikSkladista> warehouses,
            DateTime from, DateTime to, bool byDocument)
        {
            var master = materials.ToDictionary(m => m.Ident);
            var accountNames = accounts.ToDictionary(a => a.Sifkonta, a => a.Nazkont);
            var warehouseNames = warehouses.ToDictionary(w => w.Kljnaz, w => w.NazivOrg);
            var table = new DataTable(byDocument ? "PregledTroskovaPoKontimaINalozimaZaSvaSkladista" : "PregledTroskovaPoKontima");
            foreach (var name in new[] {"Vrijed", "UkupnoVrijed"}) table.Columns.Add(name, typeof(decimal));
            foreach (var name in new[] {"Datun", "Datnar", "DatumDokumenta"}) table.Columns.Add(name, typeof(DateTime));
            foreach (var name in new[] {"NazivOrg", "Nazkont", "Brfak"}) table.Columns.Add(name);
            table.Columns.Add("Kljnaz", typeof(int)); table.Columns.Add("Konto1", typeof(int));
            table.Columns.Add("FoxOrder", typeof(long));
            var rows = movements.Where(m => !m.IsDraft && m.Status == "I" && m.Datun >= from.Date && m.Datun < to.Date.AddDays(1));
            var groups = rows.GroupBy(m => new {
                // kontona1.frx consolidates all warehouses under each account.
                Kljnaz = byDocument ? (int?)null : m.Kljnaz,
                Account = master.ContainsKey(m.Ident ?? 0) ? master[m.Ident.Value].Konto1 : m.Konto1,
                Document = byDocument ? m.Brfak ?? "" : ""
            }).OrderBy(g => g.Key.Kljnaz).ThenBy(g => g.Key.Account).ThenBy(g => g.Key.Document);
            foreach (var group in groups)
            {
                var last = group.OrderBy(m => m.FoxRecno.HasValue ? 0 : 1).ThenBy(m => m.FoxRecno).ThenBy(m => m.Id).Last();
                var row = table.NewRow();
                // Preserve the original FRX expressions, including correction entries:
                // kontrosv detail = ABS(SUM(vrijed)), totals = SUM(ABS(vrijed));
                // kontona1 detail and totals = SUM(-vrijed).
                row["Vrijed"] = byDocument ? -group.Sum(m => m.Vrijed ?? 0) : Math.Abs(group.Sum(m => m.Vrijed ?? 0));
                row["UkupnoVrijed"] = byDocument ? -group.Sum(m => m.Vrijed ?? 0) : group.Sum(m => Math.Abs(m.Vrijed ?? 0));
                row["Datun"] = from.Date; row["Datnar"] = to.Date;
                row["DatumDokumenta"] = (object)last.Datnar ?? DBNull.Value;
                int warehouse = group.Key.Kljnaz ?? 0, account = group.Key.Account ?? 0;
                row["Kljnaz"] = warehouse; row["Konto1"] = account; row["Brfak"] = group.Key.Document;
                // The original Konto index contains only KONTO1. Its ties keep
                // physical record order, rather than lexical document ordering.
                row["FoxOrder"] = group.Min(m => m.FoxRecno.HasValue ? (long)m.FoxRecno.Value : (long)int.MaxValue + m.Id);
                row["NazivOrg"] = byDocument ? "Sva skladišta" : warehouseNames.ContainsKey(warehouse) ? warehouseNames[warehouse] ?? "" : "Skladište " + warehouse;
                row["Nazkont"] = accountNames.ContainsKey(account) ? accountNames[account] ?? "" : "Konto " + account;
                table.Rows.Add(row);
            }
            return table;
        }
    }
}
