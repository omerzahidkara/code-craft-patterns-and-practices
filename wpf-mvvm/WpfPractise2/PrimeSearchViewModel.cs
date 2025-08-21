using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WpfPractise2
{
    public class PrimeSearchViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly MainViewModel _main;

        public ObservableCollection<int> AbovePrimes { get; } = new();
        public ObservableCollection<int> BelowPrimes { get; } = new();

        private string _queryText = "";
        public string QueryText { get => _queryText; set { _queryText = value; OnPropertyChanged(); } }

        private string _infoText = "Sayı girin ve Ara'ya basın.";
        public string InfoText { get => _infoText; set { _infoText = value; OnPropertyChanged(); } }

        private string _nearestInfo = "";
        public string NearestInfo { get => _nearestInfo; set { _nearestInfo = value; OnPropertyChanged(); } }

        public ICommand SearchCommand { get; }
        public ICommand First99Command { get; }
        public ICommand ClearCommand { get; }

        public PrimeSearchViewModel(MainViewModel main)
        {
            _main = main;
            SearchCommand = new RelayCommand(_ => DoSearch(), _ => true);
            First99Command = new RelayCommand(_ => ShowFirst99(), _ => true);
            ClearCommand = new RelayCommand(_ => ClearPanel(), _ => true);

            // Başlangıçta BOŞ (artık otomatik ilk 99 yok)
            ClearPanel();
        }

        void ClearPanel()
        {
            AbovePrimes.Clear();
            BelowPrimes.Clear();
            NearestInfo = "";
            InfoText = "Sayı girin ve Ara'ya basın.";
        }

        void ShowFirst99()
        {
            // İsteyen tek tuşla ilk 99 asalı görebilsin
            var first = _main.CollectPrimes && _main.AllPrimeChunks != null && _main.AllPrimeChunks.Count > 0
                ? EnumerateAllPrimes().Take(99).ToList()
                : SimpleSieveUpToN(600).Take(99).ToList(); // 99. asal ~523

            // Merkez yok; iki sütuna bölelim: 50 üst – 49 alt (veya kalan)
            AbovePrimes.Clear(); BelowPrimes.Clear();
            var left = first.Take(50);
            var right = first.Skip(50);
            foreach (var p in left) AbovePrimes.Add(p);
            foreach (var p in right) BelowPrimes.Add(p);

            InfoText = "İlk 99 asal gösteriliyor (merkez yok).";
            NearestInfo = "";
        }

        void DoSearch()
        {
            // Boş veya geçersiz giriş, boş panel
            if (!int.TryParse(QueryText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 2)
            {
                ClearPanel();
                InfoText = "Geçerli bir sayı girin (≥ 2).";
                return;
            }

            if (n > _main.UpperLimit)
            {
                ClearPanel();
                InfoText = $"Sayı üst sınırı aşıyor (Limit: {_main.UpperLimit:n0}).";
                return;
            }

            int nearestPrime, below, above;
            List<int> aboveList;
            List<int> belowList;

            if (_main.CollectPrimes && _main.AllPrimeChunks != null && _main.AllPrimeChunks.Count > 0)
            {
                // Kayıtlı asallardan en yakını bulup etrafını topla (streaming; komple liste çıkarmıyoruz)
                FindAroundFromCollected(n, out nearestPrime, out below, out above,
                                        out belowList, out aboveList, wantEachSide: 60);
            }
            else
            {
                // Yerel aralıkta segmentli elek ile en yakını ve etrafını bul
                FindAroundByLocalSieving(n, _main.UpperLimit, out nearestPrime, out below, out above,
                                         out belowList, out aboveList, wantEachSide: 60);
            }

            // Paneli doldur: sol üst, sağ alt . Her iki listeyi de artan sırayla gösteriyoruz.
            AbovePrimes.Clear();
            BelowPrimes.Clear();
            foreach (var p in aboveList) AbovePrimes.Add(p);
            foreach (var p in belowList) BelowPrimes.Add(p);

            InfoText = "Sonuçlar listelendi.";
            NearestInfo = $"En yakın asal: {nearestPrime:n0}  (alt: {(below > 0 ? below.ToString() : "-")}, üst: {(above > 0 ? above.ToString() : "-")})";
        }

        // collected: tüm asallar akışı üzerinden arama 
        IEnumerable<int> EnumerateAllPrimes()
        {
            foreach (var list in _main.AllPrimeChunks!)
                foreach (var p in list)
                    yield return p;
        }

        void FindAroundFromCollected(int n, out int centerPrime, out int below, out int above,
                                     out List<int> belowList, out List<int> aboveList, int wantEachSide)
        {
            centerPrime = -1; below = -1; above = -1;
            belowList = new List<int>(wantEachSide);
            aboveList = new List<int>(wantEachSide);

            // önce en yakın asal’ı bul
            int last = -1;
            var enumerator = EnumerateAllPrimes().GetEnumerator();

            while (enumerator.MoveNext())
            {
                int p = enumerator.Current;
                if (p == n)
                {
                    centerPrime = p;
                    below = last;
                    // üst tarafı doldur
                    while (enumerator.MoveNext() && aboveList.Count < wantEachSide)
                        aboveList.Add(enumerator.Current);
                    break;
                }
                if (p > n)
                {
                    above = p;
                    below = last;
                    if (last < 0) { centerPrime = p; }
                    else centerPrime = (n - last <= p - n) ? last : p;

                    // center p değilse, enumerator şu anda 'p' de; üstü doldur
                    if (centerPrime >= p)
                    {
                        // center last ise, p üst olarak ilk eleman
                        aboveList.Add(p);
                        while (enumerator.MoveNext() && aboveList.Count < wantEachSide)
                            aboveList.Add(enumerator.Current);
                    }
                    else
                    {
                        // center p ise: üst listesi enumerator’dan devam
                        while (enumerator.MoveNext() && aboveList.Count < wantEachSide)
                            aboveList.Add(enumerator.Current);
                    }
                    break;
                }
                last = p;

                // aşağı tamponunu K-elemanlı tut (son wantEachSide tane)
                belowList.Add(p);
                if (belowList.Count > wantEachSide)
                    belowList.RemoveAt(0);
            }

            // en yakın bulunmadıysa (n son asalın üstünde ama limit içinde)
            if (centerPrime < 0)
            {
                centerPrime = last;
                below = last;
            }

            // belowList şu anda artan sırada son K; onu artan tutuyoruz (UI da artan gösterir)
        }

        // segmentli elek ile arama 
        void FindAroundByLocalSieving(int n, int upperLimit, out int centerPrime, out int below, out int above,
                                      out List<int> belowList, out List<int> aboveList, int wantEachSide)
        {
            centerPrime = -1; below = -1; above = -1;
            belowList = new List<int>(wantEachSide);
            aboveList = new List<int>(wantEachSide);

            int half = 5_000;
            while (true)
            {
                int low = Math.Max(2, n - half);
                int high = Math.Min(upperLimit, n + half);
                var box = PrimesInRange(low, high);

                if (box.Count >= wantEachSide * 2 + 4 || (low == 2 && high == upperLimit))
                {
                    int pos = box.BinarySearch(n);
                    int centerIdx;
                    if (pos >= 0) { centerPrime = box[pos]; centerIdx = pos; }
                    else
                    {
                        int ins = ~pos;
                        above = ins < box.Count ? box[ins] : -1;
                        below = ins > 0 ? box[ins - 1] : -1;
                        if (below < 0) { centerPrime = above; centerIdx = ins; }
                        else if (above < 0) { centerPrime = below; centerIdx = ins - 1; }
                        else
                        {
                            bool takeBelow = (n - below <= above - n);
                            centerPrime = takeBelow ? below : above;
                            centerIdx = takeBelow ? ins - 1 : ins;
                        }
                    }

                    // alt taraf: centerIdx’ten geriye doğru wantEachSide kadar
                    int startBelow = Math.Max(0, centerIdx - wantEachSide);
                    belowList = box.Skip(startBelow).Take(centerIdx - startBelow).ToList();

                    // üst taraf: centerIdx’ten sonraki wantEachSide kadar
                    int countAbove = Math.Min(wantEachSide, box.Count - (centerIdx + 1));
                    aboveList = box.Skip(centerIdx + 1).Take(countAbove).ToList();

                    // alt/üst eksikse aralığı büyüt
                    if ((belowList.Count < wantEachSide || aboveList.Count < wantEachSide) &&
                        !(low == 2 && high == upperLimit))
                    {
                        half *= 2;
                        continue;
                    }

                    // Bilgi için en yakın komşuları güncelle
                    if (below < 0 && belowList.Count > 0) below = belowList.Last();
                    if (above < 0 && aboveList.Count > 0) above = aboveList.First();
                    return;
                }
                half = Math.Min(half * 2, Math.Max(upperLimit, 10));
            }
        }

        // ---------- yardımcı elek fonksiyonları ----------
        static List<int> PrimesInRange(int low, int high)
        {
            if (low > high) return new List<int>();
            int limit = (int)Math.Floor(Math.Sqrt(high));
            var basePrimes = SimpleSieveUpToN(limit);

            int len = high - low + 1;
            var composite = new bool[len];

            foreach (var p in basePrimes)
            {
                long p2 = (long)p * p;
                if (p2 > high) break;
                long start = Math.Max(p2, ((low + p - 1) / p) * (long)p);
                for (long m = start; m <= high; m += p)
                    composite[m - low] = true;
            }

            var list = new List<int>();
            if (low <= 2 && 2 <= high) list.Add(2);
            int startN = Math.Max(low, 3);
            if ((startN & 1) == 0) startN++;

            for (int nn = startN; nn <= high; nn += 2)
                if (!composite[nn - low]) list.Add(nn);

            return list;
        }

        static List<int> SimpleSieveUpToN(int n)
        {
            var mark = new bool[n + 1];
            var primes = new List<int>();
            for (int p = 2; p * p <= n; p++)
                if (!mark[p])
                    for (int m = p * p; m <= n; m += p) mark[m] = true;
            for (int i = 2; i <= n; i++) if (!mark[i]) primes.Add(i);
            return primes;
        }
    }
}
