using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Tasks;
using OpenIGI.Formats.Ilff;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.Mods.Igi2Soldiers.Detection;
using OpenIGI.TestSupport;
using Xunit;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>The light Project I.G.I. bakes under the player's feet: its buildings' floors, else its ground.</summary>
public sealed class Igi1LightTests
{
    [RequiresGameDataFact]
    public void Mission_one_s_security_building_is_lit_by_its_own_floor_and_the_start_by_the_ground()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string directory) = Igi2SoldierLevelTests.LoadMission(igi1, 1);
        Igi1ObjectLight objects = Assert.IsType<Igi1ObjectLight>(
            Igi1ObjectLight.Of(scene.Placements, IresArchive.Read(File.ReadAllBytes(Path.Combine(directory, "lightmaps", "lightmaps.res")))));
        Igi1GroundLight ground = Assert.IsType<Igi1GroundLight>(Igi1GroundLight.Of(terrain, level));

        // The start is in the open, in the sun: no floor under it, the ground's light.
        Vector3d start = PlayerSpawn.FindGameplayTask(level)!.Position;
        Assert.Null(objects.At(start));
        Assert.InRange(ground.At(start)!.Value, 0.7f, 0.8f);

        // Guard 2002 stands inside the security building: its lit floor, brighter than the ground baked in the building's
        // shadow beneath it.
        Vector3d inside = level.Descendants().OfType<HumanSoldierTask>().Single(static soldier => soldier.Id == 2002).Position;
        float floor = Assert.IsType<float>(objects.At(inside));
        Assert.InRange(floor, 0f, 1f);
        Assert.True(floor > ground.At(inside)!.Value + 0.2f, $"the floor {floor} is the ground's {ground.At(inside)}");
    }
}
