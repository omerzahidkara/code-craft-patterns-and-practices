namespace WpfPractise2
{
    /// <summary>
    /// Bir segment taramasının özetini UI'ya taşımak için kullanılan veri sınıfı.
    /// </summary>
    public sealed class PrimeChunk
    {
        public int ChunkIndex { get; set; } // 1'den başlayarak artan segment sırası
        public int Start { get; set; } // Segmentin başlangıç değeri (low)
        public int End { get; set; } // Segmentin bitiş değeri (high)
        public int Count { get; set; } // Bu segmentte bulunan asal sayısı
        public int LastPrime { get; set; } // Bu segmentteki en büyük asal


        public override string ToString()
        => $"#{ChunkIndex} [{Start}..{End}] count={Count} last={LastPrime}";
    }
}
