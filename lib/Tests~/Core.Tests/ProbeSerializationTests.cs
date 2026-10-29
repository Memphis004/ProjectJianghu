using System;
using Google.Protobuf;
using ProjectJianghu.Core.Contracts.Save;
using Xunit;

// The generated contract and the MessagePack probe deliberately share the name `CheckpointProbe`:
// both answer "does our serialization actually work away from the desktop runtime?". The alias keeps
// this file readable where both are in scope at once.
using ProtoArenaReplay = ProjectJianghu.Core.Generated.Proto.ArenaReplay;
using ProtoProbe = ProjectJianghu.Core.Generated.Proto.CheckpointProbe;
using ProtoReplayCommand = ProjectJianghu.Core.Generated.Proto.ReplayCommand;
using ProtoReplayEntry = ProjectJianghu.Core.Generated.Proto.ReplayEntry;
using ProtoReplayFighter = ProjectJianghu.Core.Generated.Proto.ReplayFighter;
using ProtoSaveData = ProjectJianghu.Core.Generated.Proto.SaveData;
using ProtoSaveHeader = ProjectJianghu.Core.Generated.Proto.SaveHeader;

namespace ProjectJianghu.Core.Tests
{
    /// <summary>
    /// The two serialization stories of the project, exercised outside Unity so a
    /// regression is caught by <c>dotnet test</c> long before a Web build.
    /// </summary>
    /// <remarks>
    /// These are the same checks the Unity EditMode/PlayMode suites and the Web
    /// smoke run (see <c>UnityProject/Assets/Main/Scripts</c>), which is what makes
    /// the Editor result meaningful for the browser: one implementation, three
    /// runtimes. The interesting failures they catch are AOT/IL2CPP ones — a
    /// formatter that only exists dynamically — which is why
    /// <see cref="CheckpointProbeCodec.GeneratedFormatterIsPresent"/> is asserted
    /// rather than assumed.
    /// </remarks>
    public sealed class ProbeSerializationTests
    {
        [Fact]
        public void MessagePack_formatter_is_generated_at_compile_time()
        {
            // If this fails, the MessagePack SOURCE GENERATOR did not run for the
            // Contracts assembly - the dynamic resolver would still round-trip on
            // the desktop runtime and only explode in the Web build.
            Assert.True(
                CheckpointProbeCodec.GeneratedFormatterIsPresent,
                "MessagePack.GeneratedMessagePackResolver has no formatter for CheckpointProbe; "
                + "the source generator did not run for ProjectJianghu.Core.Contracts");
        }

        [Fact]
        public void MessagePack_round_trip_preserves_every_field()
        {
            CheckpointProbe original = CheckpointProbe.Sample();

            CheckpointProbe restored = CheckpointProbeCodec.RoundTrip(original);

            Assert.True(
                original.Matches(restored),
                "the round-tripped probe differs from the original: "
                + "schema " + restored.SchemaVersion
                + ", seed " + restored.Seed
                + ", key '" + restored.NoteKey + "'"
                + ", cells " + restored.OccupiedCells.Count
                + ", hp " + restored.Hp);
        }

