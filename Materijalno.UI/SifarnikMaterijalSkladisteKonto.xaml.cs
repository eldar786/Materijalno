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

namespace Materijalno.UI
{
    /// <summary>
    /// Interaction logic for SifarnikMaterijalSkladisteKonto.xaml
    /// </summary>
    public partial class SifarnikMaterijalSkladisteKonto : UserControl
    {
        public SifarnikMaterijalSkladisteKonto()
        {
            InitializeComponent();
        }

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as SifarnikMaterijalSkladisteKontoViewModel;
            var data = viewModel?.SifarnikMaterijalSkladisteKontoList;

            if (data == null || !data.Any())
                return;

            FlowDocument doc = new FlowDocument
            {
                PageHeight = 1056,
                PageWidth = 816,
                PagePadding = new Thickness(50),
                ColumnGap = 0,
                ColumnWidth = 816,
                FontSize = 14,
                FontFamily = new FontFamily("Segoe UI")
            };

            // === Title Header Section ===
            var headerTable = new Table();
            headerTable.Columns.Add(new TableColumn());
            headerTable.Columns.Add(new TableColumn());

            TableRowGroup headerGroup = new TableRowGroup();
            TableRow headerRow = new TableRow();

            var titleCell = new TableCell(new Paragraph(new Bold(new Run("Šifarnik materijal-skladište konto"))));
            titleCell.FontSize = 24;
            titleCell.TextAlignment = TextAlignment.Left;
            titleCell.BorderThickness = new Thickness(0);
            titleCell.Padding = new Thickness(0, 0, 0, 20);

            var dateCell = new TableCell(new Paragraph(new Run(DateTime.Now.ToString("dd/MM/yyyy"))));
            dateCell.FontSize = 14;
            dateCell.TextAlignment = TextAlignment.Right;
            dateCell.BorderThickness = new Thickness(0);
            dateCell.Padding = new Thickness(0, 0, 0, 20);

            headerRow.Cells.Add(titleCell);
            headerRow.Cells.Add(dateCell);
            headerGroup.Rows.Add(headerRow);
            headerTable.RowGroups.Add(headerGroup);

            doc.Blocks.Add(headerTable);

            // === Main Table Section ===
            Table table = new Table();
            doc.Blocks.Add(table);

            int numColumns = 3;
            for (int i = 0; i < numColumns; i++)
                table.Columns.Add(new TableColumn());

            table.Columns[0].Width = new GridLength(150); // Šifra materijala
            table.Columns[1].Width = new GridLength(150); // Šifra skladišta
            table.Columns[2].Width = new GridLength(240); // Konto

            TableRowGroup trg = new TableRowGroup();

            // Header row
            TableRow header = new TableRow();
            header.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Šifra materijala")))));
            header.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Šifra skladišta")))));
            header.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Konto")))));

            foreach (var cell in header.Cells)
            {
                cell.Padding = new Thickness(4);
                cell.BorderBrush = Brushes.Black;
                cell.BorderThickness = new Thickness(0.5);
                cell.Background = Brushes.LightGray;
            }

            trg.Rows.Add(header);

            // Data rows
            foreach (var item in data)
            {
                TableRow row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Sifmat.ToString()))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Sifskla.ToString()))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Sifkonta.ToString()))));

                foreach (var cell in row.Cells)
                {
                    cell.Padding = new Thickness(4);
                    cell.BorderBrush = Brushes.Black;
                    cell.BorderThickness = new Thickness(0.25);
                }

                trg.Rows.Add(row);
            }

            table.RowGroups.Add(trg);

            // === Print Dialog ===
            PrintDialog printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                IDocumentPaginatorSource idpSource = doc;
                printDlg.PrintDocument(idpSource.DocumentPaginator, "Štampanje materijal-skladište konto");
            }
        }

    }
}
