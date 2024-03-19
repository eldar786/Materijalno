using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Materijalno.Model.EntityModels
{
    public partial class SifarnikMaterijala
    {
        public int Ident { get; set; }
        public string Nazmat { get; set; }
        public string Jedm { get; set; }
        public int? Siftar { get; set; }
        public int? Konto1 { get; set; }
        public int? Konto2 { get; set; }
    }
}
