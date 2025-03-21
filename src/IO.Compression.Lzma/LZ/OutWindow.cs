// -----------------------------------------------------------------------
// <copyright file="OutWindow.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.LZ;

/// <summary>
/// The output window.
/// </summary>
internal class OutWindow
{
    private byte[] buffer = [];
    private int pos;
    private int startPos;
    private Stream? stream;
    private bool expandable;

    /// <summary>
    /// Gets the number of bytes to flush.
    /// </summary>
    public int BytesToWrite => this.pos - this.startPos;

    /// <summary>
    /// Creates the out window.
    /// </summary>
    /// <param name="windowSize">The window size.</param>
    public void Create(int windowSize)
    {
        if (this.buffer.Length != windowSize)
        {
            this.buffer = new byte[windowSize];
        }

        this.startPos = this.pos = 0;
    }

    /// <summary>
    /// Initializes the out window with the stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    public void Init(Stream stream)
    {
        this.ReleaseStream();
        this.stream = stream;
        this.expandable = IsExandable(this.stream);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "Checked")]
        static bool IsExandable(Stream stream)
        {
            if (stream is MemoryStream memoryStream)
            {
                var field = memoryStream.GetType().GetField("_expandable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                return field?.GetValue(memoryStream) is not bool b || b;
            }

            return true;
        }
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
        if (this.stream is null)
        {
            return;
        }

        var size = this.expandable
            ? this.pos - this.startPos
            : Math.Min(this.pos - this.startPos, (int)(this.stream.Length - this.stream.Position));

        if (size is 0)
        {
            return;
        }

        this.stream.Write(this.buffer, this.startPos, size);
        if (this.pos >= this.buffer.Length)
        {
            this.startPos = this.pos = 0;
        }
        else
        {
            this.startPos += size;
        }
    }

    /// <summary>
    /// Copies the block.
    /// </summary>
    /// <param name="distance">The distance.</param>
    /// <param name="length">The length.</param>
    /// <returns>The number of bytes copied.</returns>
    public uint CopyBlock(uint distance, uint length)
    {
        var currentPosition = this.pos - distance - 1;
        if (currentPosition >= this.buffer.Length)
        {
            currentPosition -= (uint)this.buffer.Length;
        }

        for (var i = length; i > 0; i--)
        {
            if (currentPosition >= this.buffer.Length)
            {
                currentPosition = 0U;
            }

            this.buffer[this.pos++] = this.buffer[currentPosition++];
            if (this.pos >= this.buffer.Length)
            {
                this.Flush();
            }
        }

        return length;
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
        if (this.pos >= this.buffer.Length)
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
        if (currentPosition >= this.buffer.Length)
        {
            currentPosition += (uint)this.buffer.Length;
        }

        return this.buffer[currentPosition];
    }
}