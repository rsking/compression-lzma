// -----------------------------------------------------------------------
// <copyright file="Benchmark.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Lmza.Cli;

using System.IO.Compression;
using System.IO.Compression.Common;

/// <summary>
/// The bench marls.
/// </summary>
internal static class Benchmark
{
    private const uint AdditionalSize = 6 << 20;
    private const uint CompressedAdditionalSize = 1 << 10;

    private const int SubBits = 8;

    /// <summary>
    /// Runs the benchmark.
    /// </summary>
    /// <param name="numIterations">The number of iterations.</param>
    /// <param name="dictionarySize">The dictionary size.</param>
    /// <returns>The return value.</returns>
    public static int Run(int numIterations, uint dictionarySize)
    {
        if (numIterations <= 0)
        {
            return 0;
        }

        if (dictionarySize < 1 << 18)
        {
            Console.WriteLine("\nError: dictionary size for benchmark must be >= 19 (512 KB)");
            return 1;
        }

        Console.Write("\n       Compressing                Decompressing\n\n");

        LzmaEncoder encoder = new(new Dictionary<CoderPropId, object> { { CoderPropId.DictionarySize, (int)dictionarySize } });

        var bufferSize = dictionarySize + AdditionalSize;
        var compressedBufferSize = (bufferSize / 2) + CompressedAdditionalSize;

        var propStream = new MemoryStream();
        encoder.WriteCoderProperties(propStream);
        var propArray = propStream.ToArray();

        var rg = new BenchRandomGenerator(bufferSize);

        rg.Generate();
        var crc = new Crc();
        crc.Init();
        crc.Update(rg.Buffer, 0, (uint)rg.Buffer.Length);

        var progressInfo = new ProgressInfo
        {
            ApprovedStart = dictionarySize,
        };

        var totalBenchSize = 0UL;
        var totalEncodeTime = 0UL;
        var totalDecodeTime = 0UL;
        var totalCompressedSize = 0UL;

        var inStream = new MemoryStream(rg.Buffer);
        var compressedStream = new MemoryStream((int)compressedBufferSize);
        var crcOutStream = new CrcOutStream();
        for (var i = 0; i < numIterations; i++)
        {
            progressInfo.Init();
            _ = inStream.Seek(0, SeekOrigin.Begin);
            _ = compressedStream.Seek(0, SeekOrigin.Begin);
            encoder.Encode(inStream, compressedStream, (inSize, _) => progressInfo.SetProgress(inSize));
            var sp2 = DateTime.UtcNow - progressInfo.Time;
            var encodeTime = (ulong)sp2.Ticks;

            var compressedSize = compressedStream.Position;
            if (progressInfo.InSize is 0)
            {
                throw new InvalidOperationException("Internal ERROR 1282");
            }

            ulong decodeTime = 0;
            for (var j = 0; j < 2; j++)
            {
                _ = compressedStream.Seek(0, SeekOrigin.Begin);
                crcOutStream.Init();

                var decoder = new LzmaDecoder(propArray);
                ulong outSize = bufferSize;
                var startTime = DateTime.UtcNow;
                decoder.Decode(compressedStream, crcOutStream, (long)outSize);
                var sp = DateTime.UtcNow - startTime;
                decodeTime = (ulong)sp.Ticks;
                if (crcOutStream.Digest != crc.Digest)
                {
                    throw new InvalidOperationException("CRC Error");
                }
            }

            var benchSize = bufferSize - (ulong)progressInfo.InSize;
            PrintResults(dictionarySize, encodeTime, benchSize, decompressMode: false, 0);
            Console.Write("     ");
            PrintResults(dictionarySize, decodeTime, bufferSize, decompressMode: true, (ulong)compressedSize);
            Console.WriteLine();

            totalBenchSize += benchSize;
            totalEncodeTime += encodeTime;
            totalDecodeTime += decodeTime;
            totalCompressedSize += (ulong)compressedSize;
        }

        Console.WriteLine("---------------------------------------------------");
        PrintResults(dictionarySize, totalEncodeTime, totalBenchSize, decompressMode: false, 0);
        Console.Write("     ");
        PrintResults(dictionarySize, totalDecodeTime, bufferSize * (ulong)numIterations, decompressMode: true, totalCompressedSize);
        Console.WriteLine("    Average");
        return 0;

        static void PrintResults(
            uint dictionarySize,
            ulong elapsedTime,
            ulong size,
            bool decompressMode,
            ulong secondSize)
        {
            var speed = MyMultDiv64(size, elapsedTime);
            PrintValue(speed / 1024);
            Console.Write(" KB/s  ");
            var rating = decompressMode ? GetDecompressRating(elapsedTime, size, secondSize) : GetCompressRating(dictionarySize, elapsedTime, size);
            PrintRating(rating);

            static ulong MyMultDiv64(ulong value, ulong elapsedTime)
            {
                ulong freq = TimeSpan.TicksPerSecond;
                var elTime = elapsedTime;
                while (freq > 1000000)
                {
                    freq >>= 1;
                    elTime >>= 1;
                }

                if (elTime == 0)
                {
                    elTime = 1;
                }

                return value * freq / elTime;
            }

            static void PrintValue(ulong v)
            {
                var s = v.ToString();
                for (var i = 0; i + s.Length < 6; i++)
                {
                    Console.Write(" ");
                }

                Console.Write(s);
            }

            static void PrintRating(ulong rating)
            {
                PrintValue(rating / 1000000);
                Console.Write(" MIPS");
            }

            static ulong GetDecompressRating(ulong elapsedTime, ulong outSize, ulong inSize)
            {
                var numCommands = (inSize * 220) + (outSize * 20);
                return MyMultDiv64(numCommands, elapsedTime);
            }

            static ulong GetCompressRating(uint dictionarySize, ulong elapsedTime, ulong size)
            {
                ulong t = GetLogSize(dictionarySize) - (18 << SubBits);
                var numCommandsForOne = 1060 + ((t * t * 10) >> (2 * SubBits));
                var numCommands = size * numCommandsForOne;
                return MyMultDiv64(numCommands, elapsedTime);
            }
        }
    }

