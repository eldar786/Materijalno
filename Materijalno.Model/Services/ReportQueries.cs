using System;
using System.Linq;
using Materijalno.Model.EntityModels;
using Microsoft.EntityFrameworkCore;

namespace Materijalno.Model.Services
{
    public sealed class InventoryReportUnavailableException : InvalidOperationException
    {
        public InventoryReportUnavailableException(string message) : base(message) { }
    }

    public static class ReportQueries
    {
        public static IQueryable<Mat> ForYear(materijalno_knjigovodstvoContext db, DateTime date)
        {
            int year = date.Year;
            return db.Mat.IgnoreQueryFilters().AsNoTracking().Where(m => !m.IsDraft && m.BookYear == year);
        }

        public static IQueryable<Mat> AtDate(materijalno_knjigovodstvoContext db, DateTime date)
        {
            DateTime end = date.Date.AddDays(1);
            return ForYear(db, date).Where(m => m.Datun < end);
        }

        public static IQueryable<Mat> Period(materijalno_knjigovodstvoContext db, DateTime from, DateTime to)
        {
            if (to.Date < from.Date) throw new InvalidOperationException("Krajnji datum je prije početnog.");
            if (from.Year != to.Year) throw new InvalidOperationException("Izvještaj odaberite unutar jedne knjigovodstvene godine.");
            DateTime begin = from.Date, end = to.Date.AddDays(1);
            return ForYear(db, from).Where(m => m.Datun >= begin && m.Datun < end);
        }
    }
}
