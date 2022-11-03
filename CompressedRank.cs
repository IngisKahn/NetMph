using System.Collections;
using System.Numerics;
using System.Runtime.InteropServices;

namespace NetMph;

public sealed unsafe class CompressedRank<T> : IDisposable, IEnumerable<T>
    where T : unmanaged,
              IConvertible,
              IBinaryInteger<T>
{
    //public static uint SizeEstimate(T count, T maxValue, bool isIndexable = true, bool includeSize = true)
    //{
    //    if (count > maxValue)
    //        (maxValue, count) = (count, maxValue);

    //    var r = T.Log2(T.CreateChecked(double.Round(maxValue.ToDouble(null) * .69314718 / count.ToDouble(null))));
    //    var size = (r * count).ToUInt32(null);
    //    if (includeSize)
    //        size += SevenBitIntegerSize(count) + SevenBitIntegerSize(maxValue);

    //    return size + Select.SizeEstimate(count.ToUInt32(null), maxValue.ToUInt32(null), isIndexable, false);
    //}

    // I give up trying to find the derivative of a non-continuous recursive function
    // so just do a trinary search
    public static (int remainderSizes, ulong sizeInBytes, ulong sizeInBits)[] FindBestSize(ulong count, ulong maxValue,
        bool isIndexable = true, bool includeCount = true, bool includeMaxValue = true)
    {
        (int, ulong, ulong)[]? result = null;
        if (count > maxValue)
            (count, maxValue) = (maxValue, count);

        (int, ulong, ulong)[] ComputeSize(int r)
        {
            var s = count * (ulong)r;
            var newMax = maxValue >> r;
            if (newMax == 0) 
                return new[] {(r, (s + 7) >> 3, s)};
            s += count + newMax;
            if (count <= 128 || !isIndexable) 
                return new[] {(r, (s + 7) >> 3, s)};
            var subResult = FindBestSize(count / 128, newMax + count / 128, isIndexable, false, false);
            s += subResult[0].sizeInBits;
            return subResult.Prepend((r, (s + 7) >> 3, s)).ToArray();

        }

        var remainder = (int)ulong.Log2(maxValue) + 1;
        var a = -1;
        var d = remainder + 1;
        var b = d / 3;
        var c = d - b;
        var bValue = ComputeSize(b);
        var cValue = ComputeSize(c);

        do
        {
            switch (bValue[0].Item3.CompareTo(cValue[0].Item3))
            {
                case -1:
                    if (b + 1 == c) // touching tips
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

        var r0 = result[0];
        var size = r0.Item3;
        if (includeCount)
            size += SevenBitIntegerSize(count) << 3;
        if (includeMaxValue)
            size += SevenBitIntegerSize(maxValue) << 3;
        r0.Item3 = size;
        r0.Item2 = size >> 3;
        result[0] = r0;
        return result;

    }

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

    //public uint Size => this.sel.Size + this.BitsTableSize * sizeof(uint) * 8 + 3 * sizeof(uint) * 8;
    //private uint BitsTableSize => (this.count * this.remainderBitLength + 31u) >> 5;

    private readonly ulong maxValue;
    private readonly ulong count;
    private readonly uint remainderBitLength;
    private readonly Select sel;
    private readonly ulong* valueRemainders;

    private static void Initialize(IReadOnlyList<T> values, bool isIndexable, Func<ulong, ulong, bool, (int, ulong, ulong)[]> findBestSizes, int subIndex, out ulong count, out ulong maxValue, out uint remainderBitLength, out ulong* valueRemainders, out Select select)
    {
        count = (ulong)values.Count;
        maxValue = values[^1].ToUInt64(null);
        ulong minimum, maximum;
        if (count >= maxValue)
        {
            maximum = count;
            minimum = maxValue;
        }
        else
        {
            maximum = maxValue;
            minimum = count;
        }
        // d/dx(cr + c + m/2^r) -> r = log2(ln(2) * m / c)
        //this.remainderBitLength = (uint)ulong.Log2((ulong)double.Round(maximum * .69314718 / minimum));

        //var bestSize = FindBestSize(minimum, maximum, isIndexable);
        var bestSizes = findBestSizes(minimum, maximum, isIndexable);

        remainderBitLength = (uint)bestSizes[subIndex].Item1;

        //if (this.remainderBitLength == 0)
        //    this.remainderBitLength = 1;
        var maxSignificant = maximum >> (int)remainderBitLength;
        var selectVector = new ulong[count >= maxValue ? (int)maxSignificant : (int)count];
        valueRemainders = (ulong*)NativeMemory.Alloc((nuint)(minimum * remainderBitLength + 63ul) >> 6, sizeof(ulong));
        var remainderMask = (1ul << (int)remainderBitLength) - 1u;
        if (remainderBitLength > 0)
        {
            if (count >= maxValue)
            {
                var valueIndex = 0ul;
                for (var currentValue = 1u; currentValue <= maxSignificant; currentValue++)
                {
                    while (currentValue > values[(int)valueIndex].ToUInt64(null) >> (int)remainderBitLength)
                        valueIndex++;
                    selectVector[currentValue - 1] = valueIndex >> (int)remainderBitLength;
                    BitBool.SetBitsValue(valueRemainders, currentValue - 1, valueIndex & remainderMask,
                        remainderBitLength,
                        remainderMask);
                }
            }
            else
                for (var i = 0u; i < count; i++)
                {
                    BitBool.SetBitsValue(valueRemainders, i, values[(int)i].ToUInt64(null) & remainderMask,
                        remainderBitLength,
                        remainderMask);
                    selectVector[i] = values[(int)i].ToUInt64(null) >> (int)remainderBitLength;
                }
        }
        else
        {
            if (count >= maxValue)
            {
                var valueIndex = 0u;
                for (var currentValue = 1u; currentValue <= maxSignificant; currentValue++)
                {
                    while (currentValue > values[(int)valueIndex].ToUInt32(null))
                        valueIndex++;
                    selectVector[currentValue - 1] = valueIndex;
                }
            }
            else
                for (var i = 0u; i < count; i++)
                    selectVector[i] = values[(int)i].ToUInt32(null);
        }

        select = new(selectVector, isIndexable ? bestSizes : null, subIndex + 1);
    }

    public CompressedRank(IReadOnlyList<T> values, bool isIndexable = true) // must be a sorted list of values
    {
        Initialize(values, isIndexable, (c, m, i) => FindBestSize(c, m, i), 0, out this.count, out this.maxValue, out this.remainderBitLength, out this.valueRemainders, out this.sel);

    }
    internal CompressedRank(IReadOnlyList<T> values, (int, ulong, ulong)[] sizes, int sizeIndex = 0, bool isIndexable = true) // must be a sorted list of values
    {
        Initialize(values, isIndexable, (_, _, _) => sizes, sizeIndex, out this.count, out this.maxValue, out this.remainderBitLength, out this.valueRemainders, out this.sel);
        
    }

    public CompressedRank(BinaryReader reader, ulong? valueCount = null, ulong? maxValue = null, bool isIndexable = true)
    {
        this.count = valueCount ?? (ulong)reader.Read7BitEncodedInt64();
        this.maxValue = maxValue ?? (ulong)reader.Read7BitEncodedInt64();

        this.sel = new(reader, isIndexable);
    }

    public ulong GetRank(uint valueIndex)
    {
        if (valueIndex > this.maxValue)
            return this.count;

        // TODO: get both versions

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
            selRes = (uint)(this.sel.GetBitIndex(valueSignificant - 1) + 1);
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

    public void Write(BinaryWriter writer, bool writeSizes = true)
    {
        if (writeSizes)
        {
            writer.Write7BitEncodedInt64((long)this.count);
            writer.Write7BitEncodedInt64((long)this.maxValue);
        }


        var minimum = this.count >= this.maxValue ? this.maxValue : this.count;

        if (this.remainderBitLength > 0)
            writer.Write(new Span<byte>(this.valueRemainders, (int)(this.remainderBitLength * minimum + 7) >> 3));

        this.sel.Write(writer, false, false);
    }

    public class RankEnumerator : IEnumerator<uint>
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

    public IEnumerator<T> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    ~CompressedRank() => this.Dispose(false);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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