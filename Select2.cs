using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NetMph;

public sealed unsafe class Select : IDisposable, IEnumerable<uint>
{
    private static readonly byte* rankLookupTable;
    private static readonly byte* highBitRanks;
    static Select()
    {
        static uint CountSetBitsInByte(uint id)
        {
            id -= id >> 1 & 0x55;
            id = (id & 0x33) + (id >> 2 & 0x33);
            return id + (id >> 4) & 0xF;
        }

        Select.rankLookupTable = (byte*)NativeMemory.Alloc((nuint)256);
        for (var i = 0; i < 256; i++)
            Select.rankLookupTable[i] = (byte)CountSetBitsInByte((uint)i);
        Select.highBitRanks = (byte*)NativeMemory.Alloc((nuint)(256u * 8u));
        var pHighRanks = Select.highBitRanks;
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

    private const int stepSelectTable = 128;
    private const int stepSelectTableBitCount = 7;
    private const int maskStepSelectTable = 0x7F;

    //public static ulong SizeEstimate(uint count, uint maxValue, bool isIndexable = true, bool includeSize = true)
    //{
    //    var bitCount = count + maxValue;
    //    var vecSize = (bitCount + 31) >> 5;
    //    var size = 0ul;
    //    if (includeSize)
    //        size += SevenBitIntegerSize(count) + SevenBitIntegerSize(maxValue);
    //    if (!isIndexable)
    //        return size + vecSize * sizeof(uint);
    //    var selTableCount = count >> stepSelectTableBitCount;
    //    //var selTableSize = selTableCount * bitCount;
    //    var subRangeSize = CompressedRank<uint>.FindBestSize(selTableCount, bitCount)[0].sizeInBits;
    //    size += subRangeSize;
    //    return size + vecSize * sizeof(uint);
    //}

    private static uint SevenBitIntegerSize(ulong value) => value switch
    {
        < 1ul << 7 => 1,
        < 1ul << 14 => 2,
        < 1ul << 21 => 3,
        < 1ul << 28 => 4,
        < 1ul << 35 => 5,
        < 1ul << 42 => 6,
        < 1ul << 49 => 7,
        < 1ul << 56 => 8,
        < 1ul << 63 => 9,
        _ => 10
    };

    //public ulong Size => Select.SizeEstimate(this.keyCount, this.maxValue);
    public uint* ValuePresentFlags => this.valuePresentFlags;

    /// <summary>
    /// Each bit represents either to increase the counter "0" or that there exists a value equal to counter "1"
    /// </summary>
    //public uint* ValuePresentFlags => valuePresentFlags;

    private static void Insert0(ref uint buffer) => buffer >>= 1;

    private static void Insert1(ref uint buffer) => buffer = buffer >> 1 | 0x80000000;

    private readonly ulong keyCount;
    private readonly ulong maxValue;

    /// <summary>
    /// Each bit represents either to increase the counter "0" or that there exists a value equal to counter "1"
    /// </summary>
    private readonly uint* valuePresentFlags;
    /// <summary>
    /// indexSkipTable[i] gives you the bit index(value) of the i*128th value 
    /// </summary>
    //private readonly byte* indexSkipTable;

    //private readonly uint skipSize;
    //private readonly uint skipMask;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        this.Dispose(true);
    }

    private void Dispose(bool isDisposing)
    {
        if (isDisposing && this.subIndex != null)
            this.subIndex.Dispose();
        if (this.valuePresentFlags != null)
            NativeMemory.Free(this.valuePresentFlags);
    }

    public bool IsIndexable { get; }
    private readonly CompressedRank<ulong>? subIndex;

    ~Select() => this.Dispose(false);

    public Select(ulong[] keys, bool isIndexable) : this(keys, isIndexable ? CompressedRank<uint>.FindBestSize((ulong)keys.Length, (ulong)keys[^1], true, false, false) : null)
    { }

