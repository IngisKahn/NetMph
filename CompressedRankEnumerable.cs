using System.Collections;
using System.Runtime.InteropServices;
using static NetMph.SelectEnumerable;

namespace NetMph;

public sealed unsafe class CompressedRankEnumerable : IDisposable, IEnumerable<uint>
{
    private static uint Log2(uint x)
    {
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        x -= x >> 1 & 0x55555555;
        x = (x & 0x33333333) + (x >> 2 & 0x33333333);
        return (x + (x >> 4) & 0x0F0F0F0F) * 0x01010101 >> 24;
    }

    public uint Size => this.sel.Size + this.BitsTableSize * sizeof(uint) * 8 + 3 * sizeof(uint) * 8;
    private uint BitsTableSize => (this.count * 2 + 31u) >> 5;

    //private readonly uint maxValue;
    private readonly uint count;
    //private readonly uint remainderBitLength;
    private readonly SelectEnumerable sel;
    private readonly uint* valueRemainders;

    public CompressedRankEnumerable(IReadOnlyList<uint> values) // must be a sorted list of values
    {
        this.count = (uint)values.Count;
        var maxValue = values[^1];
        //this.remainderBitLength = CompressedRankEnumerable.Log2((this.maxValue + this.count) / this.count);
        //if (this.remainderBitLength == 0)
        //    this.remainderBitLength = 1;
        var maxSignificant = maxValue >> 2;
        var selectVector = stackalloc uint[(int)maxSignificant];
        this.valueRemainders = (uint*)NativeMemory.Alloc(this.BitsTableSize, sizeof(uint));
        const uint remainderMask = 3u;
        for (var i = 0u; i < this.count; i++)
            BitBool.SetBitsValue(this.valueRemainders, i, values[(int)i] & remainderMask, 2,
                remainderMask);
        for (uint currentValue = 1, valueIndex = 0; currentValue <= maxSignificant; currentValue++)
        {
            while (currentValue > values[(int)valueIndex] >> 2)
                valueIndex++;
            selectVector[currentValue - 1] = valueIndex;
        }

        this.sel = new(selectVector, maxSignificant);
    }

    public unsafe class RankEnumerator : IEnumerator<uint>
    {
        private readonly uint* remainders;
        private readonly IEnumerator<uint> selectEnumerator;
        private uint* currentRemainder;
        private uint currentSignificant;
        private uint valueIndex;
        private int bitPosition;

        public RankEnumerator(uint* remainders, IEnumerator<uint> selectEnumerator)
        {
            this.remainders = remainders;
            this.selectEnumerator = selectEnumerator;
        }

        public bool MoveNext()
        {
            //if (this.valueIndex == 0)
            //{
            //    if (!this.selectEnumerator.MoveNext())
            //        return false;
            //    this.valueIndex
            //}



            return true;
        }

        public void Reset()
        {
            this.currentSignificant = 0;
            this.valueIndex = 0;
            this.bitPosition = 32;
            this.Current = 0;
            this.currentRemainder = this.remainders;
            this.selectEnumerator.Reset();
        }

        public uint Current { get; private set; }

        object IEnumerator.Current => Current;

        public void Dispose() => this.selectEnumerator.Dispose();
    }


    public IEnumerator<uint> GetEnumerator() => new RankEnumerator(this.valueRemainders, this.sel.GetEnumerator());

    ~CompressedRankEnumerable() => this.Dispose(false);
   IEnumerator IEnumerable.GetEnumerator()
   {
       return GetEnumerator();
   }

   public void Dispose()
    {
        GC.SuppressFinalize(this);
        this.Dispose(true);
    }

    private void Dispose(bool isDisposing)
    {
        if (isDisposing)
            this.sel.Dispose();
        NativeMemory.Free(this.valueRemainders);
    }
}