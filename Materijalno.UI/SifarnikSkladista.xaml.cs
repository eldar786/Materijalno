using Materijalno.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Materijalno.UI
{
    /// <summary>
    /// Interaction logic for SifarnikSkladista.xaml
    /// </summary>
    public partial class SifarnikSkladista : UserControl
    {
        public SifarnikSkladista()
        {
            InitializeComponent();
            //DataContext = new SifarnikSkladistaViewModel(null);

        }

        //private void btnUnos_Click(object sender, RoutedEventArgs e)
        //{
            
        //    SifarnikSkladistaFormWindow sifarnikSkladistaFormWindow = new SifarnikSkladistaFormWindow((SifarnikSkladistaViewModel)DataContext);
        //    sifarnikSkladistaFormWindow.DataContext = DataContext;

        //    sifarnikSkladistaFormWindow.Show();
        //}
    }
}
