using System;
using System.Collections.Generic;
using System.ComponentModel;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Materijalno.Model.EntityModels
{
    public partial class SifarnikSkladista : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int? Kljnaz { get; set; }
        public string NazivOrg { get; set; }
        public int? ZiroRacun { get; set; }
        public int? PozBr { get; set; }
        public int? DevizniRacun { get; set; }
        public int? PozBrD { get; set; }
        public string Opstina { get; set; }
        public string NazivSdk { get; set; }
        public string MjestoAdresa { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
