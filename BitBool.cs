using System.Runtime.InteropServices;

namespace NetMph;

internal static unsafe class BitBool
{
    private static readonly byte* bitMask;
    private static readonly uint* bitMask32;
    private static readonly byte* valueMask;

    static BitBool()
    {
        BitBool.bitMask = (byte*)NativeMemory.Alloc(8);
        for (var i = 0; i < 8; i++)
            BitBool.bitMask[i] = (byte)(1 << i);
        BitBool.bitMask32 = (uint*)NativeMemory.Alloc(32, 4);
        for (var i = 0; i < 32; i++)
            BitBool.bitMask32[i] = 1u << i;
        BitBool.valueMask = (byte*)NativeMemory.Alloc(4);
        *(uint*) BitBool.valueMask = 0x3FCFF3FC;
    }

    public static int GetBit(byte* array, int index) =>
        (array[index >> 3] & BitBool.bitMask[index & 0x00000007]) >> (index & 0x00000007);
    public static int SetBit(byte* array, int index) =>
        array[index >> 3] |= BitBool.bitMask[index & 0x00000007];

    public static void SetValue1(byte* array, int index, int value) => array[index >> 2] &=
        (byte)((value << ((index & 0x3) << 1)) | BitBool.valueMask[index & 0x00000003]);
    public static void SetValue0(byte* array, int index, int value) => array[index >> 2] |=
        (byte)(value << ((index & 0x3) << 1));
    public static int GetValue(byte* array, int index) => (array[index >> 2] >> ((index & 0x00000003) << 1)) & 0x00000003;

    public static int SetBit32(byte* array, int index) =>
        array[index >> 5] |= (byte)BitBool.bitMask32[index & 0x0000001F];
    public static int GetBit32(byte* array, int index) =>
        array[index >> 5] & (byte)BitBool.bitMask32[index & 0x0000001F];
    public static int UnSetBit32(byte* array, int index) =>
        array[index >> 5] ^= (byte)BitBool.bitMask32[index & 0x0000001F];

    public static int BitsTableSize(int length, int bitsLength) => (length * bitsLength + 31) >> 5;
    public static void SetBitsAtPos(uint[] bitsTable, uint bitIndex, uint bitsString, uint stringLength)
    {
        var wordIdx = bitIndex >> 5;
        var offset1 = bitIndex & 0x1f;
        var offset2 = 0x20 - offset1;
        var stringMask = (uint)((1 << (int)stringLength) - 1);
        //bitsTable[wordIdx] &= ~(stringMask << (int)shift1);
        //bitsTable[wordIdx] |= bitsString << (int)shift1;

        bitsTable[wordIdx] = bitsTable[wordIdx] & ~(stringMask << (int)offset1) | bitsString << (int)offset1;

        if (stringLength > offset2)
            bitsTable[wordIdx + 1] = bitsTable[wordIdx + 1] & ~(stringMask >> (int)offset2) | bitsString >> (int)offset2;
        //bitsTable[wordIdx + 1] &= ~(stringMask >> (int)offset2);
        //bitsTable[wordIdx + 1] |= bitsString >> (int)offset2;
    }
    public static void SetBitsAtPos(byte[] bitsTable, uint bitIndex, uint bitsString, uint stringLength)
    {
        var wordIdx = bitIndex >> 3;
        var shift1 = bitIndex & 0x7;
        var shift2 = 0x8 - shift1;
        var stringMask = (uint)((1 << (int)stringLength) - 1);
        bitsTable[wordIdx] &= (byte)~(stringMask << (int)shift1);
        bitsTable[wordIdx] |= (byte)(bitsString << (int)shift1);
        if (shift2 >= stringLength)
            return;
        bitsTable[wordIdx + 1] &= (byte)~(stringMask >> (int)shift2);
        bitsTable[wordIdx + 1] |= (byte)(bitsString >> (int)shift2);
    }

    public static uint GetBitsAtPos(uint[] bitsTable, uint pos, uint stringLength)
    {
        var wordIdx = pos >> 5;
        var shift1 = pos & 0x1f;
        var shift2 = 0x20 - shift1;
        var stringMask = (uint)((1 << (int)stringLength) - 1);
        var bitsString = bitsTable[wordIdx] >> (int)shift1 & stringMask;
        if (shift2 < stringLength)
            bitsString |= bitsTable[(int)(wordIdx + 1)] << (int)shift2 & stringMask;
        return bitsString;
    }

    public static uint GetBitsAtPos(byte[] bitsTable, uint pos, uint stringLength)
    {
        var wordIdx = pos >> 3;
        var shift1 = pos & 0x7;
        var shift2 = 0x8 - shift1;
        var stringMask = (uint)((1 << (int)stringLength) - 1);
        var bitsString = bitsTable[wordIdx] >> (int)shift1 & stringMask;
        if (shift2 < stringLength)
            bitsString |= bitsTable[(int)(wordIdx + 1)] << (int)shift2 & stringMask;
        return (uint)bitsString;
    }

    public static void SetBitsAtArray(uint[] bitsTable, uint index, uint bitsString, uint stringLength,
        uint stringMask)
    {
        var bitIndex = index * stringLength;
        var wordIdx = bitIndex >> 5;
        var offset1 = bitIndex & 0x1f;
        var offset2 = 0x20 - offset1;

        bitsTable[wordIdx] = bitsTable[wordIdx] & ~(stringMask << (int)offset1) | bitsString << (int)offset1;
        if (stringLength > offset2)
            bitsTable[wordIdx + 1] = bitsTable[wordIdx + 1] & ~(stringMask >> (int)offset2) | bitsString >> (int)offset2;
    }

    public static void SetBitsAtArray(byte[] bitsTable, uint index, uint bitsString, uint stringLength,
        uint stringMask)
    {
        var bitIndex = index * stringLength;
        var wordIdx = bitIndex >> 3;
        var shift1 = bitIndex & 0x7;
        var shift2 = 0x8 - shift1;
        bitsTable[wordIdx] &= (byte)~(stringMask << (int)shift1);
        bitsTable[wordIdx] |= (byte)(bitsString << (int)shift1);
        if (shift2 >= stringLength)
            return;
        bitsTable[wordIdx + 1] &= (byte)~(stringMask >> (int)shift2);
        bitsTable[wordIdx + 1] |= (byte)(bitsString >> (int)shift2);
    }

    public static uint GetBitsValue(uint[] bitsTable, uint index, uint stringLength, uint stringMask)
    {
        var bitIdx = index * stringLength;
        var wordIdx = bitIdx >> 5;
        var shift1 = bitIdx & 0x1f;
        var shift2 = 0x20 - shift1;
        var bitsString = bitsTable[wordIdx] >> (int)shift1 & stringMask;
        if (shift2 < stringLength)
            bitsString |= bitsTable[(int)(wordIdx + 1)] << (int)shift2 & stringMask;
        return bitsString;
    }

    public static uint GetBitsValue(byte[] bitsTable, uint index, uint stringLength, uint stringMask)
    {
        var bitIdx = index * stringLength;
        var wordIdx = bitIdx >> 3;
        var shift1 = bitIdx & 0x7;
        var shift2 = 0x8 - shift1;
        var bitsString = bitsTable[wordIdx] >> (int)shift1 & stringMask;
        if (shift2 < stringLength)
            bitsString |= bitsTable[(int)(wordIdx + 1)] << (int)shift2 & stringMask;
        return (uint)bitsString;
    }
}