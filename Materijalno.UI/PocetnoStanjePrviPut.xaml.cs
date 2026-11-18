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
    public partial class PocetnoStanjePrviPut : UserControl
    {
        public PocetnoStanjePrviPut()
        {
            InitializeComponent();
            DataContextChanged += Opening_DataContextChanged;
        }

        private void Opening_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PocetnoStanjePrviPutViewModel oldVm) oldVm.RequestFocusQuantity -= FocusQuantity;
            if (e.NewValue is PocetnoStanjePrviPutViewModel newVm) newVm.RequestFocusQuantity += FocusQuantity;
        }

        private void FocusQuantity()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtKolic.IsEnabled && txtKolic.IsVisible)
                {
                    Keyboard.Focus(txtKolic);
                    txtKolic.SelectAll();
                }
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void Reference_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var input = sender as TextBox;
            if (input == null || input.IsKeyboardFocusWithin) return;
            input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (Validation.GetHasError(input)) return;
            var vm = DataContext as PocetnoStanjePrviPutViewModel;
            if (vm?.IsEntryEditable == true) vm.Osvjezi();
        }

        private void Amount_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var input = sender as TextBox;
            if (input == null || input.IsKeyboardFocusWithin || !Helpers.QuantityInput.Commit(input)) return;
            var vm = DataContext as PocetnoStanjePrviPutViewModel;
            if (vm?.IsEntryEditable == true) vm.NabavnaCijena();
        }
       
        
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                (sender as UIElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
            }
        }

        private void MoveFocusToNextControl(TextBox currentTextBox)
        {
            currentTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is PocetnoStanjePrviPutViewModel pocetnoStanjePrviPutViewModel)
            {
                PocetnoStanjePrviPutIzvjestaj pocetnoStanjePrviPutIzvjestaj = new PocetnoStanjePrviPutIzvjestaj(pocetnoStanjePrviPutViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                pocetnoStanjePrviPutIzvjestaj.Show();
            }
        }

    }
}
