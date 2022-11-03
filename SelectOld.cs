using System.Runtime.InteropServices;

namespace NetMph;

public sealed unsafe class SelectOld : IDisposable
{
    private static readonly byte* rankLookupTable;
    private static readonly byte* highBitRanks;

    static SelectOld()
    {
        static uint CountSetBitsInByte(uint id)
        {
            id -= id >> 1 & 0x55;
            id = (id & 0x33) + (id >> 2 & 0x33);
            return id + (id >> 4) & 0xF;
        }

        SelectOld.rankLookupTable = (byte*)NativeMemory.Alloc((nuint)256);
        for (var i = 0; i < 256; i++)
            SelectOld.rankLookupTable[i] = (byte)CountSetBitsInByte((uint)i);
        SelectOld.highBitRanks = (byte*)NativeMemory.Alloc((nuint)256 * 8);
        var pHighRanks = SelectOld.highBitRanks;
        var bbb = pHighRanks;
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
        return 2 * sizeof(uint) + vecSize * sizeof(uint) + selTableSize * sizeof(uint);
    }

    public uint Size => SelectOld.SizeEstimate(this.keyCount, this.maxValue);

    /// <summary>
    /// Each bit represents either to increase the counter "0" or that there exists a value equal to counter "1"
    /// </summary>
    public uint* ValuePresentFlags => valuePresentFlags;

    private static void Insert0(ref uint buffer) => buffer >>= 1;

    private static void Insert1(ref uint buffer) => buffer = buffer >> 1 | 0x80000000;

    private readonly uint keyCount, maxValue;

