using System;
using Xunit;

namespace NetMph.Tests;

public class CompressedSequenceTests
{
    [Fact]
    public void EmptySequence()
    {
        using var s = new CompressedSequence(Array.Empty<uint>());
        Assert.Throws<IndexOutOfRangeException>(() => s.Query(8));
    }

    [Fact]
    public void One()
    {
        using var s = new CompressedSequence(new[] { 33u });
        Assert.Equal(33u, s.Query(0));
    }

    [Fact]
    public void Two()
    {
        using var s = new CompressedSequence(new[] { 33u, 22u });
        Assert.Equal(33u, s.Query(0));
        Assert.Equal(22u, s.Query(1));
    }

    [Fact]
    public void Three()
    {
        using var s = new CompressedSequence(new[] { 33u, 22u,789789u });
        Assert.Equal(33u, s.Query(0));
        Assert.Equal(22u, s.Query(1));
        Assert.Equal(789789u, s.Query(2));
    }

    [Fact]
    public void Thousand()
    {
        Random r = new(25);
        var values = new uint[2222];
        for (var i = 0; i < values.Length; i++)
            values[i] = (uint)r.NextInt64() >> 15;
        using var s = new CompressedSequence(values);
        for (var i = 0; i < values.Length; i++)
            Assert.Equal(values[i], s.Query((uint)i));
    }
}