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
    /// Interaction logic for UlazMaterijala.xaml
    /// </summary>
    public partial class UlazMaterijala : UserControl
    {
        public UlazMaterijala()
        {
            InitializeComponent();

            this.DataContextChanged += MainWindow_DataContextChanged;

            DataContextChanged += (sender, e) =>
            {
                if (e.OldValue is UlazMaterijalaViewModel stariVm)
                {
                    stariVm.RequestFocusSlijedecaStavka -= FokusNaSlijedecuStavku;
                    stariVm.RequestFocusIdent -= FokusNaIdent;
                    stariVm.RequestFocusAnalst -= FokusNaAnalst;
                }

                if (e.NewValue is UlazMaterijalaViewModel noviVm)
                {
                    noviVm.RequestFocusSlijedecaStavka += FokusNaSlijedecuStavku;
                    noviVm.RequestFocusIdent += FokusNaIdent;
                    noviVm.RequestFocusAnalst += FokusNaAnalst;
                }
            };
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is UlazMaterijalaViewModel oldVm)
                oldVm.OnPrintEvent -= HandlePrintRequest;
            if (e.NewValue is UlazMaterijalaViewModel ulazMaterijalaViewModel)
            {
                // Subscribe to the print event from the ViewModel
                ulazMaterijalaViewModel.OnPrintEvent += HandlePrintRequest;
            }
        }

        private void FokusNaAnalst()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtAnalst.IsEnabled)
                {
                    txtAnalst.Focus();
                    Keyboard.Focus(txtAnalst);
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void FokusNaSlijedecuStavku()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (btnSlijedecaStavka.IsEnabled)
                {
                    btnSlijedecaStavka.Focus();
                    Keyboard.Focus(btnSlijedecaStavka);
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void FokusNaIdent()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtIdent.IsEnabled)
                {
                    txtIdent.Focus();
                    Keyboard.Focus(txtIdent);
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void HandlePrintRequest()
        {
            var vm = DataContext as UlazMaterijalaViewModel;
            if (vm?.CurrentItemMat == null) return;
            var report = Materijalno.Model.Services.ReceiptPrintReport.Load(vm.CurrentItemMat.Id, vm.CurrentNazivZaSifruKomitenta);
            var preview = new PrintWindow(report) { Owner = Window.GetWindow(this) };
            preview.ShowDialog();
        }

        private void DatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            DatePicker datePicker = sender as DatePicker;

            if (datePicker == null)
                return;

            datePicker.ApplyTemplate();

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


        // Enter potvrđuje datum
        private void DatePickerTextBox_PreviewKeyDown(
    object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            DatePickerTextBox textBox = sender as DatePickerTextBox;

            if (textBox == null)
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

            DatePicker datePicker = null;

            // Pronađi kojem DatePickeru pripada TextBox
            DependencyObject parent = textBox;

            while (parent != null)
            {
                datePicker = parent as DatePicker;

                if (datePicker != null)
                    break;

                parent = VisualTreeHelper.GetParent(parent);
            }

            if (datePicker == null)
                return;

            // Postavi datum
            datePicker.SelectedDate = date;

            // Ažuriraj vrijednost u ViewModelu
            datePicker.GetBindingExpression(
                DatePicker.SelectedDateProperty)?.UpdateSource();

            textBox.Text = date.ToString("dd.MM.yyyy");

            e.Handled = true;

            // Fokus zavisno od DatePickera
            if (datePicker == dtpDatun)
            {
                txtAnalst.Focus();
                txtAnalst.CaretIndex = txtAnalst.Text.Length;
            }
            else if (datePicker == dtpOd1)
            {
                txtIdent.Focus();
                Keyboard.Focus(txtIdent);
                txtIdent.CaretIndex = txtIdent.Text.Length;
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
    }
}
