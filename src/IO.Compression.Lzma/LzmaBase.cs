// -----------------------------------------------------------------------
// <copyright file="LzmaBase.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

/// <summary>
/// The <c>LZMA</c> base class.
/// </summary>
internal abstract class LzmaBase
{
    /// <summary>
    /// The number of registered distances.
    /// </summary>
    public const uint NumRepDistances = 4;

    /// <summary>
    /// The number of states.
    /// </summary>
    public const uint NumStates = 12;

    /// <summary>
    /// The number of position slot bits.
    /// </summary>
    public const int NumPosSlotBits = 6;

    /// <summary>
    /// The number length to position state bits.
    /// </summary>
    public const int NumLenToPosStatesBits = 2; // it's for speed optimization

    /// <summary>
    /// The number length to position states.
    /// </summary>
    public const uint NumLenToPosStates = 1 << NumLenToPosStatesBits;

    /// <summary>
    /// The match minimum length.
    /// </summary>
    public const uint MatchMinLen = 2;

    /// <summary>
    /// The number of align bits.
    /// </summary>
    public const int NumAlignBits = 4;

    /// <summary>
    /// The start position model index.
    /// </summary>
    public const uint StartPosModelIndex = 4;

    /// <summary>
    /// The end position model index.
    /// </summary>
    public const uint EndPosModelIndex = 14;

    /// <summary>
    /// The number of full distances.
    /// </summary>
    public const uint NumFullDistances = 1 << ((int)EndPosModelIndex / 2);

    /// <summary>
    /// The number of position state bits maximum.
    /// </summary>
    public const int NumPosStatesBitsMax = 4;

    /// <summary>
    /// The number of position states maximum.
    /// </summary>
    public const uint NumPosStatesMax = 1 << NumPosStatesBitsMax;

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
    /// GEts the length to position state.
    /// </summary>
    /// <param name="len">The length.</param>
    /// <returns>The distance to the position state.</returns>
    public static uint GetLenToPosState(uint len)
    {
        len -= MatchMinLen;
        if (len < NumLenToPosStates)
        {
            return len;
        }

        return NumLenToPosStates - 1;
    }

    /// <summary>
    /// The state structure.
    /// </summary>
    public struct State
    {
        /// <summary>
        /// The index.
        /// </summary>
        public uint Index;

        /// <summary>
        /// Initializes the structure.
        /// </summary>
        public void Init() => this.Index = 0;

        /// <summary>
        /// Updates the character.
        /// </summary>
        public void UpdateChar()
        {
            if (this.Index < 4)
            {
                this.Index = 0;
            }
            else if (this.Index < 10)
            {
                this.Index -= 3;
            }
            else
            {
                this.Index -= 6;
            }
        }

        /// <summary>
        /// Updates the match.
        /// </summary>
        public void UpdateMatch() => this.Index = (uint)(this.Index < 7 ? 7 : 10);

        /// <summary>
        /// Updates the rep.
        /// </summary>
        public void UpdateRep() => this.Index = (uint)(this.Index < 7 ? 8 : 11);

        /// <summary>
        /// Updates the short rep.
        /// </summary>
        public void UpdateShortRep() => this.Index = (uint)(this.Index < 7 ? 9 : 11);

        /// <summary>
        /// Gets a value indicating whether this instace is a char.
        /// </summary>
        /// <returns><see langword="true"/> if this is a char; otherwise <see langword="false"/>.</returns>
        public readonly bool IsCharState() => this.Index < 7;
    }
}