// -----------------------------------------------------------------------
// <copyright file="InWindow.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.LZ;

/// <summary>
/// The input window.
/// </summary>
internal class InWindow : IInWindowStream
{
    // size of allocated memory block.
    private uint blockSize;

    private Stream? stream;

    // offset (from _buffer) of first byte when new block reading must be done
    private uint posLimit;

    // if (true) then _streamPos shows real end of stream
    private bool streamEndWasReached;

    private uint pointerToLastSafePosition;

    // how many BYTEs must be kept in buffer before _pos
    private uint keepSizeBefore;

    // how many BYTEs must be kept buffer after _pos
    private uint keepSizeAfter;

    /// <summary>
    /// Gets ths pointer to buffer with data.
    /// </summary>
    protected byte[]? BufferBase { get; private set; }

    /// <summary>
    /// Gets the buffer offset.
    /// </summary>
    protected uint BufferOffset { get; private set; }

    /// <summary>
    /// Gets the offset (from _buffer) of curent byte.
    /// </summary>
    protected uint Pos { get; private set; }

    /// <summary>
    /// Gets the offset (from _buffer) of first not read byte from Stream.
    /// </summary>
    protected uint StreamPos { get; private set; }

    /// <inheritdoc />
    public void SetStream(Stream inStream) => this.stream = inStream;

    /// <inheritdoc />
    public void ReleaseStream() => this.stream = null;

    /// <inheritdoc />
    public virtual void Init()
    {
        this.BufferOffset = 0;
        this.Pos = 0;
        this.StreamPos = 0;
        this.streamEndWasReached = false;
        this.ReadBlock();
    }

    /// <summary>
    /// Moves the position.
    /// </summary>
    public virtual void MovePos()
    {
        this.Pos++;
        if (this.Pos > this.posLimit)
        {
            var pointerToPostion = this.BufferOffset + this.Pos;
            if (pointerToPostion > this.pointerToLastSafePosition)
            {
                this.MoveBlock();
            }

            this.ReadBlock();
        }
    }

    /// <inheritdoc/>
    public byte GetIndexByte(int index) => this.BufferBase is null ? throw new InvalidOperationException() : this.BufferBase[this.BufferOffset + this.Pos + index];

    /// <inheritdoc/>
    public uint GetMatchLen(int index, uint distance, uint limit)
    {
        if (this.streamEndWasReached
            && this.Pos + index + limit > this.StreamPos)
        {
            limit = this.StreamPos - (uint)(this.Pos + index);
        }

        distance++;

        var pby = this.BufferOffset + this.Pos + (uint)index;

        if (this.BufferBase is null)
        {
            throw new InvalidOperationException();
        }

        uint i;
        for (i = 0U; i < limit && this.BufferBase[pby + i] == this.BufferBase[pby + i - distance]; i++)
        {
            // this is fine.
        }

        return i;
    }

    /// <inheritdoc/>
    public uint GetNumAvailableBytes() => this.StreamPos - this.Pos;

    /// <summary>
    /// REduce the offsets.
    /// </summary>
    /// <param name="subValue">The sub-value.</param>
    protected void ReduceOffsets(int subValue)
    {
        this.BufferOffset += (uint)subValue;
        this.posLimit -= (uint)subValue;
        this.Pos -= (uint)subValue;
        this.StreamPos -= (uint)subValue;
    }

    /// <summary>
    /// Creates this instance.
    /// </summary>
    /// <param name="keepSizeBefore">The keep size before.</param>
    /// <param name="keepSizeAfter">The keep size after.</param>
    /// <param name="keepSizeReserve">The keep size reverve.</param>
    protected void Create(uint keepSizeBefore, uint keepSizeAfter, uint keepSizeReserve)
    {
        this.keepSizeBefore = keepSizeBefore;
        this.keepSizeAfter = keepSizeAfter;
        var totalBlockSize = keepSizeBefore + keepSizeAfter + keepSizeReserve;
        if (this.BufferBase is null || this.blockSize != totalBlockSize)
        {
            this.Free();
            this.blockSize = totalBlockSize;
            this.BufferBase = new byte[this.blockSize];
        }

        this.pointerToLastSafePosition = this.blockSize - keepSizeAfter;
    }

    private void MoveBlock()
    {
        var offset = this.BufferOffset + this.Pos - this.keepSizeBefore;

        // we need one additional byte, since MovePos moves on 1 byte.
        if (offset > 0)
        {
            offset--;
        }

        var numBytes = this.BufferOffset + this.StreamPos - offset;

        if (this.BufferBase is null)
        {
            throw new InvalidOperationException();
        }

        // check negative offset ????
        for (var i = 0U; i < numBytes; i++)
        {
            this.BufferBase[i] = this.BufferBase[offset + i];
        }

        this.BufferOffset -= offset;
    }

    private void ReadBlock()
    {
        if (this.streamEndWasReached)
        {
            return;
        }

        if (this.stream is null)
        {
            throw new InvalidOperationException();
        }

        while (true)
        {
            var size = (int)(0 - this.BufferOffset + this.blockSize - this.StreamPos);
            if (size is 0)
            {
                return;
            }

            var numReadBytes = this.stream.Read(this.BufferBase, (int)(this.BufferOffset + this.StreamPos), size);
            if (numReadBytes is 0)
            {
                this.posLimit = this.StreamPos;
                var pointerToPostion = this.BufferOffset + this.posLimit;
                if (pointerToPostion > this.pointerToLastSafePosition)
                {
                    this.posLimit = this.pointerToLastSafePosition - this.BufferOffset;
                }

                this.streamEndWasReached = true;
                return;
            }

            this.StreamPos += (uint)numReadBytes;
            if (this.StreamPos >= this.Pos + this.keepSizeAfter)
            {
                this.posLimit = this.StreamPos - this.keepSizeAfter;
            }
        }
    }

    private void Free() => this.BufferBase = null;
}