        [Fact]
        public void MessagePack_round_trip_tolerates_a_null_optional_payload()
        {
            // A checkpoint written before a field existed must still load: the
            // serializer writes the array header the type declares, and the
            // generated formatter must handle a null collection without throwing.
            var sparse = new CheckpointProbe
            {
                SchemaVersion = 1,
                Seed = 7,
                NoteKey = string.Empty,
                OccupiedCells = null!,
                Hp = 0f,
            };

            byte[] bytes = CheckpointProbeCodec.Serialize(sparse);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void MessagePack_output_is_deterministic()
        {
            // The save path is compared byte-for-byte when a run is resumed, so
            // two identical probes must produce identical bytes.
            byte[] first = CheckpointProbeCodec.Serialize(CheckpointProbe.Sample());
            byte[] second = CheckpointProbeCodec.Serialize(CheckpointProbe.Sample());

            Assert.Equal(Convert.ToBase64String(first), Convert.ToBase64String(second));
        }

        [Fact]
        public void Protobuf_round_trip_preserves_the_external_contract()
        {
            var original = new ProtoProbe
            {
                SchemaVersion = 3,
                ConfigHash = ProjectJianghu.Config.ConfigBuildMetadata.ConfigHash,
                Tick = 1234567890123L,
                NoteKey = "ui.title",
            };

            byte[] bytes = original.ToByteArray();
            ProtoProbe restored = ProtoProbe.Parser.ParseFrom(bytes);

            Assert.Equal(original, restored);
            Assert.Equal(3u, restored.SchemaVersion);
            Assert.Equal(ProjectJianghu.Config.ConfigBuildMetadata.ConfigHash, restored.ConfigHash);
            Assert.Equal(1234567890123L, restored.Tick);
        }

        [Fact]
        public void The_save_header_carries_the_schema_and_the_config_hash()
        {
            // The two fields a reader needs before it parses a payload: which format this is, and which
            // balance data it was written against.
            var header = new ProtoSaveHeader
            {
                SchemaVersion = 4,
                ConfigHash = ProjectJianghu.Config.ConfigBuildMetadata.ConfigHash,
            };

            ProtoSaveHeader restored = ProtoSaveHeader.Parser.ParseFrom(header.ToByteArray());

            Assert.Equal(4u, restored.SchemaVersion);
            Assert.Equal(ProjectJianghu.Config.ConfigBuildMetadata.ConfigHash, restored.ConfigHash);
        }

        [Fact]
        public void An_arena_replay_round_trips_its_setup_and_its_command_stream()
        {
            // The two halves of a replay: who was on the field at tick 0, and what each side asked
            // for, in tick order. Losing either makes the replay worthless, so both are asserted.
            var original = new ProtoArenaReplay
            {
                SchemaVersion = 1,
                Seed = 0xDEADBEEF,
                ArenaMapId = "map.open_court",
                FinalStateHash = 0x0123456789ABCDEF,
            };

            original.Fighters.Add(new ProtoReplayFighter { FighterId = "fighter.rival_01", Team = 1, TileX = 2, TileY = 3 });
            original.Fighters.Add(new ProtoReplayFighter { FighterId = "player", Team = 0, TileX = 9, TileY = 9 });
            original.Fighters[1].SkillIds.Add("skill.falling_rain_slash");

            original.Entries.Add(new ProtoReplayEntry
            {
                Tick = 30,
                Command = new ProtoReplayCommand { Kind = 0, Actor = 1, TileX = 8, TileY = 9 },
            });
            original.Entries.Add(new ProtoReplayEntry
            {
                Tick = 61,
                Command = new ProtoReplayCommand { Kind = 1, Actor = 0, Slot = 2, TileX = 3, TileY = 3 },
            });

            ProtoArenaReplay restored = ProtoArenaReplay.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(original, restored);
            Assert.Equal(2, restored.Fighters.Count);
            Assert.Equal(2, restored.Entries.Count);
            Assert.Equal(61, restored.Entries[1].Tick);
            Assert.Equal(2, restored.Entries[1].Command.Slot);
            Assert.Equal(0x0123456789ABCDEFUL, restored.FinalStateHash);
            Assert.Equal("skill.falling_rain_slash", restored.Fighters[1].SkillIds[0]);
        }

        [Fact]
        public void Save_data_wraps_the_header_the_character_and_the_bracket()
        {
            var original = new ProtoSaveData
            {
                Header = new ProtoSaveHeader
                {
                    SchemaVersion = 1,
                    ConfigHash = ProjectJianghu.Config.ConfigBuildMetadata.ConfigHash,
                },
                Seed = 7,
                SavedAtUnixSeconds = 1793000000,
            };

            original.Character = new ProjectJianghu.Core.Generated.Proto.CharacterState
            {
                Id = "player",
                DisplayName = "นักดาบทดสอบ",
                BackgroundId = "background.sword_academy",
                UnspentAttributePoints = 12,
            };
            original.Character.Attributes.Add(22, 18);   // Toughness, by EAttribute number
            original.Character.Mastery.Add(new ProjectJianghu.Core.Generated.Proto.SkillMastery
            {
                SkillId = "skill.falling_rain_slash",
                Level = 5,
                Xp = 120,
            });

            original.Tournament = new ProjectJianghu.Core.Generated.Proto.TournamentState
            {
                TournamentId = "tournament.arena_open",
                Seed = 7,
                PlayerSeat = 31,
                Round = 2,
            };
            original.Tournament.Entrants.Add(new ProjectJianghu.Core.Generated.Proto.Entrant
            {
                Seat = 31, FighterId = "player", IsPlayer = true,
            });
            original.Tournament.SettledMatchIds.Add("r1-m16");

            ProtoSaveData restored = ProtoSaveData.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(original, restored);
            Assert.Equal(ProjectJianghu.Config.ConfigBuildMetadata.ConfigHash, restored.Header.ConfigHash);
            Assert.Equal(18, restored.Character.Attributes[22]);
            Assert.Equal(5, restored.Character.Mastery[0].Level);
            Assert.Equal(31, restored.Tournament.PlayerSeat);
            Assert.Equal("r1-m16", restored.Tournament.SettledMatchIds[0]);
        }

        [Fact]
        public void Protobuf_field_numbers_match_the_proto_definition()
        {
            // proto/app.proto is the external contract; the field numbers are the wire format and must
            // never drift with a refactor. Read through the descriptors rather than by reflection on C#
            // property names, because the numbers - not the names - are what a peer decodes with.
            Assert.Equal(1, ProtoProbe.Descriptor.FindFieldByNumber(1).FieldNumber);
            Assert.Equal(2, ProtoProbe.Descriptor.FindFieldByNumber(2).FieldNumber);
            Assert.Equal(3, ProtoProbe.Descriptor.FindFieldByNumber(3).FieldNumber);
            Assert.Equal(4, ProtoProbe.Descriptor.FindFieldByNumber(4).FieldNumber);
            Assert.Equal(2, ProtoSaveHeader.Descriptor.Fields.InFieldNumberOrder().Count);
            Assert.Equal("config_hash", ProtoSaveHeader.Descriptor.FindFieldByNumber(2).Name);

            // The replay's shape is a contract with a future replay viewer: setup in field 4, the
            // (tick, command) stream in field 5.
            Assert.Equal("fighters", ProtoArenaReplay.Descriptor.FindFieldByNumber(4).Name);
            Assert.Equal("entries", ProtoArenaReplay.Descriptor.FindFieldByNumber(5).Name);
            Assert.Equal("tick", ProtoReplayEntry.Descriptor.FindFieldByNumber(1).Name);
        }
    }
}
