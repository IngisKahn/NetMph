﻿using System.Numerics;
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
        *(uint*)BitBool.valueMask = 0x3FCFF3FC;
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

    public static void SetBitsValue(uint* bitsTable, uint index, uint bitsString, uint stringLength, uint stringMask)
    {
        var bitIndex = index * stringLength;
        var wordIndex = bitIndex >> 5;
        var shift1 = (int)bitIndex & 0x1F;
        var shift2 = 32 - shift1;

        bitsTable[wordIndex] &= ~(stringMask << shift1);
        bitsTable[wordIndex] |= bitsString << shift1;

        if (shift2 >= stringLength)
            return;
        bitsTable[wordIndex + 1] &= ~(stringMask >> shift2);
        bitsTable[wordIndex + 1] |= bitsString >> shift2;
    }
    public static void SetBitsValue(ulong* bitsTable, uint index, ulong bitsString, uint stringLength, ulong stringMask)
    {
        var bitIndex = index * stringLength;
        var wordIndex = bitIndex >> 6;
        var shift1 = (int)bitIndex & 0x3F;
        var shift2 = 64 - shift1;

        bitsTable[wordIndex] &= ~(stringMask << shift1);
        bitsTable[wordIndex] |= bitsString << shift1;

        if (shift2 >= stringLength)
            return;
        bitsTable[wordIndex + 1] &= ~(stringMask >> shift2);
        bitsTable[wordIndex + 1] |= bitsString >> shift2;
    }
    public static void SetBitsAtPos<T>(ulong* bitsTable, ulong bitIndex, T bitsString, uint stringLength)
        where T : IConvertible
    {
        var wordIdx = (long)bitIndex >> 6;
        var shift1 = (int)bitIndex & 0x3F;
        var shift2 = 64 - shift1;
        var stringMask = (1u << (int)stringLength) - 1;
        var bitsStringValue = bitsString.ToUInt64(null);
        bitsTable[wordIdx] &= ~(stringMask << shift1);
        bitsTable[wordIdx] |= bitsStringValue << shift1;
        if (shift2 >= stringLength)
            return;
        bitsTable[wordIdx + 1] &= ~(stringMask >> shift2);
        bitsTable[wordIdx + 1] |= bitsStringValue >> shift2;
    }
    public static void SetBitsAtPos(uint* bitsTable, uint bitIndex, uint bitsString, uint stringLength)
    {
        var wordIdx = (int)bitIndex >> 5;
        var shift1 = (int)bitIndex & 0x1F;
        var shift2 = 32 - shift1;
        var stringMask = (1u << (int)stringLength) - 1;
        bitsTable[wordIdx] &= ~(stringMask << shift1);
        bitsTable[wordIdx] |= bitsString << shift1;
        if (shift2 >= stringLength)
            return;
        bitsTable[wordIdx + 1] &= ~(stringMask >> shift2);
        bitsTable[wordIdx + 1] |= bitsString >> shift2;
    }
    public static void SetBitsAtPos(ulong* bitsTable, uint bitIndex, ulong bitsString, uint stringLength)
    {
        var wordIdx = (int)bitIndex >> 6;
        var shift1 = (int)bitIndex & 0x3F;
        var shift2 = 64 - shift1;
        var stringMask = (1u << (int)stringLength) - 1;
        bitsTable[wordIdx] &= ~(stringMask << shift1);
        bitsTable[wordIdx] |= bitsString << shift1;
        if (shift2 >= stringLength)
            return;
        bitsTable[wordIdx + 1] &= ~(stringMask >> shift2);
        bitsTable[wordIdx + 1] |= bitsString >> shift2;
    }

    public static uint GetBitsAtPos(uint* bitsTable, uint pos, uint stringLength)
    {
        var wordIdx = (int)pos >> 5;
        var shift1 = (int)pos & 0x1f;
        var shift2 = 0x20 - shift1;
        var stringMask = (uint)((1 << (int)stringLength) - 1);
        var bitsString = bitsTable[wordIdx] >> shift1 & stringMask;
        if (shift2 < stringLength)
            bitsString |= bitsTable[wordIdx + 1] << shift2 & stringMask;
        return bitsString;
    }

    public static ulong GetBitsAtPos(ulong* bitsTable, uint pos, uint stringLength)
    {
        var wordIdx = (int)pos >> 6;
        var shift1 = (int)pos & 0x3f;
        var shift2 = 0x40 - shift1;
        var stringMask = (uint)((1 << (int)stringLength) - 1);
        var bitsString = bitsTable[wordIdx] >> shift1 & stringMask;
        if (shift2 < stringLength)
            bitsString |= bitsTable[wordIdx + 1] << shift2 & stringMask;
        return bitsString;
    }

    public static uint GetBitsValue(uint* bitsTable, uint index, uint stringLength, uint stringMask)
    {
        var bitIdx = index * stringLength;
        var wordIdx = (int)bitIdx >> 5;
        var shift1 = (int)bitIdx & 0x1F;
        var shift2 = 32 - shift1;
        var bitsString = bitsTable[wordIdx] >> shift1 & stringMask;
        if (shift2 < stringLength)
            bitsString |= bitsTable[wordIdx + 1] << shift2 & stringMask;
        return bitsString;
    }

    public static ulong GetBitsValue(ulong* bitsTable, uint index, uint stringLength, uint stringMask)
    {
        var bitIdx = index * stringLength;
        var wordIdx = (int)bitIdx >> 6;
        var shift1 = (int)bitIdx & 0x3F;
        var shift2 = 0x40 - shift1;
        var bitsString = bitsTable[wordIdx] >> shift1 & stringMask;
        if (shift2 < stringLength)
            bitsString |= bitsTable[wordIdx + 1] << shift2 & stringMask;
        return bitsString;
    }

    public static ulong Read7BitNumber(ref byte* data)
    {
        var value = 0ul;
        byte temp;
        do
        {
            temp = *data++;
            value <<= 7;
            value |= temp & 0x7Ful;
        } while ((temp & 0x80) != 0);
        return value;
    }

    public static void Write7BitNumber(ref byte* data, ulong value)
    {
        do
        {
            var temp = (byte)(value & 0x7F);
            value >>= 7;
            temp |= (byte)((value - 1) >> 56);
            *data++ = temp;
        } while (value != 0);
    }

    public static void Write7BitNumber(Stream stream, ulong value)
    {
        var data = stackalloc byte[10];
        var pData = data;
        Write7BitNumber(ref pData, value);
        stream.Write(new(data, (int)(pData - data)));
    }
}