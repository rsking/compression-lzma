// -----------------------------------------------------------------------
// <copyright file="BinTree.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression.LZ;

/// <summary>
/// The BIN tree.
/// </summary>
internal class BinTree : InWindow, IMatchFinder
{
    private const uint Hash2Size = 1U << 10;
    private const uint Hash3Size = 1U << 16;
    private const uint BT2HashSize = 1U << 16;
    private const uint StartMaxLen = 1U;
    private const uint Hash3Offset = Hash2Size;
    private const uint EmptyHashValue = default;
    private const uint MaxValForNormalize = (1U << 31) - 1U;

    private readonly bool hashArray = true;

    private readonly uint numHashDirectBytes;
    private readonly uint minMatchCheck;
    private readonly uint fixHashSize;

    private uint cyclicBufferPos;
    private uint cyclicBufferSize;
    private uint matchMaxLen;

    private uint[]? son;
    private uint[]? hash;

    private uint cutValue = byte.MaxValue;
    private uint hashMask;
    private uint hashSizeSum;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinTree"/> class.
    /// </summary>
    /// <param name="numHashBytes">The number of hash bytes.</param>
    public BinTree(int numHashBytes)
    {
        this.hashArray = numHashBytes > 2;
        if (this.hashArray)
        {
            this.numHashDirectBytes = 0U;
            this.minMatchCheck = 4U;
            this.fixHashSize = Hash2Size + Hash3Size;
        }
        else
        {
            this.numHashDirectBytes = 2U;
            this.minMatchCheck = 2U + 1U;
            this.fixHashSize = 0U;
        }
    }

    /// <inheritdoc/>
    public override void Init()
    {
        base.Init();
        if (this.hash is not null)
        {
            for (var i = 0U; i < this.hashSizeSum; i++)
            {
                this.hash[i] = EmptyHashValue;
            }
        }

        this.cyclicBufferPos = 0;
        this.ReduceOffsets(-1);
    }

