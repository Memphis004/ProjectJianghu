using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Shared;

namespace ProjectJianghu.Core.Application.Arena.Fake
{
    /// <summary>
    /// A deterministic stand-in for the arena simulation: ATB fills at a constant rate,
    /// Move and Jump teleport, a skill deals its flat damage, and the AI always picks
    /// the first legal action. It exists so events, queries, replay and the contract
    /// suite can be built and exercised before Stage 07 writes the real rules.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything the contract promises is honoured here, but only in the simplest way
    /// that can be: an empty grid, no crit/evade/counter, and Move/Jump that arrive in
    /// one tick. The <b>geometry</b> is not simplified, though: reachability, jump
    /// landings, skill areas and facing come from the Stage 06 domain
    /// (<see cref="ArenaPathfinder"/>, <see cref="ArenaArea"/>, <see cref="ArenaFacing"/>)
    /// over an <see cref="ArenaGrid"/> whose occupant mirror this class keeps current, so
    /// a highlight the presentation shows and the command that later validates cannot
    /// disagree. The one thing it must <b>not</b> be is fuzzy — the same setup, seed and
    /// command stream always produce the same <see cref="ComputeStateHash"/>, because the
    /// replay contract is what the suite checks.
    /// </para>
    /// <para>
    /// The seed decides each fighter's initial ATB fill (a 0–12.5% head start), so a
    /// background match resolved by <see cref="FakeAutoResolver"/> changes with its
    /// seed while staying reproducible. There is no other randomness.
    /// </para>
    /// <para>
    /// Fighter ids must be below <see cref="ArenaConstants.MaxFighters"/>: the frame's
    /// cooldown table is indexed by id, and ids are the handle the presentation and
    /// replay carry.
    /// </para>
    /// </remarks>
    public sealed class FakeArenaSession : IArenaSession, IArenaEventSource
    {
        private const int MaxFighters = ArenaConstants.MaxFighters;
        private const int MaxSkillSlots = ArenaConstants.MaxSkillSlots;
        private const int AtbFull = ArenaConstants.AtbFull;

        private readonly int _introTicks;
        private readonly int _width;
        private readonly int _height;
        private readonly int _winAtTick;
        private readonly int _loseAtTick;
        private readonly int _timeoutAtTick;
        private readonly bool _devCheatsAllowed;
        private readonly AtbMode _mode;
        private readonly int _timeLimitTicks;
        private readonly ulong _seed;
        private readonly int _fighterCount;
        private readonly FakeFighter[] _fighters;
        private readonly int[] _idToIndex;
        private readonly ushort[] _cooldowns;
        private readonly int[] _initialAtb;
        private readonly float[] _previousX;
        private readonly float[] _previousY;
        private readonly GridCoord[] _tileBuffer;
        private readonly GridCoord[] _pathBuffer;
        private readonly GridCoord[] _areaBuffer;
        private readonly ArenaGrid _grid;
        private readonly ArenaFrameBuffer _frame;
        private readonly EventRing<ArenaEvent> _ring;
        private readonly FakeArenaQuery _query;
        private readonly Xoshiro128 _random;
        private readonly byte _playerFighter;
        private readonly byte _playerTeam;

        private int _tick;
        private int _elapsedTicks;
        private ArenaPhase _phase;
        private byte _awaitingFighter = ArenaConstants.NoFighter;
        private uint _latestSeq;
        private bool _timeWarningSent;
        private bool _disposed;
        private ArenaMatchResult? _result;

