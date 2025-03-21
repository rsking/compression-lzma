// -----------------------------------------------------------------------
// <copyright file="LzmaEncoder.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

/// <summary>
/// The LZMA encoder.
/// </summary>
public class LzmaEncoder : ICoder, ISetCoderProperties, IWriteCoderProperties
{
    private const uint IfinityPrice = uint.MaxValue;
    private const int DefaultDictionaryLogSize = 22;
    private const uint NumFastBytesDefault = 0x20U;
    private const uint NumOpts = 1U << 12;

    private const int PropSize = 5;

    private static readonly byte[] FastPos = new byte[1 << 11];

    private static readonly string[] MatchFinderIDs =
    [
        "BT2",
        "BT4",
    ];

    private readonly uint[] repDistances = new uint[LzmaBase.NumRepDistances];

    private readonly Optimal[] optimum = new Optimal[NumOpts];
    private readonly RangeCoder.Encoder rangeEncoder = new();

    private readonly RangeCoder.BitEncoder[] isMatch = new RangeCoder.BitEncoder[LzmaBase.NumStates << LzmaBase.NumPosStatesBitsMax];
    private readonly RangeCoder.BitEncoder[] isRep = new RangeCoder.BitEncoder[LzmaBase.NumStates];
    private readonly RangeCoder.BitEncoder[] isRepG0 = new RangeCoder.BitEncoder[LzmaBase.NumStates];
    private readonly RangeCoder.BitEncoder[] isRepG1 = new RangeCoder.BitEncoder[LzmaBase.NumStates];
    private readonly RangeCoder.BitEncoder[] isRepG2 = new RangeCoder.BitEncoder[LzmaBase.NumStates];
    private readonly RangeCoder.BitEncoder[] isRep0Long = new RangeCoder.BitEncoder[LzmaBase.NumStates << LzmaBase.NumPosStatesBitsMax];

    private readonly RangeCoder.BitTreeEncoder[] posSlotEncoder = new RangeCoder.BitTreeEncoder[LzmaBase.NumLenToPosStates];

    private readonly RangeCoder.BitEncoder[] posEncoders = new RangeCoder.BitEncoder[LzmaBase.NumFullDistances - LzmaBase.EndPosModelIndex];
    private readonly LenPriceTableEncoder lenEncoder = new();
    private readonly LenPriceTableEncoder repMatchLenEncoder = new();

    private readonly LiteralEncoder literalEncoder = new();

    private readonly uint[] matchDistances = new uint[(LzmaBase.MatchMaxLen * 2) + 2];

    private readonly RangeCoder.BitTreeEncoder posAlignEncoder = new(LzmaBase.NumAlignBits);

    private readonly uint[] posSlotPrices = new uint[1 << (LzmaBase.NumPosSlotBits + LzmaBase.NumLenToPosStatesBits)];
    private readonly uint[] distancesPrices = new uint[LzmaBase.NumFullDistances << LzmaBase.NumLenToPosStatesBits];
    private readonly uint[] alignPrices = new uint[LzmaBase.AlignTableSize];

    private readonly uint[] reps = new uint[LzmaBase.NumRepDistances];
    private readonly uint[] repLens = new uint[LzmaBase.NumRepDistances];

    private readonly byte[] properties = new byte[PropSize];

    private readonly uint[] tempPrices = new uint[LzmaBase.NumFullDistances];
    private uint matchPriceCount;

    private LzmaBase.State state = default;
    private byte previousByte;

    private LZ.IMatchFinder? matchFinder;

    private uint numFastBytes = NumFastBytesDefault;
    private uint longestMatchLength;
    private uint numDistancePairs;

    private uint additionalOffset;

    private uint optimumEndIndex;
    private uint optimumCurrentIndex;

    private bool longestMatchWasFound;

    private uint alignPriceCount;

    private uint distTableSize = DefaultDictionaryLogSize * 2;

    private int posStateBits = 2;
    private uint posStateMask = 4U - 1U;
    private int numLiteralPosStateBits;
    private int numLiteralContextBits = 3;

    private uint dictionarySize = 1U << DefaultDictionaryLogSize;
    private uint dictionarySizePrev = uint.MaxValue;
    private uint numFastBytesPrev = uint.MaxValue;

    private long nowPos64;
    private bool finished;
    private Stream? inStream;

    private EMatchFinderType matchFinderType = EMatchFinderType.BT4;
    private bool writeEndMark;

    private bool needReleaseMFStream;

    private uint trainSize;

