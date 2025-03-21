// -----------------------------------------------------------------------
// <copyright file="LzmaCompressionOptionsTests.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.Tests;

public class LzmaCompressionOptionsTests
{
    [Fact]
    public void FromDefaultOptions()
    {
        var defaultProperties = LzmaEncoderTests.GetDefaultProperties();
        var defaultFromOptions = new LzmaCompressionOptions().ToDictionary();

        Assert.Equal(defaultProperties, defaultFromOptions);
    }
}
