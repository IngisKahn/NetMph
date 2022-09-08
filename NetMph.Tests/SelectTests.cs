using System.Linq;
using Xunit;

namespace NetMph.Tests;

public class SelectTests
{
    [Fact]
    public void ItRuns()
    {
        using Select2 s = new(
            new uint[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 4,
                4
            });
        var q = s.GetStoredValue(32);
        var n = s.GetNextBitIndex(33);
    }

    [Fact]
    public void GetFirstNonZeroValue()
    {
        using Select2 s = new(
            new uint[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 4,
                4
            });
        var q = s.GetStoredValue(32);
        Assert.Equal(4u, q);

    }
    [Fact]
    public void GetLastZeroValue()
    {
        using Select2 s = new( 
            new uint[]
            {
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4,
                    4, 4,
                    4
            });
        var q = s.GetStoredValue(31);
        Assert.Equal(0u, q);
    }
    [Fact]
    public void GetAllValues()
    { var vals = new uint[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4,
                4, 4,
                4
            };
        using Select2 s = new(vals);

        for (var i = 0u; i < vals.Length; i++)
        {
            var q = s.GetStoredValue(i);
            Assert.Equal(vals[i], q);
        }
    }

    [Fact]
    public void GetRange1to50()
    {
        var vals = Enumerable.Range(1, 50).Select(i => (uint)i).ToArray();
        using Select2 s = new(vals);

        for (var i = 0u; i < vals.Length; i++)
        {
            var q = s.GetStoredValue(i);
            Assert.Equal(vals[i], q);
        }
    }

    [Fact]
    public void GetRange1to500()
    {
        var vals = Enumerable.Range(1, 500).Select(i => (uint)i).ToArray();
        using Select2 s = new(vals);

        //for (var i = 0u; i < vals.Length; i++)
        var i = 128u;
        {
            var q = s.GetStoredValue(i);
            Assert.Equal(vals[i], q);
        }
    }
}