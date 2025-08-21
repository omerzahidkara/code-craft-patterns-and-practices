using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;


namespace WpfPractise2
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private int _upperLimit = 20_000_000;
        public int UpperLimit { get => _upperLimit; set { if (_upperLimit == value) return; _upperLimit = value; OnPropertyChanged(); StartScanCommand.RaiseCanExecuteChanged(); } }

        private int _segmentSize = 1_000_000;
        public int SegmentSize { get => _segmentSize; set { if (_segmentSize == value) return; _segmentSize = value; OnPropertyChanged(); } }

        private bool _collectPrimes = false;
        public bool CollectPrimes { get => _collectPrimes; set { if (_collectPrimes == value) return; _collectPrimes = value; OnPropertyChanged(); } }

        private int _progressValue;
        public int ProgressValue { get => _progressValue; set { _progressValue = value; OnPropertyChanged(); } }

        private string _statusText = "Hazır";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

        private long _totalPrimeCount;
        public long TotalPrimeCount { get => _totalPrimeCount; set { _totalPrimeCount = value; OnPropertyChanged(); } }

        private int _lastPrime;
        public int LastPrime { get => _lastPrime; set { _lastPrime = value; OnPropertyChanged(); } }

        public ObservableCollection<PrimeChunk> Chunks { get; } = new();

        // İsteğe bağlı: tüm asallar (liste listesi). Büyük bellek kullanır!
        public List<List<int>>? AllPrimeChunks { get; private set; }

        private CancellationTokenSource? _cts;

        public AsyncRelayCommand StartScanCommand { get; }
        public RelayCommand CancelScanCommand { get; }
        public AsyncRelayCommand FakeIoCommand { get; }


        public MainViewModel()
        {
            StartScanCommand = new AsyncRelayCommand(StartScanAsync, () => _cts == null && UpperLimit >= 100_000);
            CancelScanCommand = new RelayCommand(_ => _cts?.Cancel(), _ => _cts != null);
            FakeIoCommand = new AsyncRelayCommand(FakeIoAsync);

            // UI’daki Odak Modu anahtarını, WindowManager’ın mevcut durumu ile senkronla
            FocusModeEnabled = WindowManager.Instance.FocusModeEnabled;

            PropertyChanged += (_, __) =>
            {
                StartScanCommand.RaiseCanExecuteChanged();
                CancelScanCommand.RaiseCanExecuteChanged();
            };

            OnPropertyChanged(nameof(FocusModeEnabled));

        }

        async Task StartScanAsync()
        {
            // UI reset
            _cts = new CancellationTokenSource();
            Chunks.Clear();
            AllPrimeChunks = CollectPrimes ? new List<List<int>>() : null;
            ProgressValue = 0;
            TotalPrimeCount = 0;
            LastPrime = 0;
            StatusText = "Ön hazırlık...";

            var progress = new Progress<int>(p => ProgressValue = p);

            try
            {
                // CPU-bound işi ThreadPool'a ver
                await Task.Run(() =>
                    SegmentedSieve(UpperLimit, SegmentSize, CollectPrimes, chunk =>
                    {
                        // her chunk bittiğinde UI'ya küçük bir özet bas
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            Chunks.Add(chunk);
                            TotalPrimeCount += chunk.Count;
                            if (chunk.LastPrime > 0) LastPrime = chunk.LastPrime;
                        });
                    },
                    progress, _cts.Token), _cts.Token);

                StatusText = "Bitti.";
            }
            catch (OperationCanceledException)
            {
                StatusText = "İptal edildi.";
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                StartScanCommand.RaiseCanExecuteChanged();
                CancelScanCommand.RaiseCanExecuteChanged();
            }
        }

        async Task FakeIoAsync()
        {
            StatusText = "Sahte I/O bekleniyor...";
            await Task.Delay(1500);
            StatusText = "Sahte I/O bitti.";
        }

        /// <summary>
        /// Segmentli Eratosthenes Eleği
        /// </summary>
        static void SegmentedSieve(
            int upperLimit,
            int segmentSize,
            bool collectPrimes,
            Action<PrimeChunk> onChunkReady,
            IProgress<int> progress,
            CancellationToken ct)
        {
            if (upperLimit < 2) { progress.Report(100); return; }

            // segment boyutunu makul aralığa zorla
            if (segmentSize < 64_000) segmentSize = 64_000;
            if (segmentSize > upperLimit) segmentSize = upperLimit;

            // 1) sqrt(N)'e kadar küçük asalları bul (tek seferlik klasik elek)
            int limit = (int)Math.Floor(Math.Sqrt(upperLimit));
            var basePrimes = SimpleSieve(limit, ct);

            // 2) segment segment tara
            int chunkIndex = 0;
            for (int low = 2; low <= upperLimit; low += segmentSize)
            {
                ct.ThrowIfCancellationRequested();

                int high = Math.Min(low + segmentSize - 1, upperLimit);
                int len = high - low + 1;

                var isComposite = new bool[len];

                // base asallar ile işaretleme
                foreach (int p in basePrimes)
                {
                    long p2 = (long)p * p;
                    if (p2 > high) break;

                    // bu segment için başlangıç katı
                    long start = Math.Max(p2, ((low + p - 1) / p) * (long)p);

                    for (long m = start; m <= high; m += p)
                        isComposite[m - low] = true;
                }

                var chunk = new PrimeChunk
                {
                    ChunkIndex = ++chunkIndex,
                    Start = low,
                    End = high,
                    Count = 0,
                    LastPrime = 0
                };

                List<int>? primesThisChunk = collectPrimes ? new List<int>() : null;

                // asalları topla / say
                if (low == 2) // 2 özel durumu
                {
                    if (2 <= high)
                    {
                        chunk.Count++;
                        chunk.LastPrime = 2;
                        primesThisChunk?.Add(2);
                    }
                }

                int startIdx = Math.Max(low, 3); // 2 dışında teklerden başla
                if ((startIdx & 1) == 0) startIdx++;

                for (int n = startIdx; n <= high; n += 2)
                {
                    if (!isComposite[n - low])
                    {
                        chunk.Count++;
                        chunk.LastPrime = n;
                        primesThisChunk?.Add(n);
                    }
                }

                // liste listesi isteniyorsa, ViewModel içindeki AllPrimeChunks'a ekle
                if (collectPrimes && primesThisChunk != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        if (App.Current.MainWindow?.DataContext is MainViewModel vm && vm.AllPrimeChunks != null)
                            vm.AllPrimeChunks.Add(primesThisChunk);
                    });
                }

                onChunkReady(chunk);

                int percent = (int)((long)high * 100 / upperLimit);
                if (percent > 100) percent = 100;
                progress.Report(percent);
            }

            progress.Report(100);
        }

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
