namespace NetMph.Tests;

using Xunit;

using NetMph;

public class BitBoolTests
{
    [Fact]
    public void GetBitReturns0()
    {
        unsafe
        {
            var array = stackalloc byte[] { 0b00000000, 0b00000000 };
            Assert.Equal(0, BitBool.GetBit(array, 0));
        }
    }
    [Fact]
    public void GetBitReturnsEven()
    {
        unsafe
        {
            var array = stackalloc byte[] { 0b10101010, 0b10101010 };
            for (var i = 0; i < 16; i++)
                Assert.Equal(i & 1, BitBool.GetBit(array, i));
        }
    }
    [Fact]
    public void GetBitReturnsOdd()
    {
        unsafe
        {
            var array = stackalloc byte[] { 0b1010101, 0b1010101 };
            for (var i = 0; i < 16; i++)
                Assert.Equal((i & 1) ^ 1, BitBool.GetBit(array, i));
        }
    }

    [Fact]
    public void SetBit()
    {
        unsafe
        {
            var array = stackalloc byte[] { 0, 0 };
            BitBool.SetBit(array, 8);
            Assert.Equal(1, array[1]);
        }
    }
}