using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Materijalno.Model.EntityModels;
using Microsoft.EntityFrameworkCore;

namespace Materijalno.Model.Services
{
    public static class StockFileExport
    {
        public const string DefaultPath = @"C:\zalihe\zalihesve.TXT";
        private static readonly CultureInfo Numbers = CultureInfo.GetCultureInfo("de-DE");

        public static byte[] Create(DateTime date, DateTime generated)
        {
            using (var db = new materijalno_knjigovodstvoContext())
            {
                // Original Fajl button: kljnaz<>'' AND datun<=adat2 AND redbr<>0.
                var movements = ReportQueries.AtDate(db, date).Where(m => m.Kljnaz > 0 && m.Ident > 0 &&
                    m.Redbr.HasValue && m.Redbr.Value != 0).ToList();
                var codes = new Dictionary<int, string>();
                using (var connection = new SqlConnection(DatabaseConfiguration.MaterialConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand("SELECT IDENT FROM fox_raw.mater WHERE fox_deleted=0", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string code = reader.GetString(0).Trim(); int ident;
                            if (int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out ident)) codes[ident] = code;
                        }
                    }
                }
                return Build(movements, db.TabelaMaterijala.AsNoTracking().ToList(),
                    db.SifarnikSkladista.AsNoTracking().ToList(), codes, date, generated);
            }
        }

        public static byte[] Build(IEnumerable<Mat> movements, IEnumerable<TabelaMaterijala> materials,
            IEnumerable<SifarnikSkladista> warehouses, IDictionary<int, string> sourceCodes, DateTime date, DateTime generated)
        {
            var master = materials.ToDictionary(m => m.Ident);
            var warehouseNames = warehouses.ToDictionary(w => w.Kljnaz, w => w.NazivOrg ?? "");
            Func<int, string> code = ident => sourceCodes.ContainsKey(ident) ? sourceCodes[ident] : ident.ToString(CultureInfo.InvariantCulture);
            var groups = movements.Where(m => !m.IsDraft && m.BookYear == date.Year && m.Datun.HasValue &&
                    m.Datun.Value.Date <= date.Date && m.Kljnaz > 0 && m.Ident > 0 && m.Redbr.GetValueOrDefault() != 0)
                .GroupBy(m => new { Warehouse = m.Kljnaz.Value, Ident = m.Ident.Value })
                .OrderBy(g => g.Key.Warehouse).ThenBy(g => code(g.Key.Ident), StringComparer.Ordinal).ToList();
            if (groups.Count == 0) throw new InvalidOperationException("Nema podataka o zalihama za odabrani datum.");

            var lines = new List<string>();
            var line = new Line(); line.Field(68, 9, generated.ToString("dd.MM.yyyy")); lines.Add(line.Text);
            line = new Line(); line.Label(20, "Zalihe materijala svih skladišta do:"); line.Field(48, 11, date.ToString("dd.MM.yyyy")); lines.Add(line.Text);
            lines.Add(Header());
            int page = 1, used = 0, capacity = 25;
            Action<string> append = text =>
            {
                if (used == capacity) { lines.Add(Footer(page++)); lines.Add(Header()); used = 0; capacity = 28; }
                lines.Add(text); used++;
            };
            foreach (var warehouse in groups.GroupBy(g => g.Key.Warehouse))
            {
                foreach (var group in warehouse)
                {
                    TabelaMaterijala material; master.TryGetValue(group.Key.Ident, out material);
                    line = new Line(); line.Field(2, 9, code(group.Key.Ident)); line.Field(12, 23, FirstLine(material?.Nazmat ?? "", 23));
                    line.Field(35, 5, material?.Jedm ?? "");
                    line.Field(61, 16, group.Sum(m => m.Vrijed ?? 0).ToString("N2", Numbers), true);
                    line.Field(41, 10, group.Sum(m => m.Kolic ?? 0).ToString("N2", Numbers), true);
                    append(line.Text);
                }
                string name; warehouseNames.TryGetValue(warehouse.Key, out name);
                // Preserve the original ASCII report's drawing order and fixed field widths.
                line = new Line(); line.Field(62, 15, warehouse.Sum(g => g.Sum(m => m.Vrijed ?? 0)).ToString("N2", Numbers), true);
                line.Label(43, "Ukupna vrijednost za skladište:"); line.Field(2, 36, FirstLine(name ?? "", 36)); append(line.Text);
            }
            line = new Line(); line.Field(62, 15, groups.Sum(g => g.Sum(m => m.Vrijed ?? 0)).ToString("N2", Numbers), true);
            line.Label(49, "Ukupna vrijednost:"); append(line.Text); lines.Add(Footer(page));
            return Encoding.GetEncoding(1250, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                .GetBytes(string.Join("\r\n", lines) + "\r\n");
        }

        private static string Header()
        {
            var line = new Line(); line.Label(47, "Količina"); line.Label(70, "Vrijednost"); line.Label(35, "Jed.mjere");
            line.Label(2, "Šifra mater."); line.Label(12, "Naziv materijala"); return line.Text;
        }

        private static string FirstLine(string text, int width)
        {
            int newline = text.IndexOfAny(new[] { '\r', '\n' });
            if (newline >= 0) text = text.Substring(0, newline);
            if (text.Length <= width) return text;
            if (text[width] == ' ') return text.Substring(0, width).TrimEnd();
            int space = text.LastIndexOf(' ', width - 1, width);
            return text.Substring(0, space > 0 ? space : width).TrimEnd();
        }

        private static string Footer(int page)
        {
            var line = new Line(); line.Field(33, 12, "Strana: " + page.ToString(CultureInfo.InvariantCulture), true); return line.Text;
        }

        private sealed class Line
        {
            private readonly char[] cells = Enumerable.Repeat(' ', 100).ToArray();
            public void Label(int start, string text) { for (int i = 0; i < text.Length && start + i < cells.Length; i++) cells[start + i] = text[i]; }
            public void Field(int start, int width, string text, bool right = false)
            {
                text = (text ?? "").Replace("\r", " ").Replace("\n", " ");
                text = right ? text.PadLeft(width) : text.PadRight(width);
                Label(start, text.Substring(0, width));
            }
            public string Text => new string(cells).TrimEnd();
        }

        public static void Save(byte[] content, string path)
        {
            if (content == null || content.Length == 0) throw new InvalidOperationException("Nema sadržaja za snimanje.");
            path = Path.GetFullPath(path); string folder = Path.GetDirectoryName(path); Directory.CreateDirectory(folder);
            string temporary = Path.Combine(folder, ".zalihesve_" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(temporary, content);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static void Export(DateTime date) { Save(Create(date.Date, DateTime.Today), DefaultPath); }
    }
}
