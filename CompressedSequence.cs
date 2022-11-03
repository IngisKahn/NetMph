using System.Collections;
using System.Numerics;
using System.Runtime.InteropServices;

namespace NetMph;

public unsafe class CompressedSequence<T> : IDisposable, IEnumerable<T>
    where T : unmanaged,
              IBinaryInteger<T>,
                INumberBase<T>,

IConvertible
{
    //private readonly uint* lengthRemainders;
    private readonly ulong valueCount;
    //private readonly uint remainderLength;
    //private readonly Select selectTable;
    private readonly ulong* storeTable;
    private readonly ulong totalBitLength;
    private readonly CompressedRank<ulong> rank;

    //private uint BitsTableSize => (this.valueCount * this.remainderLength + 63) >> 6;

    //public uint Size => this.selectTable.Size +
    //                    (((this.totalBitLength + sizeof(T) >> 5) * sizeof(uint)
    //                    + this.BitsTableSize * sizeof(uint)
    //                    + 3 * sizeof(uint));

    private ulong StoreTableSize => (this.totalBitLength + 63) >> 6;

    private void Dispose(bool isDisposing)
    {
        if (isDisposing)
            this.rank.Dispose();
        //NativeMemory.Free(this.lengthRemainders);
        NativeMemory.Free(this.storeTable);
    }

    public IEnumerator<T> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public class CompressedSequenceEnumerable : IEnumerator<T>
    {
        public bool MoveNext()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public T Current { get; }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    ~CompressedSequence() => this.Dispose(false);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    public CompressedSequence(T[] valuesTable, bool isIndexable = true)
    {
        uint i;
        this.valueCount = (uint)valuesTable.Length;
        // lengths: represents lengths of encoded values	
        var bitLengths = new ulong[this.valueCount];
        try
        {

            this.totalBitLength = 0;
            // store length in bitLengths, length - 1 added to total (first bit is always 1)
            for (i = 0; i < this.valueCount; i++)
                if (valuesTable[i] == T.Zero)
                    bitLengths[i] = 0;
                else
                {
                    bitLengths[i] = (ulong)(valuesTable[i] + T.One).GetShortestBitLength();
                    this.totalBitLength += bitLengths[i] - 1;
                }

            // 32 bits per record
            this.storeTable = (ulong*)NativeMemory.Alloc((nuint)this.StoreTableSize, sizeof(ulong));
            var bitPosition = 0ul;

            for (i = 0; i < this.valueCount; i++)
            {
                if (valuesTable[i] > T.One)
                {
                    var storedValue = valuesTable[i] + T.One - (T.One << (int) bitLengths[i] - 1);
                    BitBool.SetBitsAtPos(this.storeTable, bitPosition, storedValue,
                        (uint) bitLengths[i] - 1);

                // remove msb
                    bitPosition += bitLengths[i] - 1;
                }

                bitLengths[i] = bitPosition;
            }

            this.rank = new(bitLengths, isIndexable);

            //if (bitPosition > this.valueCount)
            //{
            //    this.remainderLength = uint.Log2((bitPosition + this.valueCount) / this.valueCount) - 1;

            //    if (this.remainderLength == 0)
            //        this.remainderLength = 1;
            //}
            //else
            //    this.remainderLength = 1;

            //this.lengthRemainders = (uint*)NativeMemory.Alloc(this.BitsTableSize, sizeof(uint));

            //var remsMask = (1U << (int)this.remainderLength) - 1U;
            //bitPosition = 0;

            //for (i = 0; i < this.valueCount; i++)
            //{
            //    bitPosition += bitLengths[i];
            //    BitBool.SetBitsValue(this.lengthRemainders, i, bitPosition & remsMask, this.remainderLength, remsMask);
            //    bitLengths[i] = bitPosition >> (int)this.remainderLength;
            //}

            //this.selectTable = new(bitLengths, this.valueCount, isIndexable);
        }
        finally
        {
            //NativeMemory.Free(bitLengths);
        }
    }

    public ulong Query(uint idx)
    {
        //if (idx > this.valueCount)
            throw new IndexOutOfRangeException();

        //uint selRes;
        //uint encIdx;
        //var remsMask = (uint)((1 << (int)this.remainderLength) - 1);

        //if (idx == 0)
        //{
        //    encIdx = 0;
        //    selRes = (uint) this.selectTable.GetBitIndex(idx);
        //}
        //else
        //{
        //    selRes = (uint) this.selectTable.GetBitIndex(idx - 1);
        //    encIdx = (selRes - (idx - 1)) << (int)this.remainderLength;
        //    encIdx += BitBool.GetBitsValue(this.lengthRemainders, idx - 1, this.remainderLength, remsMask);
        //    selRes = this.selectTable.GetNextBitIndex(selRes);
        //}
        //var encLength = (selRes - idx) << (int)this.remainderLength;
        //encLength += BitBool.GetBitsValue(this.lengthRemainders, idx, this.remainderLength, remsMask);
        //encLength -= encIdx;
        //if (encIdx > 0)
        //    encIdx -= idx;
        //if (encLength == 0)
        //    return 0;
        //return BitBool.GetBitsAtPos(this.storeTable, encIdx, encLength - 1) + (1u << (int)encLength - 1);
    }

    public CompressedSequence(BinaryReader reader, ulong? valueCount = null, ulong? bitCount = null, bool isIndexable = true)
    {
        this.valueCount = valueCount ?? (ulong)reader.Read7BitEncodedInt64();
        //this.remainderLength = reader.ReadByte();
        this.totalBitLength = bitCount ?? (ulong)reader.Read7BitEncodedInt64();
        this.storeTable = (ulong*)NativeMemory.Alloc((nuint)this.StoreTableSize, sizeof(ulong));
        reader.Read(new Span<byte>(this.storeTable, (int)(this.StoreTableSize * sizeof(ulong))));
        //this.lengthRemainders = (uint*)NativeMemory.Alloc(this.BitsTableSize, sizeof(uint));
        //reader.Read(new Span<byte>(this.lengthRemainders, (int)(this.BitsTableSize * sizeof(uint))));

        //this.selectTable = new(reader, this.valueCount);
        this.rank = new(reader, this.valueCount, this.totalBitLength, isIndexable);
    }

    public void Write(BinaryWriter writer, bool writeCount = true, bool writeLength = true)
    {
        if (writeCount)
            writer.Write7BitEncodedInt64((long)this.valueCount);
        if (writeLength)
            writer.Write7BitEncodedInt64((long)this.totalBitLength);
        writer.Write(new Span<byte>(this.storeTable, (int)(this.StoreTableSize * sizeof(ulong))));
        //writer.Write(new Span<byte>(this.lengthRemainders, (int)(this.BitsTableSize * sizeof(uint))));

        //this.selectTable.Write(writer);
        this.rank.Write(writer, false);
    }
}
//public unsafe class CompressedSequence64 : IDisposable
//{
//    private readonly uint* lengthRemainders;
//    private readonly uint valueCount;
//    private readonly uint remainderLength;
//    private readonly SelectOld selectOldTable;
//    private readonly ulong* storeTable;
//    private readonly uint totalBitLength;

//    private uint BitsTableSize => (this.valueCount * this.remainderLength + 31) >> 5;

//    public uint Size => this.selectOldTable.Size +
//                        (((this.totalBitLength + 31) >> 5) * sizeof(uint)
//                        + this.BitsTableSize * sizeof(uint)
//                        + 3 * sizeof(uint));

//    private static uint Log2(uint x)
//    {
//        //var isPowerOf2 = x & x - 1;
//        //isPowerOf2 |= ~isPowerOf2 + 1;
//        //isPowerOf2 >>= 31;
//        x |= x >> 1;
//        x |= x >> 2;
//        x |= x >> 4;
//        x |= x >> 8;
//        x |= x >> 16;
//        x -= x >> 1 & 0x55555555;
//        x = (x & 0x33333333) + (x >> 2 & 0x33333333);
//        return ((x + (x >> 4) & 0x0F0F0F0F) * 0x01010101 >> 24);// - 1 + isPowerOf2;
//    }

//    private static uint Log2(ulong x)
//    {
//        //var isPowerOf2 = x & x - 1;
//        //isPowerOf2 |= ~isPowerOf2 + 1;
//        //isPowerOf2 >>= 31;
//        x |= x >> 1;
//        x |= x >> 2;
//        x |= x >> 4;
//        x |= x >> 8;
//        x |= x >> 16;
//        x |= x >> 32;
//        x -= x >> 1 & 0x5555555555555555u;
//        x = (x & 0x3333333333333333u) + (x >> 2 & 0x3333333333333333);
//        return (uint)((x + (x >> 4) & 0x0F0F0F0F0F0F0F0F) * 0x0101010101010101 >> 56);// - 1 + isPowerOf2;
//    }

//    private uint StoreTableSize => (this.totalBitLength + 63) >> 6;

//    private void Dispose(bool isDisposing)
//    {
//        if (isDisposing)
//            this.selectOldTable.Dispose();
//        NativeMemory.Free(this.lengthRemainders);
//        NativeMemory.Free(this.storeTable);
//    }

//    ~CompressedSequence64() => this.Dispose(false);

//    public void Dispose()
//    {
//        this.Dispose(true);
//        GC.SuppressFinalize(this);
//    }

//    public CompressedSequence64(ulong[] valsTable)
//    {
//        uint i;
//        this.valueCount = (uint)valsTable.Length;
//        // lengths: represents lengths of encoded values	
//        var bitLengths = (uint*)NativeMemory.Alloc(this.valueCount, sizeof(uint));
//        try
//        {

//            this.totalBitLength = 0u;
//            // store length in bitLengths, length - 1 added to total (first bit is always 1)
//            for (i = 0; i < this.valueCount; i++)
//                if (valsTable[i] == 0)
//                    bitLengths[i] = 0;
//                else
//                {
//                    bitLengths[i] = CompressedSequence64.Log2(valsTable[i]);
//                    this.totalBitLength += bitLengths[i] - 1;
//                }

//            // 32 bits per record
//            this.storeTable = (ulong*)NativeMemory.Alloc(this.StoreTableSize, sizeof(ulong));
//            var bitPosition = 0u;

//            for (i = 0; i < this.valueCount; i++)
//            {
//                if (valsTable[i] <= 1)
//                    continue;
//                // remove msb
//                var storedValue = valsTable[i] - ((1u << (int)bitLengths[i] - 1) - 0u);
//                BitBool.SetBitsAtPos(this.storeTable, bitPosition, storedValue, bitLengths[i] - 1);

//                //Console.Write($"{bitPosition},{storedValue:X},{bitLengths[i] - 1} ");

//                bitPosition += bitLengths[i] - 1;
//            }
//            //Console.WriteLine();

//            if (bitPosition > this.valueCount)
//            {
//                this.remainderLength = CompressedSequence64.Log2((bitPosition + this.valueCount) / this.valueCount) - 1;

//                if (this.remainderLength == 0)
//                    this.remainderLength = 1;
//            }
//            else
//                this.remainderLength = 1;

//            this.lengthRemainders = (uint*)NativeMemory.Alloc(this.BitsTableSize, sizeof(uint));

//            var remsMask = (1U << (int)this.remainderLength) - 1U;
//            bitPosition = 0;

//            for (i = 0; i < this.valueCount; i++)
//            {
//                bitPosition += bitLengths[i];
//                BitBool.SetBitsValue(this.lengthRemainders, i, bitPosition & remsMask, this.remainderLength, remsMask);
//                bitLengths[i] = bitPosition >> (int)this.remainderLength;
//            }

//            this.selectOldTable = new(bitLengths, this.valueCount);
//        }
//        finally
//        {
//            NativeMemory.Free(bitLengths);
//        }
//    }

//    public ulong Query(uint idx)
//    {
//        if (idx > this.valueCount)
//            throw new IndexOutOfRangeException();

//        uint selRes;
//        uint encIdx;
//        var remsMask = (uint)((1 << (int)this.remainderLength) - 1);

//        if (idx == 0)
//        {
//            encIdx = 0;
//            selRes = this.selectOldTable.GetBitIndex(idx);
//        }
//        else
//        {
//            selRes = this.selectOldTable.GetBitIndex(idx - 1);
//            encIdx = (selRes - (idx - 1)) << (int)this.remainderLength;
//            encIdx += BitBool.GetBitsValue(this.lengthRemainders, idx - 1, this.remainderLength, remsMask);
//            selRes = this.selectOldTable.GetNextBitIndex(selRes);
//        }
//        var encLength = (selRes - idx) << (int)this.remainderLength;
//        encLength += BitBool.GetBitsValue(this.lengthRemainders, idx, this.remainderLength, remsMask);
//        encLength -= encIdx;
//        if (encIdx > 0)
//            encIdx -= idx;
//        if (encLength == 0)
//            return 0;
//        return BitBool.GetBitsAtPos(this.storeTable, encIdx, encLength - 1) + (1u << (int)encLength - 1);
//    }

//    public CompressedSequence64(BinaryReader reader, uint values)
//    {
//        this.valueCount = values;
//        this.remainderLength = reader.ReadByte();
//        this.totalBitLength = (uint)reader.Read7BitEncodedInt64();
//        this.storeTable = (ulong*)NativeMemory.Alloc(this.StoreTableSize, sizeof(ulong));
//        reader.Read(new Span<byte>(this.storeTable, (int)(this.StoreTableSize * sizeof(ulong))));
//        this.lengthRemainders = (uint*)NativeMemory.Alloc(this.BitsTableSize, sizeof(uint));
//        reader.Read(new Span<byte>(this.lengthRemainders, (int)(this.BitsTableSize * sizeof(uint))));

//        this.selectOldTable = new(reader, this.valueCount);//, this.totalBitLength >> (int)this.remainderLength);
//    }

//    public void Write(BinaryWriter writer)
//    {
//        writer.Write((byte)this.remainderLength);
//        writer.Write7BitEncodedInt64(this.totalBitLength);
//        writer.Write(new Span<byte>(this.storeTable, (int)(this.StoreTableSize * sizeof(ulong))));
//        writer.Write(new Span<byte>(this.lengthRemainders, (int)(this.BitsTableSize * sizeof(uint))));

//        this.selectOldTable.Write(writer);
//    }
//}