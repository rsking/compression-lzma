// -----------------------------------------------------------------------
// <copyright file="LzmaStream.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

/// <summary>
/// Provides methods and properties for compressing and decompressing streams by using the LZMA algorithm.
/// </summary>
public sealed class LzmaStream : Stream
{
    private readonly Stream stream;

    private readonly LzmaEncoder? encoder;

    private readonly LzmaDecoder? decoder;

    private readonly bool leaveOpen;

    private long bytesLeft;

    /// <summary>
    /// Initializes a new instance of the <see cref="LzmaStream"/> class by using the specified stream and compression mode, and optionally leaves the stream open.
    /// </summary>
    /// <param name="stream">The stream to which compressed data is written or from which data to decompress is read.</param>
    /// <param name="mode">One of the enumeration values that indicates whether to compress data to the stream or decompress data from the stream.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave the stream open after disposing the <see cref="LzmaStream"/> object; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public LzmaStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        this.stream = stream;
        this.leaveOpen = leaveOpen;
        if (mode is CompressionMode.Compress && this.stream.CanWrite)
        {
            this.encoder = new LzmaCompressionOptions().CreateEncoder();
            this.encoder.WriteCoderProperties(this.stream);
        }
        else if (mode is CompressionMode.Decompress && this.stream.CanRead)
        {
            const int PropertiesSize = 5;
            const int OutputSize = 8;
            var properties = new byte[PropertiesSize];
            _ = this.stream.Read(properties, 0, PropertiesSize);
            this.decoder = new(properties);

            var outputSize = 0L;
            var bytes = new byte[OutputSize];
            if (stream.Read(bytes, 0, OutputSize) is not OutputSize)
            {
                throw new InvalidOperationException("Failed to read the output size.");
            }

            for (var i = 0; i < OutputSize; i++)
            {
                var v = bytes[i];
                outputSize |= (long)v << (8 * i);
            }

            this.decoder.SetInputStream(stream);
            this.bytesLeft = outputSize;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LzmaStream"/> class by using the specified stream, compression options, and optionally leaves the stream open.
    /// </summary>
    /// <param name="stream">The stream to which compressed data is written.</param>
    /// <param name="options">The options for fine tuning the compression stream.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave the stream open after disposing the <see cref="LzmaStream"/> object; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public LzmaStream(Stream stream, LzmaCompressionOptions options, bool leaveOpen = false)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        this.stream = stream;

        this.encoder = options.CreateEncoder();
        this.leaveOpen = leaveOpen;
    }

    /// <inheritdoc/>
    public override bool CanRead => this.decoder is not null && this.stream.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => this.encoder is not null && this.stream.CanWrite;

    /// <inheritdoc/>
    public override long Length => this.stream.Length;

    /// <inheritdoc/>
    public override long Position { get => this.stream.Position; set => throw new NotSupportedException(); }

    /// <summary>
    /// Copies the specified stream into this stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <exception cref="InvalidOperationException">The encoder is <see langword="null"/>.</exception>
    public void CopyFrom(Stream stream)
    {
        if (this.encoder is null)
        {
            throw new InvalidOperationException();
        }

        this.SetLength(stream.Length);
        this.encoder.Compress(stream, this.stream);
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        if (this.encoder is null)
        {
            throw new InvalidOperationException();
        }

        // write out the length
        for (var i = 0; i < 8; i++)
        {
            this.stream.WriteByte((byte)(value >> (8 * i)));
        }
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (this.decoder is null)
        {
            throw new InvalidOperationException();
        }

        if (this.bytesLeft > 0)
        {
            var bytesToRead = Math.Min(this.bytesLeft, count);
            using var memoryStream = new MemoryStream(buffer, offset, count);
            this.decoder.Decompress(memoryStream, bytesToRead);
            this.bytesLeft -= Math.Min(memoryStream.Position, count);
            return (int)bytesToRead;
        }

        return 0;
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (this.encoder is null)
        {
            throw new InvalidOperationException();
        }

        using var memoryStream = new MemoryStream(buffer, offset, count);
        this.encoder.Compress(memoryStream, this.stream);
    }

#if NETSTANDARD2_1_OR_GREATER
    /// <inheritdoc/>
    public override void CopyTo(Stream destination, int bufferSize)
    {
        if (this.bytesLeft is 0)
        {
            return;
        }

        if (this.decoder is null)
        {
            throw new InvalidOperationException();
        }

        this.decoder.Decompress(destination, this.bytesLeft);
        this.bytesLeft = 0;
    }
#endif

    /// <inheritdoc/>
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        if (this.bytesLeft is 0)
        {
            return Task.CompletedTask;
        }

        return this.decoder is null
            ? throw new InvalidOperationException()
            : Task.Run(CopyTo, cancellationToken);

        void CopyTo()
        {
            this.decoder.Decompress(destination, this.bytesLeft);
            this.bytesLeft = 0;
        }
    }

    /// <inheritdoc/>
    public override void Close()
    {
        if (!this.leaveOpen)
        {
            this.stream.Close();
        }

        base.Close();
    }

    /// <inheritdoc/>
    public override void Flush() => this.stream.Flush();

    /// <inheritdoc/>
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await this.stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        await base.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

#if NETSTANDARD2_1_OR_GREATER
    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (!this.leaveOpen)
        {
            await this.stream.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !this.leaveOpen)
        {
            this.stream.Dispose();
        }

        base.Dispose(disposing);
    }
}