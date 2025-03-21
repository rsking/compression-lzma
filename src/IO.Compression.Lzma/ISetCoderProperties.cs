// -----------------------------------------------------------------------
// <copyright file="ISetCoderProperties.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

/// <summary>
/// Interface for setting coder properties.
/// </summary>
internal interface ISetCoderProperties
{
    /// <summary>
    /// Sets the coder properties.
    /// </summary>
    /// <param name="propIDs">The property IDs.</param>
    /// <param name="properties">The properties.</param>
    void SetCoderProperties(CoderPropId[] propIDs, object[] properties);
}