using System;
using System.Data;
using System.Linq;
using Materijalno.Model.EntityModels;

namespace Materijalno.Model.Services
{
    public enum SummaryReportKind { Receipts, ReceiptDocument, ReceiptsByAccount, IssueDocument, CostsByWorkOrder, Costs }

    public sealed class SummaryReport
    {
        public string Title { get; set; }
        public string Period { get; set; }
        public DataTable Rows { get; set; }
        public decimal Total { get; set; }
        public string Footer { get; set; }
    }

    public static class SummaryReports
    {
        public static SummaryReport Create(SummaryReportKind kind, DateTime from, DateTime to, int? warehouse, string number)
        {
            using (var db = new materijalno_knjigovodstvoContext())
            {
                var query = ReportQueries.Period(db, from, to);
                bool receipt = kind == SummaryReportKind.Receipts || kind == SummaryReportKind.ReceiptDocument || kind == SummaryReportKind.ReceiptsByAccount;
                query = receipt ? query.Where(m => m.Status == "U") : query.Where(m => m.Status == "I" || m.Status == "V");
                if (warehouse.HasValue && warehouse > 0) query = query.Where(m => m.Kljnaz == warehouse);
                bool single = kind == SummaryReportKind.ReceiptDocument || kind == SummaryReportKind.IssueDocument;
                if (single)
                {
                    if (string.IsNullOrWhiteSpace(number)) throw new InvalidOperationException("Unesite broj kalkulacije.");
                    number = number.Trim(); query = query.Where(m => m.Brfak == number);
                }
                var rows = query.OrderBy(m => m.Datun).ThenBy(m => m.Brfak).ThenBy(m => m.Redbr).ToList();
                var materials = db.TabelaMaterijala.ToDictionary(m => m.Ident, m => m.Nazmat);
                var accounts = db.SifarnikKonta.ToDictionary(a => a.Sifkonta, a => a.Nazkont);
                var table = new DataTable();
                string title;
                if (kind == SummaryReportKind.ReceiptsByAccount || kind == SummaryReportKind.CostsByWorkOrder || kind == SummaryReportKind.Costs)
                {
                    bool byWork = kind == SummaryReportKind.CostsByWorkOrder;
                    bool byDocument = kind == SummaryReportKind.ReceiptsByAccount;
                    table.Columns.Add("Konto", typeof(int)); table.Columns.Add("Naziv konta");
                    if (byWork) table.Columns.Add("Radni nalog");
                    if (byDocument) table.Columns.Add("Kalkulacija");
                    table.Columns.Add("Vrijednost", typeof(decimal));
                    foreach (var g in rows.GroupBy(m => new { Account = receipt ? m.Kontosklad : m.Konto1, Key = byWork ? m.Brnar : byDocument ? m.Brfak : "" }).OrderBy(g => g.Key.Account).ThenBy(g => g.Key.Key))
                    {
                        int account = g.Key.Account ?? 0; string name;
                        var values = new System.Collections.Generic.List<object> { account, accounts.TryGetValue(account, out name) ? name : "Konto " + account };
                        if (byWork || byDocument) values.Add(g.Key.Key ?? "");
                        values.Add(g.Sum(m => receipt ? m.Vrijed ?? 0 : -(m.Vrijed ?? 0)));
                        table.Rows.Add(values.ToArray());
                    }
                    title = byWork ? "Troškovi po kontima i radnim nalozima" : byDocument ? "Ulazi po kontima i kalkulacijama" : "Rekapitulacija troškova";
                }
                else
                {
                    table.Columns.Add("Datum", typeof(DateTime)); table.Columns.Add("Kalkulacija"); table.Columns.Add("Skladište", typeof(int));
                    table.Columns.Add("Ident", typeof(int)); table.Columns.Add("Materijal"); table.Columns.Add("Količina", typeof(decimal));
                    table.Columns.Add("Cijena", typeof(decimal)); table.Columns.Add("Vrijednost", typeof(decimal));
                    foreach (var m in rows)
                    {
                        string name; decimal sign = receipt ? 1 : -1;
                        table.Rows.Add(m.Datun, m.Brfak, m.Kljnaz, m.Ident, materials.TryGetValue(m.Ident ?? 0, out name) ? name : "", sign * (m.Kolic ?? 0), m.Nc ?? 0, sign * (m.Vrijed ?? 0));
                    }
                    title = receipt ? "Pregled ulaza" : "Pregled izlaza i povrata";
                    if (single) title += " – " + number;
                }
                if (rows.Count == 0) throw new InvalidOperationException("Nema prometa za odabrane uslove.");
                return new SummaryReport { Title = title, Period = from.ToString("dd.MM.yyyy") + " – " + to.ToString("dd.MM.yyyy"), Rows = table,
                    Footer = kind == SummaryReportKind.ReceiptDocument ? "Dokument: " + rows[0].Brdok + "   Komitent: " + rows[0].Analst + Environment.NewLine +
                        "Fakturna vrijednost: " + rows.Sum(m => LedgerMath.ParseAmount(m.Fvrijed)).ToString("N2") + "   PDV: " + rows.Sum(m => m.Porppp ?? 0).ToString("N2") +
                        "   Carina: " + rows.Sum(m => LedgerMath.ParseAmount(m.Cartro)).ToString("N2") + "   Špediter: " + rows.Sum(m => m.Trospe ?? 0).ToString("N2") +
                        "   Ostali troškovi: " + rows.Sum(m => LedgerMath.ParseAmount(m.Zavtro)).ToString("N2") : "",
                    Total = rows.Sum(m => receipt ? m.Vrijed ?? 0 : -(m.Vrijed ?? 0)) };
            }
        }
    }
}
