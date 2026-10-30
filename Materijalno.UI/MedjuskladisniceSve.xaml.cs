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
    /// Interaction logic for MedjuskladisniceSve.xaml
    /// </summary>
    public partial class MedjuskladisniceSve : UserControl
    {
        public MedjuskladisniceSve()
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
        private void DateTextBox_PreviewKeyDown(
    object sender, KeyEventArgs e)
        {
            DatePickerTextBox textBox = sender as DatePickerTextBox;

            if (textBox == null || e.Key != Key.Enter)
                return;

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
                datePicker.SelectedDate = date;

                BindingExpression binding = datePicker.GetBindingExpression(
                    DatePicker.SelectedDateProperty);

                if (binding != null)
                    binding.UpdateSource();

                // ENTER na Datun -> prelazak na Datnar
                if (datePicker == dtpOd)
                {
                    dtpDo.ApplyTemplate();

                    DatePickerTextBox datnarTextBox =
                        dtpDo.Template.FindName(
                            "PART_TextBox", dtpDo) as DatePickerTextBox;

                    if (datnarTextBox != null)
                    {
                        datnarTextBox.Focus();
                        datnarTextBox.SelectAll();
                    }
                    else
                    {
                        dtpDo.Focus();
                    }
                }
                else
                {
                    // ENTER na Datnar -> sljedeća kontrola
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
            if (DataContext is MedjuskladisniceSveViewModel medjuskladisniceSveViewModel)
            {
                MedjuskladisniceSveIzvjestaj medjuskladisniceSveIzvjestaj = new MedjuskladisniceSveIzvjestaj(medjuskladisniceSveViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                medjuskladisniceSveIzvjestaj.Show();
            }
        }
    }
}