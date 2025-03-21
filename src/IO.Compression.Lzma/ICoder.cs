// -----------------------------------------------------------------------
// <copyright file="ICoder.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

/// <summary>
/// The coder.
/// </summary>
internal interface ICoder
{
    /// <summary>
    /// Codes streams.
    /// </summary>
    /// <param name="inStream">input Stream.</param>
    /// <param name="outStream">output Stream.</param>
    /// <param name="inSize">input Size. -1 if unknown.</param>
    /// <param name="outSize">output Size. -1 if unknown.</param>
    /// <param name="progress">callback progress reference.</param>
    void Code(Stream inStream, Stream outStream, long inSize, long outSize, Action<long, long> progress);
}
