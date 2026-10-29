using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Character;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Shared;

namespace ProjectJianghu.Core.Simulation.Arena
{
    /// <summary>
    /// The arena match rules: an array of fighters, a fixed 30 Hz tick, ATB, wind-ups,
    /// walk and jump actions, hit resolution, statuses and the match result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the real <see cref="IArenaSession"/> the contract was written for (Stage 07).
    /// Storage follows [ADR 0101]: fixed struct arrays owned by the session, no ECS, no
    /// per-fighter object. Frames go through the shared <see cref="ArenaFrameBuffer"/> and
    /// events through <see cref="EventRing{T}"/>, so the presentation sees exactly what the
    /// fake published before.
    /// </para>
    /// <para>
    /// Every number that decides an outcome comes from <see cref="IGameConfig"/> (AGENTS
    /// rule 6): the <c>constant</c> row for mitigation, positional bonuses, ATB costs and
    /// regenerate, the <c>status</c> rows for durations and multipliers, and the
    /// <c>arena_map</c> row for the board. The rules themselves are in
    /// [ADR 0114](../../../../docs/adr/0114-arena-simulation-tick-resolve-and-damage.md);
    /// the one contract amendment this stage needed is
    /// [ADR 0115](../../../../docs/adr/0115-move-is-a-multi-tick-action.md).
    /// </para>
    /// <para>
    /// Determinism: one <see cref="IRandomSource"/> on the encounter stream, seeded from the
    /// match seed, consumed in a fixed order. No time, no <c>System.Random</c>, no floats in
    /// the hashed state. Allocation: everything is sized once in the constructor, so
    /// <see cref="Step"/>, <see cref="Submit"/> and every query allocate nothing after
    /// warm-up.
    /// </para>
    /// </remarks>
    public sealed class ArenaSimulation : IArenaSession, IArenaEventSource
    {
        private const int MaxFighters = ArenaConstants.MaxFighters;
        private const int MaxSkillSlots = ArenaConstants.MaxSkillSlots;
        private const int MaxGridTiles = ArenaConstants.MaxGridTiles;

        /// <summary>Statuses are indexed by their <c>EStatus</c> value, which is 1-based.</summary>
        private const int StatusSlots = 32;

        /// <summary>No status rule; also the "no status" answer of a skill slot.</summary>
        private const byte NoStatus = 255;

        /// <summary>The contract's <c>TimeWarning</c>: one event, thirty seconds before the limit.</summary>
        private const int TimeWarningTicks = ArenaConstants.TickRate * 30;

        private readonly IArenaAiHook _ai;
        private readonly AtbMode _mode;
        private readonly int _introTicks;
        private readonly int _timeLimitTicks;
        private readonly bool _devCheatsAllowed;
        private readonly ulong _seed;

        private readonly int _atbFull;
        private readonly int _mitigationK;
        private readonly int _backBonusX100;
        private readonly int _flankBonusX100;
        private readonly int _moveAtbCost;
        private readonly int _jumpAtbCost;
        private readonly int _meditateAtbCost;
        private readonly int _meditateNeiliRestore;
        private readonly int _defaultJumpCooldownTicks;

        private readonly ArenaFighter[] _fighters;
        private readonly int[] _idToIndex;
        private readonly ArenaSkillLoadout[] _skills;
        private readonly AiProfileConfig[] _aiProfiles;
        private readonly byte[] _skillStatusRule;
        private readonly int[] _skillStatusTicks;
        private readonly int[] _skillCount;
        private readonly int[] _skillUses;
        private readonly int[] _skillHits;
        private readonly ushort[] _cooldowns;
        private readonly GridCoord[] _paths;
        private readonly int[] _statusLeft;
        private readonly int[] _statusTimer;
        private readonly StatusRule[] _statusRules;
        private readonly byte[] _statusRuleByKind;

        private readonly GridCoord[] _tileBuffer;
        private readonly GridCoord[] _pathBuffer;
        private readonly GridCoord[] _areaBuffer;

        private readonly ArenaGrid _grid;
        private readonly ArenaFrameBuffer _frame;
        private readonly ArenaFrameBuffer _liveFrame;
        private readonly EventRing<ArenaEvent> _ring;
        private readonly ArenaSimulationQuery _query;
        private readonly ArenaSimulationQuery _aiQuery;
        private readonly Xoshiro128 _random;

        private readonly int _fighterCount;
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

        /// <summary>Creates a match.</summary>
        /// <param name="setup">The fighters, seed, mode and map id.</param>
        /// <param name="config">The tuned numbers and the arena map.</param>
        /// <param name="aiHook">The decision maker for AI fighters; null uses <see cref="GreedyArenaAiHook"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="setup"/> or <paramref name="config"/> is null.</exception>
        /// <exception cref="ArgumentException">The setup has no fighters, too many, duplicate ids, ids above the capacity, or too many skills.</exception>
        public ArenaSimulation(ArenaMatchSetup setup, IGameConfig config, IArenaAiHook? aiHook = null)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

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

            _ai = aiHook ?? new GreedyArenaAiHook(config);
            _aiProfiles = new AiProfileConfig[config.AiProfiles.Count];
            for (int i = 0; i < _aiProfiles.Length; i++)
            {
                AiProfileConfig profile = config.AiProfiles[i];
                _aiProfiles[i] = new AiProfileConfig
                {
                    Id = profile.Id,
                    Archetype = profile.Archetype,
                    EngageRange = profile.EngageRange,
                    RetreatHpX100 = profile.RetreatHpX100,
                    AggressionX100 = profile.AggressionX100,
                    DecisionDelayTicks = profile.DecisionDelayTicks,
                    SkillWeight = profile.SkillWeight,
                    MoveWeight = profile.MoveWeight,
                    JumpWeight = profile.JumpWeight,
                    MeditateWeight = profile.MeditateWeight,
                    FlankWeight = profile.FlankWeight,
                    RetreatWeight = profile.RetreatWeight,
                    DecisivenessDelayX100 = profile.DecisivenessDelayX100,
                    DifficultyDelayX100 = profile.DifficultyDelayX100,
                    NameKey = profile.NameKey,
                };
            }
            _mode = setup.Mode;
            _timeLimitTicks = setup.TimeLimitTicks;
            _devCheatsAllowed = setup.DevCheatsAllowed;
            _seed = setup.Seed;
            _introTicks = ArenaConstants.IntroTicks;
            _fighterCount = setup.Fighters.Count;

            ConstantConfig constant = config.Constant;
            _atbFull = constant.AtbFull > 0 ? constant.AtbFull : ArenaConstants.AtbFull;
            _mitigationK = constant.DamageMitigationK;
            _backBonusX100 = constant.BackDamageBonusX100;
            _flankBonusX100 = constant.FlankDamageBonusX100;
            _moveAtbCost = Math.Max(0, constant.MoveAtbCost);
            _jumpAtbCost = Math.Max(0, constant.JumpAtbCost);
            _meditateAtbCost = Math.Max(0, constant.MeditateAtbCost);
            _meditateNeiliRestore = Math.Max(0, constant.MeditateNeiliRestore);
            _defaultJumpCooldownTicks = Math.Max(0, constant.JumpCooldownTicks);

            _fighters = new ArenaFighter[MaxFighters];
            _idToIndex = new int[256];
            for (int i = 0; i < _idToIndex.Length; i++)
            {
                _idToIndex[i] = -1;
            }

            _skills = new ArenaSkillLoadout[MaxFighters * MaxSkillSlots];
            _skillStatusRule = new byte[MaxFighters * MaxSkillSlots];
            _skillStatusTicks = new int[MaxFighters * MaxSkillSlots];
            _skillCount = new int[MaxFighters];
            _skillUses = new int[MaxFighters * MaxSkillSlots];
            _skillHits = new int[MaxFighters * MaxSkillSlots];
            _cooldowns = new ushort[MaxFighters * MaxSkillSlots];
            _paths = new GridCoord[MaxFighters * MaxGridTiles];
            _statusLeft = new int[MaxFighters * StatusSlots];
            _statusTimer = new int[MaxFighters * StatusSlots];

            _statusRules = new StatusRule[config.Statuses.Count];
            _statusRuleByKind = new byte[StatusSlots];
            for (int i = 0; i < _statusRuleByKind.Length; i++)
            {
                _statusRuleByKind[i] = NoStatus;
            }

