using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Materijalno;
using Materijalno.Model;
using System.Windows.Input;


namespace Materijalno.ViewModel
{
    public class ApplicationViewModel : ObservableObject
    {
        public ICommand GlavniCommand { get; set; }
        public ICommand SifarnikSkladistaCommand { get; set; }
        
        private object _odabraniVM;

        public object OdabraniVM
        {

            get { return _odabraniVM; }

            set
            {
                _odabraniVM = value;
                OnPropertyChanged("OdabraniVM");
            }
        }
        public ApplicationViewModel()
        {
            GlavniCommand = new RelayCommand(OtvoriGlavni);
        }
        private void OtvoriGlavni()
        {
            OdabraniVM = new GlavniViewModel(this);
        }
    }
}
