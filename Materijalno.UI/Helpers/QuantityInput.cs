using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Materijalno.UI.Helpers
{
    public static class QuantityInput
    {
        public static readonly DependencyProperty SelectAllOnFocusProperty = DependencyProperty.RegisterAttached(
            "SelectAllOnFocus", typeof(bool), typeof(QuantityInput), new PropertyMetadata(false, OnEnabledChanged));

        public static bool GetSelectAllOnFocus(DependencyObject target) => (bool)target.GetValue(SelectAllOnFocusProperty);
        public static void SetSelectAllOnFocus(DependencyObject target, bool value) => target.SetValue(SelectAllOnFocusProperty, value);

        private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            var input = target as TextBox;
            if (input == null) return;
            if ((bool)args.OldValue)
            {
                input.GotKeyboardFocus -= SelectAll;
                input.PreviewMouseLeftButtonDown -= FocusBeforeClick;
            }
            if ((bool)args.NewValue)
            {
                input.GotKeyboardFocus += SelectAll;
                input.PreviewMouseLeftButtonDown += FocusBeforeClick;
            }
        }

        private static void SelectAll(object sender, KeyboardFocusChangedEventArgs args) => ((TextBox)sender).SelectAll();

        private static void FocusBeforeClick(object sender, MouseButtonEventArgs args)
        {
            var input = (TextBox)sender;
            if (input.IsKeyboardFocusWithin) return;
            // The first mouse click selects the value instead of placing the caret inside it.
            args.Handled = true;
            input.Focus();
        }

        public static bool Commit(TextBox input)
        {
            if (input == null) return false;
            var binding = input.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
            if (Validation.GetHasError(input)) return false;
            // Reapply N2 after a valid entry, even if the numeric value did not change.
            binding?.UpdateTarget();
            return true;
        }
    }
}
