using System;
using Xunit;

namespace NetMph.Tests;

public class ProductLogTests
{
    [Theory]
    [InlineData(-74.593580039197988, -3e-31)]
    [InlineData(-16.626508901372475, -1e-06)]
    [InlineData(-4.7841932320065395, -0.04)]
    [InlineData(-2.5426413577735265, -0.2)]
    [InlineData(-1.7813370234216279, -0.3)]
    [InlineData(-1.0707918867680508, -0.367)]
    [InlineData(-1.0409627877617027, -0.3675791)]
    public void MinusOne(double expected, double input)
    {
        var diff = Math.Abs(ProductLog.Lambert.DegreeM1(input) - expected);
        Assert.True(diff < 0.00000000000000001, $"Input of {input} should have output of {expected}, but was off by {diff}");
    }
    [Theory]
    [InlineData(4.0655187841108757, 237)]
    [InlineData(2.557483711541749, 33)]
    [InlineData(0.091276527160862264, .1)]
    [InlineData(-0.5604894830784557, -.32)]
    [InlineData(-0.96908691112626533, -.3677)]
    public void Zero(double expected, double input)
    {
        var diff = Math.Abs(ProductLog.Lambert.Degree0(input) - expected);
        Assert.True(diff < 0.00000000000000001, $"Input of {input} should have output of {expected}, but was off by {diff}");
    }
}