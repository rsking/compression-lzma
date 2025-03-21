// -----------------------------------------------------------------------
// <copyright file="RangeDecoder.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.RangeCoder;

/// <summary>
/// The range decoder.
/// </summary>
internal class RangeDecoder
{
    /// <summary>
    /// The top value.
    /// </summary>
    public const uint TopValue = 1U << 24;

    /// <summary>
    /// Gets or sets the range.
    /// </summary>
    public uint Range { get; set; }

    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public uint Code { get; set; }

    /// <summary>
    /// Gets the stream.
    /// </summary>
    public Stream? Stream { get; private set; }

    /// <summary>
    /// Initializes the decoder.
    /// </summary>
    /// <param name="stream">The stream.</param>
    public void Init(Stream stream)
    {
        this.Stream = stream;

        this.Code = 0;
        this.Range = uint.MaxValue;
        for (var i = 0; i < 5; i++)
        {
            this.Code = (this.Code << 8) | (byte)this.Stream.ReadByte();
        }
    }

    /// <summary>
    /// Releases the strem.
    /// </summary>
    public void ReleaseStream() => this.Stream = null;

    /// <summary>
    /// Normalizes the decoder.
    /// </summary>
    public void Normalize()
    {
        if (this.Stream is null)
        {
            throw new InvalidOperationException();
        }

        while (this.Range < TopValue)
        {
            this.Code = (this.Code << 8) | (byte)this.Stream.ReadByte();
            this.Range <<= 8;
        }
    }

    /// <summary>
    /// Decodes the direct bits.
    /// </summary>
    /// <param name="numTotalBits">The number of total bits.</param>
    /// <returns>The decided bits.</returns>
    public uint DecodeDirectBits(int numTotalBits)
    {
        if (this.Stream is null)
        {
            throw new InvalidOperationException();
        }

        var range = this.Range;
        var code = this.Code;
        var result = 0U;
        for (var i = numTotalBits; i > 0; i--)
        {
            range >>= 1;
            var t = (code - range) >> 31;
            code -= range & (t - 1);
            result = (result << 1) | (1 - t);

            if (range < TopValue)
            {
                code = (code << 8) | (byte)this.Stream.ReadByte();
                range <<= 8;
            }
        }

        this.Range = range;
        this.Code = code;
        return result;
    }

    /// <summary>
    /// Decodes the bit.
    /// </summary>
    /// <param name="size0">The size.</param>
    /// <param name="numTotalBits">The number of total bits.</param>
    /// <returns>The decoded bit.</returns>
    public uint DecodeBit(uint size0, int numTotalBits)
    {
        var newBound = (this.Range >> numTotalBits) * size0;
        uint symbol;
        if (this.Code < newBound)
        {
            symbol = 0;
            this.Range = newBound;
        }
        else
        {
            symbol = 1;
            this.Code -= newBound;
            this.Range -= newBound;
        }

        this.Normalize();
        return symbol;
    }
}