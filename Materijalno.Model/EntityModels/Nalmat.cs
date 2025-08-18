using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Materijalno.Model.EntityModels
{
    public partial class Nalmat
    {
        public int Id { get; set; }
        public int Rostav { get; set; }
        public DateTime Datnsta { get; set; }
        public int Sifakt { get; set; }
        public int Bronsta { get; set; }
        public int Sintstav { get; set; }
        public string Brdokst { get; set; }
        public int Kukistav { get; set; }
        public DateTime Datdokst { get; set; }
        public int? Devsta { get; set; }
        public int? Kurs { get; set; }
        public string Datvalst { get; set; }
        public int? Ourst { get; set; }
        public int? Mjtst { get; set; }
        public int? Analst { get; set; }
        public decimal? Dug1st { get; set; }
        public int? Stodug1 { get; set; }
        public decimal? Pot1st { get; set; }
        public int? Stopot1 { get; set; }
        public int? Devdugst { get; set; }
        public int? Stodevd { get; set; }
        public int? Devpotst { get; set; }
        public int? Stodevp { get; set; }
        public int? Bracuna { get; set; }
        public string Brfak { get; set; }
    }
}
