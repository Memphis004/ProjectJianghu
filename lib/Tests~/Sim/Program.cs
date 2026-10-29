using System;
using ProjectJianghu.Core.Infrastructure.Config;

namespace ProjectJianghu.Sim
{
    /// <summary>
    /// Headless harness: the fixed ticker over a synthetic frame source, drawing from the
    /// separated streams and printing a fingerprint of the sequence.
    /// </summary>
    /// <remarks>
    /// The property tools/test.ps1 checks at process level is the one that matters: the same seed
    /// prints the same fingerprint, a different seed does not, and the three streams are not one
    /// stream wearing three names. A game adds its own modes here.
    /// </remarks>
    internal static class Program
    {
        private const int ExitOk = 0;
        private const int ExitBadArguments = 1;
        private const int ExitFailed = 2;

        private static int Main(string[] args)
        {
            SimOptions options;
            try
            {
                bool helpRequested;
                if (!SimOptions.TryParse(args, out options, out helpRequested))
                {
                    return helpRequested ? ExitOk : ExitBadArguments;
                }

                if (helpRequested)
                {
                    Console.WriteLine(SimOptions.Usage);
                    return ExitOk;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("bad arguments: " + ex.Message);
                Console.Error.WriteLine(SimOptions.Usage);
                return ExitBadArguments;
            }

            try
            {
                // The streams mode only needs the tables for its banner, so a quiet run skips
                // the load; every arena mode needs them for real.
                LubanGameConfig? config = null;
                if (!options.Quiet || options.Mode == SimMode.Balance || options.Mode == SimMode.Stratified)
                {
                    // Seed and config hash are the two facts a result is only meaningful with: the first
                    // makes a run reproducible, the second says which balance data produced it.
                    config = SimEnvironment.LoadConfig(options.DataDirectory);
                }

                if (config is not null && !options.Quiet)
                {
                    Console.WriteLine(
                        "seed=" + options.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " configHash=" + config.ConfigHash
                        + " data=" + SimEnvironment.DataDirectory(options.DataDirectory)
                        + " tables=localization " + config.Localization.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ", skills " + config.Skills.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ", fighters " + config.Fighters.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ", atbFull " + config.Constant.AtbFull.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                if (options.Mode == SimMode.Balance || options.Mode == SimMode.Stratified)
                {
                    return BalanceMode.Run(options, config!);
                }

                return StreamsMode.Run(options);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("simulation failed: " + ex.Message);
                return ExitFailed;
            }
        }
    }
}
