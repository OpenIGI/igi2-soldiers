using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Tasks;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Player;
using OpenIGI.Game.Igi2.Scripting;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Player;
using OpenIGI.Game.Weapons;
using OpenIGI.Game.World;
using OpenIGI.Modding.Testing;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>I.G.I. 2's player looks as the host's player does: the mouse down looks down, unless the profile inverts it.</summary>
public sealed class Igi2PlayerLookTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void The_mouse_moved_down_looks_down_on_both_look_paths()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = Igi2SoldierLevelTests.LoadMission(igi1, 1);
        Igi2GuestContent kit = Igi2GuestContent.Load(
            GameDataSource.Igi2.RequireRoot(),
            SoldierTable.Shipped.Models.Append(Igi2GuestPlayerStart.JonesModel).Append(Igi2Gameplay.FirstPersonFallbackModel));
        kit.Guest.FileSystem.RegisterDevice(new Igi2HostMissionDevice(missionDirectory, kit.Scripts));
        HumanPlayerTask task = PlayerSpawn.FindGameplayTask(level)!;
        Assert.True(PlayerSpawn.TryFindGameplay(level, out PlayerSpawnPoint spawn));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());

        Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
        string weapons = Igi2SoldierLevel.PlayerWeaponScript(task, kit);
        output.WriteLine($"carries {weapons}");
        Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal) { WeaponScript = weapons });
        Assert.Contains(jones.Inventory!.Weapons, static w => w is { IsActive: true });

        // A weapon the mission lays about lies as I.G.I. 2's, for him to take.
        Assert.True(guest.AddGunPickup("WEAPON_ID_AK47", spawn.Position + new Vector3d(4096, 0, 0), default));
        Assert.False(guest.AddGunPickup("WEAPON_ID_NOT_A_WEAPON", spawn.Position, default));

        // The mission's doors and switches, which he uses as the mission's player does, telling it of each.
        Igi1Mission fixture = MissionFixture.Igi1(1, igi1);
        DoorRegistry doors = DoorRegistry.Build(level);
        WorldInteractionRegistry interactions = WorldInteractionRegistry.Build(level, fixture.Loaded, doors, new ScriptVarRegistry(new OpenIGI.Scripting.Runtime.NativeRegistry()));
        List<int> used = [];


        Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: guest,
            interactions: interactions.Items, activated: used.Add, ladders: scene.Ladders);
        for (int t = 0; t < 20; t++) { jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 0.5f, false)); soldiers.Tick(); }
        Matrix3f v0 = jones.Human.View.Orientation;
        for (int t = 0; t < 10; t++) { jones.Tick(new TitlePlayerInput(0f, 40f, default, default, 0.5f, false)); soldiers.Tick(); }
        Matrix3f v1 = jones.Human.View.Orientation;
        for (int t = 0; t < 10; t++) { jones.Tick(new TitlePlayerInput(0f, 40f, default, default, 0.5f, false, UnfilteredMouse: true)); soldiers.Tick(); }
        Matrix3f v2 = jones.Human.View.Orientation;
        output.WriteLine($"forward z before {v0.M32:F3} after mouse down {v1.M32:F3} unfiltered {v2.M32:F3}");
        Assert.True(v1.M32 < v0.M32 - 0.1f, "The mouse moved down did not look down.");
        Assert.True(v2.M32 <= v1.M32, "The mouse moved down did not look down with the smooth look.");

        // The profile's invert looks up instead.
        for (int t = 0; t < 10; t++) { jones.Tick(new TitlePlayerInput(0f, 40f, default, default, 0.5f, true)); soldiers.Tick(); }
        Assert.True(jones.Human.View.Orientation.M32 > v2.M32 + 0.1f, "The profile's invert did not look up.");
    }
}
