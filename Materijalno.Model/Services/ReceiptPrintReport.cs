using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Materijalno.Model.EntityModels;
using Microsoft.EntityFrameworkCore;

namespace Materijalno.Model.Services
{
    /// <summary>Data for the Fox ULazi receipt printout; reading never changes a receipt.</summary>
    public sealed class ReceiptPrintReport
    {
        public DataTable Rows { get; private set; }
        public string Organization { get; private set; }
        public string Number { get; private set; }
        public string DocumentNumber { get; private set; }
        public string DocumentDate { get; private set; }
        public string SupplierCode { get; private set; }
        public string SupplierName { get; private set; }
        public string Account { get; private set; }
        public string AccountName { get; private set; }

        public static ReceiptPrintReport Load(int selectedId, string supplierName)
        {
            using (var db = new materijalno_knjigovodstvoContext())
            {
                var selected = db.Mat.AsNoTracking().SingleOrDefault(m => m.Id == selectedId && m.Status == "U");
                if (selected == null) throw new InvalidOperationException("Prvo odaberite snimljenu ulaznu kalkulaciju.");
                // Fox: FOR brfak=m.brfak AND datun=m.datun AND datnar=m.datnar
                //       AND analst=m.analst AND brdok=m.brdok (within the U filter).
                // Keep the new application's warehouse and book-year boundaries as well.
                string number = (selected.Brfak ?? "").Trim();
                string supplier = (selected.Analst ?? "").Trim();
                string document = (selected.Brdok ?? "").Trim();
                var rows = db.Mat.AsNoTracking().Where(m => m.Status == "U" && m.BookYear == selected.BookYear &&
                    m.Kljnaz == selected.Kljnaz && (m.Brfak ?? "").Trim() == number &&
                    m.Datun == selected.Datun && m.Datnar == selected.Datnar &&
                    (m.Analst ?? "").Trim() == supplier && (m.Brdok ?? "").Trim() == document).ToList();
                var materials = db.TabelaMaterijala.AsNoTracking().ToList();
                var warehouse = db.SifarnikSkladista.AsNoTracking().FirstOrDefault(w => w.Kljnaz == selected.Kljnaz);
                var first = rows.OrderBy(m => m.FoxRecno ?? int.MaxValue).ThenBy(m => m.Id).First();
                var account = db.SifarnikKonta.AsNoTracking().FirstOrDefault(a => a.Sifkonta == first.Konto1);
                return Build(rows, materials, warehouse?.NazivOrg, supplierName, account?.Nazkont);
            }
        }

        public static ReceiptPrintReport Build(IEnumerable<Mat> source, IEnumerable<TabelaMaterijala> materials,
            string organization, string supplierName, string accountName)
        {
            // Imported rows retain Fox's physical record order. New rows use their SQL order.
            var rows = source.Where(m => !m.IsDraft && !m.IsArchived && m.Status == "U")
                .OrderBy(m => m.FoxRecno ?? int.MaxValue).ThenBy(m => m.Id).ToList();
            if (rows.Count == 0) throw new InvalidOperationException("Nema snimljenih stavki za štampanje.");
            var first = rows[0];
            var master = materials.GroupBy(m => m.Ident).ToDictionary(g => g.Key, g => g.First());
            var table = new DataTable("Ulaz");
            foreach (string name in new[] { "Id", "FoxRecno", "Redbr", "Ident" }) table.Columns.Add(name, typeof(int));
            foreach (string name in new[] { "Nazmat", "Jedm", "Tarifa" }) table.Columns.Add(name);
            foreach (string name in new[] { "Kolic", "Fcj", "Fvrijed", "Troskovi", "Cartro", "Trospe", "Zavtro", "TotalCosts", "Ppp", "Porppp", "Nc", "Vrijed" })
                table.Columns.Add(name, typeof(decimal));
            foreach (var m in rows)
            {
                TabelaMaterijala material;
                master.TryGetValue(m.Ident ?? 0, out material);
                decimal customs = LedgerMath.ParseAmount(m.Cartro), other = LedgerMath.ParseAmount(m.Zavtro), freight = m.Trospe ?? 0;
                table.Rows.Add(m.Id, (object)m.FoxRecno ?? DBNull.Value, m.Redbr ?? 0, m.Ident ?? 0,
                    material?.Nazmat ?? "", material?.Jedm ?? "", material?.Siftar?.ToString() ?? "",
                    m.Kolic ?? 0, LedgerMath.ParseAmount(m.Fcj), LedgerMath.ParseAmount(m.Fvrijed), m.Troskovi ?? 0,
                    customs, freight, other, customs + other + freight, LedgerMath.ParseAmount(m.Ppp), m.Porppp ?? 0, m.Nc ?? 0, m.Vrijed ?? 0);
            }
            return new ReceiptPrintReport { Rows = table, Organization = (organization ?? "").Trim(), Number = (first.Brfak ?? "").Trim(),
                DocumentNumber = (first.Brdok ?? "").Trim(), DocumentDate = first.Datnar?.ToString("dd.MM.yyyy") ?? "",
                SupplierCode = (first.Analst ?? "").Trim(), SupplierName = supplierName ?? "",
                Account = first.Konto1?.ToString() ?? "", AccountName = accountName ?? "" };
        }
    }
}
