// -----------------------------------------------------------------------
// <copyright file="Crc.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.LZ;

/// <summary>
/// The CRC.
/// </summary>
internal static class Crc
{
    /// <summary>
    /// The CRC table.
    /// </summary>
    public static readonly uint[] Table = CreateTable();

    private static uint[] CreateTable()
    {
        const uint Poly = 0xEDB88320;
        var table = new uint[256];
        for (var i = 0U; i < 256U; i++)
        {
            var r = i;
            for (var j = 0; j < 8; j++)
            {
                if ((r & 1U) is not 0U)
                {
                    r = (r >> 1) ^ Poly;
                }
                else
                {
                    r >>= 1;
                }
            }

            table[i] = r;
        }

        return table;
    }
}