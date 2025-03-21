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

    private readonly long outputSize;

    private readonly bool leaveOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="LzmaStream"/> class by using the specified stream and compression mode, and optionally leaves the stream open.
    /// </summary>
    /// <param name="stream">The stream to which compressed data is written or from which data to decompress is read.</param>
    /// <param name="mode">One of the enumeration values that indicates whether to compress data to the stream or decompress data from the stream.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave the stream open after disposing the <see cref="LzmaStream"/> object; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public LzmaStream(Stream stream, System.IO.Compression.CompressionMode mode, bool leaveOpen = false)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        this.stream = stream;
        this.leaveOpen = leaveOpen;
        if (mode is System.IO.Compression.CompressionMode.Compress && this.stream.CanWrite)
        {
            this.encoder = new LzmaCompressionOptions().CreateEncoder();
        }
        else if (mode is System.IO.Compression.CompressionMode.Decompress && this.stream.CanRead)
        {
            var properties = new byte[5];
            _ = this.stream.Read(properties, 0, 5);
            this.decoder = new();
            this.decoder.SetDecoderProperties(properties);

            this.outputSize = 0L;
            for (var i = 0; i < 8; i++)
            {
                var v = stream.ReadByte();
                if (v < 0)
                {
                    throw new InvalidOperationException("Can't Read 1");
                }

                this.outputSize |= ((long)(byte)v) << (8 * i);
            }
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
    public override bool CanSeek => this.stream.CanSeek;

    /// <inheritdoc/>
    public override bool CanWrite => this.encoder is not null && this.stream.CanWrite;

    /// <inheritdoc/>
    public override long Length => this.stream.Length;

    /// <inheritdoc/>
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

#if NETSTANDARD2_1_OR_GREATER
    /// <inheritdoc/>
    public override void CopyTo(Stream destination, int bufferSize)
    {
        if (this.decoder is not null)
        {
            var compressedSize = this.stream.Length - this.stream.Position;
            this.decoder.Code(this.stream, destination, compressedSize, this.outputSize);
        }
    }
#endif

    /// <inheritdoc/>
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => Task.Run(
        () =>
        {
            if (this.encoder is not null)
            {
                this.encoder.WriteCoderProperties(destination);

                var fileSize = this.stream.Length;

                for (var i = 0; i < 8; i++)
                {
                    destination.WriteByte((byte)(fileSize >> (8 * i)));
                }

                this.encoder.Code(this.stream, destination);
            }
        },
        cancellationToken);

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