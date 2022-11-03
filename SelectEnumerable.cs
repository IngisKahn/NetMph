using System.Collections;
using System.Runtime.InteropServices;

namespace NetMph;

/// <summary>
/// Does not support lookup
/// </summary>
public sealed unsafe class SelectEnumerable : IDisposable, IEnumerable<uint>
{
    private static readonly byte* rankLookupTable;
    private static readonly byte* highBitRanks;
    static SelectEnumerable()
    {
        static uint CountSetBitsInByte(uint id)
        {
            id -= id >> 1 & 0x55;
            id = (id & 0x33) + (id >> 2 & 0x33);
            return id + (id >> 4) & 0xF;
        }

        SelectEnumerable.rankLookupTable = (byte*)NativeMemory.Alloc((nuint)256);
        for (var i = 0; i < 256; i++)
            SelectEnumerable.rankLookupTable[i] = (byte)CountSetBitsInByte((uint)i);
        SelectEnumerable.highBitRanks = (byte*)NativeMemory.Alloc((nuint)(256u * 8u));
        var pHighRanks = SelectEnumerable.highBitRanks;
        for (var i = 0; i < 256; i++)
        {
            var z = 0;
            for (var j = 0; j < 8; j++)
                if ((i & (1 << j)) != 0)
                {
                    *pHighRanks++ = (byte)j;
                    z++;
                }

            while (z++ < 8)
                *pHighRanks++ = 255;
        }
    }

    private const int stepSelectTableBitCount = 7;

    public static uint SizeEstimate(uint count, uint maxValue)
    {
        var bitCount = count + maxValue;
        var vecSize = (bitCount + 31) >> 5;
        var selTableSize = (count >> stepSelectTableBitCount) + 1;
        return SevenBitIntegerSize(maxValue)
               + vecSize * sizeof(uint)
               + selTableSize * SevenBitIntegerSize(bitCount);
    }

    private static uint SevenBitIntegerSize(uint value) => value switch
    {
        < 1u << 7 => 1,
        < 1u << 14 => 2,
        < 1u << 21 => 3,
        < 1u << 28 => 4,
        <= uint.MaxValue => 5
    };

    public uint Size => SelectEnumerable.SizeEstimate(this.keyCount, this.maxValue);

    /// <summary>
    /// Each bit represents either to increase the counter "0" or that there exists a value equal to counter "1"
    /// </summary>
    //public uint* ValuePresentFlags => valuePresentFlags;

    private static void Insert0(ref uint buffer) => buffer >>= 1;

    private static void Insert1(ref uint buffer) => buffer = buffer >> 1 | 0x80000000;

    private readonly uint keyCount;
    private readonly uint maxValue;

