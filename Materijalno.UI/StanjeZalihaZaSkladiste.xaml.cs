using Materijalno.UI.Izvjestaji;
using Materijalno.ViewModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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


        private void DatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            DatePicker datePicker = sender as DatePicker;

            if (datePicker == null)
                return;

            datePicker.ApplyTemplate();

            DatePickerTextBox textBox = datePicker.Template.FindName(
                "PART_TextBox", datePicker) as DatePickerTextBox;

            if (textBox == null)
                return;

            textBox.PreviewTextInput -= DateTextBox_PreviewTextInput;
            textBox.PreviewTextInput += DateTextBox_PreviewTextInput;

            textBox.PreviewKeyDown -= DateTextBox_PreviewKeyDown;
            textBox.PreviewKeyDown += DateTextBox_PreviewKeyDown;
        }

        private void DateTextBox_PreviewTextInput(
    object sender, TextCompositionEventArgs e)
        {
            DatePickerTextBox textBox = sender as DatePickerTextBox;

            if (textBox == null)
                return;

            if (!e.Text.All(char.IsDigit))
            {
                e.Handled = true;
                return;
            }

            string current = textBox.Text ?? "";

            // Replace selected text, or insert at caret
            string updated = current.Remove(
                textBox.SelectionStart,
                textBox.SelectionLength);

            updated = updated.Insert(
                textBox.SelectionStart, e.Text);

            string digits = new string(
                updated.Where(char.IsDigit).Take(8).ToArray());

            textBox.Text = FormatDateDigits(digits);
            textBox.CaretIndex = textBox.Text.Length;

            e.Handled = true;
        }
        private string FormatDateDigits(string digits)
        {
            if (digits.Length > 8)
                digits = digits.Substring(0, 8);

            if (digits.Length > 4)
                return digits.Substring(0, 2) + "." +
                       digits.Substring(2, 2) + "." +
                       digits.Substring(4);

            if (digits.Length == 4)
                return digits.Substring(0, 2) + "." +
                       digits.Substring(2, 2) + ".";

            if (digits.Length >= 2)
                return digits.Substring(0, 2) + "." +
                       digits.Substring(2);

            return digits;
        }

        private void DatePickerTextBox_PreviewKeyDown(
            object sender, KeyEventArgs e)
        {
            DatePickerTextBox textBox = sender as DatePickerTextBox;

            if (textBox == null)
                return;

            // Allow Tab and Enter to work normally
            if (e.Key == Key.Tab || e.Key == Key.Enter)
                return;

            // Allow Backspace
            if (e.Key == Key.Back)
            {
                string digits = new string(
                    (textBox.Text ?? "").Where(char.IsDigit).ToArray());

                if (digits.Length > 0)
                    digits = digits.Substring(0, digits.Length - 1);

                textBox.Text = FormatDateDigits(digits);
                textBox.CaretIndex = textBox.Text.Length;

                e.Handled = true;
                return;
            }

            // Only numeric keys
            int number = -1;

            if (e.Key >= Key.D0 && e.Key <= Key.D9)
                number = e.Key - Key.D0;

            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                number = e.Key - Key.NumPad0;

            if (number == -1)
                return;

            string currentDigits = new string(
                (textBox.Text ?? "").Where(char.IsDigit).ToArray());

            if (currentDigits.Length >= 8)
            {
                e.Handled = true;
                return;
            }

            currentDigits += number.ToString();

            textBox.Text = FormatDateDigits(currentDigits);
            textBox.CaretIndex = textBox.Text.Length;

            e.Handled = true;
        }

        
        private void DateTextBox_PreviewKeyDown(
    object sender, KeyEventArgs e)
        {
            DatePickerTextBox textBox = sender as DatePickerTextBox;

            if (textBox == null)
                return;

            if (e.Key != Key.Enter)
                return;

            DatePicker datePicker = dtpOd;

            DateTime date;

            bool valid = DateTime.TryParseExact(
                textBox.Text,
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);

            if (valid)
            {
                datePicker.SelectedDate = date;

                // Update ViewModel binding
                BindingExpression binding = datePicker.GetBindingExpression(
                    DatePicker.SelectedDateProperty);

                if (binding != null)
                    binding.UpdateSource();

                datePicker.MoveFocus(
                    new TraversalRequest(FocusNavigationDirection.Next));
            }
            else
            {
                MessageBox.Show(
                    "Please enter a valid date in DD.MM.YYYY format.",
                    "Invalid date",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            e.Handled = true;
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