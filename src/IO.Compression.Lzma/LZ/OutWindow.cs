// -----------------------------------------------------------------------
// <copyright file="OutWindow.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.LZ;

/// <summary>
/// The out window.
/// </summary>
internal class OutWindow
{
    private byte[]? buffer;
    private uint pos;
    private uint windowSize;
    private uint streamPos;
    private Stream? stream;

    /// <summary>
    /// Gets or sets the train size.
    /// </summary>
    public uint TrainSize { get; set; }

    /// <summary>
    /// Creates the out window.
    /// </summary>
    /// <param name="windowSize">The window size.</param>
    public void Create(uint windowSize)
    {
        if (this.windowSize != windowSize)
        {
            this.buffer = new byte[windowSize];
        }

        this.windowSize = windowSize;
        this.pos = 0;
        this.streamPos = 0;
    }

    /// <summary>
    /// Initializes the out window with the stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="solid">Set to <see langword="true"/> to make this solid.</param>
    public void Init(Stream stream, bool solid)
    {
        this.ReleaseStream();
        this.stream = stream;
        if (!solid)
        {
            this.streamPos = 0;
            this.pos = 0;
            this.TrainSize = 0;
        }
    }

    /// <summary>
    /// Trains this instance with the stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns><see langword="true"/> if the training was successful; otherwise <see langword="false"/>.</returns>
    public bool Train(Stream stream)
    {
        var len = stream.Length;
        var size = (len < this.windowSize) ? (uint)len : this.windowSize;
        this.TrainSize = size;
        stream.Position = len - size;
        this.streamPos = this.pos = 0U;
        while (size > 0)
        {
            var curSize = this.windowSize - this.pos;
            if (size < curSize)
            {
                curSize = size;
            }

            var numReadBytes = (uint)stream.Read(this.buffer, (int)this.pos, (int)curSize);
            if (numReadBytes is 0U)
            {
                return false;
            }

            size -= numReadBytes;
            this.pos += numReadBytes;
            this.streamPos += numReadBytes;
            if (this.pos == this.windowSize)
            {
                this.streamPos = this.pos = 0;
            }
        }

        return true;
    }

    /// <summary>
    /// Releases the stream.
    /// </summary>
    public void ReleaseStream()
    {
        this.Flush();
        this.stream = null;
    }

    /// <summary>
    /// Flushes the window to the stream.
    /// </summary>
    public void Flush()
    {
        var size = this.pos - this.streamPos;
        if (size is 0U)
        {
            return;
        }

        if (this.stream is null)
        {
            throw new InvalidOperationException();
        }

        this.stream.Write(this.buffer, (int)this.streamPos, (int)size);
        if (this.pos >= this.windowSize)
        {
            this.pos = 0;
        }

        this.streamPos = this.pos;
    }

    /// <summary>
    /// Copies the block.
    /// </summary>
    /// <param name="distance">The distance.</param>
    /// <param name="length">The length.</param>
    public void CopyBlock(uint distance, uint length)
    {
        var currentPosition = this.pos - distance - 1;
        if (currentPosition >= this.windowSize)
        {
            currentPosition += this.windowSize;
        }

        if (this.buffer is null)
        {
            throw new InvalidOperationException();
        }

        for (; length > 0; length--)
        {
            if (currentPosition >= this.windowSize)
            {
                currentPosition = 0;
            }

            this.buffer[this.pos++] = this.buffer[currentPosition++];
            if (this.pos >= this.windowSize)
            {
                this.Flush();
            }
        }
    }

    /// <summary>
    /// Puts the byte.
    /// </summary>
    /// <param name="b">The byte to put.</param>
    public void PutByte(byte b)
    {
        if (this.buffer is null)
        {
            throw new InvalidOperationException();
        }

        this.buffer[this.pos++] = b;
        if (this.pos >= this.windowSize)
        {
            this.Flush();
        }
    }

    /// <summary>
    /// Gets hte byte.
    /// </summary>
    /// <param name="distance">The distance.</param>
    /// <returns>The byte.</returns>
    public byte GetByte(uint distance)
    {
        if (this.buffer is null)
        {
            throw new InvalidOperationException();
        }

        var currentPosition = this.pos - distance - 1;
        if (currentPosition >= this.windowSize)
        {
            currentPosition += this.windowSize;
        }

        return this.buffer[currentPosition];
    }
}