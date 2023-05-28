using System.Collections;
using System.Numerics;
using System.Runtime.InteropServices;

namespace NetMph;

public sealed unsafe class BitCounter : IDisposable, IEnumerable<uint>
{
    internal static readonly byte* RankLookupTable;
    internal static readonly byte* HighBitRanks;
    static BitCounter()
    {
        static uint CountSetBitsInByte(uint id)
        {
            id -= id >> 1 & 0x55;
            id = (id & 0x33) + (id >> 2 & 0x33);
            return id + (id >> 4) & 0xF;
        }

        BitCounter.RankLookupTable = (byte*)NativeMemory.Alloc(256);
        for (var i = 0; i < 256; i++)
            BitCounter.RankLookupTable[i] = (byte)CountSetBitsInByte((uint)i);
        BitCounter.HighBitRanks = (byte*)NativeMemory.Alloc(256u * 8u);
        var pHighRanks = BitCounter.HighBitRanks;
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

    private const int stepIndexTable = 128;
    private const int stepIndexTableBitCount = 7;
    private const int maskStepIndexTable = 0x7F;
    
    public uint* BitList => this.bitList;

    public static void Insert0(ref uint buffer) => buffer >>= 1;

    public static void Insert1(ref uint buffer) => buffer = buffer >> 1 | 0x80000000;

    private readonly ulong onesCount;
    private readonly ulong zerosCount;

    /// <summary>
    /// Each bit represents either to increase the counter "0" or that there exists a value equal to counter "1"
    /// </summary>
    private readonly uint* bitList;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        this.Dispose(true);
    }

    private void Dispose(bool isDisposing)
    {
        if (isDisposing && this.subIndex != null)
            this.subIndex.Dispose();
        if (this.bitList != null)
            NativeMemory.Free(this.bitList);
    }

    public bool IsIndexable { get; }
    private readonly CompressedRank<ulong>? subIndex;

    ~BitCounter() => this.Dispose(false);

    public BitCounter(ulong[] keys, bool isIndexable) : this(keys, isIndexable ? CompressedRank<uint>.FindBestSize((ulong)keys.Length, (ulong)keys[^1], true, false, false) : null)
    { }

    public BitCounter(ulong[] keys, (int, ulong, ulong)[]? sizes = null, int subIndex = 0)
    {
        this.IsIndexable = sizes != null;
        fixed (ulong* pKeys = keys)
        {
            this.onesCount = (ulong)keys.LongLength;
            Generate(pKeys, this.onesCount, sizes, subIndex, out this.zerosCount, out this.bitList, out this.subIndex);
        }

    }

    private static void Generate(ulong* keys, ulong keyCount, (int, ulong, ulong)[]? sizes, int subIndexNumber, out ulong maxValue, out uint* valuePresentFlags, out CompressedRank<ulong>? subIndex)
    {
        uint buffer = 0;
        if (keyCount == 0)
        {
            maxValue = 0;
            valuePresentFlags = null;
            subIndex = null;
            return;
        }

        maxValue = keys[keyCount - 1];
        var bitCount = keyCount + maxValue;
        var flagsSize = (nuint)(bitCount + 0x1f >> 5);

        valuePresentFlags = (uint*)NativeMemory.Alloc(flagsSize, (nuint)sizeof(uint));
        var vpf = valuePresentFlags;
        int SetFlags(ulong max)
        {
            var flagIndex = 0;
            for (ulong currentValue = 0, keyIndex = 0; ;)
            {
                while (keys[keyIndex] == currentValue)
                {
                    BitCounter.Insert1(ref buffer);
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
                    BitCounter.Insert0(ref buffer);
                    flagIndex++;

                    if ((flagIndex & 0x1f) == 0) // (idx & 0x1f) = idx % 32
                        vpf[(flagIndex >> 5) - 1] = buffer; // (idx >> 5) = idx/32
                    currentValue++;
                }
            }

            return flagIndex;
        }

        var flagIndex = SetFlags(maxValue);

        if ((flagIndex & 0x1f) != 0)
        {
            buffer >>= 0x20 - (flagIndex & 0x1f);
            valuePresentFlags[flagIndex - 1 >> 5] = buffer;
        }

        if (sizes == null || keyCount < stepIndexTable)
        {
            subIndex = null;
            return;
        }

        var bitsTable = (byte*)valuePresentFlags;
        var skipTableIndex = 0u;
        var skips = new ulong[keyCount >> stepIndexTableBitCount];
        ulong keyIndex = stepIndexTable;
        var valueArrayIndex = 0ul;
        var currentIndex = 0ul;
        while (keyIndex < keyCount)
        {
            ulong lastIndex;
            do
            {
                lastIndex = currentIndex;
                currentIndex += BitCounter.RankLookupTable[bitsTable[valueArrayIndex++]];
            } while (currentIndex <= keyIndex);
            skips[skipTableIndex++] = BitCounter.HighBitRanks[bitsTable[valueArrayIndex - 1] * 8 + (int)(keyIndex - lastIndex)] + (valueArrayIndex - 1 << 3);

            keyIndex += stepIndexTable;
        }

        subIndex = new(skips, sizes, subIndexNumber + 1);
        //}
    }

