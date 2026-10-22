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
    /// Interaction logic for Medjuskladisnica.xaml
    /// </summary>
    public partial class Medjuskladisnica : UserControl
    {
        public Medjuskladisnica()
        {
            InitializeComponent();
            this.DataContextChanged += MainWindow_DataContextChanged;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MoveFocusToNextControl(sender as TextBox);
                e.Handled = true;
            }
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Check if the DataContext is of type UlazMaterijalaViewModel
            if (e.NewValue is MedjuskladisnicaViewModel medjuskladisnicaViewModel)
            {
                // Subscribe to the print event from the ViewModel
                medjuskladisnicaViewModel.OnPrintEvent += HandlePrintRequest;
            }

            if (e.OldValue is MedjuskladisnicaViewModel oldVm)
            {
                oldVm.RequestFocusIdent -= FokusNaIdent;
            }

            if (e.NewValue is MedjuskladisnicaViewModel newVm)
            {
                newVm.RequestFocusIdent += FokusNaIdent;
            }
        }

        private void FokusNaIdent()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtIdent.Focus();
                Keyboard.Focus(txtIdent);
                txtIdent.SelectAll();

            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }
        private void HandlePrintRequest()
        {
            // Kreiranje Window za print
            PrintWindowMedjuskladisnica printWindow = new PrintWindowMedjuskladisnica();

            // Postavljanje DataContext za PrintWindow (uzeli smo UlazMaterijalaViewModel)
            if (DataContext is MedjuskladisnicaViewModel medjuskladisnicaViewModel)
            {
                // Dodajemo trenutni ViewModel u PrintWindow
                printWindow.DataContext = medjuskladisnicaViewModel;

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

        private void DatePicker_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            DatePicker datePicker = sender as DatePicker;

            if (datePicker == null)
                return;

            DatePickerTextBox textBox =
                datePicker.Template.FindName("PART_TextBox", datePicker)
                as DatePickerTextBox;

            if (textBox == null)
                return;

            DateTime datum;

            bool isValid = DateTime.TryParseExact(
                textBox.Text,
                "dd.MM.yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out datum);

            if (!isValid)
            {
                MessageBox.Show(
                    "Datum nije ispravan.\nUnesite datum u formatu dd.MM.yyyy.",
                    "Upozorenje",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();
                textBox.SelectAll();

                e.Handled = true;
                return;
            }

            // Postavi datum
            datePicker.SelectedDate = datum;

            // Odmah upisi datum u CurrentItemMat.Datun
            var bindingExpression =
                datePicker.GetBindingExpression(
                    DatePicker.SelectedDateProperty);

            if (bindingExpression != null)
            {
                bindingExpression.UpdateSource();
            }

            // ENTER se ponasa kao TAB
            TraversalRequest request =
                new TraversalRequest(FocusNavigationDirection.Next);

            textBox.MoveFocus(request);

            e.Handled = true;
        }

        private void KolicTextBox_KeyDown( object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;

                if (sender is TextBox textBox)
                {
                    // Update Kolic in the ViewModel before calculation
                    textBox.GetBindingExpression(TextBox.TextProperty)
                           ?.UpdateSource();
                }

                // Move focus to SLJ. STAVKA
                btnSlijedecaStavka.Focus();
            }
        }
        private void DatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            DatePicker datePicker = sender as DatePicker;

            if (datePicker == null)
                return;

            DatePickerTextBox textBox =
                datePicker.Template.FindName("PART_TextBox", datePicker)
                as DatePickerTextBox;

            if (textBox != null)
            {
                textBox.PreviewTextInput -= DatePickerTextBox_PreviewTextInput;
                textBox.PreviewTextInput += DatePickerTextBox_PreviewTextInput;
            }
        }


        private void DatePickerTextBox_PreviewTextInput(
            object sender,
            TextCompositionEventArgs e)
        {
            DatePickerTextBox textBox = sender as DatePickerTextBox;

            if (textBox == null)
                return;

            // Dozvoli samo brojeve
            if (string.IsNullOrEmpty(e.Text) || !char.IsDigit(e.Text[0]))
            {
                e.Handled = true;
                return;
            }

            string text = textBox.Text ?? "";

            // Ako je cijeli datum označen, novi unos počinje ispočetka
            if (textBox.SelectionLength == text.Length)
            {
                text = "";
                textBox.Text = "";
                textBox.CaretIndex = 0;
            }

            int caret = textBox.CaretIndex;

            // Ako je dio teksta označen, ukloni ga prije unosa
            if (textBox.SelectionLength > 0)
            {
                text = text.Remove(
                    textBox.SelectionStart,
                    textBox.SelectionLength);

                caret = textBox.SelectionStart;
            }

            // Ne dozvoli više od dd.MM.yyyy
            if (text.Length >= 10)
            {
                e.Handled = true;
                return;
            }

            // Ubaci unesenu cifru
            text = text.Insert(caret, e.Text);
            caret++;

            // Nakon dana dodaj tačku
            // 01 -> 01.
            if (text.Length == 2)
            {
                text += ".";
                caret++;
            }

            // Nakon mjeseca dodaj tačku
            // 01.11 -> 01.11.
            if (text.Length == 5)
            {
                text += ".";
                caret++;
            }

            if (text.Length <= 10)
            {
                textBox.Text = text;
                textBox.CaretIndex = caret;
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

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is MedjuskladisnicaViewModel medjuskladisnicaViewModel)
            {
                MedjuskladisnicaIzvjestaj medjuskladisnicaIzvjestaj = new MedjuskladisnicaIzvjestaj(medjuskladisnicaViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                //medjuskladisnicaIzvjestaj.DataContext = medjuskladisnicaViewModel;
                medjuskladisnicaIzvjestaj.Show();
            }
        }
    }

}
