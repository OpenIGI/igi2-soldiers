using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Tasks;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Physics;
using OpenIGI.Game.Weapons;
using OpenIGI.Game.World;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>I.G.I. 2's rounds on Project I.G.I.'s placed objects: what they strike is handed to the mission whole.</summary>
public sealed class Igi1RoundContactTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi1")]
    public void A_line_cast_at_each_of_mission_ones_security_cameras_hands_the_camera_to_the_mission()
    {
        (LevelTask _, WorldScene scene, TerrainData terrain, string _) = Igi2SoldierLevelTests.LoadMission(GameData.RequireRoot(), 1);
        Dictionary<WorldPlacement, Igi2RigidObject> solids = [];
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object(), solids);
        MissionState mission = new();
        List<(GameTask? Owner, WorldPlacement? Placement, float Damage)> contacts = [];
        mission.RoundContact = (_, owner, placement, damage) => contacts.Add((owner, placement, damage));
        Assert.True(Igi1RoundContacts.Attach(solids, mission) > 0);

        WorldPlacement[] cameras = [.. solids.Keys.Where(static p => p.Owner.Type.Name == WorldScene.SecurityCameraType)];
        Assert.NotEmpty(cameras);
        foreach (WorldPlacement camera in cameras)
        {
            // From a few metres off on each side and below, the first line that reaches it.
            Vector3d at = camera.Position.Length < 1e6 ? camera.Position * WorldScene.UnitsPerMetre : camera.Position;
            bool struck = false;
            foreach (Vector3d from in Around(at))
            {
                WorldLineTrace trace = new(1);
                trace.Starts[0] = from;
                trace.Ends[0] = at;
                world.TraceLines(trace, Igi2CollisionWorld.EveryTypeGroup);
                if (trace.Intersections[0] is { IsHit: true, HitObject.Handler: IWeaponHitTarget target } met)
                {
                    contacts.Clear();
                    target.WeaponHit(new WeaponHit(met.Position, met.Normal, 0.5f, default, null, null));
                    if (contacts is [{ Owner: { } owner, Placement: { } placement }] && ReferenceEquals(owner, camera.Owner) && ReferenceEquals(placement, camera))
                    {
                        struck = true;
                        break;
                    }
                }
            }

            output.WriteLine($"camera {camera.TaskName} at {at}: {(struck ? "struck" : "not reached")}");
            Assert.True(struck, $"No round reached camera {camera.TaskName}.");
        }
    }

    private static IEnumerable<Vector3d> Around(Vector3d at)
    {
        const double Off = 4.0 * WorldScene.UnitsPerMetre;
        foreach ((double x, double y) in new[] { (1.0, 0.0), (-1.0, 0.0), (0.0, 1.0), (0.0, -1.0), (0.7, 0.7), (-0.7, -0.7), (0.7, -0.7), (-0.7, 0.7) })
        {
            yield return at + new Vector3d(x * Off, y * Off, -Off * 0.5);
        }
    }
}
