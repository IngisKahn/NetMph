using Xunit;

namespace NetMph.Tests;

public class SelectTests
{
    [Fact]
    public void T1()
    {
        Select s = new(
            new uint[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 4,
                4
            }, 4);
        var q = s.Query(32);
        var n = s.NextQuery(32);
    }
}