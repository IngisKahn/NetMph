namespace NetMph;

internal static class JenkinsHash
{
    private static void Mix(ref uint a, ref uint b, ref uint c)
    {
        a -= b;
        a -= c;
        a ^= c >> 13;
        b -= c;
        b -= a;
        b ^= a << 8;
        c -= a;
        c -= b;
        c ^= b >> 13;
        a -= b;
        a -= c;
        a ^= c >> 12;
        b -= c;
        b -= a;
        b ^= a << 16;
        c -= a;
        c -= b;
        c ^= b >> 5;
        a -= b;
        a -= c;
        a ^= c >> 3;
        b -= c;
        b -= a;
        b ^= a << 10;
        c -= a;
        c -= b;
        c ^= b >> 15;
    }

    /// <summary>
    ///     Hash the vector K in hashes
    /// </summary>
    /// <param name="seed">Hash vector hash</param>
    /// <param name="k">Key to hash</param>
    /// <param name="hashes">Vector of 3 uints to set to the hash value</param>
    public static unsafe void HashVector(uint seed, byte* k, uint length, uint* hashes)
    {
        var p = 0;
        var len = length;
        hashes[1] = 0x9e3779b9;
        hashes[0] = 0x9e3779b9;
        hashes[2] = seed;

        while (len >= 12)
        {
            hashes[0] += k[p + 0] + ((uint)k[p + 1] << 8) + ((uint)k[p + 2] << 16) + ((uint)k[p + 3] << 24);
            hashes[1] += k[p + 4] + ((uint)k[p + 5] << 8) + ((uint)k[p + 6] << 16) + ((uint)k[p + 7] << 24);
            hashes[2] += k[p + 8] + ((uint)k[p + 9] << 8) + ((uint)k[p + 10] << 16) + ((uint)k[p + 11] << 24);
            JenkinsHash.Mix(ref hashes[0], ref hashes[1], ref hashes[2]);
            p += 12;
            len -= 12;
        }

        /*------------------------------------- handle the last 11 bytes */
        hashes[2] += length;
        switch (len) /* all the case statements fall through */
        {
            case 11:
                hashes[2] += (uint)k[p + 10] << 24;
                goto case 10;
            case 10:
                hashes[2] += (uint)k[p + 9] << 16;
                goto case 9;
            case 9:
                hashes[2] += (uint)k[p + 8] << 8;
                goto case 8;
            /* the first byte of hashes[2] is reserved for the length */
            case 8:
                hashes[1] += (uint)k[p + 7] << 24;
                goto case 7;
            case 7:
                hashes[1] += (uint)k[p + 6] << 16;
                goto case 6;
            case 6:
                hashes[1] += (uint)k[p + 5] << 8;
                goto case 5;
            case 5:
                hashes[1] += k[p + 4];
                goto case 4;
            case 4:
                hashes[0] += (uint)k[p + 3] << 24;
                goto case 3;
            case 3:
                hashes[0] += (uint)k[p + 2] << 16;
                goto case 2;
            case 2:
                hashes[0] += (uint)k[p + 1] << 8;
                goto case 1;
            case 1:
                hashes[0] += k[p + 0];
                goto default;
            default:
                JenkinsHash.Mix(ref hashes[0], ref hashes[1], ref hashes[2]);
                break;
            /* case 0: nothing left to add */
        }
    }
}