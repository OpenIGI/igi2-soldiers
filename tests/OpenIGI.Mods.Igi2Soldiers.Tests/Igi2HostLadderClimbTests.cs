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

/// <summary>I.G.I. 2's player on Project I.G.I.'s ladders: up from the foot, off at the top, and back down.</summary>
public sealed class Igi2HostLadderClimbTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void Jones_climbs_each_of_mission_ones_ladders_up_and_back_down()
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
        Game.Igi2.Player.Igi2HumanPlayer human = jones.Human;
        Game.Igi2.Interaction.Igi2HostLadder[] ladders = [.. jones.Activatables.OfType<Game.Igi2.Interaction.Igi2HostLadder>()];
        Assert.NotEmpty(ladders);
        foreach (Game.Igi2.Interaction.Igi2HostLadder ladder in ladders)
        {
            LadderPlacement l = ladder.Ladder;
            IReadOnlyList<float> m = ladder.Frame;
            float facing = (float)(Math.Atan2(-m[4], -m[1]) - (Math.PI / 2.0));

            // Up from the foot, facing it, until off at the top and standing.
            human.PopPosition(l.BottomMount - new Vector3d(0, 0, LadderPlacement.BottomVerticalOffsetUnits));
            Idle(jones, soldiers, 10);
            human.PopOrientation(facing);
            Idle(jones, soldiers, 4);
            bool on = false, off = false;
            for (int t = 0; t < 900 && !off; t++)
            {
                PlayerAction held = t < 3 ? PlayerAction.Activate : PlayerAction.MoveUp;
                Tick(jones, soldiers, held, t == 0 ? PlayerAction.Activate : default);
                on |= human.IsClimbLadder;
                off = on && !human.IsClimbLadder;
            }

            Idle(jones, soldiers, 20);
            output.WriteLine($"ladder at {l.Origin}: {ladder.Steps} steps, off at {human.Position.Z - l.Bottom.Z:F0} over its foot");
            Assert.True(on, $"Jones did not get on the ladder at {l.Origin}.");
            Assert.True(off, $"Jones did not get off the ladder at {l.Origin}'s top.");
            Assert.True(human.Position.Z > l.Bottom.Z + ((l.Top.Z - l.Bottom.Z) * 0.6), $"Jones got off the ladder at {l.Origin} short of its top.");

            // Turned round, back on from the top and down to its foot.
            human.PopOrientation(facing + MathF.PI);
            Idle(jones, soldiers, 6);
            on = false;
            off = false;
            for (int t = 0; t < 900 && !off; t++)
            {
                PlayerAction held = t < 3 ? PlayerAction.Activate : PlayerAction.MoveDown;
                Tick(jones, soldiers, held, t == 0 ? PlayerAction.Activate : default);
                on |= human.IsClimbLadder;
                off = on && !human.IsClimbLadder;
            }

            Assert.True(on, $"Jones did not find the ladder at {l.Origin} from its top.");
            Assert.True(off, $"Jones did not get off the ladder at {l.Origin}'s foot.");
            Assert.True(Math.Abs(human.Position.Z - l.Bottom.Z) < 4096.0, $"Jones left the ladder at {l.Origin} off its foot.");
        }
    }

    private static void Idle(Igi2TitlePlayer jones, Igi2HostedLevel soldiers, int ticks)
    {
        for (int t = 0; t < ticks; t++)
        {
            Tick(jones, soldiers, default, default);
        }
    }

    private static void Tick(Igi2TitlePlayer jones, Igi2HostedLevel soldiers, PlayerAction held, PlayerAction pressed)
    {
        jones.Tick(new TitlePlayerInput(0f, 0f, held, pressed, 1f, false));
        soldiers.Tick();
    }
}
