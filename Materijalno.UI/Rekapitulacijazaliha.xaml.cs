using Materijalno.UI.Izvjestaji;
using Materijalno.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
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
    public partial class RekapitulacijaZaliha : UserControl
    {
        public RekapitulacijaZaliha()
        {
            InitializeComponent();
        }

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is RekapitulacijaZalihaViewModel rekapitulacijaZalihaViewModel)
            {
                RekapitulacijaZalihaIvjestaj rekapitulacijaZalihaIvjestaj = new RekapitulacijaZalihaIvjestaj(rekapitulacijaZalihaViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                //medjuskladisnicaIzvjestaj.DataContext = medjuskladisnicaViewModel;
                rekapitulacijaZalihaIvjestaj.Show();
            }
        }
    }
}