    private static uint GetLogSize(uint size)
    {
        for (var i = SubBits; i < 32; i++)
        {
            for (var j = 0U; j < 1 << SubBits; j++)
            {
                if (size <= (1U << i) + (j << (i - SubBits)))
                {
                    return (uint)(i << SubBits) + j;
                }
            }
        }

        return 32 << SubBits;
    }

    private sealed class RandomGenerator
    {
        private uint a1;
        private uint a2;

        public RandomGenerator() => this.Init();

        public void Init()
        {
            this.a1 = 362436069;
            this.a2 = 521288629;
        }

        public uint GetRnd()
        {
            var v = this.a1 = (36969 * (this.a1 & 0xffff)) + (this.a1 >> 16);
            var v1 = this.a2 = (18000 * (this.a2 & 0xffff)) + (this.a2 >> 16);
            return (v << 16) ^ v1;
        }
    }

    private sealed class BitRandomGenerator
    {
        private readonly RandomGenerator rG = new();
        private uint value;
        private int numBits;

        public void Init()
        {
            this.value = 0;
            this.numBits = 0;
        }

        public uint GetRnd(int numBits)
        {
            uint result;
            if (this.numBits > numBits)
            {
                result = this.value & ((1U << numBits) - 1);
                this.value >>= numBits;
                this.numBits -= numBits;
                return result;
            }

            numBits -= this.numBits;
            result = this.value << numBits;
            this.value = this.rG.GetRnd();
            result |= this.value & ((1U << numBits) - 1);
            this.value >>= numBits;
            this.numBits = 32 - numBits;
            return result;
        }
    }

    private sealed class BenchRandomGenerator(uint bufferSize)
    {
        private readonly BitRandomGenerator rG = new();
        private uint pos = 0U;
        private uint rep0;

        public byte[] Buffer { get; } = new byte[bufferSize];

        public void Generate()
        {
            this.rG.Init();
            this.rep0 = 1;
            while (this.pos < this.Buffer.Length)
            {
                if (this.GetRndBit() == 0 || this.pos < 1)
                {
                    this.Buffer[this.pos++] = (byte)this.rG.GetRnd(8);
                }
                else
                {
                    uint len;
                    if (this.rG.GetRnd(3) == 0)
                    {
                        len = 1 + this.GetLen1();
                    }
                    else
                    {
                        do
                        {
                            this.rep0 = this.GetOffset();
                        }
                        while (this.rep0 >= this.pos);
                        this.rep0++;
                        len = 2 + this.GetLen2();
                    }

                    for (uint i = 0; i < len && this.pos < this.Buffer.Length; i++, this.pos++)
                    {
                        this.Buffer[this.pos] = this.Buffer[this.pos - this.rep0];
                    }
                }
            }
        }

        private uint GetRndBit() => this.rG.GetRnd(1);

        private uint GetLogRandBits(int numBits)
        {
            var len = this.rG.GetRnd(numBits);
            return this.rG.GetRnd((int)len);
        }

        private uint GetOffset() => this.GetRndBit() == 0 ? this.GetLogRandBits(4) : (this.GetLogRandBits(4) << 10) | this.rG.GetRnd(10);

        private uint GetLen1() => this.rG.GetRnd(1 + (int)this.rG.GetRnd(2));

        private uint GetLen2() => this.rG.GetRnd(2 + (int)this.rG.GetRnd(2));
    }

    private sealed class CrcOutStream : Stream
    {
        private readonly Crc crc = new();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position
        {
            get => 0;
            set => System.Diagnostics.Debug.Write(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Ignoring setting the position to {value}"));
        }

        public uint Digest => this.crc.Digest;

        public void Init() => this.crc.Init();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => 0L;

        public override void SetLength(long value)
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override void WriteByte(byte value) => this.crc.Update(value);

        public override void Write(byte[] buffer, int offset, int count) => this.crc.Update(buffer, (uint)offset, (uint)count);
    }

    private sealed class ProgressInfo
    {
        public long ApprovedStart { get; set; }

        public long InSize { get; private set; }

        public DateTime Time { get; set; }

        public void Init() => this.InSize = 0;

        public void SetProgress(long inSize)
        {
            if (inSize >= this.ApprovedStart && this.InSize is 0)
            {
                this.Time = DateTime.UtcNow;
                this.InSize = inSize;
            }
        }
    }
}