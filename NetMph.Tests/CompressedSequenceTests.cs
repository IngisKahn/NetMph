using System;
using System.IO;
using Xunit;

namespace NetMph.Tests;

public class CompressedSequenceTests
{
    [Fact]
    public void EmptySequence()
    {
        using var s = new CompressedSequence<uint>(Array.Empty<uint>());
        Assert.Throws<IndexOutOfRangeException>(() => s.Query(8));
    }

    [Fact]
    public void One()
    {
        using var s = new CompressedSequence<uint>(new[] { 33u });
        Assert.Equal(33u, s.Query(0));
    }

    [Fact]
    public void Two()
    {
        using var s = new CompressedSequence<uint>(new[] { 33u, 22u });
        Assert.Equal(33u, s.Query(0));
        Assert.Equal(22u, s.Query(1));
    }

    [Fact]
    public void Three()
    {
        using var s = new CompressedSequence<uint>(new[] { 33u, 22u,789789u, 31u, 32u, 0u });
        Assert.Equal(33u, s.Query(0));
        Assert.Equal(22u, s.Query(1));
        Assert.Equal(789789u, s.Query(2));

        Assert.Equal(31u, s.Query(3));

        Assert.Equal(32u, s.Query(4));
        Assert.Equal(0u, s.Query(5));
    }

    [Fact]
    public void Thousand()
    {
        Random r = new(25);
        var values = new uint[3000];
        for (var i = 0; i < values.Length; i++)
            values[i] = (uint)r.NextInt64() >> 15;
        using var s = new CompressedSequence<uint>(values);
        for (var i = 0; i < values.Length; i++)
            Assert.Equal(values[i], s.Query((uint)i));
    }

    [Fact]
    public void WriteAndRead()
    {
        Random r = new(25);
        var values = new uint[3000];
        for (var i = 0; i < values.Length; i++)
            values[i] = (uint)r.NextInt64() >> 15;
        using var s = new CompressedSequence<uint>(values);
        using MemoryStream s2 = new();
        using BinaryWriter writer = new(s2);
        s.Write(writer);
        s2.Position = 0;
        using BinaryReader reader = new(s2);
        using CompressedSequence<uint> ss = new(reader, 3000);
        for (var i = 0; i < values.Length; i++)
            Assert.Equal(values[i], ss.Query((uint)i));
    }

    [Fact]
    public void WriteAndQuery()
    {
        Random r = new(25);
        var values = new uint[3000];
        for (var i = 0; i < values.Length; i++)
            values[i] = (uint)r.NextInt64() >> 15;
        using var s = new CompressedSequence<uint>(values);
        using MemoryStream s2 = new();
        using BinaryWriter writer = new(s2);
        s.Write(writer);
        s2.Position = 0;
        using BinaryReader reader = new(s2);
        using CompressedSequence<uint> ss = new(reader, 3000);
        //CompressedSequence.
        for (var i = 0; i < values.Length; i++)
            Assert.Equal(values[i], ss.Query((uint)i));
    }
}