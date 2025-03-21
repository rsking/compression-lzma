// -----------------------------------------------------------------------
// <copyright file="LzmaDecoder.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.IO.Compression;

using static System.IO.Compression.Constants;

/// <summary>
/// The LZMA decoder.
/// </summary>
internal class LzmaDecoder
{
    private readonly LZ.OutWindow outWindow = new();
    private readonly RangeCoder.Decoder rangeDecoder = new();

    private readonly RangeCoder.BitDecoder[] matchDecoders = new RangeCoder.BitDecoder[NumStates << NumPosStatesBitsMax];
    private readonly RangeCoder.BitDecoder[] repDecoders = new RangeCoder.BitDecoder[NumStates];
    private readonly RangeCoder.BitDecoder[] repG0Decoders = new RangeCoder.BitDecoder[NumStates];
    private readonly RangeCoder.BitDecoder[] repG1Decoders = new RangeCoder.BitDecoder[NumStates];
    private readonly RangeCoder.BitDecoder[] repG2Decoders = new RangeCoder.BitDecoder[NumStates];
    private readonly RangeCoder.BitDecoder[] rep0LongDecoders = new RangeCoder.BitDecoder[NumStates << NumPosStatesBitsMax];

    private readonly RangeCoder.BitTreeDecoder[] posSlotDecoder = new RangeCoder.BitTreeDecoder[NumLenToPosStates];
    private readonly RangeCoder.BitDecoder[] posDecoders = new RangeCoder.BitDecoder[NumFullDistances - EndPosModelIndex];

    private readonly LenDecoder lenDecoder = new();
    private readonly LenDecoder repLenDecoder = new();

    private readonly LiteralDecoder literalDecoder = new();

    private readonly RangeCoder.BitTreeDecoder posAlignDecoder = new(NumAlignBits);

    private uint dictionarySize;
    private uint dictionarySizeCheck;

    private uint posStateMask;

    private bool solid;

    /// <summary>
    /// Initializes a new instance of the <see cref="LzmaDecoder"/> class.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public LzmaDecoder(byte[] properties)
    {
        this.dictionarySize = uint.MaxValue;
        for (var i = 0; i < NumLenToPosStates; i++)
        {
            this.posSlotDecoder[i] = new(NumPosSlotBits);
        }

        SetDecoderProperties(properties);

        void SetDecoderProperties(byte[] properties)
        {
            if (properties.Length < 5)
            {
                throw new ArgumentOutOfRangeException(nameof(properties));
            }

            var lc = properties[0] % 9;
            var remainder = properties[0] / 9;
            var lp = remainder % 5;
            var pb = remainder / 5;
            if (pb > NumPosStatesBitsMax)
            {
                throw new InvalidDataException();
            }

            var currentDictionarySize = 0U;
            for (var i = 0; i < 4; i++)
            {
                currentDictionarySize += ((uint)properties[1 + i]) << (i * 8);
            }

            SetDictionarySize(currentDictionarySize);
            SetLiteralProperties(lp, lc);
            SetPosBitsProperties(pb);

            void SetDictionarySize(uint dictionarySize)
            {
                if (this.dictionarySize != dictionarySize)
                {
                    this.dictionarySize = dictionarySize;
                    this.dictionarySizeCheck = Math.Max(this.dictionarySize, 1);
                    var blockSize = Math.Max(this.dictionarySizeCheck, 1 << 12);
                    this.outWindow.Create(blockSize);
                }
            }

            void SetLiteralProperties(int lp, int lc)
            {
                if (lp > 8)
                {
                    throw new ArgumentOutOfRangeException(nameof(lp));
                }

                if (lc > 8)
                {
                    throw new ArgumentOutOfRangeException(nameof(lc));
                }

                this.literalDecoder.Create(lp, lc);
            }

            void SetPosBitsProperties(int pb)
            {
                if (pb > NumPosStatesBitsMax)
                {
                    throw new InvalidDataException();
                }

                var numPosStates = 1U << pb;
                this.lenDecoder.Create(numPosStates);
                this.repLenDecoder.Create(numPosStates);
                this.posStateMask = numPosStates - 1;
            }
        }
    }