        /// <summary>Creates a match. <paramref name="options"/> may be null for the defaults.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="setup"/> is null.</exception>
        /// <exception cref="ArgumentException">The setup has no fighters, too many fighters, duplicate ids, ids above the capacity, or too many skill slots.</exception>
        public FakeArenaSession(ArenaMatchSetup setup, FakeArenaOptions? options = null)
        {
            if (setup is null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            if (setup.Fighters is null || setup.Fighters.Count == 0)
            {
                throw new ArgumentException("A match needs at least one fighter.", nameof(setup));
            }

            if (setup.Fighters.Count > MaxFighters)
            {
                throw new ArgumentException("A match cannot hold more than ArenaConstants.MaxFighters.", nameof(setup));
            }

            FakeArenaOptions knobs = options ?? new FakeArenaOptions();

            _introTicks = Math.Max(0, knobs.IntroTicks);
            // ArenaGrid owns the contract's maximum; an option outside it would otherwise
            // make the constructor throw where the fake used to accept any size.
            _width = Math.Clamp(knobs.Width, 1, ArenaConstants.MaxGridSize);
            _height = Math.Clamp(knobs.Height, 1, ArenaConstants.MaxGridSize);
            _winAtTick = knobs.WinAtTick;
            _loseAtTick = knobs.LoseAtTick;
            _timeoutAtTick = knobs.TimeoutAtTick;
            _devCheatsAllowed = setup.DevCheatsAllowed;
            _mode = setup.Mode;
            _timeLimitTicks = setup.TimeLimitTicks;
            _seed = setup.Seed;
            _fighterCount = setup.Fighters.Count;

            _fighters = new FakeFighter[MaxFighters];
            _idToIndex = new int[256];
            for (int i = 0; i < _idToIndex.Length; i++)
            {
                _idToIndex[i] = -1;
            }

            _cooldowns = new ushort[MaxFighters * MaxSkillSlots];
            _initialAtb = new int[MaxFighters];
            _previousX = new float[MaxFighters];
            _previousY = new float[MaxFighters];
            _tileBuffer = new GridCoord[ArenaConstants.MaxGridTiles];
            _pathBuffer = new GridCoord[ArenaConstants.MaxGridTiles];
            _areaBuffer = new GridCoord[ArenaConstants.MaxGridTiles];
            _grid = new ArenaGrid(_width, _height);
            _frame = new ArenaFrameBuffer();
            _ring = new EventRing<ArenaEvent>(ArenaConstants.EventRingCapacity);
            _query = new FakeArenaQuery(this);
            _random = new Xoshiro128(RandomStreams.DeriveStreamSeed(
                unchecked((uint)(setup.Seed ^ (setup.Seed >> 32))), RandomStreamKind.Encounter));
            _phase = ArenaPhase.Intro;
            _playerFighter = ArenaConstants.NoFighter;

            for (int i = 0; i < _fighterCount; i++)
            {
                ArenaFighterSetup source = setup.Fighters[i];
                if (source.Id >= MaxFighters)
                {
                    throw new ArgumentException(
                        "Fighter id " + source.Id + " is at or above ArenaConstants.MaxFighters.", nameof(setup));
                }

                if (_idToIndex[source.Id] >= 0)
                {
                    throw new ArgumentException("Duplicate fighter id " + source.Id + ".", nameof(setup));
                }

                int skillCount = source.Skills is null ? 0 : source.Skills.Count;
                if (skillCount > MaxSkillSlots)
                {
                    throw new ArgumentException(
                        "Fighter " + source.Id + " carries more than ArenaConstants.MaxSkillSlots skills.",
                        nameof(setup));
                }

                ref FakeFighter fighter = ref _fighters[i];
                fighter.Id = source.Id;
                fighter.Team = source.Team;
                fighter.DefId = source.DefId;
                fighter.PlayerControlled = source.PlayerControlled;
                fighter.AiReactionTicks = Math.Max(0, source.AiReactionTicks);
                fighter.Facing = source.StartFacing;
                fighter.Tile = source.StartTile;
                fighter.MaxHp = Math.Max(1, source.Stats.MaxHp);
                fighter.Hp = source.StartHp >= 0 ? Math.Min(source.StartHp, fighter.MaxHp) : fighter.MaxHp;
                fighter.MaxNeili = Math.Max(0, source.Stats.MaxNeili);
                fighter.Neili = fighter.MaxNeili;
                fighter.AtbPerTick = source.Stats.AtbFillTicks <= 0
                    ? AtbFull
                    : Math.Max(1, AtbFull / source.Stats.AtbFillTicks);
                fighter.MoveRange = source.Stats.MoveRange;
                fighter.JumpRange = source.Stats.JumpRange;
                fighter.JumpCooldownTicks = Math.Max(0, source.Stats.JumpCooldownTicks);
                fighter.State = FighterState.Charging;
                fighter.Skills = CopySkills(source.Skills, skillCount);
                fighter.SkillUses = new int[skillCount];
                fighter.SkillHits = new int[skillCount];

                _idToIndex[source.Id] = i;

                if (source.PlayerControlled && _playerFighter == ArenaConstants.NoFighter)
                {
                    _playerFighter = source.Id;
                }
            }

            _playerTeam = _playerFighter != ArenaConstants.NoFighter
                ? _fighters[_idToIndex[_playerFighter]].Team
                : _fighters[0].Team;

            for (int i = 0; i < _fighterCount; i++)
            {
                _initialAtb[i] = _random.NextInt(0, AtbFull / 8 + 1);
            }

            CapturePreviousPositions();
            Emit(ArenaEventType.MatchIntro, ArenaConstants.NoFighter, ArenaConstants.NoFighter, 0,
                _introTicks, 0, GridCoord.Invalid);
            PublishFrame();
        }

        /// <inheritdoc />
        public ArenaPhase Phase => _phase;

        /// <inheritdoc />
        public int Tick => _tick;

        /// <inheritdoc />
        public AtbMode Mode => _mode;

        /// <inheritdoc />
        public IArenaFrame Frame => _frame;

        /// <inheritdoc />
        public IArenaQuery Query => _query;

        /// <inheritdoc />
        public IArenaEventSource Events => this;

        /// <inheritdoc />
        public uint LatestSeq => _latestSeq;

        internal int GridWidth => _width;

        internal int GridHeight => _height;

        /// <inheritdoc />
        public void Step()
        {
            ThrowIfDisposed();

            if (_phase == ArenaPhase.Finished || _phase == ArenaPhase.AwaitingPlayer)
            {
                return;
            }

            CapturePreviousPositions();
            _tick++;

            if (_phase == ArenaPhase.Intro)
            {
                if (_tick >= _introTicks)
                {
                    _phase = ArenaPhase.Running;
                    _elapsedTicks = 0;
                    for (int i = 0; i < _fighterCount; i++)
                    {
                        _fighters[i].Atb = _initialAtb[i];
                    }

                    Emit(ArenaEventType.MatchStarted, ArenaConstants.NoFighter, ArenaConstants.NoFighter, 0,
                        0, 0, GridCoord.Invalid);
                }

                PublishFrame();
                return;
            }

            _elapsedTicks++;
            DecrementTimers();
            FillAtb();

            if (TryScriptedEnding())
            {
                PublishFrame();
                return;
            }

            if (ResolveTeamDefeat())
            {
                PublishFrame();
                return;
            }

            if (_timeLimitTicks > 0 && _elapsedTicks >= _timeLimitTicks)
            {
                FinishTimeout();
                PublishFrame();
                return;
            }

            EmitTimeWarning();

            if (_mode == AtbMode.Wait && EnterAwaitingPlayer())
            {
                PublishFrame();
                return;
            }

            RunAi();
            ResolveTeamDefeat();
            PublishFrame();
        }

        /// <inheritdoc />
        public CommandResult Submit(in ArenaCommand command)
        {
            ThrowIfDisposed();

            CommandResult result = Validate(in command);
            if (result != CommandResult.Ok)
            {
                return result;
            }

            Emit(ArenaEventType.CommandAccepted, command.Actor, ArenaConstants.NoFighter, 0, (int)command.Kind,
                command.Slot, command.Target);

            Apply(in command);

            if (_phase == ArenaPhase.AwaitingPlayer)
            {
                _phase = ArenaPhase.Running;
                _awaitingFighter = ArenaConstants.NoFighter;
            }

            RefreshCurrentFrame();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Exactly what <see cref="Submit"/> would answer, without changing state. The
        /// checks are ordered the same way in both paths.
        /// </summary>
        public CommandResult Validate(in ArenaCommand command) => ValidateCommand(in command, true);

        private CommandResult ValidateCommand(in ArenaCommand command, bool enforcePlayerControl)
        {
            ThrowIfDisposed();

            if (_phase == ArenaPhase.Finished)
            {
                return CommandResult.RejectedWrongPhase;
            }

            if (!TryGetFighterIndex(command.Actor, out int index))
            {
                return CommandResult.RejectedInvalidArg;
            }

            if (command.Kind == ArenaCommandKind.Surrender)
            {
                if (_phase == ArenaPhase.Intro)
                {
                    return CommandResult.RejectedWrongPhase;
                }

                if (enforcePlayerControl && !_fighters[index].PlayerControlled)
                {
                    return CommandResult.RejectedNotControlled;
                }

                return _phase == ArenaPhase.AwaitingPlayer && _awaitingFighter != command.Actor
                    ? CommandResult.RejectedWrongPhase
                    : CommandResult.Ok;
            }

            if (IsDevCommand(command.Kind))
            {
                if (!_devCheatsAllowed)
                {
                    return CommandResult.RejectedNotAllowed;
                }

                if (_phase == ArenaPhase.Intro)
                {
                    return CommandResult.RejectedWrongPhase;
                }

                return _fighters[index].Hp > 0 ? CommandResult.Ok : CommandResult.RejectedInvalidArg;
            }

            if (_phase == ArenaPhase.Intro)
            {
                return CommandResult.RejectedWrongPhase;
            }

            ref FakeFighter fighter = ref _fighters[index];
            if (enforcePlayerControl && !fighter.PlayerControlled)
            {
                return CommandResult.RejectedNotControlled;
            }

            if (_phase == ArenaPhase.AwaitingPlayer && _awaitingFighter != command.Actor)
            {
                return CommandResult.RejectedNotReady;
            }

            if (!IsReady(in fighter))
            {
                return CommandResult.RejectedNotReady;
            }

            switch (command.Kind)
            {
                case ArenaCommandKind.Move:
                    return ValidateMove(in fighter, command.Target);
                case ArenaCommandKind.Jump:
                    return ValidateJump(in fighter, command.Target);
                case ArenaCommandKind.UseSkill:
                    return ValidateSkill(in fighter, command.Slot, command.Target);
                case ArenaCommandKind.Meditate:
                    return CommandResult.Ok;
                default:
                    return CommandResult.RejectedInvalidArg;
            }
        }

        /// <inheritdoc />
        public bool TryGetResult(out ArenaMatchResult result)
        {
            result = _result!;
            return _result is not null;
        }

        /// <inheritdoc />
        public ulong ComputeStateHash()
        {
            ulong hash = 14695981039346656037UL;

            hash = Mix(hash, _tick);
            hash = Mix(hash, _elapsedTicks);
            hash = Mix(hash, (int)_phase);
            hash = Mix(hash, _awaitingFighter);
            hash = Mix(hash, _timeWarningSent ? 1 : 0);
            hash = Mix(hash, (int)(_seed & 0xFFFFFFFFUL));
            hash = Mix(hash, (int)(_seed >> 32));

            if (_result is not null)
            {
                hash = Mix(hash, (int)_result.Outcome);
                hash = Mix(hash, (int)_result.Reason);
                hash = Mix(hash, _result.WinnerTeam);
                hash = Mix(hash, _result.DurationTicks);
            }

            for (int i = 0; i < _fighterCount; i++)
            {
                ref FakeFighter fighter = ref _fighters[i];
                hash = Mix(hash, fighter.Id);
                hash = Mix(hash, fighter.Team);
                hash = Mix(hash, (int)fighter.State);
                hash = Mix(hash, (int)fighter.Facing);
                hash = Mix(hash, fighter.Tile.X);
                hash = Mix(hash, fighter.Tile.Y);
                hash = Mix(hash, fighter.Hp);
                hash = Mix(hash, fighter.Neili);
                hash = Mix(hash, fighter.Atb);
                hash = Mix(hash, fighter.StatusMask);
                hash = Mix(hash, fighter.JumpCooldown);
                hash = Mix(hash, fighter.GodMode ? 1 : 0);
                hash = Mix(hash, _initialAtb[i]);

                int cooldownOffset = fighter.Id * MaxSkillSlots;
                for (int slot = 0; slot < fighter.Skills.Length; slot++)
                {
                    hash = Mix(hash, _cooldowns[cooldownOffset + slot]);
                }
            }

            return hash;
        }

        /// <inheritdoc />
        public int Read(ref uint cursor, Span<ArenaEvent> destination, out int dropped)
        {
            ThrowIfDisposed();
            long readCursor = cursor;
            int count = _ring.Read(ref readCursor, destination, out dropped);
            cursor = (uint)readCursor;
            return count;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
        }

        // ---------------------------------------------------------------- queries

        internal int WriteReachableTiles(byte fighter, Span<GridCoord> destination)
        {
            ThrowIfDisposed();
            if (!TryGetFighterIndex(fighter, out int index))
            {
                return 0;
            }

            ref FakeFighter self = ref _fighters[index];
            SyncOccupancy();

            // A walk, not a Manhattan disk: a fighter in the corridor is an obstacle now.
            return ArenaPathfinder.GetReachable(_grid, self.Tile, self.MoveRange, destination);
        }

        internal int WriteJumpTiles(byte fighter, Span<GridCoord> destination)
        {
            ThrowIfDisposed();
            if (!TryGetFighterIndex(fighter, out int index))
            {
                return 0;
            }

            ref FakeFighter self = ref _fighters[index];
            SyncOccupancy();

            return ArenaPathfinder.GetJumpTargets(_grid, self.Tile, self.JumpRange, destination);
        }

        internal int WriteSkillRangeTiles(byte fighter, byte slot, Span<GridCoord> destination)
        {
            ThrowIfDisposed();
            if (!TryGetFighterIndex(fighter, out int index))
            {
                return 0;
            }

            ref FakeFighter self = ref _fighters[index];
            if (slot >= self.Skills.Length)
            {
                return 0;
            }

            ArenaSkillLoadout skill = self.Skills[slot];
            if (skill.Targeting == SkillTargeting.Self)
            {
                if (destination.Length == 0)
                {
                    return 0;
                }

                destination[0] = self.Tile;
                return 1;
            }

            int count = 0;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var tile = new GridCoord(x, y);
                    int distance = self.Tile.Manhattan(tile);
                    if (distance < skill.RangeMin || distance > skill.RangeMax)
                    {
                        continue;
                    }

                    if (count == destination.Length)
                    {
                        return count;
                    }

                    destination[count] = tile;
                    count++;
                }
            }

            return count;
        }

