// -----------------------------------------------------------------------
// <copyright file="LzmaStreamTests.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.Tests;

public class LzmaStreamTests
{
    public class Encode
    {
        [Test]
        public async Task CopyFrom()
        {
            using var output = new MemoryStream();
            using (var input = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
            {
                _ = await Assert.That(input).IsNotNull();
                using var lzmaStream = new LzmaStream(output, CompressionMode.Compress, leaveOpen: true);
                lzmaStream.CopyFrom(input!);
            }

            output.Position = 0;

            // compare the streams
            using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            await CompareStreams(output, lzma);
        }

        [Test]
        public async Task WithBufferSize()
        {
            using var output = new MemoryStream();
            using (var input = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
            {
                _ = await Assert.That(input).IsNotNull();
                using var lzmaStream = new LzmaStream(output, CompressionMode.Compress, leaveOpen: true);
                lzmaStream.SetLength(input!.Length);
                input.CopyTo(lzmaStream, (int)input.Length);
            }

            output.Position = 0;

            // compare the streams
            using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            await CompareStreams(output, lzma);
        }

        [Test]
        public async Task WithBufferSizeAsync()
        {
            using var output = new MemoryStream();
            using (var input = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
            {
                await Assert.That(input).IsNotNull();
#if NETCOREAPP3_0_OR_GREATER
                await
#endif
                using var lzmaStream = new LzmaStream(output, CompressionMode.Compress, leaveOpen: true);
                lzmaStream.SetLength(input!.Length);
                await input.CopyToAsync(lzmaStream, (int)input.Length);
            }

            output.Position = 0;

            // compare the streams
            using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            await CompareStreams(output, lzma);
        }

        [Test]
        public async Task WithoutBufferSize()
        {
            using var output = new MemoryStream();
            using (var input = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
            {
                _ = await Assert.That(input).IsNotNull();
                using var lzmaStream = new LzmaStream(output, CompressionMode.Compress, leaveOpen: true);
                lzmaStream.SetLength(input!.Length);
                input.CopyTo(lzmaStream);
            }

            output.Position = 0;

            // compare the streams
            using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            await CompareStreams(output, lzma);
        }

        [Test]
        public async Task WithoutBufferSizeAsync()
        {
            using var output = new MemoryStream();
            using (var input = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
            {
                await Assert.That(input).IsNotNull();
#if NETCOREAPP3_0_OR_GREATER
                await
#endif
                using var lzmaStream = new LzmaStream(output, CompressionMode.Compress, leaveOpen: true);
                lzmaStream.SetLength(input!.Length);
                await input.CopyToAsync(lzmaStream);
            }

            output.Position = 0;

            // compare the streams
            using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            await CompareStreams(output, lzma);
        }
    }

    public class Decode
    {
        [Test]
        public async Task WithBufferSize()
        {
            using var outStream = new MemoryStream();
            using var inStream = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            _ = await Assert.That(inStream).IsNotNull();
            using var lzmaStream = new LzmaStream(inStream!, CompressionMode.Decompress);
            lzmaStream.CopyTo(outStream, 1024);
            outStream.Position = 0;
            using var txt = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt");
            await CompareStreams(outStream, txt);
        }

        [Test]
        public async Task WithBufferSizeAsync()
        {
            using var outStream = new MemoryStream();
            using var inStream = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            await Assert.That(inStream).IsNotNull();
#if NETCOREAPP3_0_OR_GREATER
            await
#endif
            using var lzmaStream = new LzmaStream(inStream!, CompressionMode.Decompress);
            await lzmaStream.CopyToAsync(outStream, 1024);
            outStream.Position = 0;
            using var txt = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt");
            await CompareStreams(outStream, txt);
        }

        [Test]
        public async Task WithoutBufferSize()
        {
            using var outStream = new MemoryStream();
            using var inStream = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            _ = await Assert.That(inStream).IsNotNull();
            using var lzmaStream = new LzmaStream(inStream!, CompressionMode.Decompress);
            lzmaStream.CopyTo(outStream);
            outStream.Position = 0;
            using var txt = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt");
            await CompareStreams(outStream, txt);
        }

        [Test]
        public async Task WithoutBufferSizeAsync()
        {
            using var outStream = new MemoryStream();
            using var inStream = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            await Assert.That(inStream).IsNotNull();
#if NETCOREAPP3_0_OR_GREATER
            await
#endif
            using var lzmaStream = new LzmaStream(inStream!, CompressionMode.Decompress);
            await lzmaStream.CopyToAsync(outStream);
            outStream.Position = 0;
            using var txt = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt");
            await CompareStreams(outStream, txt);
        }
    }

    private static async Task CompareStreams(Stream? first, Stream? second)
    {
        if (first is null && second is null)
        {
            return;
        }

        _ = await Assert.That(first).IsNotNull();
        _ = await Assert.That(second).IsNotNull();
        _ = await Assert.That(first!.Length).IsEqualTo(second!.Length);

        var bytesLeft = first.Length - first.Position;
        while (bytesLeft > 0)
        {
            var bytesToRead = (int)Math.Min(bytesLeft, 128);

            var firstArray = new byte[bytesToRead];
            var secondArray = new byte[bytesToRead];

            var bytesRead = first.Read(firstArray, 0, bytesToRead);

            _ = await Assert.That(bytesRead).IsEqualTo(bytesToRead);

            bytesRead = second.Read(secondArray, 0, bytesToRead);

            _ = await Assert.That(bytesRead).IsEqualTo(bytesToRead);

            _ = await Assert.That(firstArray).IsEquivalentTo(secondArray);

            bytesLeft -= bytesRead;
        }
    }
}