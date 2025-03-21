// -----------------------------------------------------------------------
// <copyright file="Encoder.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.RangeCoder;

/// <summary>
/// The range encoder.
/// </summary>
internal class Encoder
{
    /// <summary>
    /// The top value.
    /// </summary>
    public const uint TopValue = 1U << 24;

    private Stream? stream;

    private uint cacheSize;
    private byte cache;

    private long startPosition;

    /// <summary>
    /// Gets or sets the low value.
    /// </summary>
    public ulong Low { get; set; }

    /// <summary>
    /// Gets or sets the range value.
    /// </summary>
    public uint Range { get; set; }

    /// <summary>
    /// Sets the stream.
    /// </summary>
    /// <param name="stream">The stream to set.</param>
    public void SetStream(Stream stream) => this.stream = stream;

    /// <summary>
    /// Releases the stream.
    /// </summary>
    public void ReleaseStream() => this.stream = null;

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    public void Init()
    {
        this.startPosition = this.stream?.Position ?? -1;

        this.Low = 0;
        this.Range = uint.MaxValue;
        this.cacheSize = 1;
        this.cache = 0;
    }

    /// <summary>
    /// Flushes the data.
    /// </summary>
    public void FlushData()
    {
        for (var i = 0; i < 5; i++)
        {
            this.ShiftLow();
        }
    }

    /// <summary>
    /// Flushes the stream.
    /// </summary>
    public void FlushStream() => this.stream?.Flush();

    /// <summary>
    /// Closes the stream.
    /// </summary>
    public void CloseStream() => this.stream?.Close();

    /// <summary>
    /// Encodes.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="size">The size.</param>
    /// <param name="total">The total.</param>
    public void Encode(uint start, uint size, uint total)
    {
        this.Range /= total;
        this.Low += start * this.Range;
        this.Range *= size;
        while (this.Range < TopValue)
        {
            this.Range <<= 8;
            this.ShiftLow();
        }
    }

    /// <summary>
    /// Shifts low.
    /// </summary>
    /// <exception cref="InvalidOperationException">The stream is <see langword="null"/>.</exception>
    public void ShiftLow()
    {
        if (this.stream is null)
        {
            throw new InvalidOperationException();
        }

        if ((uint)this.Low < 0xFF000000U || (uint)(this.Low >> 32) is 1U)
        {
            var temp = this.cache;
            do
            {
                this.stream.WriteByte((byte)(temp + (this.Low >> 32)));
                temp = byte.MaxValue;
            }
            while (--this.cacheSize is not 0);
            this.cache = (byte)(((uint)this.Low) >> 24);
        }

        this.cacheSize++;
        this.Low = ((uint)this.Low) << 8;
    }

    /// <summary>
    /// Encodes the direct bits.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="numTotalBits">The number of total bits.</param>
    public void EncodeDirectBits(uint value, int numTotalBits)
    {
        for (var i = numTotalBits - 1; i >= 0; i--)
        {
            this.Range >>= 1;
            if (((value >> i) & 1U) is 1U)
            {
                this.Low += this.Range;
            }

            if (this.Range < TopValue)
            {
                this.Range <<= 8;
                this.ShiftLow();
            }
        }
    }

    /// <summary>
    /// Encodes the bits.
    /// </summary>
    /// <param name="size0">The size.</param>
    /// <param name="numTotalBits">The number of total bits.</param>
    /// <param name="symbol">The symbol.</param>
    public void EncodeBit(uint size0, int numTotalBits, uint symbol)
    {
        var newBound = (this.Range >> numTotalBits) * size0;
        if (symbol is 0)
        {
            this.Range = newBound;
        }
        else
        {
            this.Low += newBound;
            this.Range -= newBound;
        }

        while (this.Range < TopValue)
        {
            this.Range <<= 8;
            this.ShiftLow();
        }
    }

    /// <summary>
    /// Gets the processing size add.
    /// </summary>
    /// <returns>The processing size add.</returns>
    /// <exception cref="InvalidOperationException">The stream is <see langword="null"/>.</exception>
    public long GetProcessedSizeAdd() => this.stream is null ? throw new InvalidOperationException() : this.cacheSize + this.stream.Position - this.startPosition + 4;
}