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
        Span<ulong> vals = stackalloc ulong[] { 2, 7, 61 };
        foreach (var t in vals)
        {
            var a = Math.Min(n - 2, t);
            var now = PowCore(d, n, a);// BigInteger.ModPow(a, d, n); // pow(a, d, n);
            if (now == 1 || now == n - 1)
                continue;
            ulong j;
            for (j = 1u; j < s; j++)
            {
                now = Pow2Core(n, now);// BigInteger.ModPow(now, 2, n); //.) = mul(now, now, n);
                if (now == n - 1)
                    break;
            }
            if (j == s)
                return false;
        }
        return true;
    }
    private static uint PowCore(uint power, uint modulus, ulong value)
    {
        // The 32-bit modulus pow algorithm for the last or
        // the only power limb using square-and-multiply.
        var result = 1ul;
        while (power != 0)
        {
            if ((power & 1) == 1)
                result = result * value % modulus;
            if (power != 1)
                value = value * value % modulus;
            power >>= 1;
        }

        return (uint)(result % modulus);
    }
    private static uint Pow2Core(uint modulus, ulong value) => (uint)(value * value % modulus);
}