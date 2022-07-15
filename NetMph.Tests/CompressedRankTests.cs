using System.Linq;
using Xunit;

namespace NetMph.Tests;

public class CompressedRankTests
{
    [Fact]
    public void SimpleRank()
    {
        CompressedRank r = new(new uint[] { 33 });
        Assert.Equal(0u, r[0]);
    }
    [Fact]
    public void SimpleRank2()
    {
        CompressedRank r = new(new uint[] { 33, 88 });
        Assert.Equal(0u, r[33]);
        Assert.Equal(1u, r[88]);
    }
    [Fact]
    public void SimpleRank3()
    {
        CompressedRank r = new(new uint[] { 33, 88, 122 });
        Assert.Equal(0u, r[3]);
        Assert.Equal(0u, r[33]);
        Assert.Equal(1u, r[44]);
        Assert.Equal(1u, r[88]);
        Assert.Equal(2u, r[99]);
        Assert.Equal(2u, r[122]);
        Assert.Equal(3u, r[222]);
    }

}

