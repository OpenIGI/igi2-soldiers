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

/// <summary>I.G.I. 2's player in Project I.G.I.'s first mission, among I.G.I. 2's soldiers.</summary>
public sealed class Igi2PlayerInIgi1Tests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void Jones_stands_where_mission_one_starts_its_player_and_is_the_soldiers_player()
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
        Assert.Contains(soldiers.Pickups!.Items, static p => p.WeaponType.WeaponId == "WEAPON_ID_AK47");

        // A gun a guard of the mission's drops falls as I.G.I. 2's.
        int lying = soldiers.Pickups.Items.Count;
        Assert.True(soldiers.DropGun("WEAPON_ID_DRAGUNOV", spawn.Position + new Vector3d(0, 4096, 8192), Matrix3f.Identity));
        Assert.Equal(lying + 1, soldiers.Pickups.Items.Count);

        // His map computer, which opens on the mission's level.
        Assert.NotNull(jones.Computer);

        Game.Igi2.Interaction.Igi2HostInteraction[] usable = [.. jones.Activatables.OfType<Game.Igi2.Interaction.Igi2HostInteraction>()];
        output.WriteLine($"{interactions.Items.Count} interactions, {usable.Length} usable by Jones");
        Game.Igi2.Interaction.IIgi2Door door = Assert.IsAssignableFrom<Game.Igi2.Interaction.IIgi2Door>(
            usable.First(static u => u.Kind == Game.Igi2.Interaction.Igi2ActivateKind.Door && !((Game.Igi2.Interaction.IIgi2Door)u).IsLocked));
        door.ToggleOpen(spawn.Position);
        Assert.Single(used);

        // The mission's ladders, the watchtowers' among them, which he climbs as I.G.I. 2's player climbs its own.
        output.WriteLine($"{scene.Ladders.Count} ladders");
        Assert.NotEmpty(scene.Ladders);
        Assert.Equal(scene.Ladders.Count, jones.Activatables.OfType<Game.Igi2.Interaction.Igi2HostLadder>().Count());
        Assert.All(jones.Activatables.OfType<Game.Igi2.Interaction.Igi2HostLadder>(), static ladder => Assert.True(ladder.Steps > 0));

        // The soldiers' player is Jones: their AI looks out for him, their detectors list him, not the mission's task.
        Assert.Same(jones, soldiers.Player);
        Assert.Same(jones.Human, soldiers.Ai.Player);
        Assert.Contains(jones.Human, soldiers.Detection.Detectables);
        Assert.DoesNotContain(task, soldiers.Detection.Detectables);

        // The mission's player is Jones from here on: its task's body is his, and the mission's rounds strike him.
        Igi2PlayerInHost body = new(jones.Human);
        task.Body = body;
        task.Events = body;
        MissionState mission = new();
        soldiers.JoinTo(mission, level);
        Assert.Contains(mission.Bodies.Sources, static source => source is Igi2ShootablePlayer);

        // He stands on the mission's ground (where Project I.G.I. spawns its player above it) and walks it, ticked as the host ticks a title's player.
        for (int tick = 0; tick < 60; tick++)
        {
            jones.Tick(new TitlePlayerInput(0f, 0f, PlayerAction.MoveUp, 0, 1f, false));
            soldiers.Tick();
        }

        Vector3d moved = jones.Human.Position - spawn.Position;
        output.WriteLine($"spawn {spawn.Position}, now {jones.Human.Position}, task feet {task.Body.Feet}");
        output.WriteLine($"distance to ground {jones.Human.DistanceToGround}");
        Assert.InRange(jones.Human.DistanceToGround, -410.0, 410.0);
        Assert.True(Math.Sqrt((moved.X * moved.X) + (moved.Y * moved.Y)) > 2048.0, "Jones did not walk");
        Assert.Equal(jones.Human.Position, task.Body.Feet);

        // A round of the mission's, aimed at his chest, finds his bones and hurts him.
        Vector3d chest = new(jones.Human.Position.X, jones.Human.Position.Y, jones.Human.Position.Z + 5000);
        Assert.True(((IShootableBody)mission.Bodies.Sources.OfType<Igi2ShootablePlayer>().Single()).TryMeet(
            chest + new Vector3d(40960, 0, 0), chest - new Vector3d(40960, 0, 0), out HumanHit hit));
        output.WriteLine($"struck at {hit.Position}");
    }

    [RequiresTitleDataFact("igi2")]
    public void A_missions_mounted_guns_are_I_G_I_2s_on_their_mounts_for_Jones_and_the_soldiers()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = Igi2SoldierLevelTests.LoadMission(igi1, 4);
        Igi2GuestContent kit = Igi2GuestContent.Load(
            GameDataSource.Igi2.RequireRoot(),
            SoldierTable.Shipped.Models.Append(Igi2GuestPlayerStart.JonesModel).Append(Igi2Gameplay.FirstPersonFallbackModel));
        kit.Guest.FileSystem.RegisterDevice(new Igi2HostMissionDevice(missionDirectory, kit.Scripts));
        HumanPlayerTask task = PlayerSpawn.FindGameplayTask(level)!;
        Assert.True(PlayerSpawn.TryFindGameplay(level, out PlayerSpawnPoint spawn));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());
        Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
        Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal));

        Igi1Mission fixture = MissionFixture.Igi1(4, igi1);
        WorldInteractionRegistry interactions = WorldInteractionRegistry.Build(
            level, fixture.Loaded, DoorRegistry.Build(level), new ScriptVarRegistry(new OpenIGI.Scripting.Runtime.NativeRegistry()));
        Game.Ai.AiStationaryGun[] mounted = [.. interactions.Items.Select(static i => i.StationaryGun).OfType<Game.Ai.AiStationaryGun>()];
        output.WriteLine($"{mounted.Length} mounted guns");
        Assert.NotEmpty(mounted);
        foreach (Game.Ai.AiStationaryGun gun in mounted)
        {
            Assert.NotNull(guest.AddStationaryGun(gun.TaskId, gun.GunPosition, default, gun.MaximumAlphaDegrees, gun.MinimumAlphaDegrees, gun.MaximumGammaDegrees));
            gun.IsHandedOver = true;
            Assert.False(gun.TryMount(new object()));
        }

        Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: guest);

        // Jones works them; a soldier ordered to man one finds it by the mission's id; they are drawn with his world.
        Assert.Equal(mounted.Length, soldiers.Guns.Count);
        Assert.All(soldiers.Guns, gun => Assert.Contains(gun, jones.Activatables));
        Assert.All(mounted, gun => Assert.Same(soldiers.Guns.Single(g => g.Id == gun.TaskId), soldiers.Ai.StationaryGun(gun.TaskId)));
        Assert.All(soldiers.Guns, static gun => Assert.NotNull(gun.Firing));
        for (int tick = 0; tick < 10; tick++)
        {
            jones.Tick(new TitlePlayerInput(0f, 0f, 0, 0, 1f, false));
            soldiers.Tick();
        }
    }
}