    /// <summary>
    /// Decodes the input stream to the output.
    /// </summary>
    /// <param name="input">The input stream.</param>
    /// <param name="output">The output stream.</param>
    /// <param name="outputSize">The output size.</param>
    public void Decode(Stream input, Stream output, long outputSize = -1)
    {
        this.Init(input, output);

        var state = new State();
        var rep0 = 0U;
        var rep1 = 0U;
        var rep2 = 0U;
        var rep3 = 0U;

        var nowPos64 = 0UL;
        var outSize64 = (ulong)outputSize;
        if (nowPos64 < outSize64)
        {
            if (this.matchDecoders[state.Index << NumPosStatesBitsMax].Decode(this.rangeDecoder) is not 0U)
            {
                throw new InvalidDataException();
            }

            state.UpdateChar();
            var b = this.literalDecoder.DecodeNormal(this.rangeDecoder, 0, 0);
            this.outWindow.PutByte(b);
            nowPos64++;
        }

        while (nowPos64 < outSize64)
        {
            var posState = (uint)nowPos64 & this.posStateMask;
            if (this.matchDecoders[(state.Index << NumPosStatesBitsMax) + posState].Decode(this.rangeDecoder) is 0)
            {
                var prevByte = this.outWindow.GetByte(0);
                var b = state.IsCharState()
                    ? this.literalDecoder.DecodeNormal(this.rangeDecoder, (uint)nowPos64, prevByte)
                    : this.literalDecoder.DecodeWithMatchByte(this.rangeDecoder, (uint)nowPos64, prevByte, this.outWindow.GetByte(rep0));
                this.outWindow.PutByte(b);
                state.UpdateChar();
                nowPos64++;
            }
            else
            {
                uint len;
                if (this.repDecoders[state.Index].Decode(this.rangeDecoder) is 1U)
                {
                    if (this.repG0Decoders[state.Index].Decode(this.rangeDecoder) is 0U)
                    {
                        if (this.rep0LongDecoders[(state.Index << NumPosStatesBitsMax) + posState].Decode(this.rangeDecoder) is 0U)
                        {
                            state.UpdateShortRep();
                            this.outWindow.PutByte(this.outWindow.GetByte(rep0));
                            nowPos64++;
                            continue;
                        }
                    }
                    else
                    {
                        uint distance;
                        if (this.repG1Decoders[state.Index].Decode(this.rangeDecoder) is 0U)
                        {
                            distance = rep1;
                        }
                        else
                        {
                            if (this.repG2Decoders[state.Index].Decode(this.rangeDecoder) is 0U)
                            {
                                distance = rep2;
                            }
                            else
                            {
                                distance = rep3;
                                rep3 = rep2;
                            }

                            rep2 = rep1;
                        }

                        rep1 = rep0;
                        rep0 = distance;
                    }

                    len = this.repLenDecoder.Decode(this.rangeDecoder, posState) + MatchMinLen;
                    state.UpdateRep();
                }
                else
                {
                    rep3 = rep2;
                    rep2 = rep1;
                    rep1 = rep0;
                    len = MatchMinLen + this.lenDecoder.Decode(this.rangeDecoder, posState);
                    state.UpdateMatch();
                    var posSlot = this.posSlotDecoder[GetLenToPosState(len)].Decode(this.rangeDecoder);
                    if (posSlot >= StartPosModelIndex)
                    {
                        var numDirectBits = (int)((posSlot >> 1) - 1);
                        rep0 = (2 | (posSlot & 1)) << numDirectBits;
                        if (posSlot < EndPosModelIndex)
                        {
                            rep0 += RangeCoder.BitTreeDecoder.ReverseDecode(this.posDecoders, rep0 - posSlot - 1, this.rangeDecoder, numDirectBits);
                        }
                        else
                        {
                            rep0 += this.rangeDecoder.DecodeDirectBits(numDirectBits - NumAlignBits) << NumAlignBits;
                            rep0 += this.posAlignDecoder.ReverseDecode(this.rangeDecoder);
                        }
                    }
                    else
                    {
                        rep0 = posSlot;
                    }
                }

                if (rep0 >= this.outWindow.TrainSize + nowPos64 || rep0 >= this.dictionarySizeCheck)
                {
                    if (rep0 is uint.MaxValue)
                    {
                        break;
                    }

                    throw new InvalidDataException();
                }

                this.outWindow.CopyBlock(rep0, len);
                nowPos64 += len;
            }
        }

        this.outWindow.Flush();
        this.outWindow.ReleaseStream();
        this.rangeDecoder.ReleaseStream();
    }

    /// <summary>
    /// Trains this instance with the stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns><see langword="true"/> if the training was successful; otherwise <see langword="false"/>.</returns>
    public bool Train(Stream stream)
    {
        this.solid = true;
        return this.outWindow.Train(stream);
    }

    private void Init(Stream inStream, Stream outStream)
    {
        this.rangeDecoder.Init(inStream);
        this.outWindow.Init(outStream, this.solid);

        for (var i = 0U; i < NumStates; i++)
        {
            for (var j = 0U; j <= this.posStateMask; j++)
            {
                var index = (i << NumPosStatesBitsMax) + j;
                this.matchDecoders[index].Init();
                this.rep0LongDecoders[index].Init();
            }

            this.repDecoders[i].Init();
            this.repG0Decoders[i].Init();
            this.repG1Decoders[i].Init();
            this.repG2Decoders[i].Init();
        }

        this.literalDecoder.Init();
        for (var i = 0U; i < NumLenToPosStates; i++)
        {
            this.posSlotDecoder[i].Init();
        }

        for (var i = 0U; i < NumFullDistances - EndPosModelIndex; i++)
        {
            this.posDecoders[i].Init();
        }

        this.lenDecoder.Init();
        this.repLenDecoder.Init();
        this.posAlignDecoder.Init();
    }

