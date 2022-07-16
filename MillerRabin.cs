using System.Numerics;

namespace NetMph;

/// <summary>
///     Miller–Rabin primality test
/// </summary>
internal static class MillerRabin
{
    /// <summary>
    ///     Check if value n is a prime number
    /// </summary>
    /// <param name="n">Number to check</param>
    /// <returns>true if n is prime</returns>
    public static bool CheckPrimality(uint n)
    {
        if ((n & 1) == 0 || n % 3 == 0 || n % 5 == 0 || n % 7 == 0)
            return false;

        var s = 0UL;
        var d = n - 1;
        do
        {
            s++;
            d >>= 1;
        } while ((d & 1) == 0);
        var vals = new ulong[] { 2, 7, 61 };
        foreach (var t in vals)
        {
            var a = Math.Min(n - 2, t);
            var now = BigInteger.ModPow(a, d, n); // pow(a, d, n);
            if (now == 1 || now == n - 1)
                continue;
            ulong j;
            for (j = 1u; j < s; j++)
            {
                now = BigInteger.ModPow(now, 2, n); //.) = mul(now, now, n);
                if (now == n - 1)
                    break;
            }
            if (j == s)
                return false;
        }
        return true;
    }
}