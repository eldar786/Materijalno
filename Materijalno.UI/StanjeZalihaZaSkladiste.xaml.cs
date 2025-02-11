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
    public partial class StanjeZalihaZaSkladiste : UserControl
    {
        public StanjeZalihaZaSkladiste()
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

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is StanjeZalihaZaSkladisteViewModel stanjeZalihaZaSkladisteViewModel)
            {
                StanjeZalihaZaSkladisteIzvjestaj stanjeZalihaZaSkladisteIzvjestaj = new StanjeZalihaZaSkladisteIzvjestaj(stanjeZalihaZaSkladisteViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                //medjuskladisnicaIzvjestaj.DataContext = medjuskladisnicaViewModel;
                stanjeZalihaZaSkladisteIzvjestaj.Show();
            }
        }
    }
}