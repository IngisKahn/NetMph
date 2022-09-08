using System.Runtime.InteropServices;

namespace NetMph;

public sealed unsafe class Select2 : IDisposable
{
    private static readonly byte* rankLookupTable;
    private static readonly byte* highBitRanks;
    static Select2()
    {
        static uint CountSetBitsInByte(uint id)
        {
            id -= id >> 1 & 0x55;
            id = (id & 0x33) + (id >> 2 & 0x33);
            return id + (id >> 4) & 0xF;
        }

        Select2.rankLookupTable = (byte*)NativeMemory.Alloc((nuint)256);
        for (var i = 0; i < 256; i++)
            Select2.rankLookupTable[i] = (byte)CountSetBitsInByte((uint)i);
        Select2.highBitRanks = (byte*)NativeMemory.Alloc((nuint)(256u * 8u));
        var pHighRanks = Select2.highBitRanks;
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

    public static uint SizeEstimate(uint count, uint maxValue)
    {
        var bitCount = count + maxValue;
        var vecSize = (bitCount + 31) >> 5;
        var selTableSize = (count >> stepSelectTableBitCount) + 1;
        return SevenBitIntegerSize(count)
               + SevenBitIntegerSize(maxValue)
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

    public uint Size => Select2.SizeEstimate(this.keyCount, this.maxValue);
    public uint* ValuePresentFlags => this.valuePresentFlags;

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
    /// <summary>
    /// indexSkipTable[i] gives you the bit index(value) of the i*128th value 
    /// </summary>
    private readonly byte* indexSkipTable;

    private readonly uint skipSize;
    private readonly uint skipMask;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        this.Dispose(true);
    }

    private void Dispose(bool isDisposing)
    {
        if (this.valuePresentFlags != null)
            NativeMemory.Free(this.valuePresentFlags);
        if (this.indexSkipTable != null)
            NativeMemory.Free(this.indexSkipTable);
    }

    ~Select2() => this.Dispose(false);

    public Select2(uint[] keys)
    {
        fixed (uint* pKeys = keys)
        {
            this.keyCount = (uint)keys.Length;
            Generate(pKeys, this.keyCount, out this.maxValue, out this.valuePresentFlags, out this.indexSkipTable, out this.skipSize, out this.skipMask);
        }
    }

    public Select2(uint* keys, uint keyCount)
    {
        this.keyCount = keyCount;
        Generate(keys, this.keyCount, out this.maxValue, out this.valuePresentFlags, out this.indexSkipTable, out this.skipSize, out this.skipMask);
    }

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

    private static void Generate(uint* keys, uint keyCount, out uint maxValue, out uint* valuePresentFlags, out byte* valueSkipTable, out uint skipEntrySize, out uint skipEntryMask)
    {
        uint buffer = 0;
        if (keyCount == 0)
        {
            maxValue = 0;
            valuePresentFlags = null;
            valueSkipTable = null;
            skipEntrySize = skipEntryMask = 0;
            return;
        }

        maxValue = keys[keyCount - 1];
        var bitCount = keyCount + maxValue;
        skipEntrySize = Log2(bitCount);
        nuint flagsSize = bitCount + 0x1f >> 5;
        var skipTableSize = (nuint)((ulong)(keyCount >> Select2.stepSelectTableBitCount) * skipEntrySize + 7) >> 3;
        valuePresentFlags = (uint*)NativeMemory.Alloc(flagsSize, (nuint)sizeof(uint));
        var vpf = valuePresentFlags;
        valueSkipTable = (byte*)NativeMemory.Alloc(skipTableSize);
        int SetFlags(ulong max)
        {
            var flagIndex = 0;
            for (ulong currentValue = 0, keyIndex = 0; ;)
            {
                while (keys[keyIndex] == currentValue)
                {
                    Select2.Insert1(ref buffer);
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
                    Select2.Insert0(ref buffer);
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
        GenerateSkipTable(valuePresentFlags, valueSkipTable, keyCount, (uint)skipEntrySize, out skipEntryMask);
    }

    private static void GenerateSkipTable(uint* valuePresentFlags, byte* vst, uint keyCount, uint skipEntrySize, out uint skipEntryMask)
    {
        skipEntryMask = (1u << (int)skipEntrySize) - 1;
        var bitsTable = (byte*)valuePresentFlags;
        var skipTableIndex = 0u;
        var keyIndex = stepSelectTable;
        var valueArrayIndex = 0u;
        var currentIndex = 0u;
        while (keyIndex < keyCount)
        {
            uint lastIndex;
            do
            {
                lastIndex = currentIndex;
                currentIndex += Select2.rankLookupTable[bitsTable[valueArrayIndex++]];
            } while (currentIndex <= keyIndex);
            BitBool.SetBitsValue((uint*)vst, skipTableIndex++, Select2.highBitRanks[bitsTable[valueArrayIndex - 1] * 8 + keyIndex - lastIndex] + (valueArrayIndex - 1 << 3), skipEntrySize, skipEntryMask);
            //this.indexSkipTable[skipTableIndex++] = Select2.highBitRanks[bitsTable[valueArrayIndex - 1] * 8 + keyIndex - lastIndex] + (valueArrayIndex - 1 << 3);
            keyIndex += stepSelectTable;
        }
    }

    private static ulong GetStoredValue(uint* presentTable, byte* skipTable, uint valueIndex, uint skipSize, uint skipMask) =>
        GetBitIndex(presentTable, skipTable, valueIndex, skipSize, skipMask) - valueIndex;

    private static ulong GetBitIndex(uint* presentTable, byte* skipTable, uint valueIndex, uint skipSize, uint skipMask)
    {
        uint lastPartSum;
        var bitTable = (byte*)presentTable;
        var bitIndex = valueIndex >= 1 << stepSelectTableBitCount 
            ? BitBool.GetBitsValue((uint*)skipTable, (valueIndex >> stepSelectTableBitCount) - 1, skipSize, skipMask) 
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

        return Select2.highBitRanks[bitTable[byteIndex - 1] * 8 + oneIndex - lastPartSum] + (byteIndex - 1 << 3);
    }

    public ulong GetStoredValue(uint valueIndex) => GetStoredValue(this.valuePresentFlags, this.indexSkipTable, valueIndex, this.skipSize, this.skipMask);

    public ulong GetBitIndex(uint valueIndex) => GetBitIndex(this.valuePresentFlags, this.indexSkipTable, valueIndex, this.skipSize, this.skipMask);

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
        return Select2.highBitRanks[bitTable[byteIndex - 1] * 8 + targetIndex - lastPartSum] + (byteIndex - 1 << 3);
    }

    public uint GetNextBitIndex(uint bitIndex) => GetNextBitIndex(this.valuePresentFlags, bitIndex);

    public Select2(byte* buffer)
    {
        //maxValue = keys[keyCount - 1];
        //var nbits = keyCount + maxValue;
        //skipEntrySize = Log2(nbits);
        //nuint flagsSize = nbits + 0x1f >> 5;
        //nuint skipTableSize = (keyCount >> Select2.stepSelectTableBitCount) + 1;
        //valuePresentFlags = (uint*)NativeMemory.Alloc(flagsSize, (nuint)sizeof(uint));
        //valueSkipTable = (byte*)NativeMemory.Alloc(skipTableSize, (nuint)skipEntrySize);
        this.keyCount = *(uint*)buffer++;
        this.maxValue = *(uint*)buffer++;
        var nbits = this.keyCount + this.maxValue;
        nuint vecSize = nbits + 0x1f >> 5;
        nuint selTableSize = (this.keyCount >> 7) + 1;
        this.valuePresentFlags = (uint*)NativeMemory.Alloc(vecSize, (nuint)sizeof(uint));
        this.indexSkipTable = (byte*)NativeMemory.Alloc(selTableSize, (nuint)sizeof(uint));
        Buffer.MemoryCopy(buffer, this.valuePresentFlags, vecSize, vecSize);
        buffer += vecSize * sizeof(uint);
        Buffer.MemoryCopy(buffer, this.indexSkipTable, selTableSize, selTableSize);
    }
    public Select2(BinaryReader reader, uint keyCount)
    {
        this.keyCount = keyCount;
        this.maxValue = reader.ReadUInt32();
        var nbits = this.keyCount + this.maxValue;
        this.skipSize = Log2(nbits);
        this.skipMask = (1u << (int)this.skipSize) - 1;
        nuint vecSize = nbits + 0x1f >> 5;
        nuint selTableSize = (nuint)((ulong)(keyCount >> Select2.stepSelectTableBitCount) * this.skipSize + 7) >> 3;
        this.valuePresentFlags = (uint*)NativeMemory.Alloc(vecSize, (nuint)sizeof(uint));
        this.indexSkipTable = (byte*)NativeMemory.Alloc(selTableSize, (nuint)sizeof(uint));
        reader.Read(new Span<byte>(this.valuePresentFlags, (int)(vecSize * sizeof(uint))));
        reader.Read(new Span<byte>(this.indexSkipTable, (int)(selTableSize * sizeof(uint))));
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
        var selTableSize = (this.keyCount >> 7) + 1;
        Buffer.MemoryCopy(this.valuePresentFlags, uintBuffer, bufferLength - 4, vecSize);
        uintBuffer += vecSize;
        Buffer.MemoryCopy(this.indexSkipTable, uintBuffer, bufferLength - 4 - vecSize, selTableSize);
    }

    public void Write(BinaryWriter writer)
    {
        //writer.Write(this.keyCount);
        writer.Write(this.maxValue);
        var nbits = this.keyCount + this.maxValue;
        var vecSize = nbits + 0x1f >> 5;
        var selTableSize = (nuint)((ulong)(keyCount >> Select2.stepSelectTableBitCount) * this.skipSize + 7) >> 3;
        writer.Write(new ReadOnlySpan<byte>(this.valuePresentFlags, (int)(vecSize * sizeof(uint))));
        writer.Write(new ReadOnlySpan<byte>(this.indexSkipTable, (int)(selTableSize * sizeof(uint))));
    }

    public static ulong GetStoredValuePacked(uint* packedSelect, uint targetIndex)
    {
        var keyCount = *packedSelect++;
        var maxValue = *packedSelect++;
        var nbits = keyCount + maxValue;
        var vecSize = nbits + 0x1f >> 5;
        return Select2.GetStoredValue(packedSelect, (byte*)(packedSelect + vecSize), targetIndex, 0, 0);
    }

    public static ulong GetBitIndexPacked(uint* packedSelect, uint targetIndex)
    {
        var keyCount = *packedSelect++;
        var maxValue = *packedSelect++;
        var nbits = keyCount + maxValue;
        var vecSize = nbits + 0x1f >> 5;
        return Select2.GetBitIndex(packedSelect, (byte*)(packedSelect + vecSize), targetIndex, 0, 0);
    }

    public static uint GetNextBitIndexPacked(uint* packedSelect, uint bitIndex) =>
        Select2.GetNextBitIndex(packedSelect + sizeof(uint) * 2, bitIndex);
}