    private static ulong GetValueAtIndex(uint* presentTable, uint valueIndex, CompressedRank<ulong>? subIndex) =>
        GetBitIndex(presentTable, valueIndex, subIndex) - valueIndex;

    private static ulong GetBitIndexOfValue(uint* presentTable, uint value, CompressedRank<ulong>? subIndex)
    {
        if (value == 0)
            return 1;
        uint lastPartSum;
        var bitTable = (byte*)presentTable;
        var bitIndex = value >= 1 << stepIndexTableBitCount && subIndex != null
            ? subIndex.GetRank((value >> stepIndexTableBitCount) - 1)
            : 0;
        var byteIndex = bitIndex >> 3;
        // starting at byteIndex, bitIndex bits are set; value = (valueIndex & ~maskStepIndexTable) - bitIndex
        var zeroIndex = value & maskStepIndexTable; // how many bits past the byteIndex to count
        zeroIndex += RankLookupTable[(byte)~bitTable[byteIndex] & ((1 << (int)(bitIndex & 7)) - 1)];
        uint partSum = 0;
        do
        {
            lastPartSum = partSum;
            partSum += RankLookupTable[(byte)~bitTable[byteIndex++]];
        } while (partSum <= zeroIndex);

        return BitCounter.HighBitRanks[(byte)~bitTable[byteIndex - 1] * 8 + zeroIndex - lastPartSum] + (byteIndex - 1 << 3) - value;
    }
    private static ulong GetBitIndex(uint* presentTable, uint valueIndex, CompressedRank<ulong>? subIndex)
    {
        uint lastPartSum;
        var bitTable = (byte*)presentTable;
        var bitIndex = valueIndex >= 1 << stepIndexTableBitCount && subIndex != null
            ? subIndex.GetValue((valueIndex >> stepIndexTableBitCount) - 1)
            : 0;
        var byteIndex = bitIndex >> 3;
        // starting at byteIndex, bitIndex bits are set; value = (valueIndex & ~maskStepIndexTable) - bitIndex
        var oneIndex = valueIndex & maskStepIndexTable; // how many bits past the byteIndex to count
        oneIndex += RankLookupTable[bitTable[byteIndex] & ((1 << (int)(bitIndex & 7)) - 1)];
        uint partSum = 0;
        do
        {
            lastPartSum = partSum;
            partSum += RankLookupTable[bitTable[byteIndex++]];
        } while (partSum <= oneIndex);

        return BitCounter.HighBitRanks[bitTable[byteIndex - 1] * 8 + oneIndex - lastPartSum] + (byteIndex - 1 << 3);
    }

    public ulong GetValueAtIndex(uint valueIndex) => GetValueAtIndex(this.bitList, valueIndex, this.subIndex);

    public ulong GetBitIndexOfValue(uint value) => GetBitIndexOfValue(this.bitList, value, this.subIndex);
    public ulong GetBitIndex(uint valueIndex) => GetBitIndex(this.bitList, valueIndex, this.subIndex);

    private static uint GetNextBitIndex(uint* presentTable, uint bitIndex)
    {
        uint lastPartSum;
        var bitTable = (byte*)presentTable;
        var byteIndex = bitIndex >> 3;
        var targetIndex = RankLookupTable[bitTable[byteIndex] & ((1 << (int)(bitIndex & 7)) - 1)] + 1;
        uint partSum = 0;
        do
        {
            lastPartSum = partSum;
            partSum += RankLookupTable[bitTable[byteIndex++]];
        } while (partSum <= targetIndex);
        return BitCounter.HighBitRanks[bitTable[byteIndex - 1] * 8 + targetIndex - lastPartSum] + (byteIndex - 1 << 3);
    }