        internal int WriteSkillArea(byte fighter, byte slot, GridCoord target, Span<GridCoord> destination)
        {
            ThrowIfDisposed();
            if (!TryGetFighterIndex(fighter, out int index))
            {
                return 0;
            }

            ref FakeFighter self = ref _fighters[index];
            if (slot >= self.Skills.Length || !_grid.IsWalkable(target) || destination.Length == 0)
            {
                return 0;
            }

            // The same call the resolve makes, so the preview is the truth rather than an
            // estimate: shape, size and the caster's facing all come from the loadout.
            ArenaSkillLoadout skill = self.Skills[slot];
            return ArenaArea.Resolve(
                skill.Area, target, ArenaFacing.Toward(self.Tile, target), skill.AreaSize, _grid, destination);
        }

        internal bool IsWalkableTile(GridCoord tile) => _grid.IsWalkable(tile);

        // ---------------------------------------------------------------- validation

        private CommandResult ValidateMove(in FakeFighter fighter, GridCoord target)
        {
            if (!IsWalkableTile(target))
            {
                return CommandResult.RejectedOutOfRange;
            }

            if (target == fighter.Tile)
            {
                return CommandResult.RejectedBlocked;
            }

            SyncOccupancy();

            // Accept exactly what the walk can reach: an in-range tile behind a fighter is
            // blocked, not "in range" — the highlight and the command now agree.
            if (ArenaPathfinder.FindPath(_grid, fighter.Tile, target, fighter.MoveRange, _pathBuffer) > 0)
            {
                return CommandResult.Ok;
            }

            return fighter.Tile.Manhattan(target) > fighter.MoveRange
                ? CommandResult.RejectedOutOfRange
                : CommandResult.RejectedBlocked;
        }

