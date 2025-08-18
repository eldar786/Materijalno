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
    public partial class SifarnikKonta : UserControl
    {
        public SifarnikKonta()
        {
            InitializeComponent();
        }
        private void stampa_button(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as SifarnikKontaViewModel;
            var data = viewModel?.SifarnikKontaList;

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

            
            var headerTable = new Table();
            headerTable.Columns.Add(new TableColumn());
            headerTable.Columns.Add(new TableColumn());

            TableRowGroup headerGroup = new TableRowGroup();
            TableRow headerRow = new TableRow();

            var titleCell = new TableCell(new Paragraph(new Bold(new Run("Kontni plan"))));
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

            
            Table table = new Table();
            doc.Blocks.Add(table);

            int numColumns = 2;
            for (int i = 0; i < numColumns; i++)
                table.Columns.Add(new TableColumn());

            table.Columns[0].Width = new GridLength(120); // Šifra konta
            table.Columns[1].Width = new GridLength(420); // Naziv konta

            TableRowGroup trg = new TableRowGroup();

            
            TableRow header = new TableRow();
            header.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Šifra konta")))));
            header.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Naziv konta")))));

            foreach (var cell in header.Cells)
            {
                cell.Padding = new Thickness(4);
                cell.BorderBrush = Brushes.Black;
                cell.BorderThickness = new Thickness(0.5);
                cell.Background = Brushes.LightGray;
            }

            trg.Rows.Add(header);

            
            foreach (var item in data)
            {
                TableRow row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Sifkonta.ToString()))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Nazkont))));

                foreach (var cell in row.Cells)
                {
                    cell.Padding = new Thickness(4);
                    cell.BorderBrush = Brushes.Black;
                    cell.BorderThickness = new Thickness(0.25);
                }

                trg.Rows.Add(row);
            }

            table.RowGroups.Add(trg);

           
            PrintDialog printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                IDocumentPaginatorSource idpSource = doc;
                printDlg.PrintDocument(idpSource.DocumentPaginator, "Štampanje Kontni plan");
            }
        }

    }
}
