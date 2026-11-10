using Materijalno.Model.Services;
using Materijalno.ViewModel.Services;
namespace Materijalno.ViewModel {
 public class PregledUlazaZaJednuKalkulacijuViewModel : SummaryReportViewModel {
  public PregledUlazaZaJednuKalkulacijuViewModel(GlavniViewModel main) : base(main, SummaryReportKind.ReceiptDocument) { }
 }
}