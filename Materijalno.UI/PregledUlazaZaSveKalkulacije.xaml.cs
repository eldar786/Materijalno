using System.Windows.Controls;
namespace Materijalno.UI {
 public partial class PregledUlazaZaSveKalkulacije : UserControl {
  public PregledUlazaZaSveKalkulacije() { InitializeComponent(); Helpers.SummaryReportPreview.Attach(this); }
 }
}