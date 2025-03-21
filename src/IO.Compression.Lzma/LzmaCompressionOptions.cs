// -----------------------------------------------------------------------
// <copyright file="LzmaCompressionOptions.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

/// <summary>
/// Provides compression options to be used with <see cref="LzmaStream"/>.
/// </summary>
public sealed class LzmaCompressionOptions
{
    private const int DefaultDictionary = 23;
    private const int DefaultFastBytes = 128;
    private const int DefaultAlgorithm = 2;
    private const int DefaultLiteralContextBits = 3;
    private const int DefaultLiteralPosBits = 0;
    private const int DefaultPosBits = 2;
    private const LzmaMatchFinder DefaultMatchFinder = LzmaMatchFinder.BT4;

    /// <summary>
    /// Gets or sets the dictionary.
    /// </summary>
    public int Dictionary { get; set; } = DefaultDictionary;

    /// <summary>
    /// Gets or sets the number of fast bytes.
    /// </summary>
    public int FastBytes { get; set; } = DefaultFastBytes;

    /// <summary>
    /// Gets or sets the number of literal context bits.
    /// </summary>
    public int LiteralContextBits { get; set; } = DefaultLiteralContextBits;

    /// <summary>
    /// Gets or sets the number of literal pos bits.
    /// </summary>
    public int LiteralPosBits { get; set; } = DefaultLiteralPosBits;

    /// <summary>
    /// Gets or sets the number of pos bits.
    /// </summary>
    public int PosBits { get; set; } = DefaultPosBits;

    /// <summary>
    /// Gets or sets the match finder.
    /// </summary>
    public LzmaMatchFinder MatchFinder { get; set; } = DefaultMatchFinder;

    /// <summary>
    /// Gets or sets a value indicating whether to write the end of stream marker.
    /// </summary>
    public bool EndMarker { get; set; } = false;

    /// <summary>
    /// Converts this instance into a dictionary.
    /// </summary>
    /// <returns>The dictionary.</returns>
    public IDictionary<CoderPropId, object> ToDictionary() => new Dictionary<CoderPropId, object>
    {
        { CoderPropId.DictionarySize, 1 << this.Dictionary },
        { CoderPropId.PosStateBits, this.PosBits },
        { CoderPropId.LitContextBits, this.LiteralContextBits },
        { CoderPropId.LitPosBits, this.LiteralPosBits },
        { CoderPropId.Algorithm, DefaultAlgorithm },
        { CoderPropId.NumFastBytes, this.FastBytes },
        { CoderPropId.MatchFinder, this.MatchFinder.ToString().ToLowerInvariant() },
        { CoderPropId.EndMarker, this.EndMarker },
    };

    /// <summary>
    /// Creates the encoder.
    /// </summary>
    /// <returns>The created encoder.</returns>
    internal LzmaEncoder CreateEncoder() => new(this.ToDictionary());
}