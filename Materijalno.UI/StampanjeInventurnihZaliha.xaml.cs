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
    public partial class StampanjeInventurnihZaliha : UserControl
    {
        public StampanjeInventurnihZaliha()
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

        private void Skladiste_LostFocus(object sender, RoutedEventArgs e)
        {
            txtSkladiste.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (DataContext is StampanjeInventurnihZalihaViewModel vm)
                vm.RefreshWarehouse();
        }

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is StampanjeInventurnihZalihaViewModel stampanjeInventurnihZalihaViewModel)
            {
                try
                {
                    txtSkladiste.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    if (Validation.GetHasError(txtSkladiste))
                        throw new Materijalno.Model.Services.InventoryReportUnavailableException("Unesite ispravnu šifru skladišta.");
                    stampanjeInventurnihZalihaViewModel.RefreshWarehouse();
                    if (stampanjeInventurnihZalihaViewModel.CurrentItemInv.Kljnaz > 0 &&
                        stampanjeInventurnihZalihaViewModel.CurrentItemTabSkladista == null)
                        throw new Materijalno.Model.Services.InventoryReportUnavailableException("Skladište nije prijavljeno u šifarnik.");
                    var report = new StampanjeInventurnihZalihaIzvjestaj(stampanjeInventurnihZalihaViewModel);
                    report.Show();
                }
                catch (Materijalno.Model.Services.InventoryReportUnavailableException ex)
                {
                    MessageBox.Show(ex.Message, "Inventurni popis", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }

}