    /// <summary>
    /// Each bit represents either to increase the counter "0" or that there exists a value equal to counter "1"
    /// </summary>
    private readonly uint* valuePresentFlags;
    /// <summary>
    /// valueSkipTable[i] gives you the bit index(value) of the i*128th value 
    /// </summary>
    private readonly uint* valueSkipTable;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        this.Dispose(true);
    }

    private void Dispose(bool isDisposing)
    {
        if (this.ValuePresentFlags != null)
            NativeMemory.Free(this.ValuePresentFlags);
        if (this.valueSkipTable != null)
            NativeMemory.Free(this.valueSkipTable);
    }

    ~SelectOld() => this.Dispose(false);

    public SelectOld(uint[] keys)
    {
        fixed (uint* pKeys = keys)
        {
            this.keyCount = (uint)keys.Length;
            this.Generate(pKeys, out this.maxValue, out this.valuePresentFlags, out this.valueSkipTable);
        }
    }

    public SelectOld(uint* keys, uint keyCount)
    {
        this.keyCount = keyCount;
        this.Generate(keys, out this.maxValue, out this.valuePresentFlags, out this.valueSkipTable);
    }

    private void Generate(uint* keys, out uint maxValue, out uint* valuePresentFlags, out uint* valueSkipTable)
    {

        uint buffer = 0;
        if (keyCount == 0)
        {
            maxValue = 0;
            valuePresentFlags = null;
            valueSkipTable = null;
            return;
        }

        maxValue = keys[keyCount - 1];
        var nbits = keyCount + maxValue;
        var flagsSize = nbits + 0x1f >> 5;
        var skipTableSize = (keyCount >> SelectOld.stepSelectTableBitCount) + 1;
        valuePresentFlags = (uint*)NativeMemory.Alloc((nuint)flagsSize, (nuint)sizeof(uint));
        valueSkipTable = (uint*)NativeMemory.Alloc((nuint)skipTableSize, (nuint)sizeof(uint));

        int SetFlags(uint max)
        {
            var flagIndex = 0;
            for (int currentValue = 0, keyIndex = 0; ;)
            {
                while (keys[keyIndex] == currentValue)
                {
                    SelectOld.Insert1(ref buffer);
                    flagIndex++;

                    if ((flagIndex & 0x1f) == 0)
                        this.ValuePresentFlags[(flagIndex >> 5) - 1] = buffer; // (idx >> 5) = idx/32
                    keyIndex++;

                    if (keyIndex == keyCount)
                        return flagIndex;
                }

                if (currentValue == max)
                    break;

                while (keys[keyIndex] > currentValue)
                {
                    SelectOld.Insert0(ref buffer);
                    flagIndex++;

                    if ((flagIndex & 0x1f) == 0) // (idx & 0x1f) = idx % 32
                        this.ValuePresentFlags[(flagIndex >> 5) - 1] = buffer; // (idx >> 5) = idx/32
                    currentValue++;
                }
            }

            return flagIndex;
        }

        var flagIndex = SetFlags(maxValue);

        if ((flagIndex & 0x1f) != 0)
        {
            buffer >>= 0x20 - (flagIndex & 0x1f);
            this.ValuePresentFlags[flagIndex - 1 >> 5] = buffer;
        }
        this.GenerateSkipTable(keyCount);
    }

    private void GenerateSkipTable(uint keyCount)
    {
        var bitsTable = (byte*)this.ValuePresentFlags;
        var skipTableIndex = 0u;
        var targetValues = 0u;
        var valueArrayIndex = 0u;
        var values = 0u;
        while (targetValues < keyCount)
        {
            uint lastValues;
            do
            {
                lastValues = values;
                values += SelectOld.rankLookupTable[bitsTable[valueArrayIndex++]];
            } while (values <= targetValues);

            this.valueSkipTable[skipTableIndex++] = SelectOld.highBitRanks[bitsTable[valueArrayIndex - 1] * 8 + targetValues - lastValues] + (valueArrayIndex - 1 << 3);
            targetValues += stepSelectTable;
        }
    }

    private static uint GetStoredValue(uint* presentTable, uint* skipTable, uint valueIndex) =>
        GetBitIndex(presentTable, skipTable, valueIndex) - valueIndex;

    private static uint GetBitIndex(uint* presentTable, uint* skipTable, uint valueIndex)
    {
        uint lastPartSum;
        var bitTable = (byte*)presentTable;
        var bitIndex = skipTable[valueIndex >> stepSelectTableBitCount];
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

        return SelectOld.highBitRanks[bitTable[byteIndex - 1] * 8 + oneIndex - lastPartSum] + (byteIndex - 1 << 3);
    }

    public uint GetStoredValue(uint valueIndex) => GetStoredValue(this.ValuePresentFlags, this.valueSkipTable, valueIndex);

    public uint GetBitIndex(uint valueIndex) => GetBitIndex(this.ValuePresentFlags, this.valueSkipTable, valueIndex);

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
        return SelectOld.highBitRanks[bitTable[byteIndex - 1] * 8 + targetIndex - lastPartSum] + (byteIndex - 1 << 3);
    }

    public uint GetNextBitIndex(uint bitIndex) => GetNextBitIndex(this.ValuePresentFlags, bitIndex);

    public SelectOld(byte* buffer)
    {
        this.keyCount = *(uint*)buffer++;
        this.maxValue = *(uint*)buffer++;
        var nbits = this.keyCount + this.maxValue;
        var vecSize = nbits + 0x1f >> 5;
        var selTableSize = (this.keyCount >> 7) + 1;
        this.valuePresentFlags = (uint*)NativeMemory.Alloc((nuint)vecSize, (nuint)sizeof(uint));
        this.valueSkipTable = (uint*)NativeMemory.Alloc((nuint)selTableSize, (nuint)sizeof(uint));
        Buffer.MemoryCopy(buffer, this.ValuePresentFlags, vecSize, vecSize);
        buffer += vecSize * sizeof(uint);
        Buffer.MemoryCopy(buffer, this.valueSkipTable, selTableSize, selTableSize);
    }
    public SelectOld(BinaryReader reader, uint keyCount)
    {
        this.keyCount = keyCount;
        this.maxValue = reader.ReadUInt32();
        var nbits = this.keyCount + this.maxValue;
        var vecSize = nbits + 0x1f >> 5;
        var selTableSize = (this.keyCount >> 7) + 1;
        this.valuePresentFlags = (uint*)NativeMemory.Alloc((nuint)vecSize, (nuint)sizeof(uint));
        this.valueSkipTable = (uint*)NativeMemory.Alloc((nuint)selTableSize, (nuint)sizeof(uint));
        reader.Read(new Span<byte>(this.valuePresentFlags, (int)(vecSize * sizeof(uint))));
        reader.Read(new Span<byte>(this.valueSkipTable, (int)(selTableSize * sizeof(uint))));
    }

    public void Write(byte* buffer, ref uint bufferLength)
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
        Buffer.MemoryCopy(this.valueSkipTable, uintBuffer, bufferLength - 4 - vecSize, selTableSize);
    }

    public void Write(BinaryWriter writer)
    {
        //writer.Write(this.keyCount);
        writer.Write(this.maxValue);
        var nbits = this.keyCount + this.maxValue;
        var vecSize = nbits + 0x1f >> 5;
        var selTableSize = (this.keyCount >> 7) + 1;
        writer.Write(new ReadOnlySpan<byte>(this.valuePresentFlags, (int)(vecSize * sizeof(uint))));
        writer.Write(new ReadOnlySpan<byte>(this.valueSkipTable, (int)(selTableSize * sizeof(uint))));
    }

    public static uint GetStoredValuePacked(uint* packedSelect, uint targetIndex)
    {
        var keyCount = *packedSelect++;
        var maxValue = *packedSelect++;
        var nbits = keyCount + maxValue;
        var vecSize = nbits + 0x1f >> 5;
        return SelectOld.GetStoredValue(packedSelect, packedSelect + vecSize, targetIndex);
    }

    public static uint GetBitIndexPacked(uint* packedSelect, uint targetIndex)
    {
        var keyCount = *packedSelect++;
        var maxValue = *packedSelect++;
        var nbits = keyCount + maxValue;
        var vecSize = nbits + 0x1f >> 5;
        return SelectOld.GetBitIndex(packedSelect, packedSelect + vecSize, targetIndex);
    }

    public static uint GetNextBitIndexPacked(uint* packedSelect, uint bitIndex) =>
        SelectOld.GetNextBitIndex(packedSelect + sizeof(uint) * 2, bitIndex);
}