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
using System.Windows.Shapes;

namespace Materijalno.UI
{
    /// <summary>
    /// Interaction logic for SifarnikSkladistaFormWindow.xaml
    /// </summary>
    public partial class SifarnikSkladistaFormWindow : Window
    {
        public SifarnikSkladistaFormWindow()
        {
            InitializeComponent();
        }

        public void CreateNewWindow()
        {
            SifarnikSkladistaFormWindow sifarnikSkladistaFormWindow = new SifarnikSkladistaFormWindow
            {
                DataContext = new SifarnikSkladista()
            };
            sifarnikSkladistaFormWindow.Show();
        }

        //private void SaveButton_Click(object sender, RoutedEventArgs e)
        //{

        //}

        //private void CancelButton_Click(object sender, RoutedEventArgs e)
        //{

        //}
    }
}
