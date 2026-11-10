using System;
using System.Globalization;

namespace Materijalno.Model.Services
{
    public sealed class StockBalance
    {
        public decimal Quantity { get; set; }
        public decimal Value { get; set; }
        public decimal UnitCost => Quantity == 0 ? 0 : LedgerMath.Price(Value / Quantity);
    }

    public static class LedgerMath
    {
        public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        public static decimal Price(decimal value) => Math.Round(value, 9, MidpointRounding.AwayFromZero);

        public static decimal ParseAmount(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            decimal value;
            var culture = text.Contains(",") ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;
            if (!decimal.TryParse(text.Trim(), NumberStyles.Number, culture, out value))
                throw new InvalidOperationException("Neispravan novčani iznos: " + text);
            return value;
        }

        public static decimal IssueValue(StockBalance stock, decimal quantity)
        {
            if (quantity <= 0) throw new InvalidOperationException("Količina mora biti veća od nule.");
            if (stock.Quantity < quantity)
                throw new InvalidOperationException("Količina je veća od raspoložive zalihe (" + stock.Quantity.ToString("0.##") + ").");
            if (stock.Quantity <= 0 || stock.Value < 0)
                throw new InvalidOperationException("Zaliha nema valjanu nabavnu procjenu.");
            return quantity == stock.Quantity ? stock.Value : Money(quantity * stock.UnitCost);
        }
    }
}
