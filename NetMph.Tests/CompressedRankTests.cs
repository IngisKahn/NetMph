using Xunit;

namespace NetMph.Tests;

public class CompressedRankTests
{
    [Fact]
    public void SimpleRank()
    {
        using CompressedRank r = new(new uint[] { 33, 33 });
        Assert.Equal(0u, r.GetRank(0));
    }
    [Fact]
    public void SimpleRank2()
    {
        using CompressedRank r = new(new uint[] { 33, 88 });
        Assert.Equal(0u, r.GetRank(33));
        Assert.Equal(1u, r.GetRank(88));
    }
    [Fact]
    public void SimpleRank3()
    {
        using CompressedRank r = new(new uint[] { 33, 88, 122 });
        Assert.Equal(0u, r.GetRank(3));
        Assert.Equal(0u, r.GetRank(33));
        Assert.Equal(1u, r.GetRank(44));
        Assert.Equal(1u, r.GetRank(88));
        Assert.Equal(2u, r.GetRank(99));
        Assert.Equal(2u, r.GetRank(122));
        Assert.Equal(3u, r.GetRank(223));
    }

}