        private CommandResult ValidateJump(in FakeFighter fighter, GridCoord target)
        {
            if (fighter.JumpCooldown > 0)
            {
                return CommandResult.RejectedOnCooldown;
            }

            if (!IsWalkableTile(target))
            {
                return CommandResult.RejectedOutOfRange;
            }

            SyncOccupancy();

            if (target == fighter.Tile || _grid.IsOccupied(target))
            {
                return CommandResult.RejectedBlocked;
            }

            if (fighter.Tile.Manhattan(target) > fighter.JumpRange)
            {
                return CommandResult.RejectedOutOfRange;
            }

            // The last word is the same query the highlight comes from.
            int count = ArenaPathfinder.GetJumpTargets(_grid, fighter.Tile, fighter.JumpRange, _areaBuffer);
            for (int i = 0; i < count; i++)
            {
                if (_areaBuffer[i] == target)
                {
                    return CommandResult.Ok;
                }
            }

            return CommandResult.RejectedBlocked;
        }

        private CommandResult ValidateSkill(in FakeFighter fighter, byte slot, GridCoord target)
        {
            if (slot >= fighter.Skills.Length)
            {
                return CommandResult.RejectedInvalidArg;
            }

            ArenaSkillLoadout skill = fighter.Skills[slot];
            if (_cooldowns[fighter.Id * MaxSkillSlots + slot] > 0)
            {
                return CommandResult.RejectedOnCooldown;
            }

            if (fighter.Neili < skill.NeiliCost)
            {
                return CommandResult.RejectedNoNeili;
            }

            if (!_grid.IsWalkable(target))
            {
                return CommandResult.RejectedOutOfRange;
            }

            int distance = fighter.Tile.Manhattan(target);
            if (distance < skill.RangeMin || distance > skill.RangeMax)
            {
                return CommandResult.RejectedOutOfRange;
            }

            switch (skill.Targeting)
            {
                case SkillTargeting.Self:
                    return target == fighter.Tile ? CommandResult.Ok : CommandResult.RejectedInvalidArg;
                case SkillTargeting.Enemy:
                    return TryFindFighterAt(target, fighter.Team, out _)
                        ? CommandResult.Ok
                        : CommandResult.RejectedInvalidArg;
                default:
                    return CommandResult.Ok;
            }
        }

        private static bool IsReady(in FakeFighter fighter) =>
            fighter.State == FighterState.Ready && fighter.Atb >= AtbFull && fighter.Hp > 0;

        private static bool IsDevCommand(ArenaCommandKind kind) => (byte)kind >= 100;

        // ---------------------------------------------------------------- tick steps

