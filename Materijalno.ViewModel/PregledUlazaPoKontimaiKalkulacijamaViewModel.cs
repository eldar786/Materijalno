using Materijalno.Model.Services;
using Materijalno.ViewModel.Services;
namespace Materijalno.ViewModel {
 public class PregledUlazaPoKontimaiKalkulacijamaViewModel : SummaryReportViewModel {
  public PregledUlazaPoKontimaiKalkulacijamaViewModel(GlavniViewModel main) : base(main, SummaryReportKind.ReceiptsByAccount) { }
 }
}