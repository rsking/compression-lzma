// -----------------------------------------------------------------------
// <copyright file="BitTreeEncoder.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.RangeCoder;

/// <summary>
/// The bit tree encoder.
/// </summary>
/// <param name="numBitLevels">The number of bit levels.</param>
internal readonly struct BitTreeEncoder(int numBitLevels)
{
    private readonly BitEncoder[] models = new BitEncoder[1 << numBitLevels];
    private readonly int numBitLevels = numBitLevels;

    /// <summary>
    /// Reverses the get price.
    /// </summary>
    /// <param name="models">The models.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="numBitLevels">The number of bit levels.</param>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The reversed price.</returns>
    public static uint ReverseGetPrice(BitEncoder[] models, uint startIndex, int numBitLevels, uint symbol)
    {
        var price = 0U;
        var m = 1U;
        for (var i = numBitLevels; i > 0; i--)
        {
            var bit = symbol & 1;
            symbol >>= 1;
            price += models[startIndex + m].GetPrice(bit);
            m = (m << 1) | bit;
        }

        return price;
    }

    /// <summary>
    /// Reverse encode.
    /// </summary>
    /// <param name="models">The models.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="rangeEncoder">The encoder.</param>
    /// <param name="numBitLevels">The number of bit levels.</param>
    /// <param name="symbol">The symbol.</param>
    public static void ReverseEncode(BitEncoder[] models, uint startIndex, Encoder rangeEncoder, int numBitLevels, uint symbol)
    {
        var m = 1U;
        for (var i = 0; i < numBitLevels; i++)
        {
            var bit = symbol & 1;
            models[startIndex + m].Encode(rangeEncoder, bit);
            m = (m << 1) | bit;
            symbol >>= 1;
        }
    }

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    public void Init()
    {
        for (var i = 1U; i < (1 << this.numBitLevels); i++)
        {
            this.models[i].Init();
        }
    }

    /// <summary>
    /// Encodes the symbol.
    /// </summary>
    /// <param name="rangeEncoder">The encoder.</param>
    /// <param name="symbol">The symbol.</param>
    public void Encode(Encoder rangeEncoder, uint symbol)
    {
        var m = 1U;
        for (var bitIndex = this.numBitLevels - 1; bitIndex > -1; bitIndex--)
        {
            var bit = (symbol >> bitIndex) & 1;
            this.models[m].Encode(rangeEncoder, bit);
            m = (m << 1) | bit;
        }
    }

    /// <summary>
    /// Reverse encodes the symbol.
    /// </summary>
    /// <param name="rangeEncoder">The encoder.</param>
    /// <param name="symbol">The symbol.</param>
    public void ReverseEncode(Encoder rangeEncoder, uint symbol)
    {
        var m = 1U;
        for (var i = 0; i < this.numBitLevels; i++)
        {
            var bit = symbol & 1;
            this.models[m].Encode(rangeEncoder, bit);
            m = (m << 1) | bit;
            symbol >>= 1;
        }
    }

    /// <summary>
    /// Gets the price.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The price.</returns>
    public uint GetPrice(uint symbol)
    {
        var price = 0U;
        var m = 1U;
        for (var bitIndex = this.numBitLevels - 1; bitIndex > -1; bitIndex--)
        {
            var bit = (symbol >> bitIndex) & 1;
            price += this.models[m].GetPrice(bit);
            m = (m << 1) + bit;
        }

        return price;
    }

    /// <summary>
    /// Reverses the get price.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The reversed price.</returns>
    public uint ReverseGetPrice(uint symbol)
    {
        var price = 0U;
        var m = 1U;
        for (var i = this.numBitLevels; i > 0; i--)
        {
            var bit = symbol & 1;
            symbol >>= 1;
            price += this.models[m].GetPrice(bit);
            m = (m << 1) | bit;
        }

        return price;
    }
}