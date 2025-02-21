using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Materijalno.Model.EntityModels
{
    public partial class SifarnikKonta : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int? Sifkonta { get; set; }
        public string Nazkont { get; set; }
        public int? Stakont { get; set; }
        public int? Stamt { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
