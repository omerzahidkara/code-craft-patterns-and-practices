/*
 * WindowManager.cs — Uygulama çapında basit pencere yöneticisi
 *
 * Bu sınıf, açılan pencereleri kaydeder ve etkin pencere değişimlerinde odak politikası uygular.
 * Odak Modu açıkken, etkinleşen pencere dışındaki görünür pencereler simge durumuna küçültülür;
 * bir pencere kapandığında, sıradaki (önceki) pencere öne getirilir.
 *
 * İşleyiş:
 *   - Register: Window.Activated ve Window.Closed olaylarına abone olur.
 *   - OnActivated: Etkin pencereyi son sıraya alır; Odak Modu açıksa diğer pencereleri minimize eder.
 *   - OnClosed: Kapanan pencereyi listeden çıkarır; Odak Modu açıksa listedeki son pencereyi öne getirir.
 *
 * Notlar:
 *   - Pencereler LinkedList ile tutulur; sondaki öğe en son etkin olan penceredir.
 *   - LastOrDefault çağrısı için System.Linq kullanılır.
 *   - Aktivasyon ve kapatma olayları WPF UI iş parçacığında gelir; ek kilit gerektirmez.
 *   - Odak Modu kapalıyken OnClosed, herhangi bir pencereyi öne getirmez.
 *
 * Oluşturan: Ömer Zahid Kara
 */

using System.Collections.Generic;
using System.Linq;            // LastOrDefault
using System.Windows;         // Window, Application, WindowState

namespace WpfPractise2
{
    /// <summary>
    /// Uygulama çapında basit pencere yöneticisi.
    /// Pencereleri kayıt eder, Odak Modu özelliğini taşır ve son/önceki pencereyi öne getirebilir.
    /// </summary>
    public sealed class WindowManager
    {
        // Tekil örnek. Uygulama süresi boyunca yaşar.
        public static WindowManager Instance { get; } = new();

        // Odak Modu bayrağı. true olduğunda etkin pencere dışındaki görünür pencereler minimize edilir.
        // Not: Bu bayrağın değiştirilmesi, mevcut açık pencerelere geriye dönük işlem yapmaz;
        //      yeni bir etkinleşme olayı ile politika uygulanır.
        public bool FocusModeEnabled { get; set; } = false;

        // En sonda en son aktif olan dursun (LRU benzeri sıra).
        private readonly LinkedList<Window> _z = new();

        /// <summary>
        /// Bir pencere açıldığında çağrılmalıdır. Aktivasyon ve kapanış olayları izlenir.
        /// </summary>
        public void Register(Window w)
        {
            if (w == null) return;
            w.Activated += (_, __) => OnActivated(w);
            w.Closed += (_, __) => OnClosed(w);
        }

        /// <summary>
        /// Etkin pencere değiştiğinde tetiklenir. Liste sonuna taşır ve gerekiyorsa diğerlerini küçültür.
        /// </summary>
        private void OnActivated(Window w)
        {
            // Varsa eski yerinden çıkar, en sona ekle ("en son etkin" mantığı)
            var node = _z.Find(w);
            if (node != null) _z.Remove(node);
            _z.AddLast(w);

            // Odak Modu: etkin pencere dışındakileri küçült
            if (FocusModeEnabled)
            {
                foreach (Window other in Application.Current.Windows)
                {
                    if (ReferenceEquals(other, w))
                        continue;

                    if (other.IsVisible && other.WindowState != WindowState.Minimized)
                        other.WindowState = WindowState.Minimized;
                }
            }
        }

        /// <summary>
        /// Pencere kapandığında tetiklenir. Listeden çıkarır; Odak Modu aktifse önceki pencereyi öne alır.
        /// </summary>
        private void OnClosed(Window w)
        {
            var node = _z.Find(w);
            if (node != null) _z.Remove(node);

            // Odak Modu kapalıysa, kapanışta herhangi bir pencereyi öne getirme.
            if (!FocusModeEnabled)
                return;

            // Listedeki son (önceki) pencereyi hedefle
            var prev = _z.LastOrDefault();
            if (prev != null)
            {
                // Minimized ise geri aç
                if (prev.WindowState == WindowState.Minimized)
                    prev.WindowState = WindowState.Normal;

                // Öne getir. Topmost bayrağı kısa süreli kullanmak, bazı durumlarda güvenilir aktivasyon sağlar.
                prev.Activate();
                prev.Topmost = true;
                prev.Topmost = false;
            }
        }
    }
}
