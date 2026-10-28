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
    
    public partial class PregledIzlazaZaSveKalkulacije : UserControl
    {
        public PregledIzlazaZaSveKalkulacije()
        {
            InitializeComponent();
        }
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                UIElement control = sender as UIElement;

                if (control != null)
                {
                    control.MoveFocus(
                        new TraversalRequest(FocusNavigationDirection.Next));
                }

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

        private void datDo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
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

            // Find the DatePicker being edited
            DatePicker datePicker = FindParentDatePicker(textBox);

            if (datePicker == null)
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
                // Update the correct DatePicker
                datePicker.SelectedDate = date;

                // Update the corresponding ViewModel property
                BindingExpression binding = datePicker.GetBindingExpression(
                    DatePicker.SelectedDateProperty);

                if (binding != null)
                    binding.UpdateSource();

                // Move focus to next control
                // Ako smo na Datun, prelazimo na Datnar
                if (datePicker == dtpOd)
                {
                    dtpDo.Focus();

                    dtpDo.ApplyTemplate();

                    DatePickerTextBox datnarTextBox =
                        dtpDo.Template.FindName(
                            "PART_TextBox", dtpDo) as DatePickerTextBox;

                    if (datnarTextBox != null)
                    {
                        datnarTextBox.Focus();
                        datnarTextBox.SelectAll();
                    }
                }
                else
                {
                    // Ako smo na Datnar, prelazimo na sljedeće polje
                    datePicker.MoveFocus(
                        new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
            else
            {
                MessageBox.Show(
                    "Unesite ispravan datum u formatu DD.MM.GGGG.",
                    "Neispravan datum",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            e.Handled = true;
        }
        private DatePicker FindParentDatePicker(DependencyObject child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if (parent is DatePicker)
                    return (DatePicker)parent;

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }
        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is PregledIzlazaZaSveKalkulacijeViewModel pregledIzlazaZaSveKalkulacijeViewModel)
            {
                PregledIzlazaZaSveKalkulacijeIzvjestaj pregledIzlazaZaSveKalkulacijeIzvjestaj = new PregledIzlazaZaSveKalkulacijeIzvjestaj(pregledIzlazaZaSveKalkulacijeViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                //medjuskladisnicaIzvjestaj.DataContext = medjuskladisnicaViewModel;
                pregledIzlazaZaSveKalkulacijeIzvjestaj.Show();
            }
        }
    }
}