        private void CapturePreviousPositions()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                _previousX[i] = _fighters[i].Tile.X;
                _previousY[i] = _fighters[i].Tile.Y;
            }
        }

        private void DecrementTimers()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref FakeFighter fighter = ref _fighters[i];
                if (fighter.JumpCooldown > 0)
                {
                    fighter.JumpCooldown--;
                }

                int offset = fighter.Id * MaxSkillSlots;
                for (int slot = 0; slot < fighter.Skills.Length; slot++)
                {
                    if (_cooldowns[offset + slot] > 0)
                    {
                        _cooldowns[offset + slot]--;
                    }
                }
            }
        }

        private void FillAtb()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref FakeFighter fighter = ref _fighters[i];
                if (fighter.Hp <= 0 || fighter.State != FighterState.Charging)
                {
                    continue;
                }

                fighter.Atb += fighter.AtbPerTick;
                if (fighter.Atb < AtbFull)
                {
                    continue;
                }

                fighter.Atb = AtbFull;
                fighter.State = FighterState.Ready;
                fighter.AiDelay = fighter.AiReactionTicks;
                Emit(ArenaEventType.AtbReady, fighter.Id, fighter.Id, 0, fighter.Atb, 0, fighter.Tile);
            }
        }

        private bool TryScriptedEnding()
        {
            if (_winAtTick >= 0 && _elapsedTicks == _winAtTick)
            {
                for (int i = 0; i < _fighterCount; i++)
                {
                    ref FakeFighter fighter = ref _fighters[i];
                    if (fighter.Team != _playerTeam && fighter.Hp > 0)
                    {
                        fighter.Hp = 0;
                        fighter.State = FighterState.Defeated;
                        fighter.Atb = 0;
                        Emit(ArenaEventType.FighterDefeated, ArenaConstants.NoFighter, fighter.Id, 0, 0, 0,
                            fighter.Tile);
                    }
                }

                Finish(ArenaOutcome.Victory, ArenaEndReason.AllEnemiesDefeated, _playerTeam);
                return true;
            }

            if (_loseAtTick >= 0 && _elapsedTicks == _loseAtTick)
            {
                for (int i = 0; i < _fighterCount; i++)
                {
                    ref FakeFighter fighter = ref _fighters[i];
                    if (fighter.Team == _playerTeam && fighter.Hp > 0)
                    {
                        fighter.Hp = 0;
                        fighter.State = FighterState.Defeated;
                        fighter.Atb = 0;
                        Emit(ArenaEventType.FighterDefeated, ArenaConstants.NoFighter, fighter.Id, 0, 0, 0,
                            fighter.Tile);
                    }
                }

                Finish(ArenaOutcome.Defeat, ArenaEndReason.AllAlliesDefeated, FirstOpposingTeam());
                return true;
            }

            if (_timeoutAtTick >= 0 && _elapsedTicks == _timeoutAtTick)
            {
                FinishTimeout();
                return true;
            }

            return false;
        }

        private bool ResolveTeamDefeat()
        {
            if (_phase == ArenaPhase.Finished)
            {
                return true;
            }

            bool playerAlive = false;
            bool otherAlive = false;
            for (int i = 0; i < _fighterCount; i++)
            {
                if (_fighters[i].Hp <= 0)
                {
                    continue;
                }

                if (_fighters[i].Team == _playerTeam)
                {
                    playerAlive = true;
                }
                else
                {
                    otherAlive = true;
                }
            }

            if (!playerAlive && !otherAlive)
            {
                Finish(ArenaOutcome.Draw, ArenaEndReason.AllAlliesDefeated, ArenaConstants.NoFighter);
                return true;
            }

            if (!playerAlive)
            {
                Finish(ArenaOutcome.Defeat, ArenaEndReason.AllAlliesDefeated, FirstOpposingTeam());
                return true;
            }

            if (!otherAlive)
            {
                Finish(ArenaOutcome.Victory, ArenaEndReason.AllEnemiesDefeated, _playerTeam);
                return true;
            }

            return false;
        }

        private bool EnterAwaitingPlayer()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref FakeFighter fighter = ref _fighters[i];
                if (fighter.PlayerControlled && IsReady(in fighter))
                {
                    _phase = ArenaPhase.AwaitingPlayer;
                    _awaitingFighter = fighter.Id;
                    return true;
                }
            }

            return false;
        }

        private void EmitTimeWarning()
        {
            if (_timeWarningSent || _timeLimitTicks <= 0)
            {
                return;
            }

            int warningTick = _timeLimitTicks - (ArenaConstants.TickRate * 30);
            if (warningTick > 0 && _elapsedTicks >= warningTick)
            {
                _timeWarningSent = true;
                Emit(ArenaEventType.TimeWarning, ArenaConstants.NoFighter, ArenaConstants.NoFighter, 0,
                    _timeLimitTicks - _elapsedTicks, 0, GridCoord.Invalid);
            }
        }

        // ---------------------------------------------------------------- actions

        private void Apply(in ArenaCommand command)
        {
            if (!TryGetFighterIndex(command.Actor, out int index))
            {
                return;
            }

            ref FakeFighter fighter = ref _fighters[index];

            switch (command.Kind)
            {
                case ArenaCommandKind.Move:
                    ApplyMove(ref fighter, command.Target);
                    break;
                case ArenaCommandKind.Jump:
                    ApplyJump(ref fighter, command.Target);
                    break;
                case ArenaCommandKind.UseSkill:
                    ApplySkill(index, ref fighter, command.Slot, command.Target);
                    break;
                case ArenaCommandKind.Meditate:
                    ApplyMeditate(ref fighter);
                    break;
                case ArenaCommandKind.Surrender:
                    Finish(ArenaOutcome.Aborted, ArenaEndReason.Surrendered, FirstOpposingTeam());
                    break;
                case ArenaCommandKind.DevKillEnemies:
                    ApplyDevKillEnemies(in fighter);
                    break;
                case ArenaCommandKind.DevGodMode:
                    fighter.GodMode = true;
                    break;
                case ArenaCommandKind.DevFillAtb:
                    fighter.Atb = AtbFull;
                    fighter.State = FighterState.Ready;
                    fighter.AiDelay = fighter.AiReactionTicks;
                    Emit(ArenaEventType.AtbReady, fighter.Id, fighter.Id, 0, fighter.Atb, 0, fighter.Tile);
                    break;
                case ArenaCommandKind.DevWinNow:
                    Finish(ArenaOutcome.Victory, ArenaEndReason.DevForced, _playerTeam);
                    break;
                case ArenaCommandKind.DevLoseNow:
                    Finish(ArenaOutcome.Defeat, ArenaEndReason.DevForced, FirstOpposingTeam());
                    break;
            }

            ResolveTeamDefeat();
        }

        private void ApplyMove(ref FakeFighter fighter, GridCoord target)
        {
            int distance = fighter.Tile.Manhattan(target);
            // The fake arrives in one tick, so the facing is the move's dominant axis
            // rather than the direction of a last step.
            fighter.Facing = ArenaFacing.Toward(fighter.Tile, target);
            fighter.Tile = target;
            fighter.State = FighterState.Charging;
            fighter.Atb = 0;
            fighter.ActiveSkillId = 0;
            fighter.ActionProgressPermille = 0;

            Emit(ArenaEventType.MoveStarted, fighter.Id, ArenaConstants.NoFighter, 0, distance, 0, target);
            Emit(ArenaEventType.MoveEnded, fighter.Id, ArenaConstants.NoFighter, 0, distance, 0, target);
        }

        private void ApplyJump(ref FakeFighter fighter, GridCoord target)
        {
            int distance = fighter.Tile.Manhattan(target);
            fighter.Facing = ArenaFacing.Toward(fighter.Tile, target);
            fighter.Tile = target;
            fighter.JumpCooldown = fighter.JumpCooldownTicks;
            fighter.State = FighterState.Charging;
            fighter.Atb = 0;
            fighter.ActiveSkillId = 0;

            Emit(ArenaEventType.JumpStarted, fighter.Id, ArenaConstants.NoFighter, 0, distance, 0, target);
            Emit(ArenaEventType.Landed, fighter.Id, ArenaConstants.NoFighter, 0, distance, 0, target);
        }

        private void ApplySkill(int index, ref FakeFighter fighter, byte slot, GridCoord target)
        {
            ArenaSkillLoadout skill = fighter.Skills[slot];

            fighter.Neili = Math.Max(0, fighter.Neili - skill.NeiliCost);
            fighter.SkillUses[slot]++;
            _cooldowns[fighter.Id * MaxSkillSlots + slot] =
                (ushort)Math.Min(ushort.MaxValue, Math.Max(0, skill.CooldownTicks));

            fighter.ActiveSkillId = skill.SkillId;
            fighter.State = FighterState.WindUp;
            fighter.ActionProgressPermille = 0;

            ArenaEventFlags flags = ArenaEventFlags.None;
            if (skill.Kind == DamageKind.Inner)
            {
                flags |= ArenaEventFlags.Inner;
            }

            if ((skill.Flags & SkillFlags.Ultimate) != 0)
            {
                flags |= ArenaEventFlags.Ultimate;
            }

            bool hasTarget = TryFindFighterAt(target, fighter.Team, out int targetIndex);
            byte targetId = hasTarget ? _fighters[targetIndex].Id : ArenaConstants.NoFighter;

            Emit(ArenaEventType.SkillWindUp, fighter.Id, targetId, skill.SkillId, slot, skill.WindUpTicks, target);
            Emit(ArenaEventType.SkillCast, fighter.Id, targetId, skill.SkillId, skill.BaseDamage, 0, target, flags);

            // The area decides who is hit, and it is the same tile set PreviewSkillArea
            // reports. For a Single shape this is the aimed tile, which is all the
            // contract suite's setup uses; a wider shape hits everyone standing in it.
            int areaCount = ArenaArea.Resolve(
                skill.Area, target, ArenaFacing.Toward(fighter.Tile, target), skill.AreaSize, _grid, _areaBuffer);

            for (int i = 0; i < areaCount; i++)
            {
                if (TryFindFighterAt(_areaBuffer[i], fighter.Team, out int victimIndex))
                {
                    ApplyHit(index, victimIndex, slot, in skill, flags);
                }
            }

            fighter.ActiveSkillId = 0;
            fighter.State = FighterState.Charging;
            fighter.Atb = 0;
            fighter.ActionProgressPermille = 0;
        }

        private void ApplyHit(int attackerIndex, int targetIndex, byte slot, in ArenaSkillLoadout skill,
            ArenaEventFlags flags)
        {
            ref FakeFighter attacker = ref _fighters[attackerIndex];
            ref FakeFighter target = ref _fighters[targetIndex];

            int damage = Math.Max(0, skill.BaseDamage);
            if (target.GodMode)
            {
                damage = 0;
            }

            target.Hp = Math.Max(0, target.Hp - damage);
            attacker.DamageDealt += damage;
            attacker.SkillHits[slot]++;
            target.DamageTaken += damage;

            Emit(ArenaEventType.Hit, attacker.Id, target.Id, skill.SkillId, damage, target.Hp, target.Tile, flags);

            if (target.Hp == 0 && target.State != FighterState.Defeated)
            {
                target.State = FighterState.Defeated;
                target.Atb = 0;
                target.ActiveSkillId = 0;
                Emit(ArenaEventType.FighterDefeated, ArenaConstants.NoFighter, target.Id, 0, 0, 0, target.Tile);
            }
        }

        private void ApplyMeditate(ref FakeFighter fighter)
        {
            int gain = Math.Max(1, fighter.MaxNeili / 10);
            fighter.Neili = Math.Min(fighter.MaxNeili, fighter.Neili + gain);
            fighter.State = FighterState.Charging;
            fighter.Atb = 0;
            fighter.ActiveSkillId = 0;

            Emit(ArenaEventType.NeiliRecovered, fighter.Id, fighter.Id, 0, gain, fighter.Neili, fighter.Tile);
        }

        private void ApplyDevKillEnemies(in FakeFighter actor)
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref FakeFighter other = ref _fighters[i];
                if (other.Team == actor.Team || other.Hp <= 0)
                {
                    continue;
                }

                other.Hp = 0;
                other.State = FighterState.Defeated;
                other.Atb = 0;
                Emit(ArenaEventType.FighterDefeated, ArenaConstants.NoFighter, other.Id, 0, 0, 0, other.Tile);
            }
        }

        // ---------------------------------------------------------------- AI

        private void RunAi()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref FakeFighter fighter = ref _fighters[i];
                if (fighter.PlayerControlled || fighter.Hp <= 0 || fighter.State != FighterState.Ready)
                {
                    continue;
                }

                if (fighter.AiDelay > 0)
                {
                    fighter.AiDelay--;
                    continue;
                }

                if (!TryChooseAiCommand(i, out ArenaCommand command)
                    || ValidateCommand(in command, false) != CommandResult.Ok)
                {
                    // Never leave a fighter Ready forever: a fake AI that cannot act gives its
                    // turn back instead of hanging the match.
                    fighter.Atb = 0;
                    fighter.State = FighterState.Charging;
                    continue;
                }

                Emit(ArenaEventType.CommandAccepted, command.Actor, ArenaConstants.NoFighter, 0, (int)command.Kind,
                    command.Slot, command.Target);
                Apply(in command);

                if (_phase == ArenaPhase.Finished)
                {
                    return;
                }
            }
        }

        private bool TryChooseAiCommand(int index, out ArenaCommand command)
        {
            ref FakeFighter self = ref _fighters[index];

            for (int slot = 0; slot < self.Skills.Length; slot++)
            {
                ArenaSkillLoadout skill = self.Skills[slot];
                if (_cooldowns[self.Id * MaxSkillSlots + slot] > 0 || self.Neili < skill.NeiliCost)
                {
                    continue;
                }

                if (skill.Targeting == SkillTargeting.Self)
                {
                    if (skill.RangeMin == 0 && skill.RangeMax >= 0)
                    {
                        command = ArenaCommand.Skill(self.Id, (byte)slot, self.Tile);
                        return true;
                    }

                    continue;
                }

                for (int other = 0; other < _fighterCount; other++)
                {
                    ref FakeFighter enemy = ref _fighters[other];
                    if (enemy.Team == self.Team || enemy.Hp <= 0)
                    {
                        continue;
                    }

                    int distance = self.Tile.Manhattan(enemy.Tile);
                    if (distance >= skill.RangeMin && distance <= skill.RangeMax)
                    {
                        command = ArenaCommand.Skill(self.Id, (byte)slot, enemy.Tile);
                        return true;
                    }
                }
            }

            int nearest = -1;
            int nearestDistance = int.MaxValue;
            for (int other = 0; other < _fighterCount; other++)
            {
                ref FakeFighter enemy = ref _fighters[other];
                if (enemy.Team == self.Team || enemy.Hp <= 0)
                {
                    continue;
                }

                int distance = self.Tile.Manhattan(enemy.Tile);
                if (distance < nearestDistance)
                {
                    nearest = other;
                    nearestDistance = distance;
                }
            }

            if (nearest >= 0 && nearestDistance > 1)
            {
                int count = _query.GetReachableTiles(self.Id, _tileBuffer);
                int bestTile = -1;
                int bestDistance = nearestDistance;
                for (int i = 0; i < count; i++)
                {
                    int distance = _tileBuffer[i].Manhattan(_fighters[nearest].Tile);
                    if (distance < bestDistance)
                    {
                        bestTile = i;
                        bestDistance = distance;
                    }
                }

                if (bestTile >= 0)
                {
                    command = ArenaCommand.Move(self.Id, _tileBuffer[bestTile]);
                    return true;
                }
            }

            command = ArenaCommand.Meditate(self.Id);
            return true;
        }

        // ---------------------------------------------------------------- results

        private void FinishTimeout()
        {
            long playerHp = 0;
            long playerMax = 0;
            long otherHp = 0;
            long otherMax = 0;

            for (int i = 0; i < _fighterCount; i++)
            {
                ref FakeFighter fighter = ref _fighters[i];
                if (fighter.Team == _playerTeam)
                {
                    playerHp += fighter.Hp;
                    playerMax += fighter.MaxHp;
                }
                else
                {
                    otherHp += fighter.Hp;
                    otherMax += fighter.MaxHp;
                }
            }

            if (playerMax <= 0 || otherMax <= 0)
            {
                Finish(ArenaOutcome.Draw, ArenaEndReason.TimeoutDraw, ArenaConstants.NoFighter);
                return;
            }

            long playerSide = playerHp * otherMax;
            long otherSide = otherHp * playerMax;

            if (playerSide > otherSide)
            {
                Finish(ArenaOutcome.Victory, ArenaEndReason.TimeoutHpRatio, _playerTeam);
            }
            else if (playerSide < otherSide)
            {
                Finish(ArenaOutcome.Defeat, ArenaEndReason.TimeoutHpRatio, FirstOpposingTeam());
            }
            else
            {
                Finish(ArenaOutcome.Draw, ArenaEndReason.TimeoutDraw, ArenaConstants.NoFighter);
            }
        }

        private void Finish(ArenaOutcome outcome, ArenaEndReason reason, byte winnerTeam)
        {
            if (_phase == ArenaPhase.Finished)
            {
                return;
            }

            _phase = ArenaPhase.Finished;
            _awaitingFighter = ArenaConstants.NoFighter;
            _result = BuildResult(outcome, reason, winnerTeam);

            // The caller publishes: Finish is reached from Step (which publishes exactly
            // once) and from Submit (which refreshes the current slot in place). Publishing
            // here as well would swap the slots twice in one tick.
            Emit(ArenaEventType.MatchEnded, ArenaConstants.NoFighter, ArenaConstants.NoFighter, 0,
                (int)reason, winnerTeam, GridCoord.Invalid);
        }

        private ArenaMatchResult BuildResult(ArenaOutcome outcome, ArenaEndReason reason, byte winnerTeam)
        {
            var stats = new FighterMatchStat[_fighterCount];
            for (int i = 0; i < _fighterCount; i++)
            {
                ref FakeFighter fighter = ref _fighters[i];

                int used = 0;
                for (int slot = 0; slot < fighter.Skills.Length; slot++)
                {
                    if (fighter.SkillUses[slot] > 0)
                    {
                        used++;
                    }
                }

                var skillUses = new SkillUseStat[used];
                int next = 0;
                for (int slot = 0; slot < fighter.Skills.Length; slot++)
                {
                    if (fighter.SkillUses[slot] > 0)
                    {
                        skillUses[next] = new SkillUseStat(
                            fighter.Skills[slot].SkillId, fighter.SkillUses[slot], fighter.SkillHits[slot]);
                        next++;
                    }
                }

                stats[i] = new FighterMatchStat
                {
                    Id = fighter.Id,
                    FinalHp = fighter.Hp,
                    DamageDealt = fighter.DamageDealt,
                    DamageTaken = fighter.DamageTaken,
                    Evades = fighter.Evades,
                    Counters = fighter.Counters,
                    SkillUses = skillUses,
                };
            }

            return new ArenaMatchResult
            {
                Outcome = outcome,
                Reason = reason,
                WinnerTeam = winnerTeam,
                DurationTicks = _elapsedTicks,
                Fighters = stats,
            };
        }

        // ---------------------------------------------------------------- frame

        private void PublishFrame()
        {
            _frame.BeginFrame();
            for (int i = 0; i < _fighterCount; i++)
            {
                _frame.Add(BuildView(i));
            }

            for (int i = 0; i < _fighterCount; i++)
            {
                _frame.SetCooldowns(_fighters[i].Id, CooldownSpan(i));
            }

            _frame.SetClock(_tick, _elapsedTicks, _timeLimitTicks, _playerFighter);
            _frame.Publish();
        }

        private void RefreshCurrentFrame()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                _frame.UpdateCurrent(i, BuildView(i));
                _frame.UpdateCurrentCooldowns(_fighters[i].Id, CooldownSpan(i));
            }

            _frame.SetClock(_tick, _elapsedTicks, _timeLimitTicks, _playerFighter);
        }

        private FighterView BuildView(int index)
        {
            ref FakeFighter fighter = ref _fighters[index];
            return new FighterView
            {
                Id = fighter.Id,
                Team = fighter.Team,
                DefId = fighter.DefId,
                State = fighter.State,
                Facing = fighter.Facing,
                Tile = fighter.Tile,
                PrevX = _previousX[index],
                PrevY = _previousY[index],
                X = fighter.Tile.X,
                Y = fighter.Tile.Y,
                Height = 0f,
                Hp = fighter.Hp,
                MaxHp = fighter.MaxHp,
                Neili = fighter.Neili,
                MaxNeili = fighter.MaxNeili,
                Atb = fighter.Atb,
                StatusMask = fighter.StatusMask,
                ActiveSkillId = fighter.ActiveSkillId,
                ActionProgressPermille = fighter.ActionProgressPermille,
                JumpCooldownTicks = (ushort)Math.Min(ushort.MaxValue, fighter.JumpCooldown),
            };
        }

        private ReadOnlySpan<ushort> CooldownSpan(int index) =>
            new ReadOnlySpan<ushort>(_cooldowns, _fighters[index].Id * MaxSkillSlots, MaxSkillSlots);

        // ---------------------------------------------------------------- helpers

        private bool TryGetFighterIndex(byte id, out int index)
        {
            index = _idToIndex[id];
            return index >= 0 && index < _fighterCount;
        }

        /// <summary>
        /// Rebuilds the grid's occupant mirror from the fighters still standing.
        /// </summary>
        /// <remarks>
        /// The fighters own their tiles; the mirror exists so one grid answers walk, jump,
        /// area and preview questions the same way. Rebuilding at every entry point costs a
        /// handful of iterations for at most eight fighters and cannot go stale, which is
        /// cheaper to reason about than an update scattered over every HP change.
        /// </remarks>
        private void SyncOccupancy()
        {
            _grid.ClearOccupancy();
            for (int i = 0; i < _fighterCount; i++)
            {
                ref FakeFighter fighter = ref _fighters[i];
                if (fighter.Hp > 0)
                {
                    _grid.TryOccupy(fighter.Tile, fighter.Id);
                }
            }
        }

        private bool TryFindFighterAt(GridCoord tile, byte team, out int index)
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                if (_fighters[i].Hp > 0 && _fighters[i].Team != team && _fighters[i].Tile == tile)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private byte FirstOpposingTeam()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                if (_fighters[i].Team != _playerTeam)
                {
                    return _fighters[i].Team;
                }
            }

            return ArenaConstants.NoFighter;
        }

        private static ArenaSkillLoadout[] CopySkills(IReadOnlyList<ArenaSkillLoadout>? source, int count)
        {
            var skills = new ArenaSkillLoadout[count];
            for (int i = 0; i < count; i++)
            {
                skills[i] = source![i];
            }

            return skills;
        }

        private void Emit(ArenaEventType type, byte source, byte target, int defId, int value, int value2,
            GridCoord tile, ArenaEventFlags flags = ArenaEventFlags.None)
        {
            var arenaEvent = new ArenaEvent
            {
                Seq = (uint)_ring.NextSeq,
                Tick = _tick,
                Type = type,
                Flags = flags,
                Source = source,
                Target = target,
                DefId = defId,
                Value = value,
                Value2 = value2,
                Tile = tile,
            };

            _ring.Write(arenaEvent);
            _latestSeq = arenaEvent.Seq;
        }

        private static ulong Mix(ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(bits >> shift);
                hash *= 1099511628211UL;
            }

            return hash;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FakeArenaSession));
            }
        }

        private struct FakeFighter
        {
            public byte Id;
            public byte Team;
            public int DefId;
            public bool PlayerControlled;
            public int AiReactionTicks;
            public int AiDelay;
            public FighterState State;
            public IsoDir Facing;
            public GridCoord Tile;
            public int Hp;
            public int MaxHp;
            public int Neili;
            public int MaxNeili;
            public int Atb;
            public int AtbPerTick;
            public ushort StatusMask;
            public int ActiveSkillId;
            public ushort ActionProgressPermille;
            public int JumpCooldown;
            public int JumpCooldownTicks;
            public int MoveRange;
            public int JumpRange;
            public ArenaSkillLoadout[] Skills;
            public int[] SkillUses;
            public int[] SkillHits;
            public bool GodMode;
            public int DamageDealt;
            public int DamageTaken;
            public int Evades;
            public int Counters;
        }
    }
}
