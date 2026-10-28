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
    /// <summary>
    /// Interaction logic for AnalitickaKartica.xaml
    /// </summary>
    public partial class AnalitickaKartica : UserControl
    {
        public AnalitickaKartica()
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

        private void DatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            DatePicker datePicker = sender as DatePicker;

            if (datePicker == null)
                return;

            DatePickerTextBox textBox =
                datePicker.Template.FindName("PART_TextBox", datePicker)
                as DatePickerTextBox;

            if (textBox == null)
                return;

            textBox.PreviewTextInput -= DatePickerTextBox_PreviewTextInput;
            textBox.PreviewTextInput += DatePickerTextBox_PreviewTextInput;

            textBox.PreviewKeyDown -= DatePickerTextBox_PreviewKeyDown;
            textBox.PreviewKeyDown += DatePickerTextBox_PreviewKeyDown;

            DataObject.RemovePastingHandler(textBox, DatePickerTextBox_Pasting);
            DataObject.AddPastingHandler(textBox, DatePickerTextBox_Pasting);
        }

        private void DatePickerTextBox_PreviewTextInput(
            object sender, TextCompositionEventArgs e)
        {
            DatePickerTextBox textBox = sender as DatePickerTextBox;

            if (textBox == null)
                return;

            string current = textBox.Text ?? "";

            string updated = current.Remove(
                textBox.SelectionStart,
                textBox.SelectionLength);

            updated = updated.Insert(textBox.SelectionStart, e.Text);

            string digits = new string(
                updated.Where(char.IsDigit).ToArray());

            if (digits.Length > 8 || e.Text.Any(c => !char.IsDigit(c)))
            {
                e.Handled = true;
                return;
            }

            string formatted = FormatDateDigits(digits);

            textBox.Text = formatted;
            textBox.CaretIndex = formatted.Length;

            e.Handled = true;
        }

        private string FormatDateDigits(string digits)
        {
            if (digits.Length == 0)
                return "";

            if (digits.Length < 2)
                return digits;

            if (digits.Length == 2)
                return digits + ".";

            if (digits.Length < 4)
                return digits.Substring(0, 2) + "." +
                       digits.Substring(2);

            if (digits.Length == 4)
                return digits.Substring(0, 2) + "." +
                       digits.Substring(2, 2) + ".";

            return digits.Substring(0, 2) + "." +
                   digits.Substring(2, 2) + "." +
                   digits.Substring(4);
        }

        private void DatePickerTextBox_PreviewKeyDown(
            object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            DatePickerTextBox textBox = sender as DatePickerTextBox;

            if (textBox == null)
                return;

            DatePicker datePicker = FindParentDatePicker(textBox);

            if (datePicker == null)
                return;

            string digits = new string(
                (textBox.Text ?? "").Where(char.IsDigit).ToArray());

            DateTime date;

            if (digits.Length != 8 ||
                !DateTime.TryParseExact(
                    digits,
                    "ddMMyyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out date))
            {
                MessageBox.Show(
                    "Unesite ispravan datum u formatu dd.MM.yyyy.");

                e.Handled = true;
                return;
            }

            datePicker.SelectedDate = date;

            datePicker.GetBindingExpression(
                DatePicker.SelectedDateProperty)?.UpdateSource();

            textBox.Text = date.ToString("dd.MM.yyyy");

            e.Handled = true;

            if (datePicker == dtpDatun)
            {
                dtpOd.Focus();

                DatePickerTextBox nextTextBox =
                    dtpOd.Template.FindName("PART_TextBox", dtpOd)
                    as DatePickerTextBox;

                nextTextBox?.Focus();
                nextTextBox?.SelectAll();
            }
            else if (datePicker == dtpOd)
            {
                datePicker.MoveFocus(
                    new TraversalRequest(
                        FocusNavigationDirection.Next));
            }
        }

        private DatePicker FindParentDatePicker(DependencyObject child)
        {
            while (child != null)
            {
                DatePicker datePicker = child as DatePicker;

                if (datePicker != null)
                    return datePicker;

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private void DatePickerTextBox_Pasting(
            object sender, DataObjectPastingEventArgs e)
        {
            // Sprječava lijepljenje neformatiranog teksta.
            e.CancelCommand();
        }

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is AnalitickaKarticaViewModel analitickaKarticaViewModel)
            {
                AnalitickaKarticaIzvjestaj analitickaIzvjestaj = new AnalitickaKarticaIzvjestaj(analitickaKarticaViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                //medjuskladisnicaIzvjestaj.DataContext = medjuskladisnicaViewModel;
                analitickaIzvjestaj.Show();
            }
        }
    }
}
