// -----------------------------------------------------------------------
// <copyright file="ISetDecoderProperties.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

/// <summary>
/// Interface for setting the decoder properties.
/// </summary>
internal interface ISetDecoderProperties
{
    /// <summary>
    /// Sets the decoder properties.
    /// </summary>
    /// <param name="properties">The properties.</param>
    void SetDecoderProperties(byte[] properties);
}