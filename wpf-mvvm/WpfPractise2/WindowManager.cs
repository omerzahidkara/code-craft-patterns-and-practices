

using System.Windows;


namespace WpfPractise2
{
    public sealed class WindowManager
    {
        public static WindowManager Instance { get; } = new();

        // Ayardan bağlanacak flag
        public bool FocusModeEnabled { get; set; } = false;

        // En sonda en son aktif olan dursun
        private readonly LinkedList<Window> _z = new();

        public void Register(Window w)
        {
            w.Activated += (_, __) => OnActivated(w);
            w.Closed += (_, __) => OnClosed(w);
        }

        private void OnActivated(Window w)
        {
            var node = _z.Find(w);
            if (node != null) _z.Remove(node);
            _z.AddLast(w);

            if (FocusModeEnabled)
            {
                foreach (Window other in Application.Current.Windows)
                {
                    if (other == w) continue;
                    if (other.IsVisible && other.WindowState != WindowState.Minimized)
                        other.WindowState = WindowState.Minimized;
                }
            }
        }
        private void OnClosed(Window w)
        {
            var node = _z.Find(w);
            if (node != null) _z.Remove(node);

            // Odak Modu KAPALIYSA hiçbir şey yapma (öncekiyi öne getirme)
            if (!FocusModeEnabled) return;

            var prev = _z.LastOrDefault();
            if (prev != null)
            {
                if (prev.WindowState == WindowState.Minimized)
                    prev.WindowState = WindowState.Normal;

                prev.Activate();
                prev.Topmost = true; prev.Topmost = false;
            }
        }

    }
}
