// -----------------------------------------------------------------------
// <copyright file="State.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

/// <summary>
/// The state structure.
/// </summary>
internal struct State
{
    /// <summary>
    /// The index.
    /// </summary>
    public uint Index;

    public State()
    {
        this.Index = default;
    }

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
    public void UpdateMatch() => this.Index = this.Index < 7U ? 7U : 10U;

    /// <summary>
    /// Updates the rep.
    /// </summary>
    public void UpdateRep() => this.Index = this.Index < 7U ? 8U : 11U;

    /// <summary>
    /// Updates the short rep.
    /// </summary>
    public void UpdateShortRep() => this.Index = this.Index < 7U ? 9U : 11U;

    /// <summary>
    /// Gets a value indicating whether this instace is a char.
    /// </summary>
    /// <returns><see langword="true"/> if this is a char; otherwise <see langword="false"/>.</returns>
    public readonly bool IsCharState() => this.Index < 7U;
}