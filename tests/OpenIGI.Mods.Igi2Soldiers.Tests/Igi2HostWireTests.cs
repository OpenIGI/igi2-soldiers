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

/// <summary>I.G.I. 2's player on Project I.G.I.'s wires: held from an end, and the hands on the cable whether climbing or sliding.</summary>
public sealed class Igi2HostWireTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void Jones_holds_mission_ones_wires_by_the_cable_from_their_ends()
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
        var human = jones.Human;
        foreach ((int id, bool slides) in new[] { (3500, false), (3501, true) })
        {
            Game.Igi2.Interaction.IIgi2Wire wire = jones.Activatables.OfType<Game.Igi2.Interaction.IIgi2Wire>()
                .Single(w => ((Game.Igi2.Interaction.Igi2HostInteraction)w).Interaction.Task.Id == id);
            WorldInteraction at = ((Game.Igi2.Interaction.Igi2HostInteraction)wire).Interaction;
            Assert.True(WireTransform.TryCreate(at.Task, out WireTransform cable));
            human.PopPosition(at.WireBodyPoint(at.Anchor));
            human.ScriptActivate(wire);
            // Past the clip that reaches up for it, half a second.
            for (int t = 0; t < 30; t++)
            {
                jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
                soldiers.Tick();
            }

            // Got on at its end and still on it.
            Assert.Equal(slides, human.IsSlideWire);
            Assert.Equal(!slides, human.IsClimbWire);

            // The fingers of each hand within a tenth of a metre of the cable.
            Vector3d axis = (cable.CableEnd - cable.CableStart).Normalized();
            foreach (string finger in new[] { "left fingers end", "right fingers en" })
            {
                Vector3d d = human.BonePosition(finger) - cable.CableStart;
                double off = (d - (axis * Vector3d.Dot(d, axis))).Length;
                output.WriteLine($"wire {id}: {finger} {off:F0} off the cable");
                Assert.True(off < 410.0, $"Jones's {finger} are {off:F0} off wire {id}'s cable.");
            }

            // A fresh press of the activate key lets go.
            for (int t = 0; t < 120; t++)
            {
                PlayerAction key = t is >= 1 and < 3 ? PlayerAction.Activate : default;
                jones.Tick(new TitlePlayerInput(0f, 0f, key, t == 1 ? PlayerAction.Activate : default, 1f, false));
                soldiers.Tick();
            }

            Assert.False(human.IsClimbWire || human.IsSlideWire, $"Jones did not let go of wire {id}.");
        }
    }
}
