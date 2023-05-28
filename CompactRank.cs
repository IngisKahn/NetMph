using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NetMph;

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
        public BestSize? IndexByValue { get; init; }
    }

    public static BestSize FindBestSize(ulong count, ulong maxValue, bool hasIndex, DataOptions dataOptions = DataOptions.IncludeCount | DataOptions.IncludeMaxValue)
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
            if (count <= 128 || !hasIndex)
                return new(r, (s + 7) >> 3, s);
            var indexByValueSize = FindBestSize(newMax / 128, newMax / 128 + count, hasIndex, 0);
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

public unsafe class CompactRank<T> : IReadOnlyList<ulong>, IDisposable where T : unmanaged, INumberBase<T>
{
    private T[] skips;
    private bool isIndexable;

    internal CompactRank(T[] skips, bool isIndexable)
    {
        this.skips = skips;
        this.isIndexable = this.isIndexable;
    }

    internal CompactRank(BinaryReader reader, ulong maxValue)
    {
        this.skips = default;
        this.isIndexable = true;
    }

    public IEnumerator<ulong> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count { get; }

    public ulong this[int index] => throw new NotImplementedException();

    public ulong GetRank(ulong value) => throw new NotImplementedException();

    public void Write(BinaryWriter writer, bool b) { throw new NotImplementedException();}

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}