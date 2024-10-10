using System;
using System.Collections.Generic;
using System.ComponentModel;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Materijalno.Model.EntityModels
{
    public class Mat
    {
        public int Id { get; set; }
        public int? Kljnaz { get; set; }
        public int? Kljnaz1 { get; set; }
        public int? Ident { get; set; }
        public string Datun { get; set; }
        public string Analst { get; set; }
        public string Brdok { get; set; }
        public string Brnar { get; set; }
        public string Datnar { get; set; }
        public string Brfak { get; set; }
        public int? Kolic { get; set; }
        public decimal? Nc { get; set; }
        public decimal? Vrijed { get; set; }
        public int? Redbr { get; set; }
        public string Status { get; set; }
        public int? Konto1 { get; set; }
        public string Konto2 { get; set; }
        public string Fcj { get; set; }
        public decimal? Troskovi { get; set; }
        public string Cartro { get; set; } = "0";
        public string Zavtro { get; set; }
        public string Ppp { get; set; }
        public string Tarifa { get; set; }
        public string Fvrijed { get; set; }
        public decimal? Porppp { get; set; }
        public string Medus { get; set; }
        public int? Kontosklad { get; set; }
        public int? Kontosklad1 { get; set; }
        public decimal? Trospe { get; set; }
        public int? Ourst { get; set; }
        public int? Mjtst { get; set; }
    }
}
