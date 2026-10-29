using System;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>
    /// The concrete <see cref="IArenaFrame"/> shared by the fake session and the real
    /// simulation (Stage 07).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same two-slot pattern as <c>Shared.PrevCurBuffer&lt;T&gt;</c> (ADR 0012/0024),
    /// specialised for <see cref="FighterView"/> and with the cooldown table doubled in
    /// step. A tick is written into <c>BeginFrame</c>/<c>Add</c>/<c>Publish</c>; the
    /// published frame becomes current and the one before it becomes previous, so a
    /// reader that needs the tick before can see it without a copy.
    /// </para>
    /// <para>
    /// An accepted <see cref="IArenaSession.Submit"/> changes the state between ticks.
    /// <see cref="UpdateCurrent"/> rewrites the published slot in place instead of
    /// publishing a second time: two publishes in one tick would push the real previous
    /// tick out of the history and make the view interpolate from the wrong place.
    /// </para>
    /// <para>
    /// All storage is allocated once in the constructor. No method allocates and no
    /// method is thread-safe — one writer, many same-thread readers, like the rest of
    /// the arena.
    /// </para>
    /// </remarks>
    public sealed class ArenaFrameBuffer : IArenaFrame
    {
        private FighterView[] _current;
        private FighterView[] _previous;
        private FighterView[] _write;

        private ushort[] _cooldownCurrent;
        private ushort[] _cooldownPrevious;
        private ushort[] _cooldownWrite;

        private int _currentCount;
        private int _previousCount;
        private int _writeCount;

        private int _tick;
        private int _elapsedTicks;
        private int _timeLimitTicks;
        private byte _playerFighter = ArenaConstants.NoFighter;

        /// <summary>Creates a frame buffer with room for <see cref="ArenaConstants.MaxFighters"/>.</summary>
        public ArenaFrameBuffer()
        {
            _current = new FighterView[ArenaConstants.MaxFighters];
            _previous = new FighterView[ArenaConstants.MaxFighters];
            _write = new FighterView[ArenaConstants.MaxFighters];

            _cooldownCurrent = new ushort[ArenaConstants.MaxFighters * ArenaConstants.MaxSkillSlots];
            _cooldownPrevious = new ushort[ArenaConstants.MaxFighters * ArenaConstants.MaxSkillSlots];
            _cooldownWrite = new ushort[ArenaConstants.MaxFighters * ArenaConstants.MaxSkillSlots];
        }

        /// <summary>How many fighters the published frame exposes.</summary>
        public int CurrentCount => _currentCount;

        /// <summary>How many fighters the frame before it exposes.</summary>
        public int PreviousCount => _previousCount;

        /// <summary>The frame just published.</summary>
        public ReadOnlySpan<FighterView> Current => new ReadOnlySpan<FighterView>(_current, 0, _currentCount);

        /// <summary>The tick before <see cref="Current"/>.</summary>
        public ReadOnlySpan<FighterView> Previous => new ReadOnlySpan<FighterView>(_previous, 0, _previousCount);

        /// <inheritdoc />
        public int Tick => _tick;

        /// <inheritdoc />
        public int ElapsedTicks => _elapsedTicks;

        /// <inheritdoc />
        public int TimeLimitTicks => _timeLimitTicks;

        /// <inheritdoc />
        public byte PlayerFighter => _playerFighter;

        /// <inheritdoc />
        public ReadOnlySpan<FighterView> Fighters => Current;

        /// <inheritdoc />
        public ReadOnlySpan<ushort> SkillCooldowns(byte fighter)
        {
            if (fighter >= ArenaConstants.MaxFighters)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fighter), fighter, "fighter must be below ArenaConstants.MaxFighters.");
            }

            return new ReadOnlySpan<ushort>(
                _cooldownCurrent, fighter * ArenaConstants.MaxSkillSlots, ArenaConstants.MaxSkillSlots);
        }

        /// <summary>Starts a tick; the write cursor is rewound and the published frame is untouched.</summary>
        public void BeginFrame()
        {
            _writeCount = 0;
        }

        /// <summary>Appends one fighter to the frame being built, ignoring overflow.</summary>
        public void Add(in FighterView view)
        {
            if (_writeCount >= _write.Length)
            {
                return;
            }

            _write[_writeCount] = view;
            _writeCount++;
        }

        /// <summary>
        /// Writes a fighter's cooldown table into the frame being built. Exactly
        /// <see cref="ArenaConstants.MaxSkillSlots"/> values are always written; a
        /// shorter source is zero-padded, so an old value can never linger.
        /// </summary>
        public void SetCooldowns(byte fighter, ReadOnlySpan<ushort> cooldowns)
        {
            if (fighter >= ArenaConstants.MaxFighters)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fighter), fighter, "fighter must be below ArenaConstants.MaxFighters.");
            }

            int offset = fighter * ArenaConstants.MaxSkillSlots;
            for (int i = 0; i < ArenaConstants.MaxSkillSlots; i++)
            {
                _cooldownWrite[offset + i] = i < cooldowns.Length ? cooldowns[i] : (ushort)0;
            }
        }

        /// <summary>Sets the clock and player reference the next published frame carries.</summary>
        public void SetClock(int tick, int elapsedTicks, int timeLimitTicks, byte playerFighter)
        {
            _tick = tick;
            _elapsedTicks = elapsedTicks;
            _timeLimitTicks = timeLimitTicks;
            _playerFighter = playerFighter;
        }

        /// <summary>Swaps the built frame into the published slot. Nothing is copied.</summary>
        public void Publish()
        {
            FighterView[] oldPrevious = _previous;
            _previous = _current;
            _previousCount = _currentCount;
            _current = _write;
            _currentCount = _writeCount;
            _write = oldPrevious;
            _writeCount = 0;

            ushort[] oldCooldownPrevious = _cooldownPrevious;
            _cooldownPrevious = _cooldownCurrent;
            _cooldownCurrent = _cooldownWrite;
            _cooldownWrite = oldCooldownPrevious;
        }

        /// <summary>
        /// Rewrites one fighter in the published frame after a mid-tick command. The
        /// previous slot is deliberately not touched.
        /// </summary>
        public void UpdateCurrent(int index, in FighterView view)
        {
            if (index < 0 || index >= _currentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be inside the current frame.");
            }

            _current[index] = view;
        }

        /// <summary>
        /// Rewrites one fighter's cooldowns in the published frame after a mid-tick
        /// command. Exactly <see cref="ArenaConstants.MaxSkillSlots"/> values are written.
        /// </summary>
        public void UpdateCurrentCooldowns(byte fighter, ReadOnlySpan<ushort> cooldowns)
        {
            if (fighter >= ArenaConstants.MaxFighters)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fighter), fighter, "fighter must be below ArenaConstants.MaxFighters.");
            }

            int offset = fighter * ArenaConstants.MaxSkillSlots;
            for (int i = 0; i < ArenaConstants.MaxSkillSlots; i++)
            {
                _cooldownCurrent[offset + i] = i < cooldowns.Length ? cooldowns[i] : (ushort)0;
            }
        }
    }
}
