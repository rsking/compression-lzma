// -----------------------------------------------------------------------
// <copyright file="LzmaStreamTests.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.Tests;
using System;

public class LzmaStreamTests
{
    [Fact(Skip = "not ready yet")]
    public void Encode()
    {
        using var outStream = new MemoryStream();
        using (var inStream = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
        {
            Assert.NotNull(inStream);
            using var lzmaStream = new LzmaStream(outStream, System.IO.Compression.CompressionMode.Compress);
            inStream.CopyTo(lzmaStream, (int)inStream.Length);
        }

        outStream.Position = 0;

        // compare the streams
        using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
        CompareStreams(outStream, lzma);
    }

    [Fact]
    public void Decode()
    {
        using var outStream = new MemoryStream();
        using var inStream = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");
        Assert.NotNull(inStream);
        using var lzmaStream = new LzmaStream(inStream, System.IO.Compression.CompressionMode.Decompress);
        lzmaStream.CopyTo(outStream, -1);
        outStream.Position = 0;
        using var txt = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt");
        CompareStreams(outStream, txt);
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
