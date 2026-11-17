using System;

namespace Materijalno.ViewModel.Services
{
    public interface IActivePrometEntry
    {
        bool HasUnsavedEntry { get; }
    }

    /// <summary>Keeps a pending entry protected even while an internal lookup is open.</summary>
    public sealed class PrometNavigationGuard
    {
        public const string Message = "Molimo unesite sva obavezna polja i kliknite SNIMI ili odustanite od unosa klikom na ODUSTANI prije otvaranja druge opcije iz menija.";
        private readonly Action<string> _notifyBlocked;
        private IActivePrometEntry _activeEntry;

        public PrometNavigationGuard(Action<string> notifyBlocked)
        {
            _notifyBlocked = notifyBlocked ?? throw new ArgumentNullException(nameof(notifyBlocked));
        }

        public void TrackPage(object page)
        {
            var entry = page as IActivePrometEntry;
            if (entry != null)
                _activeEntry = entry;
            else if (_activeEntry == null || !_activeEntry.HasUnsavedEntry)
                _activeEntry = null;
        }

        public bool TryNavigate(Action openPage)
        {
            if (_activeEntry != null && _activeEntry.HasUnsavedEntry)
            {
                _notifyBlocked(Message);
                return false;
            }
            openPage();
            return true;
        }
    }
}
