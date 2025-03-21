// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="KingR">
// Copyright (c) KingR. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO.Compression;
using Lmza.Cli;

var inputArgument = new CliArgument<FileInfo>("INPUT").AcceptExistingOnly();
var outputArgument = new CliArgument<FileInfo>("OUTPUT");

var dictionaryOption = new CliOption<int>("-d")
{
    Description = "set dictionary - [0, 29]",
    DefaultValueFactory = _ => 23,
    Validators = { CreateNumberValidator(0, 29) },
};

var numFastBytesOption = new CliOption<int>("-fb")
{
    Description = "set number of fast bytes - [5, 273]",
    DefaultValueFactory = _ => 128,
    Validators = { CreateNumberValidator(5, 273) },
};

var litContextBitsOption = new CliOption<int>("-lc")
{
    Description = "set number of literal context bits - [0, 8]",
    DefaultValueFactory = _ => 3,
    Validators = { CreateNumberValidator(0, 8) },
};

var litPosBitsOptions = new CliOption<int>("-lp")
{
    Description = "set number of literal pos bits - [0, 4]",
    DefaultValueFactory = _ => 0,
    Validators = { CreateNumberValidator(0, 4) },
};

var posBitsOption = new CliOption<int>("-pb")
{
    Description = "set number of pos bits - [0, 4]",
    DefaultValueFactory = _ => 2,
    Validators = { CreateNumberValidator(0, 4) },
};

var matchFinderOption = new CliOption<string>("-mf")
{
    HelpName = "MF_ID",
    Description = "set Match Finder: [bt2, bt4]",
    DefaultValueFactory = _ => "bt4",
};
matchFinderOption.AcceptOnlyFromAmong("bt2", "bt4");

var eosOption = new CliOption<bool>("-eos")
{
    Description = "write End Of Stream marker",
};

static Action<System.CommandLine.Parsing.OptionResult> CreateNumberValidator<T>(T minimum, T maximum)
    where T : IParsable<T>, IComparable<T>
{
    return new Action<System.CommandLine.Parsing.OptionResult>(result =>
    {
        if (result.Tokens is [{ } token, ..]
            && token.Value is { } value
            && T.TryParse(value, provider: null, out var t)
            && (t.CompareTo(minimum) < 0 || t.CompareTo(maximum) > 0))
        {
            result.AddError($"Value must be between {minimum} and {maximum}");
        }
    });
}

var encodeCommand = new CliCommand("encode")
{
    inputArgument,
    outputArgument,
    dictionaryOption,
    numFastBytesOption,
    litContextBitsOption,
    litPosBitsOptions,
    posBitsOption,
    matchFinderOption,
    eosOption,
};

encodeCommand.Aliases.Add("e");
encodeCommand.SetAction(parseResult =>
{
    var input = parseResult.GetValue(inputArgument);
    var output = parseResult.GetValue(outputArgument);

    var dictionary = 1 << parseResult.GetValue(dictionaryOption);
    var posStateBits = parseResult.GetValue(posBitsOption);
    var litContextBits = parseResult.GetValue(litContextBitsOption);
    var litPosBits = parseResult.GetValue(litPosBitsOptions);
    const int algorithm = 2;
    var numFastBytes = parseResult.GetValue(numFastBytesOption);
    var mf = parseResult.GetValue(matchFinderOption)!;
    var eos = parseResult.GetValue(eosOption);

    var properties = new Dictionary<CoderPropId, object>
    {
        { CoderPropId.DictionarySize, dictionary },
        { CoderPropId.PosStateBits, posStateBits },
        { CoderPropId.LitContextBits, litContextBits },
        { CoderPropId.LitPosBits, litPosBits },
        { CoderPropId.Algorithm, algorithm },
        { CoderPropId.NumFastBytes, numFastBytes },
        { CoderPropId.MatchFinder, mf },
        { CoderPropId.EndMarker, eos },
    };

    LzmaEncoder encoder = new(properties);
    output?.Directory?.Create();
    using var outStream = output?.OpenWrite();
    if (outStream is not null)
    {
        encoder.WriteCoderProperties(outStream);

        using var inStream = input!.OpenRead();
        var fileSize = eos ? -1L : inStream.Length;
        for (var i = 0; i < 8; i++)
        {
            outStream.WriteByte((byte)(fileSize >> (8 * i)));
        }

        encoder.Compress(inStream, outStream, progress: null);
    }
});

var decodeCommand = new CliCommand("decode")
{
    inputArgument,
    outputArgument,
};

decodeCommand.Aliases.Add("d");
decodeCommand.SetAction(parseResult =>
{
    using var input = parseResult.GetValue(inputArgument)!.OpenRead();

    var properties = new byte[5];
    if (input.Read(properties, 0, 5) != 5)
    {
        throw new InvalidDataException("input .lzma is too short");
    }

    LzmaDecoder decoder = new(properties);

    var outputSize = 0L;
    for (var i = 0; i < 8; i++)
    {
        var v = input.ReadByte();
        if (v < 0)
        {
            throw new InvalidDataException("Can't Read 1");
        }

        outputSize |= ((long)(byte)v) << (8 * i);
    }

    using var output = parseResult.GetValue(outputArgument)!.OpenWrite();
    decoder.Decode(input, output, outputSize);
});

var iterationOption = new CliOption<int>("-i") { DefaultValueFactory = _ => 10 };

var benchmarkCommand = new CliCommand("benchmark")
{
    dictionaryOption,
    iterationOption,
};

benchmarkCommand.Aliases.Add("b");
benchmarkCommand.SetAction(parseResult =>
{
    var numIterations = parseResult.GetValue(iterationOption);
    var dictionary = 1U << parseResult.GetValue(dictionaryOption);
    _ = Benchmark.Run(numIterations, dictionary);
});

var rootCommand = new CliRootCommand
{
    encodeCommand,
    decodeCommand,
    benchmarkCommand,
};

var configuration = new CliConfiguration(rootCommand);
await configuration.InvokeAsync(args).ConfigureAwait(false);