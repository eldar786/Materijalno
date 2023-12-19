using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Materijalno.Model;
using System.Windows.Input;



namespace Materijalno.ViewModel
{
   public class SifarnikSkladistaFormWindowViewModel : ObservableObject
    {
        public ICommand OpenWindowSifarnikSkladistaCommand { get; set; }

        public SifarnikSkladistaFormWindowViewModel()
        {
            OpenWindowSifarnikSkladistaCommand = new RelayCommand(OpenWindowSifarnikSkladista);

        }

        private void OpenWindowSifarnikSkladista()
        {
            
        }
    }
}
