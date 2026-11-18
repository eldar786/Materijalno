using System.Collections.Generic;
using System.Linq;
using Materijalno.Model.EntityModels;

namespace Materijalno.Model.Services
{
    public static class InitialStockRules
    {
        // unpoc.scx: filter U, GO TOP, LOCATE material/warehouse under Kljnazi.
        public static decimal? SuggestedPrice(IEnumerable<Mat> movements, string material, int warehouse, int year)
        {
            var first = movements.Where(m => !m.IsDraft && !m.IsArchived && m.BookYear == year &&
                m.Status == "U" && m.Ident == material && m.Kljnaz == warehouse)
                .OrderBy(m => m.FoxRecno ?? int.MaxValue).ThenBy(m => m.Id).FirstOrDefault();
            return first?.Nc.HasValue == true ? (decimal?)LedgerMath.Price(first.Nc.Value) : null;
        }

        public static decimal? LineValue(decimal? quantity, decimal? price)
        {
            return quantity.HasValue && price.HasValue
                ? (decimal?)LedgerMath.Money(quantity.Value * LedgerMath.Price(price.Value)) : null;
        }

        public static decimal WarehouseTotal(IEnumerable<Mat> movements, Mat current)
        {
            if (current?.Kljnaz == null || current.Kljnaz <= 0) return 0;
            var saved = movements.Where(m => !m.IsDraft && !m.IsArchived && m.Status == "P" &&
                m.BookYear == current.BookYear && m.Kljnaz == current.Kljnaz &&
                !ReferenceEquals(m, current) && (current.Id <= 0 || m.Id != current.Id));
            decimal total = saved.Sum(m => m.Vrijed ?? 0);
            // Replace the selected saved row with its current value, or preview a new row once.
            if (current.Status == "P" && !current.IsArchived) total += current.Vrijed ?? 0;
            return total;
        }
    }
}