            ReadStatusRules(config);

            _tileBuffer = new GridCoord[MaxGridTiles];
            _pathBuffer = new GridCoord[MaxGridTiles];
            _areaBuffer = new GridCoord[MaxGridTiles];

            _grid = BuildGrid(config, setup.ArenaMapId);
            _frame = new ArenaFrameBuffer();
            _liveFrame = new ArenaFrameBuffer();
            _ring = new EventRing<ArenaEvent>(ArenaConstants.EventRingCapacity);
            _query = new ArenaSimulationQuery(this, true);
            _aiQuery = new ArenaSimulationQuery(this, false);
            _random = new Xoshiro128(RandomStreams.DeriveStreamSeed(
                unchecked((uint)(setup.Seed ^ (setup.Seed >> 32))), RandomStreamKind.Encounter));

            _phase = ArenaPhase.Intro;
            _playerFighter = ArenaConstants.NoFighter;

            for (int i = 0; i < _fighterCount; i++)
            {
                ArenaFighterSetup source = setup.Fighters[i];
                if (source is null)
                {
                    throw new ArgumentException("Fighter " + i + " is missing.", nameof(setup));
                }

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

                _idToIndex[source.Id] = i;
                _skillCount[i] = skillCount;

                ref ArenaFighter fighter = ref _fighters[i];
                ApplySetup(ref fighter, source, i);

                for (int slot = 0; slot < skillCount; slot++)
                {
                    int offset = (i * MaxSkillSlots) + slot;
                    ArenaSkillLoadout skill = source.Skills![slot];
                    _skills[offset] = skill;
                    _skillStatusRule[offset] = ResolveStatusRule(skill.StatusId);
                    _skillStatusTicks[offset] = Math.Max(0, skill.StatusDurationTicks);
                }

                if (source.PlayerControlled && _playerFighter == ArenaConstants.NoFighter)
                {
                    _playerFighter = source.Id;
                }
            }

            _playerTeam = _playerFighter != ArenaConstants.NoFighter
                ? _fighters[_idToIndex[_playerFighter]].Team
                : _fighters[0].Team;

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

        /// <summary>How many fighters the match started with.</summary>
        public int FighterCount => _fighterCount;

        /// <summary>Grid width in tiles, from the <c>arena_map</c> row.</summary>
        public int Width => _grid.Width;

        /// <summary>Grid height in tiles, from the <c>arena_map</c> row.</summary>
        public int Height => _grid.Height;

        /// <inheritdoc />
        public void Step()
        {
            ThrowIfDisposed();

            if (_phase == ArenaPhase.Finished || _phase == ArenaPhase.AwaitingPlayer)
            {
                return;
            }

            _tick++;

            if (_phase == ArenaPhase.Intro)
            {
                if (_tick >= _introTicks)
                {
                    _phase = ArenaPhase.Running;
                    _elapsedTicks = 0;
                    Emit(ArenaEventType.MatchStarted, ArenaConstants.NoFighter, ArenaConstants.NoFighter, 0,
                        0, 0, GridCoord.Invalid);
                }

                CapturePreviousPositions();
                PublishFrame();
                return;
            }

            _elapsedTicks++;
            CapturePreviousPositions();

            FillAtb();
            if (_phase == ArenaPhase.Finished)
            {
                PublishFrame();
                return;
            }

            if (_mode == AtbMode.Wait && TryEnterAwaitingPlayer())
            {
                PublishFrame();
                return;
            }

            AdvanceActions();
            TickStatuses();
            RegenerateNeili();

            if (CheckEnd())
            {
                PublishFrame();
                return;
            }

            RunAi();
            if (_phase == ArenaPhase.Finished)
            {
                PublishFrame();
                return;
            }

            EmitTimeWarning();
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

            Emit(ArenaEventType.CommandAccepted, command.Actor, ArenaConstants.NoFighter, 0,
                (int)command.Kind, command.Slot, command.Target);

            ApplyCommand(_idToIndex[command.Actor], in command);

            if (_phase == ArenaPhase.AwaitingPlayer)
            {
                _phase = ArenaPhase.Running;
                _awaitingFighter = ArenaConstants.NoFighter;
            }

            CheckEnd();
            RefreshCurrentFrame();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Exactly what <see cref="Submit"/> would answer, without changing match state.
        /// </summary>
        /// <remarks>The checks run in the same order in both paths, which is what the contract suite checks.</remarks>
        public CommandResult Validate(in ArenaCommand command) => ValidateCommand(in command, true);

        /// <summary>
        /// The same validation with the player-control check selectable. The public query
        /// enforces it (that is what the contract suite checks against <see cref="Submit"/>);
        /// the AI's own query skips it, because every one of its fighters is AI-controlled by
        /// definition and a hook that could not validate its own play could not choose it.
        /// </summary>
        internal CommandResult ValidateForController(in ArenaCommand command, bool enforcePlayerControl) =>
            ValidateCommand(in command, enforcePlayerControl);

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
            hash = Mix(hash, _grid.TileCount);

            if (_result is not null)
            {
                hash = Mix(hash, (int)_result.Outcome);
                hash = Mix(hash, (int)_result.Reason);
                hash = Mix(hash, _result.WinnerTeam);
                hash = Mix(hash, _result.DurationTicks);
            }

            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
                hash = Mix(hash, fighter.Id);
                hash = Mix(hash, fighter.Team);
                hash = Mix(hash, (int)fighter.State);
                hash = Mix(hash, (int)fighter.Facing);
                hash = Mix(hash, fighter.Tile.X);
                hash = Mix(hash, fighter.Tile.Y);
                hash = Mix(hash, fighter.LandingTile.X);
                hash = Mix(hash, fighter.LandingTile.Y);
                hash = Mix(hash, fighter.Hp);
                hash = Mix(hash, fighter.Neili);
                hash = Mix(hash, fighter.Atb);
                hash = Mix(hash, fighter.AtbCarry);
                hash = Mix(hash, fighter.NeiliCarry);
                hash = Mix(hash, fighter.StatusMask);
                hash = Mix(hash, fighter.JumpCooldown);
                hash = Mix(hash, fighter.PathIndex);
                hash = Mix(hash, fighter.PathCount);
                hash = Mix(hash, fighter.ActionTicksDone);
                hash = Mix(hash, (int)fighter.ActionTarget.X);
                hash = Mix(hash, (int)fighter.ActionTarget.Y);
                hash = Mix(hash, fighter.ActionTargetFighter);
                hash = Mix(hash, fighter.GodMode ? 1 : 0);
                hash = Mix(hash, fighter.DamageDealt);
                hash = Mix(hash, fighter.DamageTaken);

                int cooldownOffset = fighter.Id * MaxSkillSlots;
                int skillBase = i * MaxSkillSlots;
                for (int slot = 0; slot < MaxSkillSlots; slot++)
                {
                    hash = Mix(hash, _cooldowns[cooldownOffset + slot]);
                    hash = Mix(hash, _skillUses[skillBase + slot]);
                    hash = Mix(hash, _skillHits[skillBase + slot]);
                }

                for (int kind = 0; kind < StatusSlots; kind++)
                {
                    int statusIndex = (i * StatusSlots) + kind;
                    hash = Mix(hash, _statusLeft[statusIndex]);
                    hash = Mix(hash, _statusTimer[statusIndex]);
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
        public void Dispose() => _disposed = true;

        // ---------------------------------------------------------------- queries

        internal int WriteReachableTiles(byte fighter, Span<GridCoord> destination)
        {
            ThrowIfDisposed();
            if (!TryGetFighterIndex(fighter, out int index))
            {
                return 0;
            }

            ref ArenaFighter self = ref _fighters[index];
            if (self.Hp <= 0 || !IsIdle(in self))
            {
                return 0;
            }

            SyncOccupancy();

            // A walk, not a Manhattan disk: a fighter in the corridor is an obstacle.
            return ArenaPathfinder.GetReachable(_grid, self.Tile, self.MoveRange, destination);
        }

        internal int WriteJumpTiles(byte fighter, Span<GridCoord> destination)
        {
            ThrowIfDisposed();
            if (!TryGetFighterIndex(fighter, out int index))
            {
                return 0;
            }

            ref ArenaFighter self = ref _fighters[index];
            if (self.Hp <= 0 || !IsIdle(in self) || self.JumpCooldown > 0)
            {
                return 0;
            }

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

            ref ArenaFighter self = ref _fighters[index];
            if (self.Hp <= 0 || slot >= _skillCount[index])
            {
                return 0;
            }

            ArenaSkillLoadout skill = _skills[(index * MaxSkillSlots) + slot];
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
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var tile = new GridCoord(x, y);
                    int distance = self.Tile.Manhattan(tile);
                    if (distance < skill.RangeMin || distance > skill.RangeMax)
                    {
                        continue;
                    }

                    // Only tiles a command would accept: the highlight must not lie.
                    if (!_grid.IsWalkable(tile))
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

            ref ArenaFighter self = ref _fighters[index];
            if (slot >= _skillCount[index] || !_grid.IsWalkable(target) || destination.Length == 0)
            {
                return 0;
            }

            // The same call the resolve makes, so the preview is the truth rather than an
            // estimate: shape, size and the caster's facing all come from the loadout.
            ArenaSkillLoadout skill = _skills[(index * MaxSkillSlots) + slot];
            return ArenaArea.Resolve(
                skill.Area, target, ArenaFacing.Toward(self.Tile, target), skill.AreaSize, _grid, destination);
        }

        internal bool IsWalkableTile(GridCoord tile) => _grid.IsWalkable(tile);

        // ---------------------------------------------------------------- validation

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

            ref ArenaFighter fighter = ref _fighters[index];
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
                    return ValidateSkill(index, in fighter, command.Slot, command.Target);
                case ArenaCommandKind.Meditate:
                    return CommandResult.Ok;
                default:
                    return CommandResult.RejectedInvalidArg;
            }
        }

