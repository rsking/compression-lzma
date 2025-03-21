// -----------------------------------------------------------------------
// <copyright file="Constants.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

/// <summary>
/// The constants class.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// The number of registered distances.
    /// </summary>
    public const uint NumRepDistances = 4U;

    /// <summary>
    /// The number of states.
    /// </summary>
    public const uint NumStates = 12U;

    /// <summary>
    /// The number of position slot bits.
    /// </summary>
    public const int NumPosSlotBits = 6;

    /// <summary>
    /// The dictionary size minimum.
    /// </summary>
    public const int DicLogSizeMin = 0;

    /// <summary>
    /// The number length to position state bits.
    /// </summary>
    public const int NumLenToPosStatesBits = 2; // it's for speed optimization

    /// <summary>
    /// The number length to position states.
    /// </summary>
    public const uint NumLenToPosStates = 1U << NumLenToPosStatesBits;

    /// <summary>
    /// The match minimum length.
    /// </summary>
    public const uint MatchMinLen = 2U;

    /// <summary>
    /// The number of align bits.
    /// </summary>
    public const int NumAlignBits = 4;

    /// <summary>
    /// The align table size.
    /// </summary>
    public const uint AlignTableSize = 1U << NumAlignBits;

    /// <summary>
    /// The align mask.
    /// </summary>
    public const uint AlignMask = AlignTableSize - 1;

    /// <summary>
    /// The start position model index.
    /// </summary>
    public const uint StartPosModelIndex = 4U;

    /// <summary>
    /// The end position model index.
    /// </summary>
    public const uint EndPosModelIndex = 14U;

    /// <summary>
    /// The number of full distances.
    /// </summary>
    public const uint NumFullDistances = 1U << ((int)EndPosModelIndex / 2);

    /// <summary>
    /// The number lit position states bit encoding maximum.
    /// </summary>
    public const uint NumLitPosStatesBitsEncodingMax = 4U;

    /// <summary>
    /// The number lit context bits maximum.
    /// </summary>
    public const uint NumLitContextBitsMax = 8U;

    /// <summary>
    /// The number of position state bits maximum.
    /// </summary>
    public const int NumPosStatesBitsMax = 4;

    /// <summary>
    /// The number of position states maximum.
    /// </summary>
    public const uint NumPosStatesMax = 1 << NumPosStatesBitsMax;

    /// <summary>
    /// The number of position states bits encoding maximum.
    /// </summary>
    public const int NumPosStatesBitsEncodingMax = 4;

    /// <summary>
    /// The number os position states encoding maximum.
    /// </summary>
    public const uint NumPosStatesEncodingMax = 1 << NumPosStatesBitsEncodingMax;

    /// <summary>
    /// The number of low length bits.
    /// </summary>
    public const int NumLowLenBits = 3;

    /// <summary>
    /// The number of mid length bits.
    /// </summary>
    public const int NumMidLenBits = 3;

    /// <summary>
    /// The number of high length bits.
    /// </summary>
    public const int NumHighLenBits = 8;

    /// <summary>
    /// The number of low length symbols.
    /// </summary>
    public const uint NumLowLenSymbols = 1 << NumLowLenBits;

    /// <summary>
    /// The number of mid length symbols.
    /// </summary>
    public const uint NumMidLenSymbols = 1 << NumMidLenBits;

    /// <summary>
    /// The number of length symbols.
    /// </summary>
    public const uint NumLenSymbols = NumLowLenSymbols + NumMidLenSymbols + (1 << NumHighLenBits);

    /// <summary>
    /// The match maximum length.
    /// </summary>
    public const uint MatchMaxLen = MatchMinLen + NumLenSymbols - 1;

    /// <summary>
    /// Gets the length to position state.
    /// </summary>
    /// <param name="len">The length.</param>
    /// <returns>The distance to the position state.</returns>
    public static uint GetLenToPosState(uint len)
    {
        len -= MatchMinLen;
        return len < NumLenToPosStates
            ? len
            : NumLenToPosStates - 1;
    }
}