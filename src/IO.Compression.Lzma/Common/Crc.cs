// -----------------------------------------------------------------------
// <copyright file="Crc.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.Common;

/// <summary>
/// The CRC.
/// </summary>
internal class Crc
{
    /// <summary>
    /// The CRC table.
    /// </summary>
    public static readonly uint[] Table = CreateTable();

    private uint value = uint.MaxValue;

    /// <summary>
    /// Gets the digest.
    /// </summary>
    public uint Digest => this.value ^ uint.MaxValue;

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    public void Init() => this.value = uint.MaxValue;

    /// <summary>
    /// Updates this instance.
    /// </summary>
    /// <param name="data">The data.</param>
    public void Update(byte data) => this.value = Table[(byte)this.value ^ data] ^ this.value >> 8;

    /// <summary>
    /// Updates this instance.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="size">The size.</param>
    public void Update(byte[] data, uint offset, uint size)
    {
        for (var i = 0U; i < size; i++)
        {
            this.value = Table[(byte)this.value ^ data[offset + i]] ^ this.value >> 8;
        }
    }

    private static uint[] CreateTable()
    {
        const uint Poly = 0xEDB88320U;
        var table = new uint[256];
        for (var i = 0U; i < 256U; i++)
        {
            var r = i;
            for (var j = 0; j < 8; j++)
            {
                if ((r & 1U) is not 0U)
                {
                    r = r >> 1 ^ Poly;
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