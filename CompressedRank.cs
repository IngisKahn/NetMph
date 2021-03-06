using System.Runtime.InteropServices;

namespace NetMph;

public sealed unsafe class CompressedRank : IDisposable
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
    private uint BitsTableSize => (this.count * this.remainderBitLength + 31u) >> 5;

    private readonly uint maxValue;
    private readonly uint count;
    private readonly uint remainderBitLength;
    private readonly Select sel;
    private readonly uint* valueRemainders;

    public CompressedRank(IReadOnlyList<uint> values) // must be a sorted list of values
    {
        this.count = (uint)values.Count;
        this.maxValue = values[^1];
        this.remainderBitLength = CompressedRank.Log2(this.maxValue / this.count) - 1;
        if (this.remainderBitLength == 0)
            this.remainderBitLength = 1;
        var maxSignificant = this.maxValue >> (int)this.remainderBitLength;
        var selectVector = stackalloc uint[(int)maxSignificant];
        this.valueRemainders = (uint*)NativeMemory.Alloc(this.BitsTableSize, sizeof(uint));
        var remainderMask = (1u << (int)this.remainderBitLength) - 1u;
        for (var i = 0u; i < this.count; i++)
            BitBool.SetBitsValue(this.valueRemainders, i, values[(int)i] & remainderMask, this.remainderBitLength,
                remainderMask);
        for (uint currentValue = 1, valueIndex = 0; currentValue <= maxSignificant; currentValue++)
        {
            while (currentValue > values[(int)valueIndex] >> (int)this.remainderBitLength)
                valueIndex++;
            selectVector[currentValue - 1] = valueIndex;
        }

        this.sel = new(selectVector, this.count);
    }

    public uint GetRank(uint valueIndex)
    {
        if (valueIndex > this.maxValue)
            return this.count;

        var valueSignificant = valueIndex >> (int)this.remainderBitLength;
        var remainderMask = (1u << (int)this.remainderBitLength) - 1u;
        var valueRemainder = valueIndex & remainderMask;
        uint rank, selRes;
        if (valueSignificant == 0)
        {
            selRes = 0;
            rank = 0;
        }
        else
        {
            selRes = this.sel.GetBitIndex(valueSignificant - 1) + 1;
            rank = selRes - valueSignificant;
        }

        for (; ; )
        {
            if (BitBool.GetBit32((byte*)this.sel.ValuePresentFlags, (int)selRes) != 0
                || BitBool.GetBitsValue(this.valueRemainders, rank, this.remainderBitLength, remainderMask) >=
                valueRemainder)
                break;
            selRes++;
            rank++;
        }

        return rank;
    }

    ~CompressedRank() => this.Dispose(false);

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