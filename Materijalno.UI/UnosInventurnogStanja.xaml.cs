using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Materijalno.UI.Helpers;
using Materijalno.UI.Izvjestaji;
using Materijalno.ViewModel;

namespace Materijalno.UI
{
    public partial class UnosInventurnogStanja : UserControl
    {
        private UnosInventurnogStanjaViewModel _vm;
        public UnosInventurnogStanja()
        {
            InitializeComponent();
            DataContextChanged += (sender, args) => Attach(DataContext as UnosInventurnogStanjaViewModel);
            Loaded += (sender, args) => Attach(DataContext as UnosInventurnogStanjaViewModel);
            Unloaded += (sender, args) => Attach(null);
        }
        private void Attach(UnosInventurnogStanjaViewModel vm)
        {
            if (_vm == vm) return;
            if (_vm != null) {
                _vm.RequestFocus -= FocusInput; _vm.OnPrintEvent -= Print;
                _vm.CommitPendingInput = null;
            }
            _vm = vm;
            if (_vm != null) {
                _vm.RequestFocus += FocusInput; _vm.OnPrintEvent += Print;
                _vm.CommitPendingInput = CommitPendingInput;
            }
        }
        private void FocusInput(string name)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                Control target = name == "Warehouse" ? (Control)OpBroj : name == "Material" ? txtIdent : name == "Quantity" ? txtKolic :
                    name == "Save" ? (Control)btnSnimi : name == "NextItem" ? btnSlijedecaStavka : name == "Edit" ? btnIzmjena : btnNovaKalkulacija;
                if (target.IsEnabled && target.IsVisible) { Keyboard.Focus(target); (target as TextBox)?.SelectAll(); }
            }), DispatcherPriority.ContextIdle);
        }
        private bool CommitPendingInput()
        {
            foreach (var input in new[] { OpBroj, txtIdent, txtKolic, txtNc, txtRedbr }) {
                if (!QuantityInput.Commit(input)) { Keyboard.Focus(input); input.SelectAll(); return false; }
            }
            return true;
        }
        private bool Refresh(Action action)
        {
            try { action(); return true; }
            catch (InvalidOperationException error) { MessageBox.Show(error.Message, "Inventura", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        }
        private bool IsCancelTarget(KeyboardFocusChangedEventArgs args) => ReferenceEquals(args.NewFocus, btnOdustani);
        private void Reference_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (_vm?.IsEntryEditable != true || IsCancelTarget(args)) return;
            var input = sender as TextBox;
            if (input == null || input.IsKeyboardFocusWithin) return;
            input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (!Validation.GetHasError(input)) Refresh(_vm.Osvjezi);
        }
        private void Amount_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (_vm?.IsEntryEditable != true || IsCancelTarget(args)) return;
            var input = sender as TextBox;
            if (input == null || input.IsKeyboardFocusWithin || !QuantityInput.Commit(input)) return;
            if (ReferenceEquals(input, txtKolic)) Refresh(_vm.NabavnaCijena);
            else _vm.RecalculateValue();
        }
        private void Input_KeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Enter || _vm?.IsEntryEditable != true) return;
            args.Handled = true;
            var input = sender as TextBox;
            if (!QuantityInput.Commit(input)) return;
            if (ReferenceEquals(input, OpBroj) || ReferenceEquals(input, txtIdent)) {
                if (!Refresh(_vm.Osvjezi)) return;
                Keyboard.Focus(ReferenceEquals(input, OpBroj) ? txtIdent : txtKolic);
            } else if (ReferenceEquals(input, txtKolic)) {
                if (!Refresh(_vm.ValidateQuantity)) return;
                if (Refresh(_vm.NabavnaCijena)) Keyboard.Focus(txtNc);
            } else {
                _vm.RecalculateValue(); Keyboard.Focus(btnSnimi);
            }
        }
        private void Print() { new UnosInventurnogStanjaIzvjestaj(_vm).Show(); }
    }
}
