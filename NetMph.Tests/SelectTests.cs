using System.Linq;
using Xunit;

namespace NetMph.Tests;

public class SelectTests
{
    [Fact]
    public void T1()
    {
        using Select s = new(
            new uint[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 4,
                4
            });
        var q = s.Query(32);
        var n = s.NextQuery(33);
    }

    [Fact]
    public void Get2()
    {
        using Select s = new(
            new uint[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 4,
                4
            });
        var q = s.Query(32);
        Assert.Equal(4u, q);

    }
    [Fact]
    public void Get3()
    {
        using Select s = new(
            new uint[]
            {
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4,
                    4, 4,
                    4
            });
        var q = s.Query(31);
        Assert.Equal(0u, q);
    }
    [Fact]
    public void Get4()
    { var vals = new uint[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4,
                4, 4,
                4
            };
        using Select s = new(vals);

        for (var i = 0u; i < vals.Length; i++)
        {
            var q = s.Query(i);
            Assert.Equal(vals[i], q);
        }
    }

    [Fact]
    public void GetRange1to50()
    {
        var vals = Enumerable.Range(1, 50).Select(i => (uint)i).ToArray();
        using Select s = new(vals);

        for (var i = 0u; i < vals.Length; i++)
        {
            var q = s.Query(i);
            Assert.Equal(vals[i], q);
        }
    }

    [Fact]
    public void GetRange1to500()
    {
        var vals = Enumerable.Range(1, 500).Select(i => (uint)i).ToArray();
        using Select s = new(vals);

        for (var i = 0u; i < vals.Length; i++)
        //var i = 128u;
        {
            var q = s.Query(i);
            Assert.Equal(vals[i], q);
        }
    }
}