    public uint GetNextBitIndex(uint bitIndex) => GetNextBitIndex(this.bitList, bitIndex);

    public BitCounter(BinaryReader reader, bool isIndexable, uint? keyCount = null, uint? maxValue = null)
    {
        this.onesCount = keyCount ?? (uint)reader.Read7BitEncodedInt64();
        this.zerosCount = maxValue ?? (uint)reader.Read7BitEncodedInt64();
        var nbits = this.onesCount + this.zerosCount;
        //this.skipSize = Log2(nbits);
        //this.skipMask = (1u << (int)this.skipSize) - 1;
        var vecSize = (nuint)(nbits + 0x1f >> 5);
        //nuint selTableSize = (nuint)((ulong)(this.onesCount >> BitCounter.stepIndexTableBitCount) * this.skipSize + 7) >> 3; 
        //var subRangeSize = CompressedRank<uint>.SizeEstimate(this.onesCount, this.skipSize, true, false);

        this.bitList = (uint*)NativeMemory.Alloc(vecSize, (nuint)sizeof(uint));
        //this.indexSkipTable = (byte*)NativeMemory.Alloc(selTableSize, (nuint)sizeof(uint));
        reader.Read(new Span<byte>(this.bitList, (int)(vecSize * sizeof(uint))));
        //reader.Read(new Span<byte>(this.indexSkipTable, (int)(selTableSize * sizeof(uint))));
        if (isIndexable)
            this.subIndex = new(reader, this.onesCount);
    }

    public void Write(BinaryWriter writer, bool writeCount = true, bool writeMax = true)
    {
        if (writeCount)
            writer.Write7BitEncodedInt64((long)this.onesCount);
        if (writeMax)
            writer.Write7BitEncodedInt64((long)this.zerosCount);
        //writer.Write(this.onesCount);
        //writer.Write(this.zerosCount);
        var nbits = this.onesCount + this.zerosCount;
        var vecSize = nbits + 0x1f >> 5;
        //var selTableSize = (nuint)((ulong)(onesCount >> BitCounter.stepIndexTableBitCount) * this.skipSize + 7) >> 3;
        writer.Write(new ReadOnlySpan<byte>(this.bitList, (int)(vecSize * sizeof(uint))));
        this.subIndex?.Write(writer, false);
    }

    public unsafe class SelectEnumerator : IEnumerator<uint>
    {
        private readonly uint* valuePresentFlags;
        private readonly ulong valueCount;
        private uint* currentFlags;
        private uint currentBitIndex;
        private long currentCount;

        public SelectEnumerator(uint* valuePresentFlags, ulong valueCount)
        {
            this.valuePresentFlags = valuePresentFlags;
            this.valueCount = valueCount;
            this.Reset();
        }

        public bool MoveNext()
        {
            if (this.currentCount >= (long)this.valueCount)
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
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<uint> GetEnumerator() => new SelectEnumerator(this.bitList, this.onesCount);
}

public sealed unsafe class BitCounter<T> : IDisposable, IEnumerable<uint> where T : unmanaged, IBinaryInteger<T>, IConvertible
{

    private const int stepIndexTable = 128;
    private const int stepIndexTableBitCount = 7;
    private const int maskStepIndexTable = 0x7F;

    public uint* BitList => this.bitList;

    private readonly ulong onesCount;
    private readonly ulong zerosCount;

    /// <summary>
    /// Each bit represents either to increase the counter "0" or that there exists a value equal to counter "1"
    /// </summary>
    private readonly uint* bitList;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        this.Dispose(true);
    }

    private void Dispose(bool isDisposing)
    {
        if (isDisposing && this.subIndex != null)
            this.subIndex.Dispose();
        if (this.bitList != null)
            NativeMemory.Free(this.bitList);
    }

    public bool IsIndexable { get; }
    private readonly CompactRank<T>? subIndex;

    ~BitCounter() => this.Dispose(false);
    
    public BitCounter(T[] keys, bool isIndexable)
    {
        this.IsIndexable = isIndexable;
        fixed (T* pKeys = keys)
        {
            this.onesCount = (ulong)keys.LongLength;
            Generate(pKeys, this.onesCount, isIndexable, out this.zerosCount, out this.bitList, out this.subIndex);
        }

    }

