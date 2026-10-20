using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Flow;
using OpenIGI.Engine.Level;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Tasks;
using OpenIGI.Engine.Time;
using OpenIGI.Engine.Vfs;
using OpenIGI.Formats;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.World;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.TestSupport;
using Xunit;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>I.G.I. 2's collision world built of a Project I.G.I. level answers as Project I.G.I.'s own collision does.</summary>
public sealed class Igi1CollisionWorldTests
{
    private static readonly Lazy<(WorldScene Scene, TerrainData Terrain)> Level1 = new(Load);

    [RequiresGameDataFact]
    public void A_vertical_line_stands_on_the_terrains_height()
    {
        (WorldScene scene, TerrainData terrain) = Level1.Value;
        Igi2CollisionWorld world = Igi1CollisionWorld.Build([], terrain, static m => m, new object());

        double[] xs = [.. scene.Placements.Select(static p => p.Position.X).Order()];
        double[] ys = [.. scene.Placements.Select(static p => p.Position.Y).Order()];
        double cx = xs[xs.Length / 2], cy = ys[ys.Length / 2];
        int compared = 0;
        for (int gx = -10; gx <= 10; gx++)
        {
            for (int gy = -10; gy <= 10; gy++)
            {
                double x = (cx + (gx * 8.0)) * WorldScene.UnitsPerMetre;
                double y = (cy + (gy * 8.0)) * WorldScene.UnitsPerMetre;
                double above = (scene.GroundHeight + 1.0) * WorldScene.UnitsPerMetre;
                if (!TerrainHeightQuery.TryGetGround(terrain, x, y, above, out TerrainGround expected))
                {
                    continue;
                }

                Igi2LineTrace trace = new(1);
                trace.Starts[0] = new Vector3d(x, y, expected.Height + 8192.0);
                world.NextZ(trace, [-16384.0], Igi2CollisionWorld.TerrainGroup);
                Igi2Intersection hit = trace.Intersections[0];
                Assert.True(hit.IsHit);
                Assert.Equal(expected.Height, hit.Position.Z, 1.0);
                compared++;
            }
        }

        Assert.True(compared > 50, $"only {compared} points stood over terrain");
    }

    [RequiresGameDataFact]
    public void A_line_meets_the_placed_objects_where_Project_IGIs_index_meets_them()
    {
        (WorldScene scene, TerrainData terrain) = Level1.Value;
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());
        ObjectCollisionIndex index = ObjectCollisionIndex.Build(scene.Placements, AssetFormatProfile.Igi1.Collision);

        int met = 0;
        foreach (WorldPlacement placement in scene.Placements.Where(static p => p.Model.NativeMarkerBound > 2048f).Take(80))
        {
            // A level line through the model's middle, from far enough out to start outside it.
            Vector3d centre = placement.Position * WorldScene.UnitsPerMetre;
            double reach = placement.Model.NativeMarkerBound * 2.0;
            Vector3d from = new(centre.X - reach, centre.Y + 7.0, centre.Z + 1024.0);
            Vector3d to = new(centre.X + reach, centre.Y + 7.0, centre.Z + 1024.0);

            bool expected = index.TryCast(from, to, out WorldRayHit own);
            Igi2LineTrace trace = new(1);
            trace.Starts[0] = from;
            trace.Ends[0] = to;
            world.TraceLines(trace, Igi2RigidObject.RigidGroup);
            Igi2Intersection hit = trace.Intersections[0];
            Assert.Equal(expected, hit.IsHit);
            if (expected)
            {
                Assert.Equal(own.Position.X, hit.Position.X, 4.0);
                met++;
            }
        }

        Assert.True(met > 20, $"only {met} lines met an object");
    }

    private static (WorldScene, TerrainData) Load()
    {
        VirtualFileSystem fileSystem = new();
        fileSystem.RegisterDevice(new LocalFileDevice(GameData.RequireRoot()));
        ResourceRegistry resources = new(fileSystem);
        GameLoop loop = new(new ManualTimeSource());
        MissionRegistry registry = new();
        registry.Register(loop.Natives);
        registry.Scan(fileSystem, resources, loop.Natives);
        MagicObjectRegistry magic = new();
        magic.Register(loop.Natives);
        magic.Load(resources, loop.Natives);
        TerrainLoader terrainLoader = new(resources, loop.Natives);
        LevelTask levelRoot = (LevelTask)loop.Tasks.New(loop.Tasks.Root, loop.Types.Get(EngineTaskTypes.Level));
        LevelLoader levelLoader = new(levelRoot, loop.TaskCommands, loop.Natives, resources);
        LoadedMission mission = new MissionLoader(registry, fileSystem, resources, levelLoader, null, terrainLoader).Load(1);
        return (WorldScene.Build(mission, levelRoot, magic), mission.Terrain!);
    }
}
