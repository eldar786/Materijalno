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
    /// Interaction logic for UlazMaterijala.xaml
    /// </summary>
    public partial class UlazMaterijala : UserControl
    {
        public UlazMaterijala()
        {
            InitializeComponent();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MoveFocusToNextControl(sender as TextBox);
                e.Handled = true;
            }
        }

        private void MoveFocusToNextControl(TextBox currentTextBox)
        {
            currentTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private void datOd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void btnNovaKalkulacija_Click(object sender, RoutedEventArgs e)
        {
            btnPrvi.IsEnabled = false;
            btnZadnji.IsEnabled = false;
            btnTrazi.IsEnabled = false;
            btnNovaKalkulacija.IsEnabled = false;
            btnKrajFakture.IsEnabled = false;
            btnStampa.IsEnabled = false;
            btnStampa1.IsEnabled = false;

            btnNovaStavka.IsEnabled = false;
            btnIzmjenaStavka.IsEnabled = false;
            btnSlijedecaStavka.IsEnabled = false;
            btnBrisanje.IsEnabled = false;
            btnIzlaz.IsEnabled = false;

            btnSnimi.IsEnabled = true;
            btnOdustani.IsEnabled = true;
        }

        private void btnOdustani_Click(object sender, RoutedEventArgs e)
        {
            btnPrvi.IsEnabled = true;
            btnZadnji.IsEnabled = true;
            btnTrazi.IsEnabled = true;
            btnNovaKalkulacija.IsEnabled = true;

            btnKrajFakture.IsEnabled = false;

            btnNovaStavka.IsEnabled = true;
            btnIzmjenaStavka.IsEnabled = true;
            btnSlijedecaStavka.IsEnabled = true;
            btnBrisanje.IsEnabled = true;
            btnIzlaz.IsEnabled = true;
        }

        private void btnSnimi_Click(object sender, RoutedEventArgs e)
        {
            btnPrvi.IsEnabled = true;
            btnZadnji.IsEnabled = true;
            btnTrazi.IsEnabled = true;
            btnNovaKalkulacija.IsEnabled = true;

            btnKrajFakture.IsEnabled = false;
            btnNovaStavka.IsEnabled = false;
            btnSlijedecaStavka.IsEnabled = false;

            btnIzmjenaStavka.IsEnabled = true;
            btnBrisanje.IsEnabled = true;
            btnIzlaz.IsEnabled = true;
        }
    }
}