    private static void Generate(T* keys, ulong keyCount, bool isIndexable, out ulong maxValue, out uint* valuePresentFlags, out CompactRank<T>? subIndex)
    {
        uint buffer = 0;
        if (keyCount == 0)
        {
            maxValue = 0;
            valuePresentFlags = null;
            subIndex = null;
            return;
        }

        maxValue = keys[keyCount - 1].ToUInt64(null);
        var bitCount = keyCount + maxValue;
        var flagsSize = (nuint)(bitCount + 0x1f >> 5);

        valuePresentFlags = (uint*)NativeMemory.Alloc(flagsSize, (nuint)sizeof(uint));
        var vpf = valuePresentFlags;
        int SetFlags(ulong max)
        {
            var flagIndex = 0;
            for (ulong currentValue = 0, keyIndex = 0; ;)
            {
                while (keys[keyIndex].ToUInt64(null) == currentValue)
                {
                    BitCounter.Insert1(ref buffer);
                    flagIndex++;

                    if ((flagIndex & 0x1f) == 0)
                        vpf[(flagIndex >> 5) - 1] = buffer; // (idx >> 5) = idx/32
                    keyIndex++;

                    if (keyIndex == keyCount)
                        return flagIndex;
                }

                if (currentValue == max)
                    break;

                while (keys[keyIndex].ToUInt64(null) > currentValue)
                {
                    BitCounter.Insert0(ref buffer);
                    flagIndex++;

                    if ((flagIndex & 0x1f) == 0) // (idx & 0x1f) = idx % 32
                        vpf[(flagIndex >> 5) - 1] = buffer; // (idx >> 5) = idx/32
                    currentValue++;
                }
            }

            return flagIndex;
        }

        var flagIndex = SetFlags(maxValue);

        if ((flagIndex & 0x1f) != 0)
        {
            buffer >>= 0x20 - (flagIndex & 0x1f);
            valuePresentFlags[flagIndex - 1 >> 5] = buffer;
        }

        if (!isIndexable || keyCount < stepIndexTable)
        {
            subIndex = null;
            return;
        }

        var bitsTable = (byte*)valuePresentFlags;
        var skipTableIndex = 0u;
        var skips = new T[keyCount >> stepIndexTableBitCount];
        ulong keyIndex = stepIndexTable;
        var valueArrayIndex = 0ul;
        var currentIndex = 0ul;
        while (keyIndex < keyCount)
        {
            ulong lastIndex;
            do
            {
                lastIndex = currentIndex;
                currentIndex += BitCounter.RankLookupTable[bitsTable[valueArrayIndex++]];
            } while (currentIndex <= keyIndex);
            skips[skipTableIndex++] = T.CreateChecked(BitCounter.HighBitRanks[bitsTable[valueArrayIndex - 1] * 8 + (int)(keyIndex - lastIndex)] + (valueArrayIndex - 1 << 3));

            keyIndex += stepIndexTable;
        }

        subIndex = new(skips, true);
    }
    private static ulong GetBitIndex(uint* presentTable, uint valueIndex, CompactRank<T>? subIndex)
    {
        uint lastPartSum;
        var bitTable = (byte*)presentTable;
        var bitIndex = valueIndex >= 1 << stepIndexTableBitCount && subIndex != null
            ? subIndex.GetRank((valueIndex >> stepIndexTableBitCount) - 1)
            : 0;
        var byteIndex = bitIndex >> 3;
        // starting at byteIndex, bitIndex bits are set; value = (valueIndex & ~maskStepIndexTable) - bitIndex
        var oneIndex = valueIndex & maskStepIndexTable; // how many bits past the byteIndex to count
        oneIndex += BitCounter.RankLookupTable[bitTable[byteIndex] & ((1 << (int)(bitIndex & 7)) - 1)];
        uint partSum = 0;
        do
        {
            lastPartSum = partSum;
            partSum += BitCounter.RankLookupTable[bitTable[byteIndex++]];
        } while (partSum <= oneIndex);

        return BitCounter.HighBitRanks[bitTable[byteIndex - 1] * 8 + oneIndex - lastPartSum] + (byteIndex - 1 << 3);
    }
    public ulong GetBitIndex(uint valueIndex) => GetBitIndex(this.bitList, valueIndex, this.subIndex);