    private sealed class LenDecoder
    {
        private readonly RangeCoder.BitTreeDecoder[] lowCoder = new RangeCoder.BitTreeDecoder[NumPosStatesMax];
        private readonly RangeCoder.BitTreeDecoder[] midCoder = new RangeCoder.BitTreeDecoder[NumPosStatesMax];
        private readonly RangeCoder.BitTreeDecoder highCoder = new(NumHighLenBits);
        private RangeCoder.BitDecoder firstChoice = default;
        private RangeCoder.BitDecoder secondChoice = default;
        private uint numPosStates;

        public void Create(uint numPosStates)
        {
            for (var posState = this.numPosStates; posState < numPosStates; posState++)
            {
                this.lowCoder[posState] = new(NumLowLenBits);
                this.midCoder[posState] = new(NumMidLenBits);
            }

            this.numPosStates = numPosStates;
        }

        public void Init()
        {
            this.firstChoice.Init();
            for (var posState = 0U; posState < this.numPosStates; posState++)
            {
                this.lowCoder[posState].Init();
                this.midCoder[posState].Init();
            }

            this.secondChoice.Init();
            this.highCoder.Init();
        }

        public uint Decode(RangeCoder.Decoder rangeDecoder, uint posState)
        {
            if (this.firstChoice.Decode(rangeDecoder) is 0U)
            {
                return this.lowCoder[posState].Decode(rangeDecoder);
            }

            var symbol = NumLowLenSymbols;
            if (this.secondChoice.Decode(rangeDecoder) is 0U)
            {
                symbol += this.midCoder[posState].Decode(rangeDecoder);
            }
            else
            {
                symbol += NumMidLenSymbols;
                symbol += this.highCoder.Decode(rangeDecoder);
            }

            return symbol;
        }
    }

    private sealed class LiteralDecoder
    {
        private Decoder2[]? coders;
        private int numPrevBits;
        private int numPosBits;
        private uint posMask;

        public void Create(int numPosBits, int numPrevBits)
        {
            if (this.coders is not null
                && this.numPrevBits == numPrevBits
                && this.numPosBits == numPosBits)
            {
                return;
            }

            this.numPosBits = numPosBits;
            this.posMask = (1U << numPosBits) - 1;
            this.numPrevBits = numPrevBits;
            var numStates = 1U << (this.numPrevBits + this.numPosBits);
            this.coders = new Decoder2[numStates];
            for (var i = 0; i < numStates; i++)
            {
                this.coders[i] = new Decoder2();
            }
        }

        public void Init()
        {
            if (this.coders is null)
            {
                return;
            }

            var numStates = 1U << (this.numPrevBits + this.numPosBits);
            for (var i = 0U; i < numStates; i++)
            {
                this.coders[i].Init();
            }
        }

        public byte DecodeNormal(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte) => this.coders is not null
            ? this.coders[this.GetState(pos, prevByte)].DecodeNormal(rangeDecoder)
            : throw new InvalidOperationException();

        public byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte, byte matchByte) => this.coders is not null
            ? this.coders[this.GetState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte)
            : throw new InvalidOperationException();

        private uint GetState(uint pos, byte prevByte) => ((pos & this.posMask) << this.numPrevBits) + (uint)(prevByte >> (8 - this.numPrevBits));

        private readonly struct Decoder2
        {
            private readonly RangeCoder.BitDecoder[] secoders;

            public Decoder2() => this.secoders = new RangeCoder.BitDecoder[0x300];

            public void Init()
            {
                for (var i = 0; i < 0x300; i++)
                {
                    this.secoders[i].Init();
                }
            }

            public byte DecodeNormal(RangeCoder.Decoder rangeDecoder)
            {
                var symbol = 1U;
                do
                {
                    symbol = (symbol << 1) | this.secoders[symbol].Decode(rangeDecoder);
                }
                while (symbol < 0x100);

                return (byte)symbol;
            }

            public byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, byte matchByte)
            {
                var symbol = 1U;
                do
                {
                    var matchBit = (uint)(matchByte >> 7) & 1U;
                    matchByte <<= 1;
                    var bit = this.secoders[((1 + matchBit) << 8) + symbol].Decode(rangeDecoder);
                    symbol = (symbol << 1) | bit;
                    if (matchBit != bit)
                    {
                        while (symbol < 0x100)
                        {
                            symbol = (symbol << 1) | this.secoders[symbol].Decode(rangeDecoder);
                        }

                        break;
                    }
                }
                while (symbol < 0x100);

                return (byte)symbol;
            }
        }
    }
}