        private CommandResult ValidateMove(in ArenaFighter fighter, GridCoord target)
        {
            if (!_grid.IsWalkable(target))
            {
                return CommandResult.RejectedOutOfRange;
            }

            if (target == fighter.Tile)
            {
                return CommandResult.RejectedBlocked;
            }

            SyncOccupancy();

            if (ArenaPathfinder.FindPath(_grid, fighter.Tile, target, fighter.MoveRange, _pathBuffer) > 0)
            {
                return CommandResult.Ok;
            }

            return fighter.Tile.Manhattan(target) > fighter.MoveRange
                ? CommandResult.RejectedOutOfRange
                : CommandResult.RejectedBlocked;
        }

        private CommandResult ValidateJump(in ArenaFighter fighter, GridCoord target)
        {
            if (fighter.JumpCooldown > 0)
            {
                return CommandResult.RejectedOnCooldown;
            }

            if (!_grid.IsWalkable(target))
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

        private CommandResult ValidateSkill(int index, in ArenaFighter fighter, byte slot, GridCoord target)
        {
            if (slot >= _skillCount[index])
            {
                return CommandResult.RejectedInvalidArg;
            }

            ArenaSkillLoadout skill = _skills[(index * MaxSkillSlots) + slot];

            if (_cooldowns[(fighter.Id * MaxSkillSlots) + slot] > 0)
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
                    return TryFindOpponentAt(target, fighter.Team, out _)
                        ? CommandResult.Ok
                        : CommandResult.RejectedInvalidArg;
                default:
                    return CommandResult.Ok;
            }
        }

        private static bool IsReady(in ArenaFighter fighter) =>
            fighter.State == FighterState.Ready && fighter.Hp > 0;

        private static bool IsIdle(in ArenaFighter fighter) =>
            fighter.State == FighterState.Charging || fighter.State == FighterState.Ready;

        private static bool IsDevCommand(ArenaCommandKind kind) => (byte)kind >= 100;

        // ---------------------------------------------------------------- tick steps

