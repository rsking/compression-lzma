// -----------------------------------------------------------------------
// <copyright file="IInWindowStream.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.LZ;

/// <summary>
/// The in windows stream.
/// </summary>
internal interface IInWindowStream
{
    /// <summary>
    /// Sets the stream.
    /// </summary>
    /// <param name="inStream">The stream to set.</param>
    void SetStream(Stream inStream);

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    void Init();

    /// <summary>
    /// Releases the stream.
    /// </summary>
    void ReleaseStream();

    /// <summary>
    /// Gets the index bytes.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The index byte.</returns>
    byte GetIndexByte(int index);

    /// <summary>
    /// Gets the match length.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="distance">The distance.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The match length.</returns>
    uint GetMatchLen(int index, uint distance, uint limit);

    /// <summary>
    /// Gets the number of available bytes.
    /// </summary>
    /// <returns>The number of available bytes.</returns>
    uint GetNumAvailableBytes();
}