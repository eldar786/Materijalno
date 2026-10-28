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
using System.Globalization;
using System.Windows.Controls.Primitives;


namespace Materijalno.UI
{
    /// <summary>
    /// Interaction logic for StanjeZalihaZaSvaSkladista.xaml
    /// </summary>
    public partial class StanjeZalihaZaSvaSkladista : UserControl
    {
        public StanjeZalihaZaSvaSkladista()
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

            // Replace selected text or insert at caret
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

        private void DateTextBox_PreviewKeyDown(
            object sender, KeyEventArgs e)
        {
            DatePickerTextBox textBox = sender as DatePickerTextBox;

            if (textBox == null)
                return;

            if (e.Key != Key.Enter)
                return;

            DateTime date;

            bool valid = DateTime.TryParseExact(
                textBox.Text,
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);

            if (valid)
            {
                dtpOd.SelectedDate = date;

                // Update ViewModel binding
                BindingExpression binding = dtpOd.GetBindingExpression(
                    DatePicker.SelectedDateProperty);

                if (binding != null)
                    binding.UpdateSource();

                dtpOd.MoveFocus(
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
        private void MoveFocusToNextControl(TextBox currentTextBox)
        {
            currentTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private void datOd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void datDo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is StanjeZalihaZaSvaSkladistaViewModel stanjeZalihaZaSvaSkladistaViewModel)
            {
                StanjeZalihaZaSvaSkladistaIzvjestaj stanjeZalihaZaSvaSkladistaIzvjestaj = new StanjeZalihaZaSvaSkladistaIzvjestaj(stanjeZalihaZaSvaSkladistaViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                //medjuskladisnicaIzvjestaj.DataContext = medjuskladisnicaViewModel;
                stanjeZalihaZaSvaSkladistaIzvjestaj.Show();
            }
        }
    }
}
