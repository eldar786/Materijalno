using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Materijalno.Model.EntityModels
{
    public partial class Nalmat
    {
        public int? LineNumber { get; set; }
        public Guid? BatchId { get; set; }
        public int? FoxRecno { get; set; }

        public int Id { get; set; }
        public int Rostav { get; set; }
        public DateTime Datnsta { get; set; }
        public int Sifakt { get; set; }
        public int Bronsta { get; set; }
        public int Sintstav { get; set; }
        public string Brdokst { get; set; }
        public int Kukistav { get; set; }
        public DateTime Datdokst { get; set; }
        public string Devsta { get; set; }
        public decimal? Kurs { get; set; }
        public string Datvalst { get; set; }
        public int? Ourst { get; set; }
        public int? Mjtst { get; set; }
        public string Analst { get; set; }
        public decimal? Dug1st { get; set; }
        public decimal? Stodug1 { get; set; }
        public decimal? Pot1st { get; set; }
        public decimal? Stopot1 { get; set; }
        public decimal? Devdugst { get; set; }
        public decimal? Stodevd { get; set; }
        public decimal? Devpotst { get; set; }
        public decimal? Stodevp { get; set; }
        public string Bracuna { get; set; }
        public string Brfak { get; set; }
    }
}
