using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Audio;
using OpenIGI.Engine.Tasks;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2.Scripting;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>What the soldiers' guns sound like in a fight.</summary>
public sealed class Igi2SoldierFireSoundTests(ITestOutputHelper output)
{
    /// <summary>
    /// A burst starts the gun's loop and ends it with its tail, once: a soldier waiting out its fire delay lets go of a busy
    /// trigger, not of a trigger already let go every tick, which played the tail thirty times a second.
    /// </summary>
    [RequiresTitleDataFact("igi2")]
    public void A_burst_ends_with_one_tail()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = Igi2SoldierLevelTests.LoadMission(igi1, 1);
        Igi2GuestContent kit = Igi2GuestContent.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped.Models);
        Igi2HostMissionDevice scripts = new(missionDirectory, kit.Scripts);
        kit.Guest.FileSystem.RegisterDevice(scripts);
        HumanPlayerTask player = PlayerSpawn.FindGameplayTask(level)!;
        player.Body = new StandingBody(player.Position);
        Audio audio = new();
        SoldierTable seen = SoldierTable.Shipped with { Visibility = new Game.Igi2.Detection.Igi2VisibilityTuning(Minimum: 1f, Maximum: 1f) };
        Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, seen, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object()), player, AiDifficulty.Normal,
            new IgiRandom(), NullLogger.Instance, audio: audio, scripts: scripts, missionDirectory: missionDirectory);
        for (int tick = 0; tick < 450; tick++)
        {
            audio.Tick = tick;
            soldiers.Tick();
        }

        foreach (IGrouping<string, (int Tick, string Name, bool? Looping)> name in audio.Log.GroupBy(static e => e.Name).OrderByDescending(static g => g.Count()))
        {
            output.WriteLine($"{name.Count(),4} {name.Key} loop={name.First().Looping} ticks {string.Join(",", name.Take(25).Select(static e => e.Tick))}");
        }

        int loops = audio.Log.Count(static e => e.Name.EndsWith("_loop", StringComparison.Ordinal));
        int tails = audio.Log.Count(static e => e.Name.EndsWith("_loop_e", StringComparison.Ordinal));
        Assert.True(loops > 0, "no soldier fired a burst");
        Assert.InRange(tails, 1, loops);
    }

    private sealed class StandingBody(Vector3d feet) : IHumanBodyState
    {
        public Vector3d Feet => feet;

        public Vector3d Eyes => new(feet.X, feet.Y, feet.Z + Igi1HumanAi.DetectionMarkAboveFeet(crouching: false));

        public bool IsKeepingLow => false;

        public bool IsActive => true;
    }

    private sealed class Audio : ITitleAudio
    {
        public int Tick { get; set; }

        public List<(int Tick, string Name, bool? Looping)> Log { get; } = [];

        public int Silenced { get; set; }

        public void UseDefinitions(ISoundDefinitionLookup definitions, IReadOnlyList<string> bankDirectories)
        {
        }

        public ITitleVoice? Play(string definition, Vector3d positionInUnits, bool? looping = null)
        {
            Log.Add((Tick, definition, looping));
            return new Voice(this);
        }

        private sealed class Voice(Audio owner) : ITitleVoice
        {
            public bool IsPlaying { get; private set; } = true;

            public void MoveTo(Vector3d positionInUnits)
            {
            }

            public void Silence()
            {
                IsPlaying = false;
                owner.Silenced++;
            }
        }
    }
}
