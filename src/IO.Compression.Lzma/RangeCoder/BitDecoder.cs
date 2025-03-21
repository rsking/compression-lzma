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
    private uint prob;

    /// <summary>
    /// Updates the model.
    /// </summary>
    /// <param name="numMoveBits">The number of move bits.</param>
    /// <param name="symbol">The symbol.</param>
    public void UpdateModel(int numMoveBits, uint symbol)
    {
        if (symbol is 0U)
        {
            this.prob += (BitModelTotal - this.prob) >> numMoveBits;
        }
        else
        {
            this.prob -= this.prob >> numMoveBits;
        }
    }

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    public void Init() => this.prob = BitModelTotal >> 1;

    /// <summary>
    /// Decodes the value.
    /// </summary>
    /// <param name="rangeDecoder">The range decoder.</param>
    /// <returns>The decoded value.</returns>
    public uint Decode(Decoder rangeDecoder)
    {
        var newBound = (rangeDecoder.Range >> NumBitModelTotalBits) * this.prob;
        if (rangeDecoder.Code < newBound)
        {
            rangeDecoder.Range = newBound;
            this.prob += (BitModelTotal - this.prob) >> NumMoveBits;
            if (rangeDecoder.Range < Decoder.TopValue && rangeDecoder.Stream is not null)
            {
                rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
                rangeDecoder.Range <<= 8;
            }

            return 0U;
        }

        rangeDecoder.Range -= newBound;
        rangeDecoder.Code -= newBound;
        this.prob -= this.prob >> NumMoveBits;
        if (rangeDecoder.Range < Decoder.TopValue && rangeDecoder.Stream is not null)
        {
            rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
            rangeDecoder.Range <<= 8;
        }

        return 1U;
    }
}