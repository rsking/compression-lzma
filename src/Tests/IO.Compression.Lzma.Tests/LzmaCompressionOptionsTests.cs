// -----------------------------------------------------------------------
// <copyright file="LzmaCompressionOptionsTests.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.Tests;

public class LzmaCompressionOptionsTests
{
    [Test]
    public async Task FromDefaultOptions()
    {
        var defaultProperties = LzmaEncoderTests.GetDefaultProperties();
        var defaultFromOptions = new LzmaCompressionOptions().ToDictionary();

        await Assert.That(defaultProperties).IsEquivalentTo(defaultFromOptions);
    }
}