    static LzmaEncoder()
    {
        const byte FastSlots = 22;
        var c = 2;
        FastPos[0] = 0;
        FastPos[1] = 1;
        for (byte slotFast = 2; slotFast < FastSlots; slotFast++)
        {
            var k = 1U << ((slotFast >> 1) - 1);
            for (var j = 0U; j < k; j++, c++)
            {
                FastPos[c] = slotFast;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LzmaEncoder"/> class.
    /// </summary>
    public LzmaEncoder()
    {
        for (var i = 0; i < NumOpts; i++)
        {
            this.optimum[i] = new Optimal();
        }

        for (var i = 0; i < LzmaBase.NumLenToPosStates; i++)
        {
            this.posSlotEncoder[i] = new RangeCoder.BitTreeEncoder(LzmaBase.NumPosSlotBits);
        }
    }

    private enum EMatchFinderType
    {
        BT2,
        BT4,
    }

    /// <inheritdoc/>
    public void Code(Stream inStream, Stream outStream, long inSize = -1, long outSize = -1, Action<long, long>? progress = null)
    {
        this.needReleaseMFStream = false;
        try
        {
            SetStreams(inStream, outStream);
            while (true)
            {
                this.CodeOneBlock(out var processedInSize, out var processedOutSize, out var isFinished);
                if (isFinished)
                {
                    return;
                }

                progress?.Invoke(processedInSize, processedOutSize);
            }
        }
        finally
        {
            this.ReleaseMFStream();
            this.rangeEncoder.ReleaseStream();
        }

        void SetStreams(Stream inStream, Stream outStream)
        {
            this.inStream = inStream;
            this.finished = false;
            this.Create();
            this.rangeEncoder.SetStream(outStream);
            this.Init();

            this.FillDistancesPrices();
            this.FillAlignPrices();

            this.lenEncoder.SetTableSize(this.numFastBytes + 1 - LzmaBase.MatchMinLen);
            this.lenEncoder.UpdateTables(1U << this.posStateBits);
            this.repMatchLenEncoder.SetTableSize(this.numFastBytes + 1 - LzmaBase.MatchMinLen);
            this.repMatchLenEncoder.UpdateTables(1U << this.posStateBits);

            this.nowPos64 = 0;
        }
    }

    /// <summary>
    /// Codes one block.
    /// </summary>
    /// <param name="inSize">The input size.</param>
    /// <param name="outSize">The output size.</param>
    /// <param name="finished">Whether this is finished.</param>
    public void CodeOneBlock(out long inSize, out long outSize, out bool finished)
    {
        if (this.matchFinder is null)
        {
            throw new InvalidOperationException();
        }

        inSize = default;
        outSize = default;
        finished = true;

        if (this.inStream is not null)
        {
            this.matchFinder.SetStream(this.inStream);
            this.matchFinder.Init();
            this.needReleaseMFStream = true;
            this.inStream = null;
            if (this.trainSize > 0)
            {
                this.matchFinder.Skip(this.trainSize);
            }
        }

        if (this.finished)
        {
            return;
        }

        this.finished = true;

        var progressPosValuePrev = this.nowPos64;
        if (this.nowPos64 is 0L)
        {
            if (this.matchFinder.GetNumAvailableBytes() is 0U)
            {
                this.Flush((uint)this.nowPos64);
                return;
            }

            this.ReadMatchDistances(out _, out _);
            var posState = (uint)this.nowPos64 & this.posStateMask;
            this.isMatch[(this.state.Index << LzmaBase.NumPosStatesBitsMax) + posState].Encode(this.rangeEncoder, 0);
            this.state.UpdateChar();
            var curByte = this.matchFinder.GetIndexByte((int)(0 - this.additionalOffset));
            this.literalEncoder.GetSubCoder((uint)this.nowPos64, this.previousByte).Encode(this.rangeEncoder, curByte);
            this.previousByte = curByte;
            this.additionalOffset--;
            this.nowPos64++;
        }

        if (this.matchFinder.GetNumAvailableBytes() is 0U)
        {
            this.Flush((uint)this.nowPos64);
            return;
        }

        while (true)
        {
            var len = this.GetOptimum((uint)this.nowPos64, out var pos);

            var posState = ((uint)this.nowPos64) & this.posStateMask;
            var complexState = (this.state.Index << LzmaBase.NumPosStatesBitsMax) + posState;
            if (len is 1 && pos is uint.MaxValue)
            {
                this.isMatch[complexState].Encode(this.rangeEncoder, 0);
                var curByte = this.matchFinder.GetIndexByte((int)(0 - this.additionalOffset));
                var subCoder = this.literalEncoder.GetSubCoder((uint)this.nowPos64, this.previousByte);
                if (!this.state.IsCharState())
                {
                    var matchByte = this.matchFinder.GetIndexByte((int)(0 - this.repDistances[0] - 1 - this.additionalOffset));
                    subCoder.EncodeMatched(this.rangeEncoder, matchByte, curByte);
                }
                else
                {
                    subCoder.Encode(this.rangeEncoder, curByte);
                }

                this.previousByte = curByte;
                this.state.UpdateChar();
            }
            else
            {
                this.isMatch[complexState].Encode(this.rangeEncoder, 1);
                if (pos < LzmaBase.NumRepDistances)
                {
                    this.isRep[this.state.Index].Encode(this.rangeEncoder, 1);
                    if (pos is 0U)
                    {
                        this.isRepG0[this.state.Index].Encode(this.rangeEncoder, 0);
                        if (len is 1U)
                        {
                            this.isRep0Long[complexState].Encode(this.rangeEncoder, 0);
                        }
                        else
                        {
                            this.isRep0Long[complexState].Encode(this.rangeEncoder, 1);
                        }
                    }
                    else
                    {
                        this.isRepG0[this.state.Index].Encode(this.rangeEncoder, 1);
                        if (pos is 1U)
                        {
                            this.isRepG1[this.state.Index].Encode(this.rangeEncoder, 0);
                        }
                        else
                        {
                            this.isRepG1[this.state.Index].Encode(this.rangeEncoder, 1);
                            this.isRepG2[this.state.Index].Encode(this.rangeEncoder, pos - 2);
                        }
                    }

                    if (len is 1U)
                    {
                        this.state.UpdateShortRep();
                    }
                    else
                    {
                        this.repMatchLenEncoder.Encode(this.rangeEncoder, len - LzmaBase.MatchMinLen, posState);
                        this.state.UpdateRep();
                    }

                    var distance = this.repDistances[pos];
                    if (pos is not 0U)
                    {
                        for (var i = pos; i >= 1; i--)
                        {
                            this.repDistances[i] = this.repDistances[i - 1];
                        }

                        this.repDistances[0] = distance;
                    }
                }
                else
                {
                    this.isRep[this.state.Index].Encode(this.rangeEncoder, 0);
                    this.state.UpdateMatch();
                    this.lenEncoder.Encode(this.rangeEncoder, len - LzmaBase.MatchMinLen, posState);
                    pos -= LzmaBase.NumRepDistances;
                    var posSlot = GetPosSlot(pos);
                    var lenToPosState = LzmaBase.GetLenToPosState(len);
                    this.posSlotEncoder[lenToPosState].Encode(this.rangeEncoder, posSlot);

                    if (posSlot >= LzmaBase.StartPosModelIndex)
                    {
                        var footerBits = (int)((posSlot >> 1) - 1);
                        var baseVal = (2 | (posSlot & 1)) << footerBits;
                        var posReduced = pos - baseVal;

                        if (posSlot < LzmaBase.EndPosModelIndex)
                        {
                            RangeCoder.BitTreeEncoder.ReverseEncode(
                                this.posEncoders,
                                baseVal - posSlot - 1,
                                this.rangeEncoder,
                                footerBits,
                                posReduced);
                        }
                        else
                        {
                            this.rangeEncoder.EncodeDirectBits(posReduced >> LzmaBase.NumAlignBits, footerBits - LzmaBase.NumAlignBits);
                            this.posAlignEncoder.ReverseEncode(this.rangeEncoder, posReduced & LzmaBase.AlignMask);
                            this.alignPriceCount++;
                        }
                    }

                    var distance = pos;
                    for (var i = LzmaBase.NumRepDistances - 1; i >= 1; i--)
                    {
                        this.repDistances[i] = this.repDistances[i - 1];
                    }

                    this.repDistances[0] = distance;
                    this.matchPriceCount++;
                }

                this.previousByte = this.matchFinder.GetIndexByte((int)(len - 1 - this.additionalOffset));
            }

            this.additionalOffset -= len;
            this.nowPos64 += len;
            if (this.additionalOffset is 0U)
            {
                // if (!_fastMode)
                if (this.matchPriceCount >= (1U << 7))
                {
                    this.FillDistancesPrices();
                }

                if (this.alignPriceCount >= LzmaBase.AlignTableSize)
                {
                    this.FillAlignPrices();
                }

                inSize = this.nowPos64;
                outSize = this.rangeEncoder.GetProcessedSizeAdd();
                if (this.matchFinder.GetNumAvailableBytes() is 0U)
                {
                    this.Flush((uint)this.nowPos64);
                    return;
                }

                if (this.nowPos64 - progressPosValuePrev >= (1 << 12))
                {
                    this.finished = false;
                    finished = false;
                    return;
                }
            }
        }
    }

    /// <inheritdoc/>
    public void SetCoderProperties(CoderPropId[] propIDs, object[] properties)
    {
        for (var i = 0; i < properties.Length; i++)
        {
            var prop = properties[i];
            switch (propIDs[i])
            {
                case CoderPropId.NumFastBytes:
                    if (prop is not int)
                    {
                        throw new InvalidDataException();
                    }

                    var propertyNumFastBytes = (int)prop;
                    if (propertyNumFastBytes < 5 || propertyNumFastBytes > LzmaBase.MatchMaxLen)
                    {
                        throw new InvalidDataException();
                    }

                    this.numFastBytes = (uint)propertyNumFastBytes;
                    break;

                case CoderPropId.Algorithm:
                    break;
                case CoderPropId.MatchFinder:
                    if (prop is not string stringProp)
                    {
                        throw new InvalidDataException();
                    }

                    var matchFinderIndexPrev = this.matchFinderType;
                    var m = FindMatchFinder(stringProp.ToUpperInvariant());
                    if (m < 0)
                    {
                        throw new InvalidDataException();
                    }

                    this.matchFinderType = (EMatchFinderType)m;
                    if (this.matchFinder != null && matchFinderIndexPrev != this.matchFinderType)
                    {
                        this.dictionarySizePrev = uint.MaxValue;
                        this.matchFinder = null;
                    }

                    break;

                case CoderPropId.DictionarySize:
                    const int DicLogSizeMaxCompress = 30;
                    if (prop is not int dictionarySizeProp)
                    {
                        throw new InvalidDataException();
                    }

                    if (dictionarySizeProp < 1U << LzmaBase.DicLogSizeMin ||
                        dictionarySizeProp > 1U << DicLogSizeMaxCompress)
                    {
                        throw new InvalidDataException();
                    }

                    this.dictionarySize = (uint)dictionarySizeProp;
                    int dicLogSize;
                    for (dicLogSize = 0; dicLogSize < DicLogSizeMaxCompress; dicLogSize++)
                    {
                        if (dictionarySizeProp <= (1U << dicLogSize))
                        {
                            break;
                        }
                    }

                    this.distTableSize = (uint)dicLogSize * 2;
                    break;

                case CoderPropId.PosStateBits:
                    if (prop is not int posStateBitsProp)
                    {
                        throw new InvalidDataException();
                    }

                    if (posStateBitsProp < 0 || posStateBitsProp > (uint)LzmaBase.NumPosStatesBitsEncodingMax)
                    {
                        throw new InvalidDataException();
                    }

                    this.posStateBits = posStateBitsProp;
                    this.posStateMask = (1U << this.posStateBits) - 1;
                    break;

                case CoderPropId.LitPosBits:
                    if (prop is not int numLiteralPosStateBitsProp)
                    {
                        throw new InvalidDataException();
                    }

                    if (numLiteralPosStateBitsProp < 0 || numLiteralPosStateBitsProp > LzmaBase.NumLitPosStatesBitsEncodingMax)
                    {
                        throw new InvalidDataException();
                    }

                    this.numLiteralPosStateBits = numLiteralPosStateBitsProp;
                    break;

                case CoderPropId.LitContextBits:
                    if (prop is not int numLiteralContextBitsProp)
                    {
                        throw new InvalidDataException();
                    }

                    if (numLiteralContextBitsProp < 0 || numLiteralContextBitsProp > LzmaBase.NumLitContextBitsMax)
                    {
                        throw new InvalidDataException();
                    }

                    this.numLiteralContextBits = numLiteralContextBitsProp;
                    break;

                case CoderPropId.EndMarker:
                    if (prop is not bool boolProp)
                    {
                        throw new InvalidDataException();
                    }

                    this.SetWriteEndMarkerMode(boolProp);
                    break;

                default:
                    throw new InvalidDataException();
            }
        }

        static int FindMatchFinder(string s)
        {
            for (var m = 0; m < MatchFinderIDs.Length; m++)
            {
                if (string.Equals(s, MatchFinderIDs[m], StringComparison.Ordinal))
                {
                    return m;
                }
            }

            return -1;
        }
    }

    /// <inheritdoc/>
    public void WriteCoderProperties(Stream outStream)
    {
        this.properties[0] = (byte)((((this.posStateBits * 5) + this.numLiteralPosStateBits) * 9) + this.numLiteralContextBits);
        for (var i = 0; i < 4; i++)
        {
            this.properties[1 + i] = (byte)((this.dictionarySize >> (8 * i)) & byte.MaxValue);
        }

        outStream.Write(this.properties, 0, PropSize);
    }

    /// <summary>
    /// Sets the traing size.
    /// </summary>
    /// <param name="trainSize">The train size to set.</param>
    public void SetTrainSize(uint trainSize) => this.trainSize = trainSize;

    private static uint GetPosSlot(uint pos) => pos switch
    {
        < 1 << 11 => FastPos[pos],
        < 1 << 21 => FastPos[pos >> 10] + 20U,
        _ => FastPos[pos >> 20] + 40U,
    };

    private void BaseInit()
    {
        this.state.Init();
        this.previousByte = 0;
        for (var i = 0U; i < LzmaBase.NumRepDistances; i++)
        {
            this.repDistances[i] = 0;
        }
    }

    private void Create()
    {
        if (this.matchFinder is null)
        {
            var numHashBytes = this.matchFinderType switch
            {
                EMatchFinderType.BT2 => 2,
                _ => 4,
            };

            this.matchFinder = new LZ.BinTree(numHashBytes);
        }

        this.literalEncoder.Create(this.numLiteralPosStateBits, this.numLiteralContextBits);

        if (this.dictionarySize == this.dictionarySizePrev && this.numFastBytesPrev == this.numFastBytes)
        {
            return;
        }

        this.matchFinder.Create(this.dictionarySize, NumOpts, this.numFastBytes, LzmaBase.MatchMaxLen + 1);
        this.dictionarySizePrev = this.dictionarySize;
        this.numFastBytesPrev = this.numFastBytes;
    }

    private void SetWriteEndMarkerMode(bool writeEndMarker) => this.writeEndMark = writeEndMarker;

    private void Init()
    {
        this.BaseInit();
        this.rangeEncoder.Init();

        for (var i = 0U; i < LzmaBase.NumStates; i++)
        {
            for (var j = 0U; j <= this.posStateMask; j++)
            {
                var complexState = (i << LzmaBase.NumPosStatesBitsMax) + j;
                this.isMatch[complexState].Init();
                this.isRep0Long[complexState].Init();
            }

            this.isRep[i].Init();
            this.isRepG0[i].Init();
            this.isRepG1[i].Init();
            this.isRepG2[i].Init();
        }

        this.literalEncoder.Init();
        for (var i = 0U; i < LzmaBase.NumLenToPosStates; i++)
        {
            this.posSlotEncoder[i].Init();
        }

        for (var i = 0U; i < LzmaBase.NumFullDistances - LzmaBase.EndPosModelIndex; i++)
        {
            this.posEncoders[i].Init();
        }

        this.lenEncoder.Init(1U << this.posStateBits);
        this.repMatchLenEncoder.Init(1U << this.posStateBits);

        this.posAlignEncoder.Init();

        this.longestMatchWasFound = false;
        this.optimumEndIndex = 0;
        this.optimumCurrentIndex = 0;
        this.additionalOffset = 0;
    }

    private void ReadMatchDistances(out uint lenRes, out uint numDistancePairs)
    {
        if (this.matchFinder is null)
        {
            throw new InvalidOperationException();
        }

        lenRes = 0;
        numDistancePairs = this.matchFinder.GetMatches(this.matchDistances);
        if (numDistancePairs > 1)
        {
            lenRes = this.matchDistances[numDistancePairs - 2];
            if (lenRes == this.numFastBytes)
            {
                lenRes += this.matchFinder.GetMatchLen(
                    (int)lenRes - 1,
                    this.matchDistances[numDistancePairs - 1],
                    LzmaBase.MatchMaxLen - lenRes);
            }
        }

        this.additionalOffset++;
    }

    private void MovePos(uint num)
    {
        if (num > 0 && this.matchFinder is not null)
        {
            this.matchFinder.Skip(num);
            this.additionalOffset += num;
        }
    }

    private uint GetRepLen1Price(LzmaBase.State state, uint posState) => this.isRepG0[state.Index].GetPrice0() + this.isRep0Long[(state.Index << LzmaBase.NumPosStatesBitsMax) + posState].GetPrice0();

    private uint GetPureRepPrice(uint repIndex, LzmaBase.State state, uint posState)
    {
        uint price;
        if (repIndex is 0U)
        {
            price = this.isRepG0[state.Index].GetPrice0();
            price += this.isRep0Long[(state.Index << LzmaBase.NumPosStatesBitsMax) + posState].GetPrice1();
        }
        else
        {
            price = this.isRepG0[state.Index].GetPrice1();
            if (repIndex is 1U)
            {
                price += this.isRepG1[state.Index].GetPrice0();
            }
            else
            {
                price += this.isRepG1[state.Index].GetPrice1();
                price += this.isRepG2[state.Index].GetPrice(repIndex - 2);
            }
        }

        return price;
    }

    private uint GetRepPrice(uint repIndex, uint len, LzmaBase.State state, uint posState)
    {
        var price = this.repMatchLenEncoder.GetPrice(len - LzmaBase.MatchMinLen, posState);
        return price + this.GetPureRepPrice(repIndex, state, posState);
    }

    private uint GetPosLenPrice(uint pos, uint len, uint posState)
    {
        uint price;
        var lenToPosState = LzmaBase.GetLenToPosState(len);
        if (pos < LzmaBase.NumFullDistances)
        {
            price = this.distancesPrices[(lenToPosState * LzmaBase.NumFullDistances) + pos];
        }
        else
        {
            price = this.posSlotPrices[(lenToPosState << LzmaBase.NumPosSlotBits) + GetPosSlot2(pos)] +
                this.alignPrices[pos & LzmaBase.AlignMask];
        }

        return price + this.lenEncoder.GetPrice(len - LzmaBase.MatchMinLen, posState);

        static uint GetPosSlot2(uint pos)
        {
            return pos switch
            {
                < 1 << 17 => FastPos[pos >> 6] + 12U,
                < 1 << 27 => FastPos[pos >> 16] + 32U,
                _ => FastPos[pos >> 26] + 52U,
            };
        }
    }

    private uint Backward(out uint backRes, uint cur)
    {
        this.optimumEndIndex = cur;
        var posMem = this.optimum[cur].PosPrev;
        var backMem = this.optimum[cur].BackPrev;
        do
        {
            if (this.optimum[cur].Prev1IsChar)
            {
                this.optimum[posMem].MakeAsChar();
                this.optimum[posMem].PosPrev = posMem - 1;
                if (this.optimum[cur].Prev2)
                {
                    this.optimum[posMem - 1].Prev1IsChar = false;
                    this.optimum[posMem - 1].PosPrev = this.optimum[cur].PosPrev2;
                    this.optimum[posMem - 1].BackPrev = this.optimum[cur].BackPrev2;
                }
            }

            var posPrev = posMem;
            var backCur = backMem;

            backMem = this.optimum[posPrev].BackPrev;
            posMem = this.optimum[posPrev].PosPrev;

            this.optimum[posPrev].BackPrev = backCur;
            this.optimum[posPrev].PosPrev = cur;
            cur = posPrev;
        }
        while (cur > 0);
        backRes = this.optimum[0].BackPrev;
        this.optimumCurrentIndex = this.optimum[0].PosPrev;
        return this.optimumCurrentIndex;
    }

    private uint GetOptimum(uint position, out uint backRes)
    {
        if (this.optimumEndIndex != this.optimumCurrentIndex)
        {
            var lenRes = this.optimum[this.optimumCurrentIndex].PosPrev - this.optimumCurrentIndex;
            backRes = this.optimum[this.optimumCurrentIndex].BackPrev;
            this.optimumCurrentIndex = this.optimum[this.optimumCurrentIndex].PosPrev;
            return lenRes;
        }

        this.optimumCurrentIndex = this.optimumEndIndex = 0;

        uint lenMain;
        uint currentNumDistancePairs;
        if (!this.longestMatchWasFound)
        {
            this.ReadMatchDistances(out lenMain, out currentNumDistancePairs);
        }
        else
        {
            lenMain = this.longestMatchLength;
            currentNumDistancePairs = this.numDistancePairs;
            this.longestMatchWasFound = false;
        }

        if (this.matchFinder is null)
        {
            throw new InvalidOperationException();
        }

        var numAvailableBytes = this.matchFinder.GetNumAvailableBytes() + 1;
        if (numAvailableBytes < 2)
        {
            backRes = uint.MaxValue;
            return 1;
        }

        var repMaxIndex = 0U;
        for (var i = 0U; i < LzmaBase.NumRepDistances; i++)
        {
            this.reps[i] = this.repDistances[i];
            this.repLens[i] = this.matchFinder.GetMatchLen(0 - 1, this.reps[i], LzmaBase.MatchMaxLen);
            if (this.repLens[i] > this.repLens[repMaxIndex])
            {
                repMaxIndex = i;
            }
        }

        if (this.repLens[repMaxIndex] >= this.numFastBytes)
        {
            backRes = repMaxIndex;
            var lenRes = this.repLens[repMaxIndex];
            this.MovePos(lenRes - 1);
            return lenRes;
        }

        if (lenMain >= this.numFastBytes)
        {
            backRes = this.matchDistances[currentNumDistancePairs - 1] + LzmaBase.NumRepDistances;
            this.MovePos(lenMain - 1);
            return lenMain;
        }

        var currentByte = this.matchFinder.GetIndexByte(0 - 1);
        var matchByte = this.matchFinder.GetIndexByte((int)(0 - this.repDistances[0] - 1 - 1));

        if (lenMain < 2U && currentByte != matchByte && this.repLens[repMaxIndex] < 2U)
        {
            backRes = uint.MaxValue;
            return 1U;
        }

        this.optimum[0].State = this.state;

        var posState = position & this.posStateMask;

        this.optimum[1].Price = this.isMatch[(this.state.Index << LzmaBase.NumPosStatesBitsMax)
            + posState].GetPrice0()
            + this.literalEncoder.GetSubCoder(position, this.previousByte).GetPrice(!this.state.IsCharState(), matchByte, currentByte);
        this.optimum[1].MakeAsChar();

        var matchPrice = this.isMatch[(this.state.Index << LzmaBase.NumPosStatesBitsMax) + posState].GetPrice1();
        var repMatchPrice = matchPrice + this.isRep[this.state.Index].GetPrice1();

        if (matchByte == currentByte)
        {
            var shortRepPrice = repMatchPrice + this.GetRepLen1Price(this.state, posState);
            if (shortRepPrice < this.optimum[1].Price)
            {
                this.optimum[1].Price = shortRepPrice;
                this.optimum[1].MakeAsShortRep();
            }
        }

        var lenEnd = (lenMain >= this.repLens[repMaxIndex]) ? lenMain : this.repLens[repMaxIndex];

        if (lenEnd < 2)
        {
            backRes = this.optimum[1].BackPrev;
            return 1;
        }

        this.optimum[1].PosPrev = 0;

        this.optimum[0].Backs0 = this.reps[0];
        this.optimum[0].Backs1 = this.reps[1];
        this.optimum[0].Backs2 = this.reps[2];
        this.optimum[0].Backs3 = this.reps[3];

        var len = lenEnd;
        do
        {
            this.optimum[len--].Price = IfinityPrice;
        }
        while (len >= 2);

        for (var i = 0U; i < LzmaBase.NumRepDistances; i++)
        {
            var repLen = this.repLens[i];
            if (repLen < 2)
            {
                continue;
            }

            var price = repMatchPrice + this.GetPureRepPrice(i, this.state, posState);
            do
            {
                var curAndLenPrice = price + this.repMatchLenEncoder.GetPrice(repLen - 2, posState);
                var currentOptimum = this.optimum[repLen];
                if (curAndLenPrice < currentOptimum.Price)
                {
                    currentOptimum.Price = curAndLenPrice;
                    currentOptimum.PosPrev = 0;
                    currentOptimum.BackPrev = i;
                    currentOptimum.Prev1IsChar = false;
                }
            }
            while (--repLen >= 2);
        }

        var normalMatchPrice = matchPrice + this.isRep[this.state.Index].GetPrice0();

        len = (this.repLens[0] >= 2) ? this.repLens[0] + 1 : 2;
        if (len <= lenMain)
        {
            var offs = 0U;
            while (len > this.matchDistances[offs])
            {
                offs += 2;
            }

#pragma warning disable S1994 // "for" loop increment clauses should modify the loops' counters
            for (; ; len++)
            {
                var distance = this.matchDistances[offs + 1];
                var curAndLenPrice = normalMatchPrice + this.GetPosLenPrice(distance, len, posState);
                var currentOptimum = this.optimum[len];
                if (curAndLenPrice < currentOptimum.Price)
                {
                    currentOptimum.Price = curAndLenPrice;
                    currentOptimum.PosPrev = 0;
                    currentOptimum.BackPrev = distance + LzmaBase.NumRepDistances;
                    currentOptimum.Prev1IsChar = false;
                }

                if (len == this.matchDistances[offs])
                {
                    offs += 2;
                    if (offs == currentNumDistancePairs)
                    {
                        break;
                    }
                }
            }
#pragma warning restore S1994 // "for" loop increment clauses should modify the loops' counters
        }

        var cur = 0U;

        while (true)
        {
            cur++;
            if (cur == lenEnd)
            {
                return this.Backward(out backRes, cur);
            }

            this.ReadMatchDistances(out var newLen, out currentNumDistancePairs);
            if (newLen >= this.numFastBytes)
            {
                this.numDistancePairs = currentNumDistancePairs;
                this.longestMatchLength = newLen;
                this.longestMatchWasFound = true;
                return this.Backward(out backRes, cur);
            }

            position++;
            var posPrev = this.optimum[cur].PosPrev;
            LzmaBase.State optimumState;
            if (this.optimum[cur].Prev1IsChar)
            {
                posPrev--;
                if (this.optimum[cur].Prev2)
                {
                    optimumState = this.optimum[this.optimum[cur].PosPrev2].State;
                    if (this.optimum[cur].BackPrev2 < LzmaBase.NumRepDistances)
                    {
                        optimumState.UpdateRep();
                    }
                    else
                    {
                        optimumState.UpdateMatch();
                    }
                }
                else
                {
                    optimumState = this.optimum[posPrev].State;
                }

                optimumState.UpdateChar();
            }
            else
            {
                optimumState = this.optimum[posPrev].State;
            }

            if (posPrev == cur - 1)
            {
                if (this.optimum[cur].IsShortRep())
                {
                    optimumState.UpdateShortRep();
                }
                else
                {
                    optimumState.UpdateChar();
                }
            }
            else
            {
                uint pos;
                if (this.optimum[cur].Prev1IsChar && this.optimum[cur].Prev2)
                {
                    posPrev = this.optimum[cur].PosPrev2;
                    pos = this.optimum[cur].BackPrev2;
                    optimumState.UpdateRep();
                }
                else
                {
                    pos = this.optimum[cur].BackPrev;
                    if (pos < LzmaBase.NumRepDistances)
                    {
                        optimumState.UpdateRep();
                    }
                    else
                    {
                        optimumState.UpdateMatch();
                    }
                }

                var opt = this.optimum[posPrev];
                if (pos < LzmaBase.NumRepDistances)
                {
                    if (pos is 0U)
                    {
                        this.reps[0] = opt.Backs0;
                        this.reps[1] = opt.Backs1;
                        this.reps[2] = opt.Backs2;
                        this.reps[3] = opt.Backs3;
                    }
                    else if (pos is 1U)
                    {
                        this.reps[0] = opt.Backs1;
                        this.reps[1] = opt.Backs0;
                        this.reps[2] = opt.Backs2;
                        this.reps[3] = opt.Backs3;
                    }
                    else if (pos is 2U)
                    {
                        this.reps[0] = opt.Backs2;
                        this.reps[1] = opt.Backs0;
                        this.reps[2] = opt.Backs1;
                        this.reps[3] = opt.Backs3;
                    }
                    else
                    {
                        this.reps[0] = opt.Backs3;
                        this.reps[1] = opt.Backs0;
                        this.reps[2] = opt.Backs1;
                        this.reps[3] = opt.Backs2;
                    }
                }
                else
                {
                    this.reps[0] = pos - LzmaBase.NumRepDistances;
                    this.reps[1] = opt.Backs0;
                    this.reps[2] = opt.Backs1;
                    this.reps[3] = opt.Backs2;
                }
            }

            this.optimum[cur].State = optimumState;
            this.optimum[cur].Backs0 = this.reps[0];
            this.optimum[cur].Backs1 = this.reps[1];
            this.optimum[cur].Backs2 = this.reps[2];
            this.optimum[cur].Backs3 = this.reps[3];
            var curPrice = this.optimum[cur].Price;

            currentByte = this.matchFinder.GetIndexByte(0 - 1);
            matchByte = this.matchFinder.GetIndexByte((int)(0 - this.reps[0] - 1 - 1));

            posState = position & this.posStateMask;

            var curAnd1Price = curPrice +
                this.isMatch[(optimumState.Index << LzmaBase.NumPosStatesBitsMax) + posState].GetPrice0() +
                this.literalEncoder.GetSubCoder(position, this.matchFinder.GetIndexByte(0 - 2)).
                GetPrice(!optimumState.IsCharState(), matchByte, currentByte);

            var nextOptimum = this.optimum[cur + 1];

            var nextIsChar = false;
            if (curAnd1Price < nextOptimum.Price)
            {
                nextOptimum.Price = curAnd1Price;
                nextOptimum.PosPrev = cur;
                nextOptimum.MakeAsChar();
                nextIsChar = true;
            }

            matchPrice = curPrice + this.isMatch[(optimumState.Index << LzmaBase.NumPosStatesBitsMax) + posState].GetPrice1();
            repMatchPrice = matchPrice + this.isRep[optimumState.Index].GetPrice1();

            if (matchByte == currentByte &&
                !(nextOptimum.PosPrev < cur && nextOptimum.BackPrev is 0U))
            {
                var shortRepPrice = repMatchPrice + this.GetRepLen1Price(optimumState, posState);
                if (shortRepPrice <= nextOptimum.Price)
                {
                    nextOptimum.Price = shortRepPrice;
                    nextOptimum.PosPrev = cur;
                    nextOptimum.MakeAsShortRep();
                    nextIsChar = true;
                }
            }

            var numAvailableBytesFull = this.matchFinder.GetNumAvailableBytes() + 1;
            numAvailableBytesFull = Math.Min(NumOpts - 1 - cur, numAvailableBytesFull);
            numAvailableBytes = numAvailableBytesFull;

            if (numAvailableBytes < 2)
            {
                continue;
            }

            if (numAvailableBytes > this.numFastBytes)
            {
                numAvailableBytes = this.numFastBytes;
            }

            if (!nextIsChar && matchByte != currentByte)
            {
                // try Literal + rep0
                var t = Math.Min(numAvailableBytesFull - 1, this.numFastBytes);
                var lenTest2 = this.matchFinder.GetMatchLen(0, this.reps[0], t);
                if (lenTest2 >= 2)
                {
                    var state2 = optimumState;
                    state2.UpdateChar();
                    var posStateNext = (position + 1) & this.posStateMask;
                    var nextRepMatchPrice = curAnd1Price +
                        this.isMatch[(state2.Index << LzmaBase.NumPosStatesBitsMax) + posStateNext].GetPrice1() +
                        this.isRep[state2.Index].GetPrice1();

                    var offset = cur + 1 + lenTest2;
                    while (lenEnd < offset)
                    {
                        this.optimum[++lenEnd].Price = IfinityPrice;
                    }

                    var curAndLenPrice = nextRepMatchPrice + this.GetRepPrice(0, lenTest2, state2, posStateNext);
                    var currentOptimum = this.optimum[offset];
                    if (curAndLenPrice < currentOptimum.Price)
                    {
                        currentOptimum.Price = curAndLenPrice;
                        currentOptimum.PosPrev = cur + 1;
                        currentOptimum.BackPrev = 0;
                        currentOptimum.Prev1IsChar = true;
                        currentOptimum.Prev2 = false;
                    }
                }
            }

            // speed optimization
            var startLen = 2U;

            for (var repIndex = 0U; repIndex < LzmaBase.NumRepDistances; repIndex++)
            {
                var lenTest = this.matchFinder.GetMatchLen(0 - 1, this.reps[repIndex], numAvailableBytes);
                if (lenTest < 2U)
                {
                    continue;
                }

                var lenTestTemp = lenTest;
                do
                {
                    while (lenEnd < cur + lenTest)
                    {
                        this.optimum[++lenEnd].Price = IfinityPrice;
                    }

                    var curAndLenPrice = repMatchPrice + this.GetRepPrice(repIndex, lenTest, optimumState, posState);
                    var currentOptimum = this.optimum[cur + lenTest];
                    if (curAndLenPrice < currentOptimum.Price)
                    {
                        currentOptimum.Price = curAndLenPrice;
                        currentOptimum.PosPrev = cur;
                        currentOptimum.BackPrev = repIndex;
                        currentOptimum.Prev1IsChar = false;
                    }
                }
                while (--lenTest >= 2);
                lenTest = lenTestTemp;

                if (repIndex is 0U)
                {
                    startLen = lenTest + 1;
                }

                // if (_maxMode)
                if (lenTest < numAvailableBytesFull)
                {
                    var t = Math.Min(numAvailableBytesFull - 1 - lenTest, this.numFastBytes);
                    var lenTest2 = this.matchFinder.GetMatchLen((int)lenTest, this.reps[repIndex], t);
                    if (lenTest2 >= 2)
                    {
                        var state2 = optimumState;
                        state2.UpdateRep();
                        var posStateNext = (position + lenTest) & this.posStateMask;
                        var curAndLenCharPrice = repMatchPrice
                            + this.GetRepPrice(repIndex, lenTest, optimumState, posState)
                            + this.isMatch[(state2.Index << LzmaBase.NumPosStatesBitsMax) + posStateNext].GetPrice0()
                            + this.literalEncoder.GetSubCoder(position + lenTest, this.matchFinder.GetIndexByte((int)lenTest - 1 - 1))
                                .GetPrice(
                                    matchMode: true,
                                    this.matchFinder.GetIndexByte((int)lenTest - 1 - (int)(this.reps[repIndex] + 1)),
                                    this.matchFinder.GetIndexByte((int)lenTest - 1));
                        state2.UpdateChar();
                        posStateNext = (position + lenTest + 1) & this.posStateMask;
                        var nextMatchPrice = curAndLenCharPrice + this.isMatch[(state2.Index << LzmaBase.NumPosStatesBitsMax) + posStateNext].GetPrice1();
                        var nextRepMatchPrice = nextMatchPrice + this.isRep[state2.Index].GetPrice1();

                        var offset = lenTest + 1 + lenTest2;
                        while (lenEnd < cur + offset)
                        {
                            this.optimum[++lenEnd].Price = IfinityPrice;
                        }

                        var curAndLenPrice = nextRepMatchPrice + this.GetRepPrice(0, lenTest2, state2, posStateNext);
                        var currentOptimum = this.optimum[cur + offset];
                        if (curAndLenPrice < currentOptimum.Price)
                        {
                            currentOptimum.Price = curAndLenPrice;
                            currentOptimum.PosPrev = cur + lenTest + 1;
                            currentOptimum.BackPrev = 0;
                            currentOptimum.Prev1IsChar = true;
                            currentOptimum.Prev2 = true;
                            currentOptimum.PosPrev2 = cur;
                            currentOptimum.BackPrev2 = repIndex;
                        }
                    }
                }
            }

            if (newLen > numAvailableBytes)
            {
                newLen = numAvailableBytes;
                for (currentNumDistancePairs = 0; newLen > this.matchDistances[currentNumDistancePairs]; currentNumDistancePairs += 2)
                {
                    // this goes through the distances.
                }

                this.matchDistances[currentNumDistancePairs] = newLen;
                currentNumDistancePairs += 2;
            }

            if (newLen >= startLen)
            {
                normalMatchPrice = matchPrice + this.isRep[optimumState.Index].GetPrice0();
                while (lenEnd < cur + newLen)
                {
                    this.optimum[++lenEnd].Price = IfinityPrice;
                }

                var offs = 0U;
                while (startLen > this.matchDistances[offs])
                {
                    offs += 2;
                }

#pragma warning disable S1994 // "for" loop increment clauses should modify the loops' counters
                for (var lenTest = startLen; ; lenTest++)
                {
                    var curBack = this.matchDistances[offs + 1];
                    var curAndLenPrice = normalMatchPrice + this.GetPosLenPrice(curBack, lenTest, posState);
                    var currentOptimum = this.optimum[cur + lenTest];
                    if (curAndLenPrice < currentOptimum.Price)
                    {
                        currentOptimum.Price = curAndLenPrice;
                        currentOptimum.PosPrev = cur;
                        currentOptimum.BackPrev = curBack + LzmaBase.NumRepDistances;
                        currentOptimum.Prev1IsChar = false;
                    }

                    if (lenTest == this.matchDistances[offs])
                    {
                        if (lenTest < numAvailableBytesFull)
                        {
                            var t = Math.Min(numAvailableBytesFull - 1 - lenTest, this.numFastBytes);
                            var lenTest2 = this.matchFinder.GetMatchLen((int)lenTest, curBack, t);
                            if (lenTest2 >= 2)
                            {
                                var state2 = optimumState;
                                state2.UpdateMatch();
                                var posStateNext = (position + lenTest) & this.posStateMask;
                                var curAndLenCharPrice = curAndLenPrice
                                    + this.isMatch[(state2.Index << LzmaBase.NumPosStatesBitsMax)
                                    + posStateNext].GetPrice0()
                                    + this.literalEncoder.GetSubCoder(position + lenTest, this.matchFinder.GetIndexByte((int)lenTest - 1 - 1))
                                        .GetPrice(
                                            matchMode: true,
                                            this.matchFinder.GetIndexByte((int)lenTest - (int)(curBack + 1) - 1),
                                            this.matchFinder.GetIndexByte((int)lenTest - 1));
                                state2.UpdateChar();
                                posStateNext = (position + lenTest + 1) & this.posStateMask;
                                var nextMatchPrice = curAndLenCharPrice + this.isMatch[(state2.Index << LzmaBase.NumPosStatesBitsMax) + posStateNext].GetPrice1();
                                var nextRepMatchPrice = nextMatchPrice + this.isRep[state2.Index].GetPrice1();

                                var offset = lenTest + 1 + lenTest2;
                                while (lenEnd < cur + offset)
                                {
                                    this.optimum[++lenEnd].Price = IfinityPrice;
                                }

                                curAndLenPrice = nextRepMatchPrice + this.GetRepPrice(0, lenTest2, state2, posStateNext);
                                currentOptimum = this.optimum[cur + offset];
                                if (curAndLenPrice < currentOptimum.Price)
                                {
                                    currentOptimum.Price = curAndLenPrice;
                                    currentOptimum.PosPrev = cur + lenTest + 1;
                                    currentOptimum.BackPrev = 0;
                                    currentOptimum.Prev1IsChar = true;
                                    currentOptimum.Prev2 = true;
                                    currentOptimum.PosPrev2 = cur;
                                    currentOptimum.BackPrev2 = curBack + LzmaBase.NumRepDistances;
                                }
                            }
                        }

                        offs += 2;
                        if (offs == currentNumDistancePairs)
                        {
                            break;
                        }
                    }
                }
#pragma warning restore S1994 // "for" loop increment clauses should modify the loops' counters
            }
        }
    }

    private void Flush(uint nowPos)
    {
        this.ReleaseMFStream();
        WriteEndMarker(nowPos & this.posStateMask);
        this.rangeEncoder.FlushData();
        this.rangeEncoder.FlushStream();

        void WriteEndMarker(uint posState)
        {
            if (!this.writeEndMark)
            {
                return;
            }

            this.isMatch[(this.state.Index << LzmaBase.NumPosStatesBitsMax) + posState].Encode(this.rangeEncoder, 1);
            this.isRep[this.state.Index].Encode(this.rangeEncoder, 0);
            this.state.UpdateMatch();
            const uint len = LzmaBase.MatchMinLen;
            this.lenEncoder.Encode(this.rangeEncoder, len - LzmaBase.MatchMinLen, posState);
            const uint posSlot = (1U << LzmaBase.NumPosSlotBits) - 1U;
            var lenToPosState = LzmaBase.GetLenToPosState(len);
            this.posSlotEncoder[lenToPosState].Encode(this.rangeEncoder, posSlot);
            const int footerBits = 30;
            const uint posReduced = (1U << footerBits) - 1U;
            this.rangeEncoder.EncodeDirectBits(posReduced >> LzmaBase.NumAlignBits, footerBits - LzmaBase.NumAlignBits);
            this.posAlignEncoder.ReverseEncode(this.rangeEncoder, posReduced & LzmaBase.AlignMask);
        }
    }

    private void ReleaseMFStream()
    {
        if (this.matchFinder is not null && this.needReleaseMFStream)
        {
            this.matchFinder.ReleaseStream();
            this.needReleaseMFStream = false;
        }
    }

    private void FillDistancesPrices()
    {
        for (var i = LzmaBase.StartPosModelIndex; i < LzmaBase.NumFullDistances; i++)
        {
            var posSlot = GetPosSlot(i);
            var footerBits = (int)((posSlot >> 1) - 1);
            var baseVal = (2U | (posSlot & 1U)) << footerBits;
            this.tempPrices[i] = RangeCoder.BitTreeEncoder.ReverseGetPrice(this.posEncoders, baseVal - posSlot - 1, footerBits, i - baseVal);
        }

        for (var lenToPosState = 0U; lenToPosState < LzmaBase.NumLenToPosStates; lenToPosState++)
        {
            var encoder = this.posSlotEncoder[lenToPosState];
            var st = lenToPosState << LzmaBase.NumPosSlotBits;

            uint posSlot;
            for (posSlot = 0; posSlot < this.distTableSize; posSlot++)
            {
                this.posSlotPrices[st + posSlot] = encoder.GetPrice(posSlot);
            }

            for (posSlot = LzmaBase.EndPosModelIndex; posSlot < this.distTableSize; posSlot++)
            {
                this.posSlotPrices[st + posSlot] += ((posSlot >> 1) - 1 - LzmaBase.NumAlignBits) << RangeCoder.BitEncoder.NumBitPriceShiftBits;
            }

            var st2 = lenToPosState * LzmaBase.NumFullDistances;
            uint i;
            for (i = 0U; i < LzmaBase.StartPosModelIndex; i++)
            {
                this.distancesPrices[st2 + i] = this.posSlotPrices[st + i];
            }

            for (; i < LzmaBase.NumFullDistances; i++)
            {
                this.distancesPrices[st2 + i] = this.posSlotPrices[st + GetPosSlot(i)] + this.tempPrices[i];
            }
        }

        this.matchPriceCount = 0;
    }

    private void FillAlignPrices()
    {
        for (var i = 0U; i < LzmaBase.AlignTableSize; i++)
        {
            this.alignPrices[i] = this.posAlignEncoder.ReverseGetPrice(i);
        }

        this.alignPriceCount = 0;
    }

    private sealed class LiteralEncoder
    {
        private Encoder2[]? coders;
        private int numPrevBits;
        private int numPosBits;
        private uint posMask;

        public void Create(int numPosBits, int numPrevBits)
        {
            if (this.coders is not null && this.numPrevBits == numPrevBits && this.numPosBits == numPosBits)
            {
                return;
            }

            this.numPosBits = numPosBits;
            this.posMask = (1U << numPosBits) - 1;
            this.numPrevBits = numPrevBits;
            var numStates = 1U << (this.numPrevBits + this.numPosBits);
            this.coders = new Encoder2[numStates];
            for (var i = 0U; i < numStates; i++)
            {
                this.coders[i] = new();
            }
        }

        public void Init()
        {
            if (this.coders is null)
            {
                throw new InvalidOperationException();
            }

            var numStates = 1U << (this.numPrevBits + this.numPosBits);
            for (var i = 0U; i < numStates; i++)
            {
                this.coders[i].Init();
            }
        }

        public Encoder2 GetSubCoder(uint pos, byte prevByte)
        {
            if (this.coders is null)
            {
                throw new InvalidOperationException();
            }

            return this.coders[((pos & this.posMask) << this.numPrevBits) + (uint)(prevByte >> (8 - this.numPrevBits))];
        }

        public readonly struct Encoder2
        {
            private readonly RangeCoder.BitEncoder[] encoders = new RangeCoder.BitEncoder[0x300];

            public Encoder2()
            {
            }

            public readonly void Init()
            {
                for (var i = 0; i < 0x300; i++)
                {
                    this.encoders[i].Init();
                }
            }

            public readonly void Encode(RangeCoder.Encoder rangeEncoder, byte symbol)
            {
                var context = 1U;
                for (var i = 7; i >= 0; i--)
                {
                    var bit = (uint)(symbol >> i) & 1U;
                    this.encoders[context].Encode(rangeEncoder, bit);
                    context = (context << 1) | bit;
                }
            }

            public readonly void EncodeMatched(RangeCoder.Encoder rangeEncoder, byte matchByte, byte symbol)
            {
                var context = 1U;
                var same = true;
                for (var i = 7; i >= 0; i--)
                {
                    var bit = (uint)(symbol >> i) & 1U;
                    var state = context;
                    if (same)
                    {
                        var matchBit = (uint)(matchByte >> i) & 1U;
                        state += (1 + matchBit) << 8;
                        same = matchBit == bit;
                    }

                    this.encoders[state].Encode(rangeEncoder, bit);
                    context = (context << 1) | bit;
                }
            }

            public readonly uint GetPrice(bool matchMode, byte matchByte, byte symbol)
            {
                var price = 0U;
                var context = 1U;
                var i = 7;
                if (matchMode)
                {
                    for (; i >= 0; i--)
                    {
                        var matchBit = (uint)(matchByte >> i) & 1U;
                        var bit = (uint)(symbol >> i) & 1U;
                        price += this.encoders[((1U + matchBit) << 8) + context].GetPrice(bit);
                        context = (context << 1) | bit;
                        if (matchBit != bit)
                        {
                            i--;
                            break;
                        }
                    }
                }

                for (; i >= 0; i--)
                {
                    var bit = (uint)(symbol >> i) & 1;
                    price += this.encoders[context].GetPrice(bit);
                    context = (context << 1) | bit;
                }

                return price;
            }
        }
    }

    private class LenEncoder
    {
        private readonly RangeCoder.BitTreeEncoder[] lowCoder = new RangeCoder.BitTreeEncoder[LzmaBase.NumPosStatesEncodingMax];
        private readonly RangeCoder.BitTreeEncoder[] midCoder = new RangeCoder.BitTreeEncoder[LzmaBase.NumPosStatesEncodingMax];
        private readonly RangeCoder.BitTreeEncoder highCoder = new(LzmaBase.NumHighLenBits);
        private RangeCoder.BitEncoder firstChoice = default;
        private RangeCoder.BitEncoder secondChoice = default;

        public LenEncoder()
        {
            for (var posState = 0U; posState < LzmaBase.NumPosStatesEncodingMax; posState++)
            {
                this.lowCoder[posState] = new RangeCoder.BitTreeEncoder(LzmaBase.NumLowLenBits);
                this.midCoder[posState] = new RangeCoder.BitTreeEncoder(LzmaBase.NumMidLenBits);
            }
        }

        public void Init(uint numPosStates)
        {
            this.firstChoice.Init();
            this.secondChoice.Init();
            for (var posState = 0U; posState < numPosStates; posState++)
            {
                this.lowCoder[posState].Init();
                this.midCoder[posState].Init();
            }

            this.highCoder.Init();
        }

        public void Encode(RangeCoder.Encoder rangeEncoder, uint symbol, uint posState)
        {
            if (symbol < LzmaBase.NumLowLenSymbols)
            {
                this.firstChoice.Encode(rangeEncoder, 0);
                this.lowCoder[posState].Encode(rangeEncoder, symbol);
            }
            else
            {
                symbol -= LzmaBase.NumLowLenSymbols;
                this.firstChoice.Encode(rangeEncoder, 1);
                if (symbol < LzmaBase.NumMidLenSymbols)
                {
                    this.secondChoice.Encode(rangeEncoder, 0);
                    this.midCoder[posState].Encode(rangeEncoder, symbol);
                }
                else
                {
                    this.secondChoice.Encode(rangeEncoder, 1);
                    this.highCoder.Encode(rangeEncoder, symbol - LzmaBase.NumMidLenSymbols);
                }
            }
        }

        public void SetPrices(uint posState, uint numSymbols, uint[] prices, uint st)
        {
            var a0 = this.firstChoice.GetPrice0();
            var a1 = this.firstChoice.GetPrice1();
            var b0 = a1 + this.secondChoice.GetPrice0();
            var b1 = a1 + this.secondChoice.GetPrice1();
            uint i;
            for (i = 0U; i < LzmaBase.NumLowLenSymbols; i++)
            {
                if (i >= numSymbols)
                {
                    return;
                }

                prices[st + i] = a0 + this.lowCoder[posState].GetPrice(i);
            }

            for (; i < LzmaBase.NumLowLenSymbols + LzmaBase.NumMidLenSymbols; i++)
            {
                if (i >= numSymbols)
                {
                    return;
                }

                prices[st + i] = b0 + this.midCoder[posState].GetPrice(i - LzmaBase.NumLowLenSymbols);
            }

            for (; i < numSymbols; i++)
            {
                prices[st + i] = b1 + this.highCoder.GetPrice(i - LzmaBase.NumLowLenSymbols - LzmaBase.NumMidLenSymbols);
            }
        }
    }

    private sealed class LenPriceTableEncoder : LenEncoder
    {
        private readonly uint[] prices = new uint[LzmaBase.NumLenSymbols << LzmaBase.NumPosStatesBitsEncodingMax];
        private readonly uint[] counters = new uint[LzmaBase.NumPosStatesEncodingMax];
        private uint tableSize;

        public LenPriceTableEncoder()
            : base()
        {
        }

        public void SetTableSize(uint tableSize) => this.tableSize = tableSize;

        public uint GetPrice(uint symbol, uint posState) => this.prices[(posState * LzmaBase.NumLenSymbols) + symbol];

        public void UpdateTables(uint numPosStates)
        {
            for (var posState = 0U; posState < numPosStates; posState++)
            {
                this.UpdateTable(posState);
            }
        }

        public new void Encode(RangeCoder.Encoder rangeEncoder, uint symbol, uint posState)
        {
            base.Encode(rangeEncoder, symbol, posState);
            if (--this.counters[posState] is 0U)
            {
                this.UpdateTable(posState);
            }
        }

        private void UpdateTable(uint posState)
        {
            this.SetPrices(posState, this.tableSize, this.prices, posState * LzmaBase.NumLenSymbols);
            this.counters[posState] = this.tableSize;
        }
    }

    private sealed class Optimal
    {
        public LzmaBase.State State { get; set; }

        public bool Prev1IsChar { get; set; }

        public bool Prev2 { get; set; }

        public uint PosPrev2 { get; set; }

        public uint BackPrev2 { get; set; }

        public uint Price { get; set; }

        public uint PosPrev { get; set; }

        public uint BackPrev { get; set; }

        public uint Backs0 { get; set; }

        public uint Backs1 { get; set; }

        public uint Backs2 { get; set; }

        public uint Backs3 { get; set; }

        public void MakeAsChar()
        {
            this.BackPrev = uint.MaxValue;
            this.Prev1IsChar = false;
        }

        public void MakeAsShortRep()
        {
            this.BackPrev = 0;
            this.Prev1IsChar = false;
        }

        public bool IsShortRep() => this.BackPrev is 0U;
    }
}