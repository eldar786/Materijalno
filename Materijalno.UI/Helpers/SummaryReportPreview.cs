using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Materijalno.Model.Services;
using Materijalno.ViewModel.Services;

namespace Materijalno.UI.Helpers
{
    public static class SummaryReportPreview
    {
        public static void Attach(UserControl control)
        {
            SummaryReportViewModel current = null;
            Action<SummaryReport> show = report => Show(report, Window.GetWindow(control));
            Action connect = () =>
            {
                if (current != null) current.ReportRequested -= show;
                current = control.DataContext as SummaryReportViewModel;
                if (current != null) current.ReportRequested += show;
            };
            control.Loaded += (s, e) => connect();
            control.DataContextChanged += (s, e) => connect();
            control.Unloaded += (s, e) => { if (current != null) current.ReportRequested -= show; current = null; };
        }

        public static void Show(SummaryReport report, Window owner)
        {
            var window = new Window { Title = report.Title, Owner = owner, Width = 1100, Height = 740, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var document = CreateDocument(report);
            var viewer = new FlowDocumentReader { Document = document, ViewingMode = FlowDocumentReaderViewingMode.Scroll, IsFindEnabled = true };
            var root = new DockPanel();
            var print = new Button { Content = "Štampaj…", Padding = new Thickness(14, 7, 14, 7), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8) };
            print.Click += (s, e) =>
            {
                var dialog = new PrintDialog();
                dialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
                if (dialog.ShowDialog() == true) dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, report.Title);
            };
            DockPanel.SetDock(print, Dock.Top); root.Children.Add(print); root.Children.Add(viewer); window.Content = root; window.ShowDialog();
        }

        public static FlowDocument CreateDocument(SummaryReport report)
        {
            var document = new FlowDocument { PageWidth = 1056, PageHeight = 816, ColumnWidth = 996, FontFamily = new FontFamily("Segoe UI"), FontSize = 11, PagePadding = new Thickness(30) };
            document.Blocks.Add(new Paragraph(new Run(report.Title)) { FontSize = 20, FontWeight = FontWeights.Bold });
            document.Blocks.Add(new Paragraph(new Run(report.Period)));
            var table = new Table { CellSpacing = 0 };
            for (int i = 0; i < report.Rows.Columns.Count; i++) table.Columns.Add(new TableColumn());
            var group = new TableRowGroup(); table.RowGroups.Add(group);
            var header = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
            foreach (DataColumn col in report.Rows.Columns) header.Cells.Add(Cell(col.ColumnName));
            group.Rows.Add(header);
            foreach (DataRow source in report.Rows.Rows)
            {
                var row = new TableRow();
                foreach (DataColumn column in report.Rows.Columns)
                {
                    var value = source[column];
                    row.Cells.Add(Cell(value is decimal ? ((decimal)value).ToString(column.ColumnName == "Cijena" ? "N9" : "N2") : value is DateTime ? ((DateTime)value).ToString("dd.MM.yyyy") : Convert.ToString(value)));
                }
                group.Rows.Add(row);
            }
            document.Blocks.Add(table);
            document.Blocks.Add(new Paragraph(new Run("Ukupno: " + report.Total.ToString("N2"))) { FontWeight = FontWeights.Bold });
            if (!string.IsNullOrWhiteSpace(report.Footer)) document.Blocks.Add(new Paragraph(new Run(report.Footer)));
            return document;
        }

        private static TableCell Cell(string value) => new TableCell(new Paragraph(new Run(value ?? ""))) { Padding = new Thickness(4), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 0.5) };
    }
}
