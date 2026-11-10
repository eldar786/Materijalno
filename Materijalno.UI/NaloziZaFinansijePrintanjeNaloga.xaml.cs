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
    /// <summary>
    /// Interaction logic for NaloziZaFinansijePrintanjeNaloga.xaml
    /// </summary>
    public partial class NaloziZaFinansijePrintanjeNaloga : UserControl
    {
        public NaloziZaFinansijePrintanjeNaloga()
        {
            InitializeComponent();
        }

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is NaloziZaFinansijePrintanjeNalogaViewModel naloziZaFinansijePrintanjeNalogaViewModel)
            {
                try
                {
                    txtGodina.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    txtBrojKalkulacije.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    if (Validation.GetHasError(txtGodina))
                        throw new InvalidOperationException("Provjerite unesenu godinu i broj kalkulacije.");
                    new NaloziZaFinansijePrintanjeNalogaIzvjestaj(naloziZaFinansijePrintanjeNalogaViewModel).Show();
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(ex.Message, "Finansijski nalog", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
