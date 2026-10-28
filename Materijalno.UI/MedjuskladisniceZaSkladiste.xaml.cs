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
    /// Interaction logic for MedjuskladisniceZaSkladiste.xaml
    /// </summary>
    public partial class MedjuskladisniceZaSkladiste : UserControl
    {
        public MedjuskladisniceZaSkladiste()
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

        private void datDo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

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
        }


        // Automatsko dodavanje tačaka
        private string FormatDateDigits(string digits)
        {
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


        // Ručni unos datuma
        private void DatePickerTextBox_PreviewTextInput(
            object sender, TextCompositionEventArgs e)
        {
            DatePickerTextBox textBox = sender as DatePickerTextBox;

            if (textBox == null)
                return;

            if (e.Text.Any(c => !char.IsDigit(c)))
            {
                e.Handled = true;
                return;
            }

            string updated = (textBox.Text ?? "").Remove(
                textBox.SelectionStart,
                textBox.SelectionLength);

            updated = updated.Insert(
                textBox.SelectionStart,
                e.Text);

            string digits = new string(
                updated.Where(char.IsDigit).ToArray());

            if (digits.Length > 8)
            {
                e.Handled = true;
                return;
            }

            textBox.Text = FormatDateDigits(digits);
            textBox.CaretIndex = textBox.Text.Length;

            e.Handled = true;
        }


        // Enter potvrđuje datum i mijenja fokus
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
                (textBox.Text ?? "")
                .Where(char.IsDigit)
                .ToArray());

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

            // Potvrdi datum
            datePicker.SelectedDate = date;

            // Ažuriraj CurrentItemMat
            datePicker.GetBindingExpression(
                DatePicker.SelectedDateProperty)?.UpdateSource();

            textBox.Text = date.ToString("dd.MM.yyyy");

            e.Handled = true;

            // DATUN -> DATNAR
            if (datePicker == dtpOd)
            {
                dtpDo.Focus();

                DatePickerTextBox nextTextBox =
                    dtpDo.Template.FindName(
                        "PART_TextBox", dtpDo)
                    as DatePickerTextBox;

                if (nextTextBox != null)
                {
                    nextTextBox.Focus();
                    nextTextBox.SelectAll();
                }
            }
            // DATNAR -> ŠTAMPA
            else if (datePicker == dtpDo)
            {
                if (btnStampa.IsVisible && btnStampa.IsEnabled)
                {
                    btnStampa.Focus();
                }
            }
        }


        // Pronalaženje roditeljskog DatePicker kontrola
        private DatePicker FindParentDatePicker(
            DependencyObject child)
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


        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is MedjuskladisniceZaSkladisteViewModel medjuskladisniceZaSkladisteViewModel)
            {
                MedjuskladisniceZaSkladisteIzvjestaj medjuskladisniceZaSkladisteIzvjestaj  = new MedjuskladisniceZaSkladisteIzvjestaj(medjuskladisniceZaSkladisteViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                medjuskladisniceZaSkladisteIzvjestaj.Show();
            }
        }
    }
}
