using Materijalno.Model.Services;
using Materijalno.ViewModel.Services;
namespace Materijalno.ViewModel {
 public class PregledUlazaZaSveKalkulacijeViewModel : SummaryReportViewModel {
  public PregledUlazaZaSveKalkulacijeViewModel(GlavniViewModel main) : base(main, SummaryReportKind.Receipts) { }
 }
}