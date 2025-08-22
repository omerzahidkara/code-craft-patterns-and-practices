/*
 * MainViewModel.cs — Prime Searcher MVVM uygulaması için ViewModel
 * 
 * Bu sınıf, WPF/MVVM mimarisinde uygulamanın UI durumunu ve komutlarını yönetir.
 * Görevleri:
 *   - Kullanıcıdan alınan üst sınır, segment boyutu ve "tüm asalları topla" tercihini tutmak
 *   - İlerlemenin, durum metninin, toplam asal sayısının ve son bulunan asalin UI'ya bildirilmesi
 *   - CPU-yoğun segmentli eleği UI iş parçacığı dışında çalıştırmak ve her segment bittiğinde UI'yı güncellemek
 *   - İptal akışını koordine etmek ve komutların etkinlik durumlarını canlı tutmak
 *   - WindowManager üzerinden "Odak Modu" ayarını expose etmek
 * 
 * İş parçacığı modeli:
 *   - SegmentedSieve işlemi Task.Run ile ThreadPool üzerinde çalıştırılır.
 *   - Her segment tamamlandığında, UI koleksiyonları yalnızca Dispatcher üzerinden güncellenir.
 *   - İptal, CancellationTokenSource aracılığıyla kooperatif biçimde uygulanır.
 * 
 * Notlar:
 *   - AllPrimeChunks özelliği isteğe bağlıdır ve büyük bellek kullanır; CollectPrimes=false iken null bırakılır.
 *   - ObservableCollection, koleksiyon değişimlerini otomatik olarak UI'ya bildirir; OnPropertyChanged gerektirmez.
 * 
 * Oluşturan: Ömer Zahid Kara
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;      // UI'ya koleksiyon değişimini bildiren koleksiyon tipi
using System.ComponentModel;                // INotifyPropertyChanged
using System.Runtime.CompilerServices;      // CallerMemberName
using System.Threading;                     // CancellationToken, CTS
using System.Threading.Tasks;               // Task, Task.Run

namespace WpfPractise2
{
    /// <summary>
    /// Prime Searcher uygulamasının ana ViewModel'i.
    /// UI'dan bağlanan özellikleri ve komutları içerir; segmentli eleği arka planda çalıştırır.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        // WPF Binding'lerinin haberdar edilmesi için standart olay.
        public event PropertyChangedEventHandler? PropertyChanged;

        // Özellik değişimini bildirmek için yardımcı metod. CallerMemberName, çağıranın adını otomatik geçirir.
        void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // Kullanıcının taramak istediği üst sınır. Varsayılan: 20 milyon.
        private int _upperLimit = 20_000_000;

        // UpperLimit özelliği; set sırasında değer değişmişse UI'ya haber verilir ve komut etkinliği yenilenir.
        public int UpperLimit
        {
            get => _upperLimit;
            set
            {
                if (_upperLimit == value) return;        // Aynı değer ise gereksiz bildirim yapılmaz
                _upperLimit = value;
                OnPropertyChanged();                      // Binding'lere değişti sinyali
                StartScanCommand.RaiseCanExecuteChanged(); // Başlat butonunun CanExecute koşulu değişebilir
            }
        }

        // Segment boyutu; çok küçük olması verimsiz, çok büyük olması bellek baskısı oluşturur.
        private int _segmentSize = 1_000_000;
        public int SegmentSize
        {
            get => _segmentSize;
            set
            {
                if (_segmentSize == value) return;
                _segmentSize = value;
                OnPropertyChanged();
            }
        }

        // Tüm asalları (sadece saymak yerine) listeler halinde toplama tercihi. Büyük bellek kullanır.
        private bool _collectPrimes = false;
        public bool CollectPrimes
        {
            get => _collectPrimes;
            set
            {
                if (_collectPrimes == value) return;
                _collectPrimes = value;
                OnPropertyChanged();
            }
        }

        // İlerleme çubuğu değeri; 0..100 aralığında tutulur.
        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }

        // Durum metni; kullanıcıya süreç hakkında bilgi vermek için kullanılır.
        private string _statusText = "Hazır";
        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        // Toplam bulunan asal sayısı; segmentlerden gelen kısmi sayımların toplamıdır.
        private long _totalPrimeCount;
        public long TotalPrimeCount
        {
            get => _totalPrimeCount;
            set
            {
                _totalPrimeCount = value;
                OnPropertyChanged();
            }
        }

        // Son bulunan asal; segmentler ilerledikçe yükselir.
        private int _lastPrime;
        public int LastPrime
        {
            get => _lastPrime;
            set
            {
                _lastPrime = value;
                OnPropertyChanged();
            }
        }

        // UI'ya segment özetlerini göstermek için kullanılan koleksiyon.
        // ObservableCollection, Add/Remove gibi değişimleri otomatik bildirir; UI thread'inde güncellenmelidir.
        public ObservableCollection<PrimeChunk> Chunks { get; } = new();

        // İsteğe bağlı: her segmentin tam asal listesini tutmak için liste listesi.
        // Büyük veri üretebilir; CollectPrimes=false iken hiç ayırmamak için null bırakılır.
        public List<List<int>>? AllPrimeChunks { get; private set; }

        // Mevcut taramanın iptali için CTS. null değer, aktif tarama bulunmadığını ifade eder.
        private CancellationTokenSource? _cts;

        // UI komutları. AsyncRelayCommand, async/await akışı ile uyumludur; RelayCommand senkron eylemler içindir.
        public AsyncRelayCommand StartScanCommand { get; }
        public RelayCommand CancelScanCommand { get; }
        public AsyncRelayCommand FakeIoCommand { get; }

        // Kurucu: komutları bağlar, odak modunu senkronlar ve komutların CanExecute durumunu property değişimlerine bağlar.
        public MainViewModel()
        {
            // Başlat komutu: yalnızca iptal kaynağı yokken ve üst sınır yeterince büyükken etkin olsun.
            StartScanCommand = new AsyncRelayCommand(StartScanAsync, () => _cts == null && UpperLimit >= 100_000);

            // İptal komutu: bir tarama aktifken (CTS var iken) etkin olur.
            CancelScanCommand = new RelayCommand(_ => _cts?.Cancel(), _ => _cts != null);

            // Deneme amaçlı sahte I/O gecikmesi; UI donmadan beklemeyi gösterir.
            FakeIoCommand = new AsyncRelayCommand(FakeIoAsync);

            // UI'daki Odak Modu anahtarını, WindowManager'ın mevcut durumu ile eşitle.
            FocusModeEnabled = WindowManager.Instance.FocusModeEnabled;

            // Herhangi bir property değişiminde komutların etkinlik durumunu tazele.
            PropertyChanged += (_, __) =>
            {
                StartScanCommand.RaiseCanExecuteChanged();
                CancelScanCommand.RaiseCanExecuteChanged();
            };

            // Başlangıçta FocusModeEnabled değerini UI'ya yansıtmak için bildirim tetikle.
            OnPropertyChanged(nameof(FocusModeEnabled));
        }

        /// <summary>
        /// Segmentli eleği başlatır, ilerlemeyi ve segment özetlerini UI üzerinde günceller.
        /// CPU-yoğun iş, UI iş parçacığını bloklamamak için Task.Run ile arka planda yürütülür.
        /// </summary>
        async Task StartScanAsync()
        {
            // UI başlangıç durumu: önceki sonuçları temizle ve göstergeleri sıfırla.
            _cts = new CancellationTokenSource();
            Chunks.Clear();
            AllPrimeChunks = CollectPrimes ? new List<List<int>>() : null; // İsteniyorsa tüm asallar için dış liste ayrılır.
            ProgressValue = 0;
            TotalPrimeCount = 0;
            LastPrime = 0;
            StatusText = "Ön hazırlık...";

            // İlerleme bildirimi, oluşturulduğu bağlamda (UI) marshal edilir.
            var progress = new Progress<int>(p => ProgressValue = p);

            try
            {
                // CPU-yoğun işi ThreadPool'a ver. Parametresiz lambda, SegmentedSieve çağrısını kapsar.
                await Task.Run(() =>
                    SegmentedSieve(
                        UpperLimit,
                        SegmentSize,
                        CollectPrimes,
                        chunk =>
                        {
                            // Her segment tamamlandığında UI'ya küçük bir özet basılır.
                            // UI koleksiyonları yalnızca UI iş parçacığından güncellenmelidir.
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                Chunks.Add(chunk);
                                TotalPrimeCount += chunk.Count;
                                if (chunk.LastPrime > 0)
                                    LastPrime = chunk.LastPrime;
                            });
                        },
                        progress,
                        _cts.Token), _cts.Token);

                StatusText = "Bitti."; // Normal tamamlanma durumu.
            }
            catch (OperationCanceledException)
            {
                StatusText = "İptal edildi."; // İptal akışı.
            }
            finally
            {
                // Kaynakları serbest bırak ve komut etkinliklerini yenile.
                _cts.Dispose();
                _cts = null;
                StartScanCommand.RaiseCanExecuteChanged();
                CancelScanCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Örnek amaçlı kısa süreli bekleme. UI'yi bloklamadan beklenir.
        /// </summary>
        async Task FakeIoAsync()
        {
            StatusText = "Sahte I/O bekleniyor...";
            await Task.Delay(1500);
            StatusText = "Sahte I/O bitti.";
        }

        /// <summary>
        /// Segmentli Eratosthenes Eleği. Büyük aralıkları, daha küçük segmentlerde işleyerek bellek tüketimini sınırlar.
        /// basePrimes, yalnızca √N'e kadar olan asallardır; her segment içinde bu tabanların katları işaretlenir.
        /// </summary>
        static void SegmentedSieve(
            int upperLimit,
            int segmentSize,
            bool collectPrimes,
            Action<PrimeChunk> onChunkReady,
            IProgress<int> progress,
            CancellationToken ct)
        {
            // 2'den küçük üst sınırlarda iş yoktur; ilerleme tamamlandı olarak bildirilebilir.
            if (upperLimit < 2)
            {
                progress.Report(100);
                return;
            }

            // Segment boyutunu makul aralığa zorlamak verimi artırır ve bellek taşmalarını engeller.
            if (segmentSize < 64_000) segmentSize = 64_000;
            if (segmentSize > upperLimit) segmentSize = upperLimit;

            // 1) Klasik elek ile √N'e kadar olan taban asalları bir kez hesapla.
            int limit = (int)Math.Floor(Math.Sqrt(upperLimit));
            var basePrimes = SimpleSieve(limit, ct);

            // 2) Pencereyi [low..high] aralığında segment segment gez.
            int chunkIndex = 0;
            for (int low = 2; low <= upperLimit; low += segmentSize)
            {
                ct.ThrowIfCancellationRequested();

                int high = Math.Min(low + segmentSize - 1, upperLimit);
                int len = high - low + 1;

                // Bu segmentteki bileşikleri işaretlemek için boolean dizi.
                // Haritalama: sayı n  →  index = n - low
                var isComposite = new bool[len];

                // Taban asallar ile işaretleme.
                foreach (int p in basePrimes)
                {
                    long p2 = (long)p * p;      // p*p, büyük N'lerde taşmayı önlemek için long tutulur.
                    if (p2 > high) break;       // p^2 bu segmentin üstünde ise bu segmentte işaretleme yapmaya gerek yoktur.

                    // Bu segment için başlangıç katını belirle.
                    // Kural: p^2'den küçük katlar zaten daha küçük asal çarpanlarla işaretlenmiştir;
                    // bu nedenle işaretlemeye en az p^2'den başlanır. Segmentin içinde görünür ilk kat için ceil(low/p)*p alınır.
                    long start = Math.Max(p2, ((low + p - 1) / p) * (long)p);

                    // start, start+p, start+2p, ... şeklinde tüm katları bileşik olarak işaretle.
                    for (long m = start; m <= high; m += p)
                        isComposite[m - low] = true;
                }

                // Segment özeti. Count ve LastPrime, aşağıdaki tarama ile doldurulur.
                var chunk = new PrimeChunk
                {
                    ChunkIndex = ++chunkIndex,
                    Start = low,
                    End = high,
                    Count = 0,
                    LastPrime = 0
                };

                // İsteğe bağlı olarak, bu segmentte bulunan asalların tamamını tutmak için liste ayır.
                List<int>? primesThisChunk = collectPrimes ? new List<int>() : null;

                // 2 sayısı teklerden ayrı ele alınır. Aşağıdaki döngü yalnızca tek sayıları gezecektir.
                if (low == 2)
                {
                    if (2 <= high)
                    {
                        chunk.Count++;
                        chunk.LastPrime = 2;
                        primesThisChunk?.Add(2);
                    }
                }

                // 3'ten başlamak üzere yalnızca tek sayıları kontrol et. Even-skip ile işlem yarı yarıya azalır.
                int startIdx = Math.Max(low, 3);
                if ((startIdx & 1) == 0) startIdx++; // çiftse bir sonraki tek sayıya kaydır

                for (int n = startIdx; n <= high; n += 2)
                {
                    int idx = n - low;
                    if (!isComposite[idx])
                    {
                        chunk.Count++;
                        chunk.LastPrime = n;      // n monoton arttığı için son asal güvenle güncellenir.
                        primesThisChunk?.Add(n);
                    }
                }

                // Tüm asal listeleri tutulmak isteniyorsa, ViewModel içindeki büyük listeye eklenir.
                // UI koleksiyonları UI iş parçacığından erişilmelidir.
                if (collectPrimes && primesThisChunk != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        if (App.Current.MainWindow?.DataContext is MainViewModel vm && vm.AllPrimeChunks != null)
                            vm.AllPrimeChunks.Add(primesThisChunk);
                    });
                }

                // Segment özeti dışarıya bildirilir. ViewModel, Chunks koleksiyonuna ekleyip sayaçları günceller.
                onChunkReady(chunk);

                // Yüzde hesaplaması segment üst sınırına göre yapılır.
                int percent = (int)((long)high * 100 / upperLimit);
                if (percent > 100) percent = 100;
                progress.Report(percent);
            }

            // Döngü tamamlandığında ilerleme %100 olarak bildirilir.
            progress.Report(100);
        }

        /// <summary>
        /// Klasik Eratosthenes Eleği ile 2..n aralığındaki asalları döndürür. p*p kuralı uygulanır.
        /// </summary>
        static List<int> SimpleSieve(int n, CancellationToken ct)
        {
            var mark = new bool[n + 1];
            var primes = new List<int>();

            for (int p = 2; p * p <= n; p++)
            {
                ct.ThrowIfCancellationRequested();
                if (!mark[p])
                {
                    for (int m = p * p; m <= n; m += p)
                        mark[m] = true;
                }
            }

            for (int i = 2; i <= n; i++)
                if (!mark[i]) primes.Add(i);

            return primes;
        }

        // Odak modu ayarını WindowManager üzerinden expose eden proxy özellik.
        // Getter, WindowManager'daki güncel değeri döndürür; setter, değeri günceller ve UI'ya bildirir.
        public bool FocusModeEnabled
        {
            get => WindowManager.Instance.FocusModeEnabled;
            set
            {
                if (WindowManager.Instance.FocusModeEnabled == value) return;
                WindowManager.Instance.FocusModeEnabled = value;
                OnPropertyChanged();
            }
        }
    }
}
