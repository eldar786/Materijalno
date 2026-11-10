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
using System.Windows.Controls.Primitives;
using System.Globalization;
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
    /// Interaction logic for PovratMaterijala.xaml
    /// </summary>
    public partial class PovratMaterijala : UserControl
    {
        public PovratMaterijala()
        {
            InitializeComponent();

            this.DataContextChanged += MainWindow_DataContextChanged;
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PovratMaterijalaViewModel oldVm)
            {
                oldVm.OnPrintEvent -= HandlePrintRequest;
                oldVm.RequestFocusSlijedecaStavka -= FocusSlijedecaStavka;
                oldVm.RequestFocusIdent -= FocusIdent;
            }
            if (e.NewValue is PovratMaterijalaViewModel newVm)
            {
                newVm.OnPrintEvent += HandlePrintRequest;
                newVm.RequestFocusSlijedecaStavka += FocusSlijedecaStavka;
                newVm.RequestFocusIdent += FocusIdent;
            }
        }

        private void FocusSlijedecaStavka()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (btnSlijedecaStavka.IsVisible && btnSlijedecaStavka.IsEnabled)
                    Keyboard.Focus(btnSlijedecaStavka);
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void FocusIdent()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtIdent.IsVisible && txtIdent.IsEnabled)
                {
                    Keyboard.Focus(txtIdent);
                    txtIdent.SelectAll();
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void Kolicina_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            var input = sender as TextBox;
            if (input == null) return;
            input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (Validation.GetHasError(input)) return;
            if (btnSnimi.IsEnabled && btnSnimi.IsVisible) Keyboard.Focus(btnSnimi);
        }

        private static DatePickerTextBox DateInput(DatePicker picker)
        {
            picker.ApplyTemplate();
            return picker.Template?.FindName("PART_TextBox", picker) as DatePickerTextBox;
        }

        private void DatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            var input = DateInput((DatePicker)sender);
            if (input == null) return;
            input.MaxLength = 11;
            input.PreviewTextInput -= DateInput_PreviewTextInput;
            input.PreviewTextInput += DateInput_PreviewTextInput;
            input.GotKeyboardFocus -= DateInput_GotKeyboardFocus;
            input.GotKeyboardFocus += DateInput_GotKeyboardFocus;
            DataObject.RemovePastingHandler(input, DateInput_Pasting);
            DataObject.AddPastingHandler(input, DateInput_Pasting);
        }

        private void DateInput_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ((DatePickerTextBox)sender).SelectAll();
        }

        private static bool IsDateDigit(char value) => value >= '0' && value <= '9';

        private static string MaskDate(string digits)
        {
            var result = new StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                result.Append(digits[i]);
                if (i == 1 || i == 3 || i == 7) result.Append('.');
            }
            return result.ToString();
        }

        private static void SetDateDigits(DatePickerTextBox input, string digits, int digitCaret)
        {
            input.Text = MaskDate(digits);
            int caret = digitCaret + (digitCaret >= 2 ? 1 : 0) + (digitCaret >= 4 ? 1 : 0) + (digitCaret >= 8 ? 1 : 0);
            input.CaretIndex = Math.Min(caret, input.Text.Length);
        }

        private static void InsertDateDigits(DatePickerTextBox input, string incoming)
        {
            string text = input.Text ?? "";
            int before = text.Take(input.SelectionStart).Count(IsDateDigit);
            int selected = text.Skip(input.SelectionStart).Take(input.SelectionLength).Count(IsDateDigit);
            string digits = new string(text.Where(IsDateDigit).ToArray());
            string edited = digits.Remove(before, selected).Insert(before, incoming);
            if (edited.Length <= 8) SetDateDigits(input, edited, before + incoming.Length);
        }

        private void DateInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true;
            if (!string.IsNullOrEmpty(e.Text) && e.Text.All(IsDateDigit))
                InsertDateDigits((DatePickerTextBox)sender, e.Text);
            // Dots are inserted by the mask; manually typed dots are harmless.
        }

        private void DateInput_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            var text = e.DataObject.GetData(DataFormats.UnicodeText) as string
                ?? e.DataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrWhiteSpace(text) || text.Any(c => !IsDateDigit(c) && c != '.' && c != '/' && c != '-' && !char.IsWhiteSpace(c))) return;
            InsertDateDigits((DatePickerTextBox)sender, new string(text.Where(IsDateDigit).ToArray()));
        }

        private void DatePicker_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var picker = (DatePicker)sender;
            var input = DateInput(picker);
            if (input == null || !input.IsKeyboardFocusWithin) return;
            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                e.Handled = true;
                string text = input.Text ?? "";
                string digits = new string(text.Where(IsDateDigit).ToArray());
                int before = text.Take(input.SelectionStart).Count(IsDateDigit);
                int selected = text.Skip(input.SelectionStart).Take(input.SelectionLength).Count(IsDateDigit);
                if (selected > 0) digits = digits.Remove(before, selected);
                else if (e.Key == Key.Back && before > 0) { digits = digits.Remove(before - 1, 1); before--; }
                else if (e.Key == Key.Delete && before < digits.Length) digits = digits.Remove(before, 1);
                SetDateDigits(input, digits, before);
                return;
            }
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            DateTime date;
            var binding = picker.GetBindingExpression(DatePicker.SelectedDateProperty);
            if (!DateTime.TryParseExact((input.Text ?? "").Trim().TrimEnd('.'), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                if (binding != null) Validation.MarkInvalid(binding, new ValidationError(new ExceptionValidationRule(), binding, "Unesite ispravan datum u formatu dd.MM.yyyy.", null));
                picker.ToolTip = "Unesite ispravan datum u formatu dd.MM.yyyy.";
                return;
            }
            if (binding != null) Validation.ClearInvalid(binding);
            picker.ClearValue(ToolTipProperty);
            picker.SetCurrentValue(DatePicker.SelectedDateProperty, date.Date);
            binding?.UpdateSource();
            // Explicit targets skip the calendar button and read-only fields.
            if (ReferenceEquals(picker, dtpOd)) Keyboard.Focus(txtBrdok);
            else Keyboard.Focus(txtIdent);
        }


        private void HandlePrintRequest()
        {
            // Kreiranje Window za print
            PrintWindowPovrat printWindow = new PrintWindowPovrat();

            // Postavljanje DataContext za PrintWindow (uzeli smo )
            if (DataContext is PovratMaterijalaViewModel povratMaterijalaViewModel)
            {
                // Dodajemo trenutni ViewModel u PrintWindow
                printWindow.DataContext = povratMaterijalaViewModel;

                // Prikazi prozor za print
                printWindow.Show();

                // Koristimo PrintDialog za ispis zadrzaja prozora
                PrintDialog printDialog = new PrintDialog();

                if (printDialog.ShowDialog() == true)
                {
                    // Postavljamo velicinu na A4 i vodoravna orijentacija
                    printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
                    printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;

                    //  Izračunamo faktor skaliranja kako bi sadržaj odgovarao formatu A4
                    double scaleX = printDialog.PrintableAreaWidth / printWindow.ActualWidth;
                    double scaleY = printDialog.PrintableAreaHeight / printWindow.ActualHeight;
                    double scale = Math.Min(scaleX, scaleY);

                    // Save the original transform
                    Transform originalTransform = printWindow.LayoutTransform;

                    // Apply scaling transform to the PrintWindow
                    printWindow.LayoutTransform = new ScaleTransform(scale, scale);

                    // Measure and arrange the page to the size of the printable area
                    Size pageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
                    printWindow.Measure(pageSize);
                    printWindow.Arrange(new Rect(0, 0, pageSize.Width, pageSize.Height));

                    // Print the window's visual
                    printDialog.PrintVisual(printWindow, "Print DataGrid");

                    // Restore the original transform
                    printWindow.LayoutTransform = originalTransform;
                }

                // Close the print window after printing
                printWindow.Close();
            }
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

        private void datOd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is PovratMaterijalaViewModel povratViewModel)
            {
                PovratIzvjestaj povratIzvjestaj = new PovratIzvjestaj(povratViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                //medjuskladisnicaIzvjestaj.DataContext = medjuskladisnicaViewModel;
                povratIzvjestaj.Show();
            }
        }
    }

}