    public Select(ulong[] keys, (int, ulong, ulong)[]? sizes = null, int subIndex = 0)
    {
        this.IsIndexable = sizes != null;
        fixed (ulong* pKeys = keys)
        {
            this.keyCount = (ulong)keys.LongLength;
            Generate(pKeys, this.keyCount, sizes, subIndex, out this.maxValue, out this.valuePresentFlags, out this.subIndex);
        }

    }

    //public Select(ulong* keys, ulong keyCount, bool isIndexable = true)
    //{
    //    this.keyCount = keyCount;
    //    this.IsIndexable = isIndexable;
    //    Generate(keys, this.keyCount, this.IsIndexable, out this.maxValue, out this.valuePresentFlags, out this.subIndex);
    //}

    private static uint Log2(uint x)
    {
        //var isPowerOf2 = x & x - 1;
        //isPowerOf2 |= ~isPowerOf2 + 1;
        //isPowerOf2 >>= 31;
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        x -= x >> 1 & 0x55555555u;
        x = (x & 0x33333333u) + (x >> 2 & 0x33333333u);
        return (x + (x >> 4) & 0x0F0F0F0F) * 0x01010101 >> 24;// - 1 + isPowerOf2;
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
        //var skipCount = keyCount >> Select.stepSelectTableBitCount;
        //var skipTableSize = (nuint)((ulong)skipCount * skipEntrySize + 7) >> 3;
        //var subRangeSize = CompressedRank<uint>.SizeEstimate(skipCount, skipEntrySize, true, false);
        valuePresentFlags = (uint*)NativeMemory.Alloc(flagsSize, (nuint)sizeof(uint));
        var vpf = valuePresentFlags;
        int SetFlags(ulong max)
        {
            var flagIndex = 0;
            for (ulong currentValue = 0, keyIndex = 0; ;)
            {
                while (keys[keyIndex] == currentValue)
                {
                    Select.Insert1(ref buffer);
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
                    Select.Insert0(ref buffer);
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

        if (sizes == null || keyCount < stepSelectTable)
        {
            subIndex = null;
            return;
        }

        //if (skipTableSize <= subRangeSize)
        //{
        //    valueSkipTable = (byte*) NativeMemory.Alloc(skipTableSize);
        //    GenerateSkipTable(valuePresentFlags, valueSkipTable, keyCount, (uint) skipEntrySize, out skipEntryMask);
        //    subIndex = null;
        //}
        //else
        //{

        var bitsTable = (byte*)valuePresentFlags;
        var skipTableIndex = 0u;
        var skips = new ulong[keyCount >> stepSelectTableBitCount];
        ulong keyIndex = stepSelectTable;
        var valueArrayIndex = 0ul;
        var currentIndex = 0ul;
        while (keyIndex < keyCount)
        {
            ulong lastIndex;
            do
            {
                lastIndex = currentIndex;
                currentIndex += Select.rankLookupTable[bitsTable[valueArrayIndex++]];
            } while (currentIndex <= keyIndex);
            skips[skipTableIndex++] = Select.highBitRanks[bitsTable[valueArrayIndex - 1] * 8 + (int)(keyIndex - lastIndex)] + (valueArrayIndex - 1 << 3);

            keyIndex += stepSelectTable;
        }

        subIndex = new(skips, sizes, subIndexNumber + 1);
        //}
    }

    //private static void GenerateSkipTable(uint* valuePresentFlags, byte* vst, uint keyCount, uint skipEntrySize, out uint skipEntryMask)
    //{
    //    skipEntryMask = (1u << (int)skipEntrySize) - 1;
    //    var bitsTable = (byte*)valuePresentFlags;
    //    var skipTableIndex = 0u;
    //    var keyIndex = stepSelectTable;
    //    var valueArrayIndex = 0u;
    //    var currentIndex = 0u;
    //    while (keyIndex < keyCount)
    //    {
    //        uint lastIndex;
    //        do
    //        {
    //            lastIndex = currentIndex;
    //            currentIndex += Select.rankLookupTable[bitsTable[valueArrayIndex++]];
    //        } while (currentIndex <= keyIndex);
    //        BitBool.SetBitsValue((uint*)vst, skipTableIndex++, Select.highBitRanks[bitsTable[valueArrayIndex - 1] * 8 + keyIndex - lastIndex] + (valueArrayIndex - 1 << 3), skipEntrySize, skipEntryMask);
    //        //this.indexSkipTable[skipTableIndex++] = Select.highBitRanks[bitsTable[valueArrayIndex - 1] * 8 + keyIndex - lastIndex] + (valueArrayIndex - 1 << 3);
    //        keyIndex += stepSelectTable;
    //    }
    //}

    private static ulong GetValueAtIndex(uint* presentTable, uint valueIndex, CompressedRank<ulong>? subIndex) =>
        GetBitIndex(presentTable, valueIndex, subIndex) - valueIndex;

    private static ulong GetBitIndexOfValue(uint* presentTable, uint value, CompressedRank<ulong>? subIndex)
    {
        uint lastPartSum;
        var bitTable = (byte*)presentTable;
        var bitIndex = value >= 1 << stepSelectTableBitCount && subIndex != null
            ? subIndex.GetRank((value >> stepSelectTableBitCount) - 1)
            : 0;
        var byteIndex = bitIndex >> 3;
        // starting at byteIndex, bitIndex bits are set; value = (valueIndex & ~maskStepSelectTable) - bitIndex
        var oneIndex = value & maskStepSelectTable; // how many bits past the byteIndex to count
        oneIndex += rankLookupTable[bitTable[byteIndex] & ((1 << (int)(bitIndex & 7)) - 1)];
        uint partSum = 0;
        do
        {
            lastPartSum = partSum;
            partSum += rankLookupTable[bitTable[byteIndex++]];
        } while (partSum <= oneIndex);

        return Select.highBitRanks[bitTable[byteIndex - 1] * 8 + oneIndex - lastPartSum] + (byteIndex - 1 << 3);
    }
    private static ulong GetBitIndex(uint* presentTable, uint valueIndex, CompressedRank<ulong>? subIndex)
    {
        uint lastPartSum;
        var bitTable = (byte*)presentTable;
        var bitIndex = valueIndex >= 1 << stepSelectTableBitCount && subIndex != null
            ? subIndex.GetValue((valueIndex >> stepSelectTableBitCount) - 1)
            : 0;
        var byteIndex = bitIndex >> 3;
        // starting at byteIndex, bitIndex bits are set; value = (valueIndex & ~maskStepSelectTable) - bitIndex
        var oneIndex = valueIndex & maskStepSelectTable; // how many bits past the byteIndex to count
        oneIndex += rankLookupTable[bitTable[byteIndex] & ((1 << (int)(bitIndex & 7)) - 1)];
        uint partSum = 0;
        do
        {
            lastPartSum = partSum;
            partSum += rankLookupTable[bitTable[byteIndex++]];
        } while (partSum <= oneIndex);

        return Select.highBitRanks[bitTable[byteIndex - 1] * 8 + oneIndex - lastPartSum] + (byteIndex - 1 << 3);
    }

    public ulong GetValueAtIndex(uint valueIndex) => GetValueAtIndex(this.valuePresentFlags, valueIndex, this.subIndex);

    public ulong GetBitIndexOfValue(uint value) => GetBitIndexOfValue(this.valuePresentFlags, value, this.subIndex);
    public ulong GetBitIndex(uint valueIndex) => GetBitIndex(this.valuePresentFlags, valueIndex, this.subIndex);

    private static uint GetNextBitIndex(uint* presentTable, uint bitIndex)
    {
        uint lastPartSum;
        var bitTable = (byte*)presentTable;
        var byteIndex = bitIndex >> 3;
        var targetIndex = rankLookupTable[bitTable[byteIndex] & ((1 << (int)(bitIndex & 7)) - 1)] + 1;
        uint partSum = 0;
        do
        {
            lastPartSum = partSum;
            partSum += rankLookupTable[bitTable[byteIndex++]];
        } while (partSum <= targetIndex);
        return Select.highBitRanks[bitTable[byteIndex - 1] * 8 + targetIndex - lastPartSum] + (byteIndex - 1 << 3);
    }

    public uint GetNextBitIndex(uint bitIndex) => GetNextBitIndex(this.valuePresentFlags, bitIndex);

    //public Select(byte* buffer)
    //{
    //    //maxValue = keys[keyCount - 1];
    //    //var nbits = keyCount + maxValue;
    //    //skipEntrySize = Log2(nbits);
    //    //nuint flagsSize = nbits + 0x1f >> 5;
    //    //nuint skipTableSize = (keyCount >> Select.stepSelectTableBitCount) + 1;
    //    //valuePresentFlags = (uint*)NativeMemory.Alloc(flagsSize, (nuint)sizeof(uint));
    //    //valueSkipTable = (byte*)NativeMemory.Alloc(skipTableSize, (nuint)skipEntrySize);
    //    this.keyCount = *(uint*)buffer++;
    //    this.maxValue = *(uint*)buffer++;
    //    var nbits = this.keyCount + this.maxValue;
    //    nuint vecSize = nbits + 0x1f >> 5;
    //    nuint selTableSize = (this.keyCount >> 7) + 1;
    //    this.valuePresentFlags = (uint*)NativeMemory.Alloc(vecSize, (nuint)sizeof(uint));
    //    this.indexSkipTable = (byte*)NativeMemory.Alloc(selTableSize, (nuint)sizeof(uint));
    //    Buffer.MemoryCopy(buffer, this.valuePresentFlags, vecSize, vecSize);
    //    buffer += vecSize * sizeof(uint);
    //    Buffer.MemoryCopy(buffer, this.indexSkipTable, selTableSize, selTableSize);
    //}
    public Select(BinaryReader reader, bool isIndexable, uint? keyCount = null, uint? maxValue = null)
    {
        this.keyCount = keyCount ?? (uint)reader.Read7BitEncodedInt64();
        this.maxValue = maxValue ?? (uint)reader.Read7BitEncodedInt64();
        var nbits = this.keyCount + this.maxValue;
        //this.skipSize = Log2(nbits);
        //this.skipMask = (1u << (int)this.skipSize) - 1;
        var vecSize = (nuint)(nbits + 0x1f >> 5);
        //nuint selTableSize = (nuint)((ulong)(this.keyCount >> Select.stepSelectTableBitCount) * this.skipSize + 7) >> 3; 
        //var subRangeSize = CompressedRank<uint>.SizeEstimate(this.keyCount, this.skipSize, true, false);

        this.valuePresentFlags = (uint*)NativeMemory.Alloc(vecSize, (nuint)sizeof(uint));
        //this.indexSkipTable = (byte*)NativeMemory.Alloc(selTableSize, (nuint)sizeof(uint));
        reader.Read(new Span<byte>(this.valuePresentFlags, (int)(vecSize * sizeof(uint))));
        //reader.Read(new Span<byte>(this.indexSkipTable, (int)(selTableSize * sizeof(uint))));
        if (isIndexable)
            this.subIndex = new(reader, this.keyCount);
    }

    //public void Write(byte* buffer, ref ulong bufferLength)
    //{
    //    if (buffer == null)
    //    {
    //        bufferLength = this.Size;
    //        return;
    //    }

    //    if (bufferLength < this.Size)
    //        throw new InsufficientMemoryException();
    //    var uintBuffer = (uint*)buffer;
    //    *uintBuffer++ = this.keyCount;
    //    *uintBuffer++ = this.maxValue;
    //    var nbits = this.keyCount + this.maxValue;
    //    var vecSize = nbits + 0x1f >> 5;
    //    var selTableSize = (this.keyCount >> 7) + 1;
    //    Buffer.MemoryCopy(this.valuePresentFlags, uintBuffer, bufferLength - 4, vecSize);
    //    uintBuffer += vecSize;
    //    Buffer.MemoryCopy(this.indexSkipTable, uintBuffer, bufferLength - 4 - vecSize, selTableSize);
    //}

    public void Write(BinaryWriter writer, bool writeCount = true, bool writeMax = true)
    {
        if (writeCount)
            writer.Write7BitEncodedInt64((long)this.keyCount);
        if (writeMax)
            writer.Write7BitEncodedInt64((long)this.maxValue);
        //writer.Write(this.keyCount);
        //writer.Write(this.maxValue);
        var nbits = this.keyCount + this.maxValue;
        var vecSize = nbits + 0x1f >> 5;
        //var selTableSize = (nuint)((ulong)(keyCount >> Select.stepSelectTableBitCount) * this.skipSize + 7) >> 3;
        writer.Write(new ReadOnlySpan<byte>(this.valuePresentFlags, (int)(vecSize * sizeof(uint))));
        this.subIndex?.Write(writer, false);
    }

    //public static ulong GetStoredValuePacked(uint* packedSelect, uint targetIndex)
    //{
    //    var keyCount = *packedSelect++;
    //    var maxValue = *packedSelect++;
    //    var nbits = keyCount + maxValue;
    //    var vecSize = nbits + 0x1f >> 5;
    //    return Select.GetValueAtIndex(packedSelect, (byte*)(packedSelect + vecSize), targetIndex, 0, 0);
    //}

    //public static ulong GetBitIndexPacked(uint* packedSelect, uint targetIndex)
    //{
    //    var keyCount = *packedSelect++;
    //    var maxValue = *packedSelect++;
    //    var nbits = keyCount + maxValue;
    //    var vecSize = nbits + 0x1f >> 5;
    //    return Select.GetBitIndex(packedSelect, (byte*)(packedSelect + vecSize), targetIndex, 0, 0);
    //}

    //public static uint GetNextBitIndexPacked(uint* packedSelect, uint bitIndex) =>
    //    Select.GetNextBitIndex(packedSelect + sizeof(uint) * 2, bitIndex);



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

    public IEnumerator<uint> GetEnumerator() => new SelectEnumerator(this.valuePresentFlags, this.keyCount);
}

[Flags]
public enum IndexOptions : byte
{
    None,
    ByPosition,
    ByValue,
}

[Flags]
public enum DataOptions : byte
{
    None,
    IncludeCount,
    IncludeMaxValue
}

internal unsafe class CompactRank
{
    private static uint SevenBitIntegerSize(ulong value) => value switch
    {
        < 1ul << 7 => 1,
        < 1ul << 14 => 2,
        < 1ul << 21 => 3,
        < 1ul << 28 => 4,
        < 1ul << 35 => 5,
        < 1ul << 42 => 6,
        < 1ul << 49 => 7,
        < 1ul << 56 => 8,
        < 1ul << 63 => 9,
        _ => 10
    };

    public record BestSize(int RemainderBitCount, ulong SizeInBytes, ulong SizeInBits)
    {
        public BestSize? IndexByPosition { get; init; }
        public BestSize? IndexByValue { get; init; }
    }

    public static BestSize FindBestSize(ulong count, ulong maxValue, IndexOptions indexOptions = IndexOptions.ByValue, DataOptions dataOptions = DataOptions.IncludeCount | DataOptions.IncludeMaxValue)
    {
        if (count > maxValue)
            (count, maxValue) = (maxValue, count);

        BestSize ComputeSize(int r)
        {
            var s = count * (ulong)r;
            var newMax = maxValue >> r;
            if (newMax == 0)
                return new(r, (s + 7) >> 3, s);
            s += count + newMax;
            if (count <= 128 || indexOptions == IndexOptions.None)
                return new(r, (s + 7) >> 3, s);
            BestSize? indexByValueSize;
            if (indexOptions.HasFlag(IndexOptions.ByPosition))
            {
                var indexByPositionSize = FindBestSize(count / 128, newMax + count / 128, IndexOptions.ByPosition, 0);
                s += indexByPositionSize.SizeInBits;

                if (!indexOptions.HasFlag(IndexOptions.ByValue))
                    return new(r, (s + 7) >> 3, s) { IndexByPosition = indexByPositionSize };

                indexByValueSize = FindBestSize(newMax / 128, newMax / 128 + count, IndexOptions.ByValue, 0);
                s += indexByValueSize.SizeInBits;
                return new(r, (s + 7) >> 3, s)
                { IndexByPosition = indexByPositionSize, IndexByValue = indexByValueSize };
            }

            indexByValueSize = FindBestSize(newMax / 128, newMax / 128 + count, IndexOptions.ByValue, 0);
            s += indexByValueSize.SizeInBits;
            return new(r, (s + 7) >> 3, s) { IndexByValue = indexByValueSize };
        }

        var remainder = (int)ulong.Log2(maxValue) + 1;
        var guess = (int)ulong.Log2(maxValue / count);
        var a = -1;
        var d = remainder + 1;
        var b = Math.Min(guess + 1, remainder);
        var c = Math.Max(guess - 1, 0);
        var bValue = ComputeSize(b);
        var cValue = ComputeSize(c);

        BestSize? result = null;
        do
        {
            switch (bValue.SizeInBits.CompareTo(cValue.SizeInBits))
            {
                case -1:
                    if (b + 1 >= c) // touching tips
                    {
                        if (a + 1 == b) // nothing to the left
                        {
                            result = bValue;
                            break;
                        }

                        // move left over b
                        d = c;
                        c = b;
                        cValue = bValue;

                        b = a + ((c - a) >> 1);
                        bValue = ComputeSize(b);
                    }
                    else // normal case, move right side in
                    {
                        d = c;
                        c = b + ((d - b) >> 1);
                        cValue = ComputeSize(c);
                    }
                    break;
                case 1:
                    if (b + 1 == c) // touching tips
                    {
                        if (c + 1 == d) // nothing to the right
                        {
                            result = cValue;
                            break;
                        }

                        // move right over c
                        a = b;
                        b = c;
                        bValue = cValue;

                        c = b + ((d - b) >> 1);
                        cValue = ComputeSize(c);
                    }
                    else // normal case, move left side in
                    {
                        a = b;
                        b = a + ((c - a) >> 1);
                        bValue = ComputeSize(b);
                    }
                    break;
                default: // equal values, need to move both sides in
                    if (b + 1 == c) // touching tips, right wins
                    {
                        if (c + 1 == d)
                            result = cValue;
                        else
                        {
                            a = b;
                            b = c++;
                            bValue = cValue;
                            cValue = ComputeSize(c);
                        }
                        break;
                    }
                    if (a < b - 1)
                        a = b;
                    if (d > c + 1)
                        d = c;
                    var diff = (d - a) / 3;
                    if (diff == 0) // they are only 1 apart, move left over
                    {
                        b++;
                        bValue = ComputeSize(b);
                        break;
                    }
                    b = a + diff;
                    c = b + diff;
                    bValue = ComputeSize(b);
                    cValue = ComputeSize(c);
                    break;
            }
        } while (result == null);

        var size = result.SizeInBits;
        if (dataOptions.HasFlag(DataOptions.IncludeCount))
            size += SevenBitIntegerSize(count) << 3;
        if (dataOptions.HasFlag(DataOptions.IncludeMaxValue))
            size += SevenBitIntegerSize(maxValue) << 3;
        return result with { SizeInBits = size, SizeInBytes = (size + 7) >> 3 };
    }
}

public unsafe class CompactRank<T> : IReadOnlyList<T>, IDisposable where T : unmanaged, INumberBase<T>
{
    public IEnumerator<T> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count { get; }

    public T this[int index] => throw new NotImplementedException();

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}

internal static unsafe class BitList
{
    private static readonly byte* rankLookupTable;
    private static readonly byte* highBitRanks;
    static BitList()
    {
        static uint CountSetBitsInByte(uint id)
        {
            id -= id >> 1 & 0x55;
            id = (id & 0x33) + (id >> 2 & 0x33);
            return id + (id >> 4) & 0xF;
        }

        BitList.rankLookupTable = (byte*)NativeMemory.Alloc((nuint)256);
        for (var i = 0; i < 256; i++)
            BitList.rankLookupTable[i] = (byte)CountSetBitsInByte((uint)i);
        BitList.highBitRanks = (byte*)NativeMemory.Alloc((nuint)(256u * 8u));
        var pHighRanks = BitList.highBitRanks;
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

    public const int StepSelectTable = 128;
    public const int StepSelectTableBitCount = 7;
    public const int MaskStepSelectTable = 0x7F;

    public static void Insert0(ref uint buffer) => buffer >>= 1;

    public static void Insert1(ref uint buffer) => buffer = buffer >> 1 | 0x80000000;
}

public unsafe class BitList<T> : IReadOnlyList<T>, IDisposable where T : unmanaged, INumberBase<T>, IConvertible
{
    public IEnumerator<T> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count { get; }

    public T this[int index] => throw new NotImplementedException();

    /// <summary>
    /// Each bit represents either to increase the counter "0" or that there exists a value equal to counter "1"
    /// </summary>
    public uint* ValuePresentFlags => this.valuePresentFlags;

    private readonly ulong keyCount;
    private readonly ulong maxValue;

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
        if (isDisposing)
        {
            this.valueIndex?.Dispose();
            this.positionIndex?.Dispose();
        }

        if (this.valuePresentFlags != null)
            NativeMemory.Free(this.valuePresentFlags);
    }

    private readonly CompactRank<ulong>? valueIndex;
    private readonly CompactRank<ulong>? positionIndex;

    ~BitList() => this.Dispose(false);

    public BitList(T[] keys, IndexOptions indexOptions = IndexOptions.ByPosition)
        : this(keys,
               indexOptions.HasFlag(IndexOptions.ByValue) ? CompactRank.FindBestSize(keys[^1].ToUInt64(null) / BitList.StepSelectTable, (ulong)keys.Length, IndexOptions.ByValue, 0) : null,
               indexOptions.HasFlag(IndexOptions.ByPosition) ? CompactRank.FindBestSize((ulong)keys.Length / BitList.StepSelectTable, keys[^1].ToUInt64(null), IndexOptions.ByPosition, 0) : null)
    { }

    internal BitList(T[] keys, CompactRank.BestSize? valueIndexSizes = null, CompactRank.BestSize? positionIndexSizes = null)
    {
        fixed (T* pKeys = keys)
        {
            this.keyCount = (ulong)keys.LongLength;
            Generate(pKeys, this.keyCount, valueIndexSizes, positionIndexSizes, out this.maxValue, out this.valuePresentFlags, out this.valueIndex, out this.positionIndex);
        }

    }

    private static void Generate(T* keys, ulong keyCount, CompactRank.BestSize? valueIndexSizes, CompactRank.BestSize? positionIndexSizes, out ulong maxValue, out uint* valuePresentFlags, out CompactRank<ulong>? valueIndex, out CompactRank<ulong>? positionIndex)
    {
        uint buffer = 0;
        if (keyCount == 0)
        {
            maxValue = 0;
            valuePresentFlags = null;
            valueIndex = positionIndex = null;
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
                    BitList.Insert1(ref buffer);
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
                    BitList.Insert0(ref buffer);
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

        if (valueIndexSizes == null && positionIndexSizes == null || keyCount < BitList.StepSelectTable)
        {
            positionIndex = valueIndex = null;
            return;
        }
        
        var bitsTable = (byte*)valuePresentFlags;
        var skipTableIndex = 0u;
        var skips = new ulong[keyCount >> stepSelectTableBitCount];
        ulong keyIndex = stepSelectTable;
        var valueArrayIndex = 0ul;
        var currentIndex = 0ul;
        while (keyIndex < keyCount)
        {
            ulong lastIndex;
            do
            {
                lastIndex = currentIndex;
                currentIndex += Select.rankLookupTable[bitsTable[valueArrayIndex++]];
            } while (currentIndex <= keyIndex);
            skips[skipTableIndex++] = Select.highBitRanks[bitsTable[valueArrayIndex - 1] * 8 + (int)(keyIndex - lastIndex)] + (valueArrayIndex - 1 << 3);

            keyIndex += stepSelectTable;
        }

        subIndex = new(skips, sizes, subIndexNumber + 1);
        //}
    }
}