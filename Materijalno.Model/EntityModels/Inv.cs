using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Materijalno.Model.EntityModels
{
    public partial class Inv
    {
        public bool IsDraft { get; set; } = true;
        public bool IsArchived { get; set; }
        public DateTime? InventoryDate { get; set; }
        public Guid? InventorySessionId { get; set; }
        public int? FoxRecno { get; set; }
        public byte[] RowVersion { get; set; }

        public int Id { get; set; }
        public int? Kljnaz { get; set; }
        public int? Ident { get; set; }
        public DateTime? Datun { get; set; } = DateTime.Today;
        public string Analst { get; set; }
        public string Brdok { get; set; }
        public string Brnar { get; set; }
        public DateTime? Datnar { get; set; } = DateTime.Today;
        public string Brfak { get; set; }
        public decimal? Kolic { get; set; }
        public decimal Nc { get; set; }
        public decimal Vrijed { get; set; }
        public int? Redbr { get; set; }
        public string Status { get; set; }
        public int? Konto1 { get; set; }
        public string Konto2 { get; set; }
        public string Fcj { get; set; }
        public decimal? Troskovi { get; set; }
        public string Cartro { get; set; }
        public string Zavtro { get; set; }
        public string Ppp { get; set; }
        public string Tarifa { get; set; }
        public string Fvrijed { get; set; }
        public decimal? Porppp { get; set; }
        public int? Kontosklad { get; set; }
        public int? Kontosklad1 { get; set; }
        public decimal? Trospe { get; set; }
        public int? Ourst { get; set; }
        public int? Mjtst { get; set; }
        
    }
}
