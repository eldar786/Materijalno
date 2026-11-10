using System.Windows.Controls;
namespace Materijalno.UI {
 public partial class RekapitulacijaTroskova : UserControl {
  public RekapitulacijaTroskova() { InitializeComponent(); Helpers.SummaryReportPreview.Attach(this); }
 }
}