using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Materijalno.Model.EntityModels
{
    public partial class SifarnikMaterijalSkladisteKonto
    {
        public int Id { get; set; }
        public int? Sifmat { get; set; }
        public int? Sifskla { get; set; }
        public int? Sifkonta { get; set; }
    }
}