    private static uint GetNextBitIndex(uint* presentTable, uint bitIndex)
    {
        uint lastPartSum;
        var bitTable = (byte*)presentTable;
        var byteIndex = bitIndex >> 3;
        var targetIndex = BitCounter.RankLookupTable[bitTable[byteIndex] & ((1 << (int)(bitIndex & 7)) - 1)] + 1;
        uint partSum = 0;
        do
        {
            lastPartSum = partSum;
            partSum += BitCounter.RankLookupTable[bitTable[byteIndex++]];
        } while (partSum <= targetIndex);
        return BitCounter.HighBitRanks[bitTable[byteIndex - 1] * 8 + targetIndex - lastPartSum] + (byteIndex - 1 << 3);
    }

    public uint GetNextBitIndex(uint bitIndex) => GetNextBitIndex(this.bitList, bitIndex);

    public BitCounter(BinaryReader reader, bool isIndexable, uint? keyCount = null, uint? maxValue = null)
    {
        this.onesCount = keyCount ?? (uint)reader.Read7BitEncodedInt64();
        this.zerosCount = maxValue ?? (uint)reader.Read7BitEncodedInt64();
        var nbits = this.onesCount + this.zerosCount;
        //this.skipSize = Log2(nbits);
        //this.skipMask = (1u << (int)this.skipSize) - 1;
        var vecSize = (nuint)(nbits + 0x1f >> 5);
        //nuint selTableSize = (nuint)((ulong)(this.onesCount >> BitCounter.stepIndexTableBitCount) * this.skipSize + 7) >> 3; 
        //var subRangeSize = CompressedRank<uint>.SizeEstimate(this.onesCount, this.skipSize, true, false);

        this.bitList = (uint*)NativeMemory.Alloc(vecSize, (nuint)sizeof(uint));
        //this.indexSkipTable = (byte*)NativeMemory.Alloc(selTableSize, (nuint)sizeof(uint));
        reader.Read(new Span<byte>(this.bitList, (int)(vecSize * sizeof(uint))));
        //reader.Read(new Span<byte>(this.indexSkipTable, (int)(selTableSize * sizeof(uint))));
        if (isIndexable)
            this.subIndex = new(reader, this.onesCount);
    }

    public void Write(BinaryWriter writer, bool writeCount = true, bool writeMax = true)
    {
        if (writeCount)
            writer.Write7BitEncodedInt64((long)this.onesCount);
        if (writeMax)
            writer.Write7BitEncodedInt64((long)this.zerosCount);
        //writer.Write(this.onesCount);
        //writer.Write(this.zerosCount);
        var nbits = this.onesCount + this.zerosCount;
        var vecSize = nbits + 0x1f >> 5;
        //var selTableSize = (nuint)((ulong)(onesCount >> BitCounter.stepIndexTableBitCount) * this.skipSize + 7) >> 3;
        writer.Write(new ReadOnlySpan<byte>(this.bitList, (int)(vecSize * sizeof(uint))));
        this.subIndex?.Write(writer, false);
    }

    /// <summary>
    /// Enumerates the number of zero bits that appear before each one bit
    /// </summary>
    public unsafe class ZeroCountEnumerator : IEnumerator<uint>
    {
        private readonly uint* bitList;
        private readonly ulong onesCount;
        private uint* currentBits;
        private uint currentBitIndex;
        private long currentOnesCount;

        public ZeroCountEnumerator(uint* bitList, ulong onesCount)
        {
            this.bitList = bitList;
            this.onesCount = onesCount;
            this.Reset();
        }

        public bool MoveNext()
        {
            if (this.currentOnesCount >= (long)this.onesCount)
                return false;
            for (; ; )
            {
                if (this.currentBitIndex == 0)
                {
                    this.currentBitIndex = 32;
                    this.currentBits++;
                }

                if ((*this.currentBits & (1u << (int) --this.currentBitIndex)) != 0)
                {
                    this.currentOnesCount++;
                    return true;
                }

                this.Current++;
            }
        }

        public void Reset()
        {
            this.currentBits = this.bitList;
            this.currentBitIndex = 32;
            this.currentOnesCount = -1;
            this.Current = 0;
        }

        public uint Current { get; private set; }

        object IEnumerator.Current => Current;

        public void Dispose() { }
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<uint> GetEnumerator() => new ZeroCountEnumerator(this.bitList, this.onesCount);
}