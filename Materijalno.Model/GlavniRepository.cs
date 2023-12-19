using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Materijalno.Model.EntityModels;

namespace Materijalno.Model
{
    public class GlavniRepository
    {
        private readonly materijalno_knjigovodstvoContext _materijalno_knjigovodstvoContext;

        // IEnumerable - It provides a way to iterate through a collection of objects using the foreach statement.
        // IEnumerable - is read-only and does not provide methods to modify the collection (Add, remove...)
        public IEnumerable<SifarnikSkladista> NapuniSifarnikSkladista()
        {
            return _materijalno_knjigovodstvoContext.SifarnikSkladista.ToList();
        }
    }


}
