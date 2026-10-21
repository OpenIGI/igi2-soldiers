using OpenIGI.Core.Content;
using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Graphics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Tasks;
using OpenIGI.Engine.Vfs;
using OpenIGI.Formats.Ai;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi2.Soldiers;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.Modding;
using OpenIGI.Modding.Presentation;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.Mods.Igi2Soldiers.Presentation;
using OpenIGI.Mods.Igi2Soldiers.Scripts;
using OpenIGI.Presentation;
using OpenIGI.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>The I.G.I. 2 soldiers as Project I.G.I.'s frame draws them.</summary>
public sealed class Igi2SoldierPresenterTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void A_guard_in_front_of_the_camera_is_skinned_and_drawn()
    {
        string igi1 = GameData.RequireRoot();
        string missionDirectory = Path.Combine(igi1, "missions", "location0", "level1");
        (LevelTask level, WorldScene scene, TerrainData terrain) = Igi2SoldierLevelTests.Load(igi1);
        Igi2SoldierKit kit = Igi2SoldierKit.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped);
        kit.Guest.FileSystem.RegisterDevice(new Igi1MissionDevice(missionDirectory, kit.Scripts));
        Igi2SoldierLevel soldiers = Igi2SoldierLevel.Spawn(
            kit,
            SoldierTable.Shipped,
            level,
            NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object()),
            PlayerSpawn.FindGameplayTask(level)!,
            AiDifficulty.Normal,
            new IgiRandom(),
            NullLogger.Instance);
        soldiers.Tick();

        // Ten metres behind a guard, at its eyes, looking the way the default camera looks.
        Igi2HumanSoldier guard = soldiers.HandedOver.Values.First();
        Camera3D look = Camera3D.Default;
        Vector3d eye = new(guard.Position.X / 4096.0, (guard.Position.Y / 4096.0) - 10.0, (guard.Position.Z / 4096.0) + 1.7);
        Camera3D camera = new(eye, look.Orientation);
        Matrix4f projection = Matrix4f.CreateProjection(640f, 360f, 640f, 360f, 1280, 720, 0.1f, 1000f);

        using RecordingGraphicsDevice device = new();
        using Igi2SoldierPresenter presenter = new(device, kit, soldiers, NullLogger.Instance);
        presenter.Draw(camera, projection, 0f);

        output.WriteLine($"{device.AnimatedMeshUpdates.Count} bodies skinned, {device.MeshDraws.Count} meshes drawn");
        Assert.NotEmpty(device.AnimatedMeshUpdates);
        Assert.NotEmpty(device.MeshDraws);
    }

    [Fact]
    public void Project_IGI_draws_the_soldiers_with_one_presenter_for_the_level_and_none_before_they_spawn()
    {
        ModSet mods = new([new InstalledMod(new ModManifest("igi2soldiers", "I.G.I. 2 soldiers", "1.0", ModApi.Version, []), string.Empty, new Igi2SoldiersMod())]);
        PresenterRegistry igi1 = new();
        PresenterRegistry igi2 = new();
        Assert.Equal(1, PresentationMods.Apply(mods, GameTitle.Igi1, igi1));
        PresentationMods.Apply(mods, GameTitle.Igi2, igi2);

        Assert.Equal(1, igi1.LevelPresenterCount);
        Assert.Equal(0, igi2.LevelPresenterCount);
        TaskPresenterRequest request = new(new RecordingGraphicsDevice(), new ResourceRegistry(new VirtualFileSystem()), new LevelTask(), NullLogger.Instance);
        Assert.Empty(igi1.CreateForLevel(request));
    }
}
