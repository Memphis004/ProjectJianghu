using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Application.Arena
{
    /// <summary>One accepted command and the tick at which it was applied.</summary>
    public readonly struct ArenaReplayCommand
    {
        /// <summary>The session tick the command was submitted at.</summary>
        public readonly int Tick;

        /// <summary>The command itself.</summary>
        public readonly ArenaCommand Command;

        /// <summary>What the original session answered; the replay must answer the same.</summary>
        public readonly CommandResult Result;

        /// <summary>Creates one replay entry.</summary>
        public ArenaReplayCommand(int tick, in ArenaCommand command, CommandResult result)
        {
            Tick = tick;
            Command = command;
            Result = result;
        }
    }

    /// <summary>
    /// Replays an accepted command stream through any <see cref="IArenaSessionFactory"/>
    /// and returns the final state hash.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the executable form of the contract's determinism promise: the same setup
    /// plus the same commands must hash the same on the fake and on the real simulation,
    /// which is what makes a saved tournament (Stage 13) and a bug report replayable.
    /// The entry carries the tick because a command is only meaningful at the state that
    /// existed when it was accepted.
    /// </para>
    /// <para>
    /// Replay does not care which factory it is given. It stops at
    /// <paramref name="stopAtTick"/> even if the original run was longer, so a test can
    /// compare a prefix of a match as well as a whole one.
    /// </para>
    /// </remarks>
    public static class ArenaReplay
    {
        /// <summary>
        /// Replays <paramref name="commands"/> into a fresh session and returns its
        /// <see cref="IArenaSession.ComputeStateHash"/> after stepping to
        /// <paramref name="stopAtTick"/> (or until the match finishes).
        /// </summary>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// A replayed command did not produce the recorded result, i.e. the replay
        /// diverged from the original run.
        /// </exception>
        public static ulong Play(
            IArenaSessionFactory factory,
            ArenaMatchSetup setup,
            IReadOnlyList<ArenaReplayCommand> commands,
            int stopAtTick)
        {
            if (factory is null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (setup is null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            if (commands is null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            if (stopAtTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stopAtTick), stopAtTick, "stopAtTick must not be negative.");
            }

            using IArenaSession session = factory.Create(setup);
            int next = 0;

            while (session.Phase != ArenaPhase.Finished && session.Tick < stopAtTick)
            {
                while (next < commands.Count && commands[next].Tick == session.Tick)
                {
                    ArenaReplayCommand entry = commands[next];
                    CommandResult actual = session.Submit(in entry.Command);
                    if (actual != entry.Result)
                    {
                        throw new InvalidOperationException(
                            "Replay diverged at tick " + entry.Tick + ": command " + entry.Command.Kind
                            + " returned " + actual + ", recorded " + entry.Result + ".");
                    }

                    next++;
                }

                session.Step();
            }

            return session.ComputeStateHash();
        }
    }
}
