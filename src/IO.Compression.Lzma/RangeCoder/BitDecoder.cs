// -----------------------------------------------------------------------
// <copyright file="BitDecoder.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.RangeCoder;

/// <summary>
/// The bit decoder.
/// </summary>
internal struct BitDecoder
{
    /// <summary>
    /// The number of bit model total bits.
    /// </summary>
    public const int NumBitModelTotalBits = 11;

    /// <summary>
    /// The bit model total.
    /// </summary>
    public const uint BitModelTotal = 1 << NumBitModelTotalBits;
    private const int NumMoveBits = 5;
    private uint probability;

    /// <summary>
    /// Updates the model.
    /// </summary>
    /// <param name="numMoveBits">The number of move bits.</param>
    /// <param name="symbol">The symbol.</param>
    public void UpdateModel(int numMoveBits, uint symbol)
    {
        if (symbol is 0U)
        {
            this.probability += (BitModelTotal - this.probability) >> numMoveBits;
        }
        else
        {
            this.probability -= this.probability >> numMoveBits;
        }
    }

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    public void Init() => this.probability = BitModelTotal >> 1;

    /// <summary>
    /// Decodes the value.
    /// </summary>
    /// <param name="rangeDecoder">The range decoder.</param>
    /// <returns>The decoded value.</returns>
    public uint Decode(RangeDecoder rangeDecoder)
    {
        var newBound = (rangeDecoder.Range >> NumBitModelTotalBits) * this.probability;
        if (rangeDecoder.Code < newBound)
        {
            rangeDecoder.Range = newBound;
            this.probability += (BitModelTotal - this.probability) >> NumMoveBits;
            if (rangeDecoder.Range < RangeDecoder.TopValue && rangeDecoder.Stream is not null)
            {
                rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
                rangeDecoder.Range <<= 8;
            }

            return 0U;
        }

        rangeDecoder.Range -= newBound;
        rangeDecoder.Code -= newBound;
        this.probability -= this.probability >> NumMoveBits;
        if (rangeDecoder.Range < RangeDecoder.TopValue && rangeDecoder.Stream is not null)
        {
            rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
            rangeDecoder.Range <<= 8;
        }

        return 1U;
    }
}