    /// <summary>
    /// Each bit represents either to increase the counter "0" or that there exists a value equal to counter "1"
    /// </summary>
    private readonly uint* valuePresentFlags;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        this.Dispose(true);
    }

    private void Dispose(bool isDisposing)
    {
        if (this.valuePresentFlags != null)
            NativeMemory.Free(this.valuePresentFlags);
    }

    public unsafe class SelectEnumerator : IEnumerator<uint>
    {
        private readonly uint* valuePresentFlags;
        private readonly uint valueCount;
        private uint* currentFlags;
        private uint currentBitIndex;
        private int currentCount;

        public SelectEnumerator(uint* valuePresentFlags, uint valueCount)
        {
            this.valuePresentFlags = valuePresentFlags;
            this.valueCount = valueCount;
            this.Reset();
        }

        public bool MoveNext()
        {
            if (this.currentCount >= this.valueCount)
                return false;
            for (; ; )
            {
                if (this.currentBitIndex == 0)
                {
                    this.currentBitIndex = 32;
                    this.currentFlags++;
                }

                if ((*this.currentFlags & (1u << (int)--this.currentBitIndex)) != 0)
                    return true;
                this.Current++;
            }
        }

        public void Reset()
        {
            this.currentFlags = this.valuePresentFlags;
            this.currentBitIndex = 32;
            this.currentCount = -1;
            this.Current = 0;
        }

        public uint Current { get; private set; }

        object IEnumerator.Current => Current;

        public void Dispose() { }
    }

    public IEnumerator<uint> GetEnumerator() => new SelectEnumerator(this.valuePresentFlags, this.keyCount);

    ~SelectEnumerable() => this.Dispose(false);
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public SelectEnumerable(uint[] keys)
    {
        fixed (uint* pKeys = keys)
        {
            this.keyCount = (uint)keys.Length;
            Generate(pKeys, this.keyCount, out this.maxValue, out this.valuePresentFlags);
        }
    }

    public SelectEnumerable(uint* keys, uint keyCount)
    {
        this.keyCount = keyCount;
        Generate(keys, this.keyCount, out this.maxValue, out this.valuePresentFlags);
    }

    private static void Generate(uint* keys, uint keyCount, out uint maxValue, out uint* valuePresentFlags)
    {
        uint buffer = 0;
        if (keyCount == 0)
        {
            maxValue = 0;
            valuePresentFlags = null;
            return;
        }

        maxValue = keys[keyCount - 1];
        var bitCount = keyCount + maxValue;
        nuint flagsSize = bitCount + 0x1f >> 5;
        valuePresentFlags = (uint*)NativeMemory.Alloc(flagsSize, (nuint)sizeof(uint));
        var vpf = valuePresentFlags;
        int SetFlags(ulong max)
        {
            var flagIndex = 0;
            for (ulong currentValue = 0, keyIndex = 0; ;)
            {
                while (keys[keyIndex] == currentValue)
                {
                    SelectEnumerable.Insert1(ref buffer);
                    flagIndex++;

                    if ((flagIndex & 0x1f) == 0)
                        vpf[(flagIndex >> 5) - 1] = buffer; // (idx >> 5) = idx/32
                    keyIndex++;

                    if (keyIndex == keyCount)
                        return flagIndex;
                }

                if (currentValue == max)
                    break;

                while (keys[keyIndex] > currentValue)
                {
                    SelectEnumerable.Insert0(ref buffer);
                    flagIndex++;

                    if ((flagIndex & 0x1f) == 0) // (idx & 0x1f) = idx % 32
                        vpf[(flagIndex >> 5) - 1] = buffer; // (idx >> 5) = idx/32
                    currentValue++;
                }
            }

            return flagIndex;
        }

        var flagIndex = SetFlags(maxValue);

        if ((flagIndex & 0x1f) == 0)
            return;
        buffer >>= 0x20 - (flagIndex & 0x1f);
        valuePresentFlags[flagIndex - 1 >> 5] = buffer;
    }

    public SelectEnumerable(byte* buffer)
    {
        this.keyCount = (uint)BitBool.Read7BitNumber(ref buffer);
        this.maxValue = (uint)BitBool.Read7BitNumber(ref buffer);
        var nbits = this.keyCount + this.maxValue;
        nuint vecSize = nbits + 0x1f >> 5;
        this.valuePresentFlags = (uint*)NativeMemory.Alloc(vecSize, (nuint)sizeof(uint));
        Buffer.MemoryCopy(buffer, this.valuePresentFlags, vecSize, vecSize);
    }

    public SelectEnumerable(byte* buffer, uint keyCount)
    {
        this.keyCount = keyCount;
        this.maxValue = (uint)BitBool.Read7BitNumber(ref buffer);
        var nbits = this.keyCount + this.maxValue;
        nuint vecSize = nbits + 0x1f >> 5;
        this.valuePresentFlags = (uint*)NativeMemory.Alloc(vecSize, (nuint)sizeof(uint));
        Buffer.MemoryCopy(buffer, this.valuePresentFlags, vecSize, vecSize);
    }
    public SelectEnumerable(BinaryReader reader, uint keyCount)
    {
        this.keyCount = keyCount;
        this.maxValue = (uint)reader.Read7BitEncodedInt();
        var nbits = this.keyCount + this.maxValue;
        nuint vecSize = nbits + 0x1f >> 5;
        this.valuePresentFlags = (uint*)NativeMemory.Alloc(vecSize, (nuint)sizeof(uint));
        reader.Read(new Span<byte>(this.valuePresentFlags, (int)(vecSize * sizeof(uint))));
    }

    public void Write(byte* buffer, ref ulong bufferLength)
    {
        if (buffer == null)
        {
            bufferLength = this.Size;
            return;
        }

        if (bufferLength < this.Size)
            throw new InsufficientMemoryException();
        var uintBuffer = (uint*)buffer;
        *uintBuffer++ = this.keyCount;
        *uintBuffer++ = this.maxValue;
        var nbits = this.keyCount + this.maxValue;
        var vecSize = nbits + 0x1f >> 5;
        Buffer.MemoryCopy(this.valuePresentFlags, uintBuffer, bufferLength - 4, vecSize);
    }

    public void Write(BinaryWriter writer)
    {
        //writer.Write(this.keyCount);
        writer.Write(this.maxValue);
        var nbits = this.keyCount + this.maxValue;
        var vecSize = nbits + 0x1f >> 5;
        writer.Write(new ReadOnlySpan<byte>(this.valuePresentFlags, (int)(vecSize * sizeof(uint))));
    }

    public static IEnumerator<uint> GetEnumeratorPacked(uint* packedSelect)
    {
        var keyCount = *packedSelect;
        return new SelectEnumerator(packedSelect + 2, keyCount);
    }
}