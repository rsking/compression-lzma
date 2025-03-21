// -----------------------------------------------------------------------
// <copyright file="LzmaEncoderTests.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.Tests;

public class LzmaEncoderTests
{
    [Fact]
    public void Encode()
    {
        var dictionary = 1 << 23;
        var posStateBits = 2;
        var litContextBits = 3;
        var litPosBits = 0;
        var algorithm = 2;
        var numFastBytes = 128;
        var mf = "bt4";
        var eos = false;

        var properties = new Dictionary<CoderPropId, object>
        {
            { CoderPropId.DictionarySize, dictionary },
            { CoderPropId.PosStateBits, posStateBits },
            { CoderPropId.LitContextBits, litContextBits },
            { CoderPropId.LitPosBits, litPosBits },
            { CoderPropId.Algorithm, algorithm },
            { CoderPropId.NumFastBytes, numFastBytes },
            { CoderPropId.MatchFinder, mf },
            { CoderPropId.EndMarker, eos },
        };


        var encoder = new LzmaEncoder(properties);

        using var outStream = new MemoryStream();
        encoder.WriteCoderProperties(outStream);

        using (var inStream = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
        {
            Assert.NotNull(inStream);

            var fileSize = inStream.Length;

            for (var i = 0; i < 8; i++)
            {
                outStream.WriteByte((byte)(fileSize >> (8 * i)));
            }

            encoder.Encode(inStream, outStream);
        }

        outStream.Position = 0;

        // compare the streams
        using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");

        Assert.NotNull(lzma);
        Assert.Equal(outStream.Length, lzma.Length);

        var bytesLeft = outStream.Length - outStream.Position;
        while (bytesLeft > 0)
        {
            var bytesToRead = (int)Math.Min(bytesLeft, 128);

            var first = new byte[bytesToRead];
            var second = new byte[bytesToRead];

            var bytesRead = outStream.Read(first, 0, bytesToRead);

            Assert.Equal(bytesRead, bytesToRead);

            bytesRead = lzma.Read(second, 0, bytesToRead);

            Assert.Equal(bytesRead, bytesToRead);

            Assert.Equal(first, second);

            bytesLeft -= bytesRead;
        }
    }
}
