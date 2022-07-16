using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace NetMph;

public unsafe class CompressedSequence : IDisposable
{
    private readonly uint* lengthRemainders;
    private readonly uint valueCount;
    private readonly uint remainderLength;
    private readonly Select selectTable;
    private readonly uint* storeTable;
    private readonly uint totalBitLength;

    private uint BitsTableSize => (this.valueCount * this.remainderLength + 31) >> 5;

    public uint Size => this.selectTable.Size +
                        (((this.totalBitLength + 31) >> 5) * sizeof(uint)
                        + this.BitsTableSize * sizeof(uint)
                        + 4 * sizeof(uint)) * 8u;

    private static uint Log2(uint x)
    {
        var isPowerOf2 = x & x - 1;
        isPowerOf2 |= ~isPowerOf2 + 1;
        isPowerOf2 >>= 31;
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        x -= x >> 1 & 0x55555555;
        x = (x & 0x33333333) + (x >> 2 & 0x33333333);
        return ((x + (x >> 4) & 0x0F0F0F0F) * 0x01010101 >> 24) - 1 + isPowerOf2;
    }

    private uint StoreTableSize => (this.totalBitLength + 31) >> 5;
    private uint EncodedTableSize => (this.valueCount * this.remainderLength + 0x1f) >> 5;

    private void Dispose(bool isDisposing)
    {
        if (isDisposing)
            this.selectTable.Dispose();
        NativeMemory.Free(this.lengthRemainders);
        NativeMemory.Free(this.storeTable);
    }

    ~CompressedSequence() => this.Dispose(false);

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    public CompressedSequence(uint[] valsTable)
    {
        uint i;
        this.valueCount = (uint)valsTable.Length;
        // lengths: represents lengths of encoded values	
        var bitLengths = stackalloc uint[(int)this.valueCount];

        this.totalBitLength = 0u;

        for (i = 0; i < this.valueCount; i++)
            if (valsTable[i] == 0)
                bitLengths[i] = 0;
            else
            {
                bitLengths[i] = CompressedSequence.Log2(valsTable[i] + 1);
                this.totalBitLength += bitLengths[i];
            }

        // 32 bits per record
        this.storeTable = (uint*)NativeMemory.Alloc(this.StoreTableSize, sizeof(uint));
        var bitPosition = 0u;

        for (i = 0; i < this.valueCount; i++)
        {
            if (valsTable[i] == 0)
                continue;
            var storedValue = valsTable[i] - ((1u << (int)bitLengths[i]) - 1u);
            BitBool.SetBitsAtPos(this.storeTable, bitPosition, storedValue, bitLengths[i]);
            bitPosition += bitLengths[i];
        }

        if (bitPosition > this.valueCount)
        {
            this.remainderLength = CompressedSequence.Log2(bitPosition / this.valueCount);

            if (this.remainderLength == 0)
                this.remainderLength = 1;
        }
        else
            this.remainderLength = 1;

        this.lengthRemainders = (uint*)NativeMemory.Alloc(this.EncodedTableSize, sizeof(uint));

        var remsMask = (1U << (int)this.remainderLength) - 1U;
        bitPosition = 0;

        for (i = 0; i < this.valueCount; i++)
        {
            bitPosition += bitLengths[i];
            BitBool.SetBitsValue(this.lengthRemainders, i, bitPosition & remsMask, this.remainderLength, remsMask);
            bitLengths[i] = bitPosition >> (int)this.remainderLength;
        }
        
        this.selectTable = new(bitLengths, this.totalBitLength >> (int)this.remainderLength);
    }

    public uint Query(uint idx)
    {
        uint selRes;
        uint encIdx;
        var remsMask = (uint)((1 << (int)this.remainderLength) - 1);

        if (idx == 0)
        {
            encIdx = 0;
            selRes = this.selectTable.GetBitIndex(idx);
        }
        else
        {
            selRes = this.selectTable.GetBitIndex(idx - 1);
            encIdx = (selRes - (idx - 1)) << (int)this.remainderLength;
            encIdx += BitBool.GetBitsValue(this.lengthRemainders, idx - 1, this.remainderLength, remsMask);
            selRes = this.selectTable.GetNextBitIndex(selRes);
        }
        var encLength = (selRes - idx) << (int)this.remainderLength;
        encLength += BitBool.GetBitsValue(this.lengthRemainders, idx, this.remainderLength, remsMask);
        encLength -= encIdx;
        if (encLength == 0)
            return 0;
        return BitBool.GetBitsAtPos(this.storeTable, encIdx, encLength) + (uint)((1 << (int)encLength) - 1);
    }

    //public CompressedSequence(BinaryReader reader, uint values)
    //{
    //    this.valueCount = values;
    //    this.remainderLength = reader.ReadUInt32();
    //    this.totalBitLength = reader.ReadUInt32();
    //    var bytes = reader.ReadBytes((int)this.StoreTableSize << 2);
    //    this.storeTable = (uint*)NativeMemory.Alloc(this.StoreTableSize, sizeof(uint));
    //    this.lengthRemainders = (uint*)NativeMemory.Alloc(this.EncodedTableSize, sizeof(uint));
    //    fixed (byte* fbp = bytes)
    //    {
    //        var stp = this.storeTable;
    //        var uip = (uint*)fbp;
    //        for (var i = 0; i < this.StoreTableSize; i++)
    //            *stp++ = *uip++;
    //    }

    //    bytes = reader.ReadBytes((int)this.EncodedTableSize << 2);
    //    fixed (byte* fbp = bytes)
    //    {
    //        var lrp = this.lengthRemainders;
    //        var uip = (uint*)fbp;
    //        for (var i = 0; i < this.EncodedTableSize; i++)
    //            *lrp++ = *uip++;
    //    }

    //    this.selectTable = new(reader, this.valueCount, this.totalBitLength >> (int)this.remainderLength);
    //}

    //public void Write(BinaryWriter writer)
    //{
    //    writer.Write(this.remainderLength);
    //    writer.Write(this.totalBitLength);

    //    var bytes = new byte[this.storeTable.Length << 2];
    //    unsafe
    //    {
    //        fixed (byte* fbp = bytes)
    //        fixed (uint* fbitsp = this.storeTable)
    //        {
    //            var bitsp = fbitsp;
    //            var uip = (uint*)fbp;
    //            for (var i = 0; i < this.storeTable.Length; i++)
    //                *uip++ = *bitsp++;
    //        }
    //    }
    //    writer.Write(bytes);
    //    bytes = new byte[this.lengthRemainders.Length << 2];
    //    unsafe
    //    {
    //        fixed (byte* fbp = bytes)
    //        fixed (uint* fselp = this.lengthRemainders)
    //        {
    //            var selp = fselp;
    //            var uip = (uint*)fbp;
    //            for (var i = 0; i < this.lengthRemainders.Length; i++)
    //                *uip++ = *selp++;
    //        }
    //    }
    //    writer.Write(bytes);

    //    this.selectTable.Write(writer);
    //}
}