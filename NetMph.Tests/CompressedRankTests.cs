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
        CompressedRank r = new(new uint[] { 33,88 });
        Assert.Equal(0u, r[33]);
        Assert.Equal(1u, r[88]);
    }

}

