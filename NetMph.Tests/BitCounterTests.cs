using System.Linq;
using Xunit;

namespace NetMph.Tests;

public class BitCounterTests
{
    [Fact]
    public void ItRuns()
    {
        using BitCounter s = new(
            new ulong[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 4,
                4
            });
        var q = s.GetValueAtIndex(32);
        var n = s.GetNextBitIndex(33);
    }

    [Fact]
    public void GetFirstNonZeroValue()
    {
        using BitCounter s = new(
            new ulong[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 4,
                4
            });
        var q = s.GetValueAtIndex(32);
        Assert.Equal(4u, q);

    }
    [Fact]
    public void GetLastZeroValue()
    {
        using BitCounter s = new( 
            new ulong[]
            {
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4,
                    4, 4,
                    4
            });
        var q = s.GetValueAtIndex(31);
        Assert.Equal(0u, q);
    }
    [Fact]
    public void GetAllValues()
    { var vals = new ulong[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4,
                4, 4,
                4
            };
        using BitCounter s = new(vals);

        for (var i = 0u; i < vals.Length; i++)
        {
            var q = s.GetValueAtIndex(i);
            Assert.Equal(vals[i], q);
        }
    }

    [Fact]
    public void GetRange1to50()
    {
        var vals = Enumerable.Range(1, 50).Select(i => (ulong)i).ToArray();
        using BitCounter s = new(vals);

        for (var i = 0u; i < vals.Length; i++)
        {
            var q = s.GetValueAtIndex(i);
            Assert.Equal(vals[i], q);
        }
    }

    [Fact]
    public void GetRange1to500()
    {
        var vals = Enumerable.Range(1, 500).Select(i => (ulong)i).ToArray();
        using BitCounter s = new(vals, true);

        //for (var i = 0u; i < vals.Length; i++)
        var i = 128u;
        {
            var q = s.GetValueAtIndex(i);
            Assert.Equal(vals[i], q);
        }
    }

    [Fact]
    public void Test1()
    {
        using BitCounter s = new(
            new ulong[]
            {
                2, 2
            });
        var q = s.GetBitIndexOfValue(2);
        Assert.Equal(2u, q);

    }

    [Fact]
    public void Test2()
    {
        using BitCounter s = new(
            new ulong[]
            {
                1, 2
            });
        var q = s.GetBitIndexOfValue(2);
        Assert.Equal(2u, q);

    }

    [Fact]
    public void Test3()
    {
        using BitCounter s = new(
            new ulong[]
            {
                2, 4,4,6
            });
        var q = s.GetBitIndexOfValue(1);
        Assert.Equal(0u, q);
        q = s.GetBitIndexOfValue(2);
        Assert.Equal(1u, q);
        q = s.GetBitIndexOfValue(3);
        Assert.Equal(1u, q);
        q = s.GetBitIndexOfValue(4);
        Assert.Equal(3u, q);
        q = s.GetBitIndexOfValue(5);
        Assert.Equal(3u, q);
        q = s.GetBitIndexOfValue(6);
        Assert.Equal(4u, q);
        q = s.GetBitIndexOfValue(7);
        Assert.Equal(4u, q);

    }

}