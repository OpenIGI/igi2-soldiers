using OpenIGI.Engine.Tasks;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi2.Soldiers;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Player;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.Modding.Testing;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>I.G.I. 2's player in a Project I.G.I. mission knows what he walks on, and so how his steps sound.</summary>
public sealed class Igi2GuestFootstepTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void Jones_walking_from_mission_ones_start_treads_on_ground_he_can_name()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = Igi2SoldierLevelTests.LoadMission(igi1, 1);
        Igi2GuestContent kit = Igi2GuestContent.Load(
            GameDataSource.Igi2.RequireRoot(),
            SoldierTable.Shipped.Models.Append(Igi2GuestPlayerStart.JonesModel).Append(Igi2Gameplay.FirstPersonFallbackModel));
        HumanPlayerTask task = PlayerSpawn.FindGameplayTask(level)!;
        Assert.True(PlayerSpawn.TryFindGameplay(level, out PlayerSpawnPoint spawn));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());
        Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
        Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal)
        {
            WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit),
        });

        Assert.NotNull(jones.Materials);
        HashSet<string> trodden = [];
        for (int tick = 0; tick < 150; tick++)
        {
            jones.Tick(new TitlePlayerInput(0f, 0f, PlayerAction.MoveUp, default, 1f, false));
            if (jones.Human.GroundIntersection is { IsHit: true, EnterMaterial: int row } && jones.Materials![row] is { } material)
            {
                Assert.NotEmpty(material.WalkSounds);
                trodden.Add(material.Name);
            }
        }

        output.WriteLine($"Jones trod on {string.Join(", ", trodden)}.");
        Assert.NotEmpty(trodden);
    }

    [RequiresTitleDataFact("igi2")]
    public void Jones_spawning_at_mission_ones_start_is_not_searched_for()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = Igi2SoldierLevelTests.LoadMission(igi1, 1);
        Igi2GuestContent kit = Igi2GuestContent.Load(
            GameDataSource.Igi2.RequireRoot(),
            SoldierTable.Shipped.Models.Append(Igi2GuestPlayerStart.JonesModel).Append(Igi2Gameplay.FirstPersonFallbackModel));
        Game.Igi2.Scripting.Igi2HostMissionDevice device = new(missionDirectory, kit.Scripts);
        kit.Guest.FileSystem.RegisterDevice(device);
        HumanPlayerTask task = PlayerSpawn.FindGameplayTask(level)!;
        Assert.True(PlayerSpawn.TryFindGameplay(level, out PlayerSpawnPoint spawn));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());
        Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
        Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal)
        {
            WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit),
        });
        Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, Game.Igi1.Ai.NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, Core.Diagnostics.NullLogger.Instance),
            world, task, AiDifficulty.Normal, new Core.Numerics.IgiRandom(), Core.Diagnostics.NullLogger.Instance, guest: guest, scripts: device);

        // Standing still where the mission starts him, out of every soldier's sight: no one is onto him, though I.G.I. 2 lists
        // him with every soldier in reach from the first tick.
        for (int tick = 0; tick < 90; tick++)
        {
            jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
            soldiers.Tick();
            Assert.Equal(PlayerAlertStatus.None, jones.AlertStatus());
        }

        soldiers.Leave();
    }
}
