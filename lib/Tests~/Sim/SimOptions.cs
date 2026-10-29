using System;
using System.Globalization;

namespace ProjectJianghu.Sim
{
    /// <summary>
    /// Command line of the headless harness.
    /// </summary>
    /// <remarks>
    /// The template keeps one mode: <b>streams</b>, the fixed-tick determinism check. A game adds
    /// its own modes (a bot that plays, a benchmark that measures) here, next to this one.
    /// </remarks>
    internal enum SimMode
    {
        /// <summary>The fixed-tick determinism fingerprint (Stage 01).</summary>
        Streams = 0,

        /// <summary>Auto-resolved arena matches and the balance CSV (Stage 07).</summary>
        Balance = 1,

        /// <summary>Roster-wide rank/profile matched arena comparisons.</summary>
        Stratified = 2,
    }

    internal sealed class SimOptions
    {
        internal const uint DefaultSeed = 20261013u;
        internal const long DefaultTicks = 300;
        internal const double DefaultTickHz = 30.0;
        internal const double DefaultFrameHz = 60.0;
        internal const long DefaultRunIntervalTicks = 10;
        internal const int DefaultMatches = 100;

        internal const string Usage =
            "usage: ProjectJianghu.Sim [--seed N] [--ticks N] [--tick-hz X] [--frame-hz X]\n"
            + "                         [--run-interval-ticks N] [--trace N] [--data DIR] [--quiet]\n"
            + "                         [--mode streams|balance|stratified] [--matches N] [--out PATH]\n"
            + "  --seed N                 run seed (default 20261013)\n"
            + "  --ticks N                ticks to produce (default 300)\n"
            + "  --tick-hz X              fixed tick rate (default 30)\n"
            + "  --frame-hz X             synthetic frame rate (default 60)\n"
            + "  --run-interval-ticks N   draw the run stream every N ticks (default 10)\n"
            + "  --trace N                print the first N ticks\n"
            + "  --data DIR               directory holding the generated .bytes\n"
            + "  --quiet                  print only the determinism fingerprint\n"
            + "  --mode NAME              streams (default), balance or stratified\n"
            + "  --matches N              balance: AI matches; stratified: repeats per fighter pairing\n"
            + "  --out PATH               balance CSV path (stratified defaults to artifacts/balance-stratified.csv)\n"
            + "  --help, -h               this text";

        private SimOptions()
        {
        }

        internal uint Seed { get; private set; } = DefaultSeed;

        internal long Ticks { get; private set; } = DefaultTicks;

        internal double TickHz { get; private set; } = DefaultTickHz;

        internal double FrameHz { get; private set; } = DefaultFrameHz;

        internal long RunIntervalTicks { get; private set; } = DefaultRunIntervalTicks;

        internal int Trace { get; private set; }

        internal bool Quiet { get; private set; }

        internal string? DataDirectory { get; private set; }

        internal SimMode Mode { get; private set; } = SimMode.Streams;

        internal int Matches { get; private set; } = DefaultMatches;

        internal string BalanceOut { get; private set; } = "artifacts/balance.csv";

        /// <summary>Parses the command line; <paramref name="helpRequested"/> means "print and exit 0".</summary>
        internal static bool TryParse(string[] args, out SimOptions options, out bool helpRequested)
        {
            var parsed = new SimOptions();
            helpRequested = false;
            options = parsed;
            bool outputSet = false;
            bool matchesSet = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "--help":
                    case "-h":
                        helpRequested = true;
                        return true;

                    case "--quiet":
                        parsed.Quiet = true;
                        break;

                    case "--seed":
                        if (!TryReadUInt(args, ref i, arg, out uint seed))
                        {
                            return false;
                        }

                        parsed.Seed = seed;
                        break;

                    case "--ticks":
                        if (!TryReadLong(args, ref i, arg, out long ticks) || ticks <= 0)
                        {
                            return false;
                        }

                        parsed.Ticks = ticks;
                        break;

                    case "--tick-hz":
                        if (!TryReadDouble(args, ref i, arg, out double tickHz) || !(tickHz > 0))
                        {
                            return false;
                        }

                        parsed.TickHz = tickHz;
                        break;

                    case "--frame-hz":
                        if (!TryReadDouble(args, ref i, arg, out double frameHz) || !(frameHz > 0))
                        {
                            return false;
                        }

                        parsed.FrameHz = frameHz;
                        break;

                    case "--run-interval-ticks":
                        if (!TryReadLong(args, ref i, arg, out long interval) || interval <= 0)
                        {
                            return false;
                        }

                        parsed.RunIntervalTicks = interval;
                        break;

                    case "--trace":
                        if (!TryReadLong(args, ref i, arg, out long trace) || trace < 0)
                        {
                            return false;
                        }

                        parsed.Trace = (int)Math.Min(trace, int.MaxValue);
                        break;

                    case "--mode":
                        if (!ReadValue(args, ref i, arg, out string modeName))
                        {
                            return false;
                        }

                        if (string.Equals(modeName, "streams", StringComparison.OrdinalIgnoreCase))
                        {
                            parsed.Mode = SimMode.Streams;
                        }
                        else if (string.Equals(modeName, "balance", StringComparison.OrdinalIgnoreCase))
                        {
                            parsed.Mode = SimMode.Balance;
                        }
                        else if (string.Equals(modeName, "stratified", StringComparison.OrdinalIgnoreCase))
                        {
                            parsed.Mode = SimMode.Stratified;
                        }
                        else
                        {
                            Console.Error.WriteLine("unknown mode: " + modeName);
                            Console.Error.WriteLine(Usage);
                            return false;
                        }

                        break;

                    case "--matches":
                        if (!TryReadLong(args, ref i, arg, out long matches) || matches < 1 || matches > int.MaxValue)
                        {
                            return false;
                        }

                        parsed.Matches = (int)matches;
                        matchesSet = true;
                        if (parsed.Mode != SimMode.Stratified)
                        {
                            parsed.Mode = SimMode.Balance;
                        }
                        break;

                    case "--out":
                        if (!ReadValue(args, ref i, arg, out string output) || string.IsNullOrWhiteSpace(output))
                        {
                            return false;
                        }

                        parsed.BalanceOut = output;
                        outputSet = true;
                        break;

                    case "--data":
                        if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                        {
                            Console.Error.WriteLine("--data needs a directory");
                            return false;
                        }

                        parsed.DataDirectory = args[++i];
                        break;

                    default:
                        Console.Error.WriteLine("unknown argument: " + arg);
                        Console.Error.WriteLine(Usage);
                        return false;
                }
            }

            if (parsed.Mode == SimMode.Stratified)
            {
                if (!matchesSet) parsed.Matches = 1;
                if (!outputSet) parsed.BalanceOut = "artifacts/balance-stratified.csv";
            }

            return true;
        }

        private static bool TryReadUInt(string[] args, ref int index, string name, out uint value)
        {
            value = 0;
            return ReadValue(args, ref index, name, out string raw)
                && uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadLong(string[] args, ref int index, string name, out long value)
        {
            value = 0;
            return ReadValue(args, ref index, name, out string raw)
                && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadDouble(string[] args, ref int index, string name, out double value)
        {
            value = 0;
            return ReadValue(args, ref index, name, out string raw)
                && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool ReadValue(string[] args, ref int index, string name, out string raw)
        {
            raw = string.Empty;
            if (index + 1 >= args.Length)
            {
                Console.Error.WriteLine(name + " needs a value");
                Console.Error.WriteLine(Usage);
                return false;
            }

            raw = args[++index];
            return true;
        }
    }
}
