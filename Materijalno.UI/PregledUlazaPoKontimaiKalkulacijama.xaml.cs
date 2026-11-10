using System.Windows.Controls;
namespace Materijalno.UI {
 public partial class PregledUlazaPoKontimaiKalkulacijama : UserControl {
  public PregledUlazaPoKontimaiKalkulacijama() { InitializeComponent(); Helpers.SummaryReportPreview.Attach(this); }
 }
}