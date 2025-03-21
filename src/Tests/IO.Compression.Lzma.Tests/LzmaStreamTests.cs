// -----------------------------------------------------------------------
// <copyright file="LzmaStreamTests.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.Tests;
using System;

public class LzmaStreamTests
{
    public class Encode
    {
        [Fact]
        public void CopyFrom()
        {
            using var output = new MemoryStream();
            using (var input = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
            {
                Assert.NotNull(input);
                using var lzmaStream = new LzmaStream(output, System.IO.Compression.CompressionMode.Compress, leaveOpen: true);
                lzmaStream.CopyFrom(input);
            }

            output.Position = 0;

            // compare the streams
            using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            CompareStreams(output, lzma);
        }

        [Fact]
        public void WithBufferSize()
        {
            using var output = new MemoryStream();
            using (var input = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
            {
                Assert.NotNull(input);
                using var lzmaStream = new LzmaStream(output, System.IO.Compression.CompressionMode.Compress, leaveOpen: true);
                lzmaStream.SetLength(input.Length);
                input.CopyTo(lzmaStream, (int)input.Length);
            }

            output.Position = 0;

            // compare the streams
            using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            CompareStreams(output, lzma);
        }

        [Fact]
        public void WithoutBufferSize()
        {
            using var output = new MemoryStream();
            using (var input = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
            {
                Assert.NotNull(input);
                using var lzmaStream = new LzmaStream(output, System.IO.Compression.CompressionMode.Compress, leaveOpen: true);
                lzmaStream.SetLength(input.Length);
                input.CopyTo(lzmaStream);
            }

            output.Position = 0;

            // compare the streams
            using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            CompareStreams(output, lzma);
        }
    }

    public class Decode
    {
        [Fact]
        public void WithBufferSize()
        {
            using var outStream = new MemoryStream();
            using var inStream = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            Assert.NotNull(inStream);
            using var lzmaStream = new LzmaStream(inStream, System.IO.Compression.CompressionMode.Decompress);
            lzmaStream.CopyTo(outStream, short.MaxValue);
            outStream.Position = 0;
            using var txt = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt");
            CompareStreams(outStream, txt);
        }

        [Fact]
        public void WithoutBufferSize()
        {
            using var outStream = new MemoryStream();
            using var inStream = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
            Assert.NotNull(inStream);
            using var lzmaStream = new LzmaStream(inStream, System.IO.Compression.CompressionMode.Decompress);
            lzmaStream.CopyTo(outStream);
            outStream.Position = 0;
            using var txt = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt");
            CompareStreams(outStream, txt);
        }
    }

    private static void CompareStreams(Stream? first, Stream? second)
    {
        if (first is null && second is null)
        {
            return;
        }

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Length, second.Length);

        var bytesLeft = first.Length - first.Position;
        while (bytesLeft > 0)
        {
            var bytesToRead = (int)Math.Min(bytesLeft, 128);

            var firstArray = new byte[bytesToRead];
            var secondArray = new byte[bytesToRead];

            var bytesRead = first.Read(firstArray, 0, bytesToRead);

            Assert.Equal(bytesRead, bytesToRead);

            bytesRead = second.Read(secondArray, 0, bytesToRead);

            Assert.Equal(bytesRead, bytesToRead);

            Assert.Equal(firstArray, secondArray);

            bytesLeft -= bytesRead;
        }
    }
}
