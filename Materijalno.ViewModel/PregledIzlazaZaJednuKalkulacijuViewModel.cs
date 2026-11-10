using Materijalno.Model.Services;
using Materijalno.ViewModel.Services;
namespace Materijalno.ViewModel {
 public class PregledIzlazaZaJednuKalkulacijuViewModel : SummaryReportViewModel {
  public PregledIzlazaZaJednuKalkulacijuViewModel(GlavniViewModel main) : base(main, SummaryReportKind.IssueDocument) { }
 }
}