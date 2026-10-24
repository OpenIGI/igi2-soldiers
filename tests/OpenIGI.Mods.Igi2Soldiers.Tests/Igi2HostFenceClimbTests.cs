using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Tasks;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Interaction;
using OpenIGI.Game.Igi2.Player;
using OpenIGI.Game.Igi2.Scripting;
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

/// <summary>I.G.I. 2's player over Project I.G.I.'s fences: from either side, over to the other.</summary>
public sealed class Igi2HostFenceClimbTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void Jones_climbs_over_each_of_mission_ones_fences_from_either_side()
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
        Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal) { WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit) });

        Igi1Mission fixture = MissionFixture.Igi1(1, igi1);
        DoorRegistry doors = DoorRegistry.Build(level);
        WorldInteractionRegistry interactions = WorldInteractionRegistry.Build(level, fixture.Loaded, doors, new ScriptVarRegistry(new OpenIGI.Scripting.Runtime.NativeRegistry()));
        Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: guest,
            interactions: interactions.Items, activated: static _ => { }, ladders: scene.Ladders);
        Igi2HumanPlayer human = jones.Human;
        Igi2HostInteraction[] fences = [.. jones.Activatables.OfType<Igi2HostInteraction>().Where(static f => f.Kind == Igi2ActivateKind.Fence)];
        Assert.NotEmpty(fences);
        List<string> failures = [];
        foreach (Igi2HostInteraction fence in fences)
        {
            WorldInteraction w = fence.Interaction;
            if (w.IsElectrified)
            {
                continue;
            }

            Assert.True(w.HasLocalBounds);
            foreach ((double along, int side) in new[] { (0.5, 1), (0.5, -1), (0.12, 1), (0.88, -1) })
            {
                // A metre off the fence, at its middle and near either end, facing it.
                double x = w.LocalBoundsMinimum.X + ((w.LocalBoundsMaximum.X - w.LocalBoundsMinimum.X) * along);
                double y = (w.LocalBoundsMinimum.Y + w.LocalBoundsMaximum.Y) * 0.5;
                Vector3d mid = w.Anchor + w.Orientation.Transposed().Transform(new Vector3d(x, y, 0));
                Vector3d start = mid + w.Orientation.Transposed().Transform(new Vector3d(0, side * 4096.0, 0));
                human.PopPosition(start);
                Idle(jones, soldiers, 15);
                Vector3d toward = mid - human.Position;
                human.PopOrientation((float)(Math.Atan2(toward.Y, toward.X) - (Math.PI / 2.0)));
                Idle(jones, soldiers, 4);
                double before = Side(w, human.Position);
                bool on = false;
                for (int t = 0; t < 400; t++)
                {
                    Tick(jones, soldiers, t < 2 ? PlayerAction.Activate : default, t == 0 ? PlayerAction.Activate : default);
                    on |= human.IsClimbFence;
                }

                double after = Side(w, human.Position);
                string line = $"fence {w.Task.Id} at {along:P0} side {side}: on={on} local Y {before:F0} -> {after:F0}, at {human.Position}";
                output.WriteLine(line);
                if (!on || Math.Sign(before) == Math.Sign(after))
                {
                    failures.Add(line);
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    // Which side of the fence a point is, along the fence's local Y, as Project I.G.I. reads it.
    private static double Side(WorldInteraction fence, Vector3d at) => fence.Orientation.Transform(at - fence.Anchor).Y;

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
