// -----------------------------------------------------------------------
// <copyright file="BitTreeDecoder.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.RangeCoder;

/// <summary>
/// The bit tree decoder.
/// </summary>
/// <param name="numBitLevels">The number of bit levels.</param>
internal readonly struct BitTreeDecoder(int numBitLevels)
{
    /// <summary>
    /// Gets the models.
    /// </summary>
    public BitDecoder[] Models { get; } = new BitDecoder[1 << numBitLevels];

    /// <summary>
    /// Gets the number of bit levels.
    /// </summary>
    public int NumBitLevels { get; } = numBitLevels;

    /// <summary>
    /// Reverse decodes the value.
    /// </summary>
    /// <param name="models">The models.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="rangeDecoder">The range decoder.</param>
    /// <param name="numBitLevels">The number of bit levels.</param>
    /// <returns>The decoded value.</returns>
    public static uint ReverseDecode(BitDecoder[] models, uint startIndex, Decoder rangeDecoder, int numBitLevels)
    {
        var m = 1U;
        var symbol = 0U;
        for (var bitIndex = 0; bitIndex < numBitLevels; bitIndex++)
        {
            var bit = models[startIndex + m].Decode(rangeDecoder);
            m <<= 1;
            m += bit;
            symbol |= bit << bitIndex;
        }

        return symbol;
    }

    /// <summary>
    /// Initializes the models.
    /// </summary>
    public readonly void Init()
    {
        for (var i = 1; i < (1 << this.NumBitLevels); i++)
        {
            this.Models[i].Init();
        }
    }

    /// <summary>
    /// Decodes the value.
    /// </summary>
    /// <param name="rangeDecoder">The range decoder.</param>
    /// <returns>The decoded value.</returns>
    public uint Decode(Decoder rangeDecoder)
    {
        var m = 1U;
        for (var bitIndex = this.NumBitLevels; bitIndex > 0; bitIndex--)
        {
            m = (m << 1) + this.Models[m].Decode(rangeDecoder);
        }

        return m - (1U << this.NumBitLevels);
    }

    /// <summary>
    /// Reverse decodes the value.
    /// </summary>
    /// <param name="rangeDecoder">The range decoder.</param>
    /// <returns>The decoded value.</returns>
    public uint ReverseDecode(Decoder rangeDecoder)
    {
        var m = 1U;
        var symbol = 0U;
        for (var bitIndex = 0; bitIndex < this.NumBitLevels; bitIndex++)
        {
            var bit = this.Models[m].Decode(rangeDecoder);
            m <<= 1;
            m += bit;
            symbol |= bit << bitIndex;
        }

        return symbol;
    }
}
