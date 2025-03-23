// -----------------------------------------------------------------------
// <copyright file="LzmaDecoderTests.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.Tests;

public class LzmaDecoderTests
{
    [Test]
    public async Task Test1()
    {
        using var lzma = typeof(LzmaDecoderTests).Assembly.GetManifestResourceStream(typeof(LzmaDecoderTests), "lorem-ipsum.lzma");

        await Assert.That(lzma).IsNotNull();

        var properties = new byte[5];
        _ = lzma!.Read(properties, 0, 5);

        var decoder = new LzmaDecoder(properties);

        var outSize = 0L;
        for (var i = 0; i < 8; i++)
        {
            var v = lzma.ReadByte();
            if (v < 0)
            {
                throw new Exception("Can't Read 1");
            }

            outSize |= ((long)(byte)v) << (8 * i);
        }

        await Assert.That(outSize).IsNotEqualTo(0L);


        var compressedSize = lzma.Length - lzma.Position;

        using var output = new MemoryStream();
        decoder.Decode(lzma, output, outSize);

        output.Position = 0;
        await Assert.That(output.Length).IsNotEqualTo(0L);
    }
}