    /// <inheritdoc/>
    public override void MovePos()
    {
        if (++this.cyclicBufferPos >= this.cyclicBufferSize)
        {
            this.cyclicBufferPos = 0;
        }

        base.MovePos();
        if (this.Pos is MaxValForNormalize)
        {
            this.Normalize();
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="historySize"/> is too large.</exception>
    public void Create(uint historySize, uint keepAddBufferBefore, uint matchMaxLen, uint keepAddBufferAfter)
    {
        if (historySize > MaxValForNormalize - 256)
        {
            throw new ArgumentOutOfRangeException(nameof(historySize));
        }

        this.cutValue = 16 + (matchMaxLen >> 1);

        var windowReservSize = ((historySize + keepAddBufferBefore + matchMaxLen + keepAddBufferAfter) / 2) + 256;

        this.Create(historySize + keepAddBufferBefore, matchMaxLen + keepAddBufferAfter, windowReservSize);

        this.matchMaxLen = matchMaxLen;

        var newCyclicBufferSize = historySize + 1;
        if (this.cyclicBufferSize != newCyclicBufferSize)
        {
            this.cyclicBufferSize = newCyclicBufferSize;
            this.son = new uint[this.cyclicBufferSize * 2];
        }

        var hs = BT2HashSize;

        if (this.hashArray)
        {
            hs = historySize - 1;
            hs |= hs >> 1;
            hs |= hs >> 2;
            hs |= hs >> 4;
            hs |= hs >> 8;
            hs >>= 1;
            hs |= ushort.MaxValue;
            if (hs > (1 << 24))
            {
                hs >>= 1;
            }

            this.hashMask = hs;
            hs++;
            hs += this.fixHashSize;
        }

        if (hs != this.hashSizeSum)
        {
            this.hashSizeSum = hs;
            this.hash = new uint[hs];
        }
    }

    /// <inheritdoc/>
    public uint GetMatches(uint[] distances)
    {
        uint lenLimit;
        if (this.Pos + this.matchMaxLen <= this.StreamPos)
        {
            lenLimit = this.matchMaxLen;
        }
        else
        {
            lenLimit = this.StreamPos - this.Pos;
            if (lenLimit < this.minMatchCheck)
            {
                this.MovePos();
                return 0;
            }
        }

        var offset = 0U;
        var matchMinPos = (this.Pos > this.cyclicBufferSize) ? (this.Pos - this.cyclicBufferSize) : 0;
        var cur = this.BufferOffset + this.Pos;

        // to avoid items for len < hashSize
        var maxLen = StartMaxLen;
        uint hashValue;
        var hash2Value = 0U;
        var hash3Value = 0U;

        if (this.BufferBase is null)
        {
            throw new InvalidOperationException();
        }

        if (this.hashArray)
        {
            var temp = Crc.Table[this.BufferBase[cur]] ^ this.BufferBase[cur + 1];
            hash2Value = temp & (Hash2Size - 1);
            temp ^= (uint)this.BufferBase[cur + 2] << 8;
            hash3Value = temp & (Hash3Size - 1);
            hashValue = (temp ^ (Crc.Table[this.BufferBase[cur + 3]] << 5)) & this.hashMask;
        }
        else
        {
            hashValue = this.BufferBase[cur] ^ ((uint)this.BufferBase[cur + 1] << 8);
        }

        if (this.hash is null)
        {
            throw new InvalidOperationException();
        }

        var curMatch = this.hash[this.fixHashSize + hashValue];
        if (this.hashArray)
        {
            var curMatch2 = this.hash[hash2Value];
            var curMatch3 = this.hash[Hash3Offset + hash3Value];
            this.hash[hash2Value] = this.Pos;
            this.hash[Hash3Offset + hash3Value] = this.Pos;
            if (curMatch2 > matchMinPos
                && this.BufferBase[this.BufferOffset + curMatch2] == this.BufferBase[cur])
            {
                distances[offset++] = maxLen = 2;
                distances[offset++] = this.Pos - curMatch2 - 1;
            }

            if (curMatch3 > matchMinPos
                && this.BufferBase[this.BufferOffset + curMatch3] == this.BufferBase[cur])
            {
                if (curMatch3 == curMatch2
                    && offset > 1)
                {
                    offset -= 2;
                }

                distances[offset++] = maxLen = 3;
                distances[offset++] = this.Pos - curMatch3 - 1;
                curMatch2 = curMatch3;
            }

            if (offset is > 1 && curMatch2 == curMatch)
            {
                offset -= 2;
                maxLen = StartMaxLen;
            }
        }

        this.hash[this.fixHashSize + hashValue] = this.Pos;

        var ptr0 = (this.cyclicBufferPos << 1) + 1;
        var ptr1 = this.cyclicBufferPos << 1;

        var len0 = this.numHashDirectBytes;
        var len1 = this.numHashDirectBytes;

        if (this.numHashDirectBytes is not 0
            && curMatch > matchMinPos
            && this.BufferBase[this.BufferOffset + curMatch + this.numHashDirectBytes] != this.BufferBase[cur + this.numHashDirectBytes])
        {
            distances[offset++] = maxLen = this.numHashDirectBytes;
            distances[offset++] = this.Pos - curMatch - 1;
        }

        var count = this.cutValue;

        if (this.son is null)
        {
            throw new InvalidOperationException();
        }

        while (true)
        {
            if (curMatch <= matchMinPos || count-- is 0)
            {
                this.son[ptr0] = this.son[ptr1] = EmptyHashValue;
                break;
            }

            var delta = this.Pos - curMatch;
            var cyclicPos = ((delta <= this.cyclicBufferPos)
                ? (this.cyclicBufferPos - delta)
                : (this.cyclicBufferPos - delta + this.cyclicBufferSize)) << 1;

            var pby1 = this.BufferOffset + curMatch;
            var len = Math.Min(len0, len1);
            if (this.BufferBase[pby1 + len] == this.BufferBase[cur + len])
            {
                while (++len != lenLimit)
                {
                    if (this.BufferBase[pby1 + len] != this.BufferBase[cur + len])
                    {
                        break;
                    }
                }

                if (maxLen < len)
                {
                    distances[offset++] = maxLen = len;
                    distances[offset++] = delta - 1;
                    if (len == lenLimit)
                    {
                        this.son[ptr1] = this.son[cyclicPos];
                        this.son[ptr0] = this.son[cyclicPos + 1];
                        break;
                    }
                }
            }

            if (this.BufferBase[pby1 + len] < this.BufferBase[cur + len])
            {
                this.son[ptr1] = curMatch;
                ptr1 = cyclicPos + 1;
                curMatch = this.son[ptr1];
                len1 = len;
            }
            else
            {
                this.son[ptr0] = curMatch;
                ptr0 = cyclicPos;
                curMatch = this.son[ptr0];
                len0 = len;
            }
        }

        this.MovePos();
        return offset;
    }

    /// <inheritdoc />
    public void Skip(uint num)
    {
        do
        {
            uint lenLimit;
            if (this.Pos + this.matchMaxLen <= this.StreamPos)
            {
                lenLimit = this.matchMaxLen;
            }
            else
            {
                lenLimit = this.StreamPos - this.Pos;
                if (lenLimit < this.minMatchCheck)
                {
                    this.MovePos();
                    continue;
                }
            }

            var matchMinPos = (this.Pos > this.cyclicBufferSize) ? (this.Pos - this.cyclicBufferSize) : 0;
            var cur = this.BufferOffset + this.Pos;

            uint hashValue;
            if (this.BufferBase is null)
            {
                throw new InvalidOperationException();
            }

            if (this.hash is null)
            {
                throw new InvalidOperationException();
            }

            if (this.hashArray)
            {
                var temp = Crc.Table[this.BufferBase[cur]] ^ this.BufferBase[cur + 1];
                var hash2Value = temp & (Hash2Size - 1);
                this.hash[hash2Value] = this.Pos;
                temp ^= (uint)this.BufferBase[cur + 2] << 8;
                var hash3Value = temp & (Hash3Size - 1);
                this.hash[Hash3Offset + hash3Value] = this.Pos;
                hashValue = (temp ^ (Crc.Table[this.BufferBase[cur + 3]] << 5)) & this.hashMask;
            }
            else
            {
                hashValue = this.BufferBase[cur] ^ ((uint)this.BufferBase[cur + 1] << 8);
            }

            var curMatch = this.hash[this.fixHashSize + hashValue];
            this.hash[this.fixHashSize + hashValue] = this.Pos;

            var ptr0 = (this.cyclicBufferPos << 1) + 1;
            var ptr1 = this.cyclicBufferPos << 1;

            var len0 = this.numHashDirectBytes;
            var len1 = this.numHashDirectBytes;

            if (this.son is null)
            {
                throw new InvalidOperationException();
            }

            var count = this.cutValue;
            while (true)
            {
                if (curMatch <= matchMinPos || count-- is 0)
                {
                    this.son[ptr0] = this.son[ptr1] = EmptyHashValue;
                    break;
                }

                var delta = this.Pos - curMatch;
                var cyclicPos = ((delta <= this.cyclicBufferPos)
                    ? (this.cyclicBufferPos - delta)
                    : (this.cyclicBufferPos - delta + this.cyclicBufferSize)) << 1;

                var pby1 = this.BufferOffset + curMatch;
                var len = Math.Min(len0, len1);
                if (this.BufferBase[pby1 + len] == this.BufferBase[cur + len])
                {
                    while (++len != lenLimit)
                    {
                        if (this.BufferBase[pby1 + len] != this.BufferBase[cur + len])
                        {
                            break;
                        }
                    }

                    if (len == lenLimit)
                    {
                        this.son[ptr1] = this.son[cyclicPos];
                        this.son[ptr0] = this.son[cyclicPos + 1];
                        break;
                    }
                }

                if (this.BufferBase[pby1 + len] < this.BufferBase[cur + len])
                {
                    this.son[ptr1] = curMatch;
                    ptr1 = cyclicPos + 1;
                    curMatch = this.son[ptr1];
                    len1 = len;
                }
                else
                {
                    this.son[ptr0] = curMatch;
                    ptr0 = cyclicPos;
                    curMatch = this.son[ptr0];
                    len0 = len;
                }
            }

            this.MovePos();
        }
        while (--num is not 0U);
    }

    private void NormalizeLinks(uint[] items, uint numItems, uint subValue)
    {
        for (var i = 0U; i < numItems; i++)
        {
            var value = items[i];
            if (value <= subValue)
            {
                value = EmptyHashValue;
            }
            else
            {
                value -= subValue;
            }

            items[i] = value;
        }
    }

    private void Normalize()
    {
        var subValue = this.Pos - this.cyclicBufferSize;

        if (this.son is null)
        {
            throw new InvalidOperationException();
        }

        this.NormalizeLinks(this.son, this.cyclicBufferSize * 2, subValue);

        if (this.hash is null)
        {
            throw new InvalidOperationException();
        }

        this.NormalizeLinks(this.hash, this.hashSizeSum, subValue);

        this.ReduceOffsets((int)subValue);
    }
}