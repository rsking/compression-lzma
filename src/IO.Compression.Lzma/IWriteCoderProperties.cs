// -----------------------------------------------------------------------
// <copyright file="IWriteCoderProperties.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

/// <summary>
/// Interface for writing the coder properties.
/// </summary>
internal interface IWriteCoderProperties
{
    /// <summary>
    /// Writes the coder properties.
    /// </summary>
    /// <param name="outStream">The stream to write to.</param>
    void WriteCoderProperties(Stream outStream);
}