        private void CapturePreviousPositions()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
                fighter.PrevX = fighter.X;
                fighter.PrevY = fighter.Y;
            }
        }

        /// <summary>
        /// Fills each charging fighter's bar. Statuses multiply the fill, which is how the
        /// acupoint seal stops it and the stagger slows it.
        /// </summary>
        private void FillAtb()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
                if (fighter.Hp <= 0 || fighter.State != FighterState.Charging)
                {
                    continue;
                }

                int multiplier = AtbMultiplier(i);
                if (multiplier <= 0)
                {
                    continue;
                }

                // Exact fixed-point fill: AtbFull per AtbFillTicks, with the remainder carried.
                fighter.AtbCarry += _atbFull;
                int delta = fighter.AtbCarry / fighter.AtbFillTicks;
                fighter.AtbCarry -= delta * fighter.AtbFillTicks;
                delta = (int)(((long)delta * multiplier) / 10000L);

                fighter.Atb += delta;
                if (fighter.Atb < _atbFull)
                {
                    continue;
                }

                fighter.Atb = _atbFull;
                fighter.State = FighterState.Ready;
                fighter.AiDelay = ComputeAiDelay(i);
                Emit(ArenaEventType.AtbReady, fighter.Id, fighter.Id, 0, fighter.Atb, 0, fighter.Tile);
            }
        }

        /// <summary>Progresses walks, jumps, wind-ups and recoveries. At most one step each per tick.</summary>
        private void AdvanceActions()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
                if (fighter.Hp <= 0)
                {
                    continue;
                }

                switch (fighter.State)
                {
                    case FighterState.Moving:
                        StepMove(i, ref fighter);
                        break;
                    case FighterState.Jumping:
                        StepJump(i, ref fighter);
                        break;
                    case FighterState.WindUp:
                        if (--fighter.ActionTicksLeft <= 0)
                        {
                            ResolveSkill(i);
                        }
                        else
                        {
                            SetProgress(ref fighter);
                        }

                        break;
                    case FighterState.Recovery:
                        if (--fighter.ActionTicksLeft <= 0)
                        {
                            fighter.State = FighterState.Charging;
                            fighter.ActiveSkillId = 0;
                            fighter.ActionProgressPermille = 0;
                        }
                        else
                        {
                            SetProgress(ref fighter);
                        }

                        break;
                }
            }
        }

        private void StepMove(int index, ref ArenaFighter fighter)
        {
            int pathOffset = fighter.PathBase + fighter.PathIndex;
            GridCoord from = fighter.Tile;
            GridCoord to = _paths[pathOffset];

            fighter.Tile = to;
            fighter.Facing = ArenaFacing.AfterStep(from, to);
            fighter.X = to.X;
            fighter.Y = to.Y;
            fighter.PathIndex++;

            if (fighter.PathIndex >= fighter.PathCount)
            {
                fighter.State = FighterState.Charging;
                fighter.ActionProgressPermille = 0;
                fighter.PathCount = 0;
                fighter.PathIndex = 0;
                fighter.ActiveSkillId = 0;
                Emit(ArenaEventType.MoveEnded, fighter.Id, ArenaConstants.NoFighter, 0,
                    from.Manhattan(to), 0, fighter.Tile);
                return;
            }

            fighter.ActionProgressPermille = (ushort)(((long)fighter.PathIndex * 1000L) / fighter.PathCount);
        }

        private void StepJump(int index, ref ArenaFighter fighter)
        {
            fighter.ActionTicksDone++;
            int total = Math.Max(1, fighter.ActionTicksTotal);
            int pitch = (int)(((long)fighter.ActionTicksDone * 1000L) / total);
            fighter.ActionProgressPermille = (ushort)Math.Min(1000, pitch);

            float progress = Math.Min(1f, (float)fighter.ActionTicksDone / total);
            fighter.X = fighter.ActionStartTile.X + ((fighter.LandingTile.X - fighter.ActionStartTile.X) * progress);
            fighter.Y = fighter.ActionStartTile.Y + ((fighter.LandingTile.Y - fighter.ActionStartTile.Y) * progress);
            fighter.Height = 4f * progress * (1f - progress);

            if (fighter.ActionTicksDone < total)
            {
                return;
            }

            fighter.Tile = fighter.LandingTile;
            fighter.X = fighter.LandingTile.X;
            fighter.Y = fighter.LandingTile.Y;
            fighter.Height = 0f;
            fighter.State = FighterState.Charging;
            fighter.ActionProgressPermille = 0;
            fighter.ActiveSkillId = 0;
            Emit(ArenaEventType.Landed, fighter.Id, ArenaConstants.NoFighter, 0,
                fighter.ActionStartTile.Manhattan(fighter.LandingTile), 0, fighter.LandingTile);
        }

        private static void SetProgress(ref ArenaFighter fighter)
        {
            int total = Math.Max(1, fighter.ActionTicksTotal);
            int done = fighter.ActionTicksTotal - fighter.ActionTicksLeft;
            int permille = (int)(((long)done * 1000L) / total);
            fighter.ActionProgressPermille = (ushort)Math.Clamp(permille, 0, 1000);
        }

        /// <summary>Applies each active status: duration, damage over time, expiry.</summary>
        private void TickStatuses()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
                if (fighter.Hp <= 0 || fighter.StatusMask == 0)
                {
                    continue;
                }

                for (int kind = 0; kind < StatusSlots; kind++)
                {
                    int bit = 1 << kind;
                    if ((fighter.StatusMask & bit) == 0)
                    {
                        continue;
                    }

                    int statusIndex = (i * StatusSlots) + kind;
                    byte ruleIndex = _statusRuleByKind[kind];
                    if (ruleIndex != NoStatus)
                    {
                        StatusRule rule = _statusRules[ruleIndex];
                        if (rule.DamagePerTick > 0 && rule.TickIntervalTicks > 0)
                        {
                            _statusTimer[statusIndex]++;
                            if (_statusTimer[statusIndex] >= rule.TickIntervalTicks)
                            {
                                _statusTimer[statusIndex] = 0;
                                ApplyStatusDamage(ref fighter, rule);
                                if (fighter.Hp <= 0)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    _statusLeft[statusIndex]--;
                    if (_statusLeft[statusIndex] > 0)
                    {
                        continue;
                    }

                    _statusLeft[statusIndex] = 0;
                    _statusTimer[statusIndex] = 0;
                    fighter.StatusMask = (ushort)(fighter.StatusMask & ~bit);
                    Emit(ArenaEventType.StatusExpired, ArenaConstants.NoFighter, fighter.Id,
                        ruleIndex == NoStatus ? 0 : _statusRules[ruleIndex].Id, 0, 0, fighter.Tile);
                }
            }
        }

        private void ApplyStatusDamage(ref ArenaFighter fighter, in StatusRule rule)
        {
            int damage = fighter.GodMode ? 0 : rule.DamagePerTick;
            if (damage <= 0)
            {
                return;
            }

            fighter.Hp = Math.Max(0, fighter.Hp - damage);
            fighter.DamageTaken += damage;
            Emit(ArenaEventType.Hit, ArenaConstants.NoFighter, fighter.Id, rule.Id, damage, fighter.Hp,
                fighter.Tile, ArenaEventFlags.Inner);

            if (fighter.Hp > 0)
            {
                return;
            }

            fighter.State = FighterState.Defeated;
            fighter.Atb = 0;
            fighter.ActiveSkillId = 0;
            fighter.ActionProgressPermille = 0;
            Emit(ArenaEventType.FighterDefeated, ArenaConstants.NoFighter, fighter.Id, 0, 0, 0, fighter.Tile);
        }

        private void RegenerateNeili()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
                if (fighter.Hp <= 0 || fighter.MaxNeili <= 0 || fighter.Neili >= fighter.MaxNeili)
                {
                    continue;
                }

                int multiplier = NeiliRegenMultiplier(i);
                int perSecondX100 = (int)(((long)fighter.NeiliRegenPerSecX100 * multiplier) / 10000L);
                fighter.NeiliCarry += perSecondX100;

                // NeiliCarry is in 1/(TickRate * 100) of a point of inner power.
                int unit = ArenaConstants.TickRate * 100;
                int gain = fighter.NeiliCarry / unit;
                fighter.NeiliCarry -= gain * unit;

                if (gain <= 0)
                {
                    continue;
                }

                int before = fighter.Neili;
                fighter.Neili = Math.Min(fighter.MaxNeili, fighter.Neili + gain);
                if (fighter.Neili != before)
                {
                    Emit(ArenaEventType.NeiliRecovered, fighter.Id, fighter.Id, 0,
                        fighter.Neili - before, fighter.Neili, fighter.Tile);
                }
            }
        }

        private int AtbMultiplier(int index)
        {
            ref ArenaFighter fighter = ref _fighters[index];
            if (fighter.StatusMask == 0)
            {
                return 10000;
            }

            int multiplier = 10000;
            for (int kind = 0; kind < StatusSlots; kind++)
            {
                int bit = 1 << kind;
                if ((fighter.StatusMask & bit) == 0)
                {
                    continue;
                }

                byte ruleIndex = _statusRuleByKind[kind];
                if (ruleIndex != NoStatus)
                {
                    multiplier = (int)(((long)multiplier * Math.Max(0, _statusRules[ruleIndex].AtbMultiplierX100)) / 10000L);
                }
            }

            return multiplier;
        }

        private int NeiliRegenMultiplier(int index)
        {
            ref ArenaFighter fighter = ref _fighters[index];
            if (fighter.StatusMask == 0)
            {
                return 10000;
            }

            int multiplier = 10000;
            for (int kind = 0; kind < StatusSlots; kind++)
            {
                int bit = 1 << kind;
                if ((fighter.StatusMask & bit) == 0)
                {
                    continue;
                }

                byte ruleIndex = _statusRuleByKind[kind];
                if (ruleIndex != NoStatus)
                {
                    multiplier = (int)(((long)multiplier
                        * Math.Max(0, _statusRules[ruleIndex].NeiliRegenMultiplierX100)) / 10000L);
                }
            }

            return multiplier;
        }

        private bool TryEnterAwaitingPlayer()
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
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

            int warningTick = _timeLimitTicks - TimeWarningTicks;
            if (warningTick > 0 && _elapsedTicks >= warningTick)
            {
                _timeWarningSent = true;
                Emit(ArenaEventType.TimeWarning, ArenaConstants.NoFighter, ArenaConstants.NoFighter, 0,
                    _timeLimitTicks - _elapsedTicks, 0, GridCoord.Invalid);
            }
        }

        // ---------------------------------------------------------------- commands

        private void ApplyCommand(int index, in ArenaCommand command)
        {
            if (index < 0 || index >= _fighterCount)
            {
                return;
            }

            ref ArenaFighter fighter = ref _fighters[index];

            switch (command.Kind)
            {
                case ArenaCommandKind.Move:
                    BeginMove(index, ref fighter, command.Target);
                    break;
                case ArenaCommandKind.Jump:
                    BeginJump(index, ref fighter, command.Target);
                    break;
                case ArenaCommandKind.UseSkill:
                    BeginSkill(index, ref fighter, command.Slot, command.Target);
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
                    fighter.Atb = _atbFull;
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
        }

        private void BeginMove(int index, ref ArenaFighter fighter, GridCoord target)
        {
            Span<GridCoord> path = new Span<GridCoord>(_paths, index * MaxGridTiles, MaxGridTiles);
            int steps = ArenaPathfinder.FindPath(_grid, fighter.Tile, target, fighter.MoveRange, path);
            if (steps <= 0)
            {
                // Validation said yes, so this is unreachable unless the board changed under
                // us; give the turn back rather than start a walk with no steps.
                fighter.State = FighterState.Charging;
                return;
            }

            fighter.PathBase = index * MaxGridTiles;
            fighter.PathCount = steps;
            fighter.PathIndex = 0;
            fighter.State = FighterState.Moving;
            fighter.ActiveSkillId = 0;
            fighter.ActionTicksTotal = steps;
            fighter.ActionTicksLeft = steps;
            fighter.ActionProgressPermille = 0;
            fighter.Atb = Math.Max(0, fighter.Atb - _moveAtbCost);

            Emit(ArenaEventType.MoveStarted, fighter.Id, ArenaConstants.NoFighter, 0, steps, 0, target);
        }

        private void BeginJump(int index, ref ArenaFighter fighter, GridCoord target)
        {
            int distance = fighter.Tile.Manhattan(target);
            fighter.ActionStartTile = fighter.Tile;
            fighter.LandingTile = target;
            fighter.ActionTicksTotal = Math.Max(1, distance);
            fighter.ActionTicksDone = 0;
            fighter.ActionTicksLeft = fighter.ActionTicksTotal;
            fighter.JumpCooldown = fighter.JumpCooldownTicks;
            fighter.State = FighterState.Jumping;
            fighter.ActiveSkillId = 0;
            fighter.ActionProgressPermille = 0;
            fighter.Facing = ArenaFacing.Toward(fighter.Tile, target);
            fighter.Atb = Math.Max(0, fighter.Atb - _jumpAtbCost);

            Emit(ArenaEventType.JumpStarted, fighter.Id, ArenaConstants.NoFighter, 0, distance, 0, target);
        }

        private void BeginSkill(int index, ref ArenaFighter fighter, byte slot, GridCoord target)
        {
            ArenaSkillLoadout skill = _skills[(index * MaxSkillSlots) + slot];
            int offset = (fighter.Id * MaxSkillSlots) + slot;

            fighter.Neili = Math.Max(0, fighter.Neili - skill.NeiliCost);
            fighter.Atb = 0;
            fighter.ActiveSkillId = skill.SkillId;
            fighter.ActionSlot = slot;
            fighter.ActionTarget = target;
            fighter.ActionTicksTotal = ScaleTicks(skill.WindUpTicks, fighter.ActionSpeedX100);
            fighter.ActionTicksLeft = fighter.ActionTicksTotal;
            fighter.ActionTicksDone = 0;
            fighter.State = FighterState.WindUp;
            fighter.ActionProgressPermille = 0;

            _skillUses[(index * MaxSkillSlots) + slot]++;
            _cooldowns[offset] = (ushort)Math.Min(ushort.MaxValue, Math.Max(0, skill.CooldownTicks));

            byte targetId = TryFindOpponentAt(target, fighter.Team, out int targetIndex)
                ? _fighters[targetIndex].Id
                : ArenaConstants.NoFighter;

            fighter.ActionTargetFighter = targetId;

            Emit(ArenaEventType.SkillWindUp, fighter.Id, targetId, skill.SkillId, slot,
                fighter.ActionTicksTotal, target);

            if (fighter.ActionTicksTotal == 0)
            {
                ResolveSkill(index);
            }
        }

        /// <summary>
        /// Resolves a wind-up: the cut-in, then every fighter standing in the area, then
        /// the recovery. A fighter that left the area — by jumping, or by landing outside
        /// it — is a miss, which is the dodge the telegraph promises.
        /// </summary>
        private void ResolveSkill(int index)
        {
            ref ArenaFighter caster = ref _fighters[index];
            ArenaSkillLoadout skill = _skills[(index * MaxSkillSlots) + caster.ActionSlot];
            GridCoord target = caster.ActionTarget;

            ArenaEventFlags flags = ArenaEventFlags.None;
            if (skill.Kind == DamageKind.Inner)
            {
                flags |= ArenaEventFlags.Inner;
            }

            if ((skill.Flags & SkillFlags.Ultimate) != 0)
            {
                flags |= ArenaEventFlags.Ultimate;
            }

            // The fighter the wind-up was aimed at, remembered from the moment the telegraph
            // went up: it is the one whose dodge must be reported, even if it has left the
            // aimed tile by now.
            byte aimedId = caster.ActionTargetFighter;
            int aimedIndex = aimedId != ArenaConstants.NoFighter ? _idToIndex[aimedId] : -1;
            if (aimedIndex >= _fighterCount)
            {
                aimedIndex = -1;
            }

            Emit(ArenaEventType.SkillCast, caster.Id, aimedId, skill.SkillId, skill.BaseDamage, 0, target, flags);

            // The same geometry the player saw as a telegraph (PreviewSkillArea).
            int areaCount = ArenaArea.Resolve(
                skill.Area, target, ArenaFacing.Toward(caster.Tile, target), skill.AreaSize, _grid, _areaBuffer);

            int hitMask = 0;
            bool aimedHit = false;

            for (int i = 0; i < areaCount; i++)
            {
                if (!TryFindOpponentAt(_areaBuffer[i], caster.Team, out int victimIndex))
                {
                    continue;
                }

                int bit = 1 << victimIndex;
                if ((hitMask & bit) != 0)
                {
                    continue;
                }

                hitMask |= bit;
                if (victimIndex == aimedIndex)
                {
                    aimedHit = true;
                }

                ref ArenaFighter victim = ref _fighters[victimIndex];
                if (victim.State == FighterState.Jumping)
                {
                    // In the air: the i-frame. The jump itself is the dodge.
                    victim.Evades++;
                    Emit(ArenaEventType.Evaded, caster.Id, victim.Id, skill.SkillId, 0, victim.Hp,
                        victim.Tile, flags);
                    continue;
                }

                if (caster.Hp <= 0)
                {
                    continue;
                }

                ApplySkillHit(index, victimIndex, in skill, flags);
            }

            // The aim tile's fighter is gone from the area at resolve time: it walked or
            // landed out of the telegraph. Reported as the evade it is.
            if (!aimedHit && aimedIndex >= 0 && _fighters[aimedIndex].Hp > 0
                && _fighters[aimedIndex].State != FighterState.Jumping)
            {
                ref ArenaFighter dodger = ref _fighters[aimedIndex];
                dodger.Evades++;
                Emit(ArenaEventType.Evaded, caster.Id, dodger.Id, skill.SkillId, 0, dodger.Hp,
                    dodger.Tile, flags);
            }

            ref ArenaFighter after = ref _fighters[index];
            if (after.Hp <= 0)
            {
                return;
            }

            after.State = FighterState.Recovery;
            after.ActiveSkillId = skill.SkillId;
            after.ActionTicksTotal = ScaleTicks(skill.RecoveryTicks, after.ActionSpeedX100);
            after.ActionTicksLeft = after.ActionTicksTotal;
            after.ActionTicksDone = 0;
            after.ActionProgressPermille = 0;

            if (after.ActionTicksTotal == 0)
            {
                after.State = FighterState.Charging;
                after.ActiveSkillId = 0;
            }
        }

        private void ApplySkillHit(int attackerIndex, int victimIndex, in ArenaSkillLoadout skill,
            ArenaEventFlags flags)
        {
            ref ArenaFighter attacker = ref _fighters[attackerIndex];
            ref ArenaFighter victim = ref _fighters[victimIndex];

            int hits = Math.Max(1, skill.Hits);
            for (int hit = 0; hit < hits; hit++)
            {
                if (victim.Hp <= 0 || attacker.Hp <= 0)
                {
                    return;
                }

                int chance = Math.Clamp(attacker.AccuracyX100 - victim.EvasionX100, 0, 10000);
                int roll = _random.NextInt(0, 10000);
                if (roll >= chance)
                {
                    victim.Evades++;
                    Emit(ArenaEventType.Evaded, attacker.Id, victim.Id, skill.SkillId, 0, victim.Hp,
                        victim.Tile, flags);
                    continue;
                }

                bool critical = _random.NextInt(0, 10000) < Math.Max(0, attacker.CritChanceX100);

                RelativeAngle angle = ArenaFacing.Relative(victim.Tile, victim.Facing, attacker.Tile);
                ArenaEventFlags hitFlags = flags;
                if (critical)
                {
                    hitFlags |= ArenaEventFlags.Critical;
                }

                if (angle == RelativeAngle.Back)
                {
                    hitFlags |= ArenaEventFlags.Backstab;
                }
                else if (angle == RelativeAngle.Flank)
                {
                    hitFlags |= ArenaEventFlags.Flank;
                }

                int damage = ComputeDamage(in attacker, in victim, in skill, angle, critical);
                if (victim.GodMode)
                {
                    damage = 0;
                }

                victim.Hp = Math.Max(0, victim.Hp - damage);
                attacker.DamageDealt += damage;
                victim.DamageTaken += damage;
                _skillHits[(attackerIndex * MaxSkillSlots) + skill.Slot]++;

                Emit(ArenaEventType.Hit, attacker.Id, victim.Id, skill.SkillId, damage, victim.Hp,
                    victim.Tile, hitFlags);

                if (victim.Hp <= 0)
                {
                    Defeat(victimIndex);
                    return;
                }

                TryApplyStatus(attackerIndex, victimIndex, in skill);
                if (victim.Hp <= 0)
                {
                    return;
                }

                if (skill.KnockbackTiles > 0)
                {
                    Push(victimIndex, attacker.Tile, skill.KnockbackTiles);
                }

                if ((skill.Flags & SkillFlags.Uncounterable) == 0)
                {
                    TryCounter(victimIndex, attackerIndex);
                }
            }
        }

        /// <summary>
        /// The damage pipeline of [ADR 0114]: power, mastery, position, crit, mitigation,
        /// all integer, in that order.
        /// </summary>
        private int ComputeDamage(
            in ArenaFighter attacker, in ArenaFighter victim, in ArenaSkillLoadout skill,
            RelativeAngle angle, bool critical)
        {
            long power = skill.Kind == DamageKind.Inner ? attacker.InnerPower : attacker.ExternalPower;
            long raw = skill.BaseDamage + ((power * skill.RatioX100) / 10000L);
            if (raw <= 0)
            {
                return 0;
            }

            int mastery = skill.MasteryX100 > 0 ? skill.MasteryX100 : 10000;
            raw = (raw * mastery) / 10000L;

            if (angle == RelativeAngle.Back)
            {
                raw = (raw * (10000L + _backBonusX100)) / 10000L;
            }
            else if (angle == RelativeAngle.Flank)
            {
                raw = (raw * (10000L + _flankBonusX100)) / 10000L;
            }

            if (critical)
            {
                raw = (raw * Math.Max(1, attacker.CritDamageX100)) / 10000L;
            }

            if ((skill.Flags & SkillFlags.IgnoreGuard) == 0)
            {
                long defense = skill.Kind == DamageKind.Inner ? victim.InnerDefense : victim.Defense;
                if (defense > 0 && _mitigationK > 0)
                {
                    raw = (raw * _mitigationK) / (defense + _mitigationK);
                }
            }

            return raw > int.MaxValue ? int.MaxValue : (int)raw;
        }

        private void TryApplyStatus(int attackerIndex, int victimIndex, in ArenaSkillLoadout skill)
        {
            byte ruleIndex = _skillStatusRule[(attackerIndex * MaxSkillSlots) + skill.Slot];
            if (ruleIndex == NoStatus || skill.StatusChanceX100 <= 0)
            {
                return;
            }

            if (_random.NextInt(0, 10000) >= skill.StatusChanceX100)
            {
                return;
            }

            ref ArenaFighter victim = ref _fighters[victimIndex];
            if (_random.NextInt(0, 10000) < Math.Clamp(victim.StatusResistX100, 0, 10000))
            {
                return;
            }

            StatusRule rule = _statusRules[ruleIndex];
            int bit = 1 << rule.Kind;
            int duration = _skillStatusTicks[(attackerIndex * MaxSkillSlots) + skill.Slot];
            if (duration <= 0)
            {
                duration = rule.DurationTicks;
            }

            if (duration <= 0)
            {
                return;
            }

            int statusIndex = (victimIndex * StatusSlots) + rule.Kind;
            bool fresh = (victim.StatusMask & bit) == 0;
            victim.StatusMask = (ushort)(victim.StatusMask | bit);
            _statusLeft[statusIndex] = Math.Max(_statusLeft[statusIndex], duration);
            if (fresh)
            {
                _statusTimer[statusIndex] = 0;
            }

            Emit(ArenaEventType.StatusApplied, _fighters[attackerIndex].Id, victim.Id, rule.Id, duration, 0,
                victim.Tile, skill.Kind == DamageKind.Inner ? ArenaEventFlags.Inner : ArenaEventFlags.None);
        }

        /// <summary>
        /// Pushes the victim away along the dominant axis, one tile at a time, stopping at
        /// the grid edge, a wall or another fighter.
        /// </summary>
        private void Push(int victimIndex, GridCoord from, int tiles)
        {
            ref ArenaFighter victim = ref _fighters[victimIndex];
            int steps = tiles - Math.Max(0, victim.KnockbackResist);
            if (steps <= 0)
            {
                return;
            }

            GridCoord delta = ArenaFacing.Delta(
                ArenaFacing.FromDelta(victim.Tile.X - from.X, victim.Tile.Y - from.Y));

            SyncOccupancy();
            int pushed = 0;
            for (int i = 0; i < steps; i++)
            {
                var next = new GridCoord(victim.Tile.X + delta.X, victim.Tile.Y + delta.Y);
                if (!_grid.IsFree(next))
                {
                    break;
                }

                victim.Tile = next;
                victim.X = next.X;
                victim.Y = next.Y;
                pushed++;
            }

            if (pushed == 0)
            {
                return;
            }

            SyncOccupancy();
            Emit(ArenaEventType.KnockedBack, ArenaConstants.NoFighter, victim.Id, 0, pushed, 0, victim.Tile);
        }

        private void TryCounter(int defenderIndex, int attackerIndex)
        {
            ref ArenaFighter defender = ref _fighters[defenderIndex];
            if (defender.Hp <= 0 || _skillCount[defenderIndex] == 0)
            {
                return;
            }

            // The counter uses the defender's first real skill for its numbers; a loadout
            // with empty lower slots falls through to the first one it has.
            ArenaSkillLoadout counter = default;
            for (int slot = 0; slot < _skillCount[defenderIndex]; slot++)
            {
                if (_skills[(defenderIndex * MaxSkillSlots) + slot].SkillId != 0)
                {
                    counter = _skills[(defenderIndex * MaxSkillSlots) + slot];
                    break;
                }
            }

            if (counter.SkillId == 0)
            {
                return;
            }

            if (_random.NextInt(0, 10000) >= Math.Clamp(defender.CounterChanceX100, 0, 10000))
            {
                return;
            }

            int distance = defender.Tile.Manhattan(_fighters[attackerIndex].Tile);
            if (distance < counter.RangeMin || distance > counter.RangeMax)
            {
                return;
            }

            ref ArenaFighter attacker = ref _fighters[attackerIndex];
            RelativeAngle angle = ArenaFacing.Relative(attacker.Tile, attacker.Facing, defender.Tile);
            int damage = ComputeDamage(in defender, in attacker, in counter, angle, false);
            if (attacker.GodMode)
            {
                damage = 0;
            }

            attacker.Hp = Math.Max(0, attacker.Hp - damage);
            defender.DamageDealt += damage;
            attacker.DamageTaken += damage;
            defender.Counters++;

            ArenaEventFlags flags = counter.Kind == DamageKind.Inner ? ArenaEventFlags.Inner : ArenaEventFlags.None;
            Emit(ArenaEventType.Countered, defender.Id, attacker.Id, counter.SkillId, damage, attacker.Hp,
                attacker.Tile, flags);

            if (attacker.Hp <= 0)
            {
                Defeat(attackerIndex);
            }
        }

        private void Defeat(int index)
        {
            ref ArenaFighter fighter = ref _fighters[index];
            if (fighter.State == FighterState.Defeated)
            {
                return;
            }

            fighter.Hp = 0;
            fighter.State = FighterState.Defeated;
            fighter.Atb = 0;
            fighter.ActiveSkillId = 0;
            fighter.ActionProgressPermille = 0;
            fighter.PathCount = 0;
            fighter.PathIndex = 0;
            Emit(ArenaEventType.FighterDefeated, ArenaConstants.NoFighter, fighter.Id, 0, 0, 0, fighter.Tile);
        }

        private void ApplyMeditate(ref ArenaFighter fighter)
        {
            int gain = Math.Min(_meditateNeiliRestore, Math.Max(0, fighter.MaxNeili - fighter.Neili));
            fighter.Neili += gain;
            fighter.Atb = Math.Max(0, fighter.Atb - _meditateAtbCost);
            fighter.State = FighterState.Charging;
            fighter.ActiveSkillId = 0;
            fighter.ActionProgressPermille = 0;

            Emit(ArenaEventType.NeiliRecovered, fighter.Id, fighter.Id, 0, gain, fighter.Neili, fighter.Tile);
        }

        private void ApplyDevKillEnemies(in ArenaFighter actor)
        {
            for (int i = 0; i < _fighterCount; i++)
            {
                if (_fighters[i].Team == actor.Team || _fighters[i].Hp <= 0)
                {
                    continue;
                }

                Defeat(i);
            }
        }

        // ---------------------------------------------------------------- AI

        private int ComputeAiDelay(int index)
        {
            ref ArenaFighter fighter = ref _fighters[index];
            if (fighter.PlayerControlled)
            {
                return fighter.AiReactionTicks;
            }

            AiProfileConfig? profile = null;
            for (int i = 0; i < _aiProfiles.Length; i++)
            {
                if (StatGrowth.StableId(_aiProfiles[i].Id) == fighter.AiProfileId)
                {
                    profile = _aiProfiles[i];
                    break;
                }
            }

            if (profile is null)
            {
                return fighter.AiReactionTicks;
            }

            long delayScale = 10000L - (long)Math.Max(0, fighter.Decisiveness) * profile.DecisivenessDelayX100;
            delayScale -= (long)Math.Max(0, fighter.PowerRank - 1) * profile.DifficultyDelayX100;
            delayScale = Math.Clamp(delayScale, 0L, 10000L);
            return (int)((long)profile.DecisionDelayTicks * delayScale / 10000L);
        }

        private void RunAi()
        {
            if (_fighterCount < 2)
            {
                return;
            }

            PublishLiveFrame();

            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
                if (fighter.PlayerControlled || fighter.Hp <= 0 || fighter.State != FighterState.Ready)
                {
                    continue;
                }

                if (fighter.AiDelay > 0)
                {
                    fighter.AiDelay--;
                    continue;
                }

                ArenaCommand command;
                if (!_ai.TryChoose(_liveFrame, _aiQuery, _random, fighter.Id, out command)
                    || ValidateCommand(in command, false) != CommandResult.Ok)
                {
                    // A hook that cannot find a play does not stall the match: meditating is
                    // always legal for a ready fighter and gives the turn back honestly.
                    command = ArenaCommand.Meditate(fighter.Id);
                    if (ValidateCommand(in command, false) != CommandResult.Ok)
                    {
                        continue;
                    }
                }

                Emit(ArenaEventType.CommandAccepted, command.Actor, ArenaConstants.NoFighter, 0,
                    (int)command.Kind, command.Slot, command.Target);
                ApplyCommand(i, in command);

                if (_phase == ArenaPhase.Finished)
                {
                    return;
                }

                if (CheckEnd())
                {
                    return;
                }

                // The next ready fighter decides after this one has already acted, so the
                // frame it is handed is refreshed in place. A stale frame here made two
                // fighters with the same reach walk into each other's old tile forever.
                RefreshLiveFighter(i);
            }
        }

        /// <summary>Rewrites one fighter in the live frame used by the AI decisions of this tick.</summary>
        private void RefreshLiveFighter(int index)
        {
            _liveFrame.UpdateCurrent(index, BuildView(index));
            _liveFrame.UpdateCurrentCooldowns(_fighters[index].Id, CooldownSpan(index));
        }

        // ---------------------------------------------------------------- endings

        private bool CheckEnd()
        {
            if (_phase == ArenaPhase.Finished)
            {
                return true;
            }

            bool alliesAlive = false;
            bool othersAlive = false;
            for (int i = 0; i < _fighterCount; i++)
            {
                if (_fighters[i].Hp <= 0)
                {
                    continue;
                }

                if (_fighters[i].Team == _playerTeam)
                {
                    alliesAlive = true;
                }
                else
                {
                    othersAlive = true;
                }
            }

            if (!alliesAlive && !othersAlive)
            {
                Finish(ArenaOutcome.Draw, ArenaEndReason.AllAlliesDefeated, ArenaConstants.NoFighter);
                return true;
            }

            if (!alliesAlive)
            {
                Finish(ArenaOutcome.Defeat, ArenaEndReason.AllAlliesDefeated, FirstOpposingTeam());
                return true;
            }

            if (!othersAlive)
            {
                Finish(ArenaOutcome.Victory, ArenaEndReason.AllEnemiesDefeated, _playerTeam);
                return true;
            }

            if (_timeLimitTicks > 0 && _elapsedTicks >= _timeLimitTicks)
            {
                FinishTimeout();
                return true;
            }

            return false;
        }

        private void FinishTimeout()
        {
            long playerHp = 0;
            long playerMax = 0;
            long otherHp = 0;
            long otherMax = 0;

            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
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

            // The caller publishes: Finish is reached from Step (exactly one publish) and from
            // Submit (a refresh in place). Publishing here would swap the slots twice a tick.
            Emit(ArenaEventType.MatchEnded, ArenaConstants.NoFighter, ArenaConstants.NoFighter, 0,
                (int)reason, winnerTeam, GridCoord.Invalid);
        }

        private ArenaMatchResult BuildResult(ArenaOutcome outcome, ArenaEndReason reason, byte winnerTeam)
        {
            var stats = new FighterMatchStat[_fighterCount];
            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
                int skillBase = i * MaxSkillSlots;

                int used = 0;
                for (int slot = 0; slot < _skillCount[i]; slot++)
                {
                    if (_skillUses[skillBase + slot] > 0)
                    {
                        used++;
                    }
                }

                var skillUses = new SkillUseStat[used];
                int next = 0;
                for (int slot = 0; slot < _skillCount[i]; slot++)
                {
                    if (_skillUses[skillBase + slot] <= 0)
                    {
                        continue;
                    }

                    skillUses[next] = new SkillUseStat(
                        _skills[skillBase + slot].SkillId,
                        _skillUses[skillBase + slot],
                        _skillHits[skillBase + slot]);
                    next++;
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

        /// <summary>
        /// Publishes the tick's state into the second buffer, which only the AI hook reads.
        /// The published frame belongs to the previous tick until the end of this one, and a
        /// decision must not be made on a stale board.
        /// </summary>
        private void PublishLiveFrame()
        {
            _liveFrame.BeginFrame();
            for (int i = 0; i < _fighterCount; i++)
            {
                _liveFrame.Add(BuildView(i));
            }

            for (int i = 0; i < _fighterCount; i++)
            {
                _liveFrame.SetCooldowns(_fighters[i].Id, CooldownSpan(i));
            }

            _liveFrame.SetClock(_tick, _elapsedTicks, _timeLimitTicks, _playerFighter);
            _liveFrame.Publish();
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
            ref ArenaFighter fighter = ref _fighters[index];
            return new FighterView
            {
                Id = fighter.Id,
                Team = fighter.Team,
                DefId = fighter.DefId,
                AiProfileId = fighter.AiProfileId,
                ActiveSkillSlot = fighter.ActionSlot,
                ActionTarget = fighter.ActionTarget,
                State = fighter.State,
                Facing = fighter.Facing,
                Tile = fighter.Tile,
                PrevX = fighter.PrevX,
                PrevY = fighter.PrevY,
                X = fighter.X,
                Y = fighter.Y,
                Height = fighter.Height,
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

        // ---------------------------------------------------------------- setup

        private void ApplySetup(ref ArenaFighter fighter, ArenaFighterSetup source, int index)
        {
            CombatStatBlock stats = source.Stats;

            fighter.Id = source.Id;
            fighter.Team = source.Team;
            fighter.DefId = source.DefId;
            fighter.PlayerControlled = source.PlayerControlled;
            fighter.AiProfileId = source.AiProfileId;
            fighter.AiReactionTicks = Math.Max(0, source.AiReactionTicks);
            fighter.PowerRank = source.PowerRank > 0 ? source.PowerRank : 1;
            fighter.Decisiveness = source.Decisiveness;
            fighter.AiDelay = fighter.AiReactionTicks;
            fighter.State = FighterState.Charging;
            fighter.Facing = source.StartFacing;
            fighter.Tile = source.StartTile;
            fighter.ActionStartTile = source.StartTile;
            fighter.LandingTile = source.StartTile;
            fighter.ActionTarget = GridCoord.Invalid;
            fighter.ActionTargetFighter = ArenaConstants.NoFighter;
            fighter.X = source.StartTile.X;
            fighter.Y = source.StartTile.Y;
            fighter.PrevX = fighter.X;
            fighter.PrevY = fighter.Y;

            fighter.MaxHp = Math.Max(1, stats.MaxHp);
            fighter.Hp = source.StartHp >= 0 ? Math.Min(source.StartHp, fighter.MaxHp) : fighter.MaxHp;
            fighter.MaxNeili = Math.Max(0, stats.MaxNeili);
            fighter.Neili = fighter.MaxNeili;

            fighter.AtbFillTicks = Math.Max(1, stats.AtbFillTicks);
            fighter.ActionSpeedX100 = stats.ActionSpeedX100 > 0 ? stats.ActionSpeedX100 : 10000;
            fighter.MoveRange = Math.Max(0, (int)stats.MoveRange);
            fighter.JumpRange = Math.Max(0, (int)stats.JumpRange);
            fighter.JumpCooldownTicks = stats.JumpCooldownTicks > 0
                ? stats.JumpCooldownTicks
                : _defaultJumpCooldownTicks;

            fighter.AccuracyX100 = stats.AccuracyX100;
            fighter.EvasionX100 = stats.EvasionX100;
            fighter.CritChanceX100 = stats.CritChanceX100;
            fighter.CritDamageX100 = stats.CritDamageX100;
            fighter.CounterChanceX100 = stats.CounterChanceX100;
            fighter.StatusResistX100 = stats.StatusResistX100;
            fighter.KnockbackResist = stats.KnockbackResist;
            fighter.ExternalPower = stats.ExternalPower;
            fighter.InnerPower = stats.InnerPower;
            fighter.Defense = stats.Defense;
            fighter.InnerDefense = stats.InnerDefense;
            fighter.NeiliRegenPerSecX100 = Math.Max(0, stats.NeiliRegenPerSecX100);

            fighter.PathBase = index * MaxGridTiles;
        }

        private void ReadStatusRules(IGameConfig config)
        {
            for (int i = 0; i < _statusRules.Length; i++)
            {
                StatusConfig row = config.Statuses[i];
                int kind = (int)row.Kind;
                var rule = new StatusRule
                {
                    Id = StatGrowth.StableId(row.Id),
                    Kind = kind,
                    DurationTicks = Math.Max(0, row.DurationTicks),
                    TickIntervalTicks = Math.Max(0, row.TickIntervalTicks),
                    DamagePerTick = Math.Max(0, row.DamagePerTick),
                    AtbMultiplierX100 = Math.Max(0, row.AtbMultiplierX100),
                    NeiliRegenMultiplierX100 = Math.Max(0, row.NeiliRegenMultiplierX100),
                };

                _statusRules[i] = rule;

                if (kind > 0 && kind < StatusSlots && _statusRuleByKind[kind] == NoStatus)
                {
                    _statusRuleByKind[kind] = (byte)i;
                }
            }
        }

        private byte ResolveStatusRule(int statusId)
        {
            if (statusId == 0)
            {
                return NoStatus;
            }

            for (int i = 0; i < _statusRules.Length; i++)
            {
                if (_statusRules[i].Id == statusId)
                {
                    return (byte)i;
                }
            }

            return NoStatus;
        }

        /// <summary>
        /// The board of the setup's <c>arena_map</c> row.
        /// </summary>
        /// <remarks>
        /// <see cref="ArenaMatchSetup.ArenaMapId"/> is 0 for the default map, otherwise the
        /// row's stable id (as Stage 05 writes skill and status ids) or its 1-based ordinal
        /// — see [ADR 0114](../../../../docs/adr/0114-arena-simulation-tick-resolve-and-damage.md) §6.
        /// </remarks>
        private static ArenaGrid BuildGrid(IGameConfig config, int arenaMapId)
        {
            IReadOnlyList<ArenaMapConfig> maps = config.ArenaMaps;
            if (maps.Count == 0)
            {
                throw new InvalidOperationException("The arena_map table is empty; a match needs a board.");
            }

            int chosen = 0;
            if (arenaMapId > 0)
            {
                int byStableId = -1;
                for (int i = 0; i < maps.Count; i++)
                {
                    if (StatGrowth.StableId(maps[i].Id) == arenaMapId)
                    {
                        byStableId = i;
                        break;
                    }
                }

                if (byStableId >= 0)
                {
                    chosen = byStableId;
                }
                else if (arenaMapId <= maps.Count)
                {
                    chosen = arenaMapId - 1;
                }
            }

            ArenaMapConfig row = maps[chosen];
            return ArenaGrid.FromMask(row.Width, row.Height, row.Mask);
        }

        // ---------------------------------------------------------------- helpers

        private void SyncOccupancy()
        {
            _grid.ClearOccupancy();
            for (int i = 0; i < _fighterCount; i++)
            {
                ref ArenaFighter fighter = ref _fighters[i];
                if (fighter.Hp <= 0)
                {
                    continue;
                }

                // A fighter in the air holds the tile it will land on, so nobody walks into it.
                if (fighter.State == FighterState.Jumping)
                {
                    _grid.TryOccupy(fighter.LandingTile, fighter.Id);
                    continue;
                }

                _grid.TryOccupy(fighter.Tile, fighter.Id);

                // A walker holds the rest of its route as well. Without this, two fighters
                // starting a walk in the same tick can be routed into the same tile — or
                // through each other — and end the tick standing on one square.
                if (fighter.State != FighterState.Moving)
                {
                    continue;
                }

                for (int step = fighter.PathIndex; step < fighter.PathCount; step++)
                {
                    _grid.TryOccupy(_paths[fighter.PathBase + step], fighter.Id);
                }
            }
        }

        private bool TryGetFighterIndex(byte id, out int index)
        {
            index = _idToIndex[id];
            return index >= 0 && index < _fighterCount;
        }

        private bool TryFindOpponentAt(GridCoord tile, byte team, out int index)
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

        private static int ScaleTicks(int baseTicks, int actionSpeedX100)
        {
            if (baseTicks <= 0)
            {
                return 0;
            }

            if (actionSpeedX100 <= 0)
            {
                return baseTicks;
            }

            int scaled = (int)(((long)baseTicks * 10000L) / actionSpeedX100);
            return scaled < 1 ? 1 : scaled;
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
                throw new ObjectDisposedException(nameof(ArenaSimulation));
            }
        }

        /// <summary>One <c>status</c> row, resolved once at construction.</summary>
        private struct StatusRule
        {
            public int Id;
            public int Kind;
            public int DurationTicks;
            public int TickIntervalTicks;
            public int DamagePerTick;
            public int AtbMultiplierX100;
            public int NeiliRegenMultiplierX100;
        }

        /// <summary>One fighter. An element of the session's fixed array (ADR 0101).</summary>
        private struct ArenaFighter
        {
            public byte Id;
            public byte Team;
            public int DefId;
            public bool PlayerControlled;
            public int AiProfileId;
            public int AiReactionTicks;
            public int AiDelay;
            public int PowerRank;
            public int Decisiveness;

            public FighterState State;
            public IsoDir Facing;
            public GridCoord Tile;
            public GridCoord ActionStartTile;
            public GridCoord LandingTile;
            public GridCoord ActionTarget;
            public byte ActionTargetFighter;
            public byte ActionSlot;
            public int ActiveSkillId;

            public int ActionTicksDone;
            public int ActionTicksTotal;
            public int ActionTicksLeft;
            public ushort ActionProgressPermille;

            public int PathBase;
            public int PathIndex;
            public int PathCount;

            public int Hp;
            public int MaxHp;
            public int Neili;
            public int MaxNeili;
            public int Atb;
            public int AtbCarry;
            public int NeiliCarry;
            public int AtbFillTicks;
            public int ActionSpeedX100;

            public int MoveRange;
            public int JumpRange;
            public int JumpCooldown;
            public int JumpCooldownTicks;

            public int AccuracyX100;
            public int EvasionX100;
            public int CritChanceX100;
            public int CritDamageX100;
            public int CounterChanceX100;
            public int StatusResistX100;
            public int KnockbackResist;
            public int ExternalPower;
            public int InnerPower;
            public int Defense;
            public int InnerDefense;
            public int NeiliRegenPerSecX100;

            public float PrevX;
            public float PrevY;
            public float X;
            public float Y;
            public float Height;

            public ushort StatusMask;
            public bool GodMode;
            public int DamageDealt;
            public int DamageTaken;
            public int Evades;
            public int Counters;
        }
    }
}
