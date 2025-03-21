// -----------------------------------------------------------------------
// <copyright file="LzmaEncoderTests.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.Tests;

public class LzmaEncoderTests
{
    internal static IDictionary<CoderPropId, object> GetDefaultProperties()
    {
        var dictionary = 1 << 23;
        var posStateBits = 2;
        var litContextBits = 3;
        var litPosBits = 0;
        var algorithm = 2;
        var numFastBytes = 128;
        var mf = "bt4";
        var eos = false;

        return new Dictionary<CoderPropId, object>
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
    }

    [Test]
    public async Task Encode()
    {
        var encoder = new LzmaEncoder(GetDefaultProperties());

        using var output = new MemoryStream();
        encoder.WriteCoderProperties(output);

        using (var input = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.txt"))
        {
            await Assert.That(input).IsNotNull();

            var fileSize = input!.Length;

            for (var i = 0; i < 8; i++)
            {
                output.WriteByte((byte)(fileSize >> (8 * i)));
            }

            encoder.Compress(input, output);
        }

        output.Position = 0;

        // compare the streams
        using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");

        await Assert.That(lzma).IsNotNull();
        await Assert.That(lzma!.Length).IsEqualTo(output.Length);

        var bytesLeft = output.Length - output.Position;
        while (bytesLeft > 0)
        {
            var bytesToRead = (int)Math.Min(bytesLeft, 128);

            var first = new byte[bytesToRead];
            var second = new byte[bytesToRead];

            var bytesRead = output.Read(first, 0, bytesToRead);

            await Assert.That(bytesRead).IsEqualTo(bytesToRead);

            bytesRead = lzma.Read(second, 0, bytesToRead);

            await Assert.That(bytesRead).IsEqualTo(bytesToRead);

            await Assert.That(first).IsEquivalentTo(second);

            bytesLeft -= bytesRead;
        }
    }
}