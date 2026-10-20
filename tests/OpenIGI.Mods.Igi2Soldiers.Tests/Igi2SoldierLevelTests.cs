using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Flow;
using OpenIGI.Engine.Level;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Tasks;
using OpenIGI.Engine.Time;
using OpenIGI.Engine.Vfs;
using OpenIGI.Formats.Ai;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2.Ai.Runtime;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Soldiers;
using OpenIGI.Game.Player;
using OpenIGI.Game.Weapons;
using OpenIGI.Game.World;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.Mods.Igi2Soldiers.Scripts;
using OpenIGI.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>Project I.G.I.'s first mission, its soldiers I.G.I. 2's.</summary>
public sealed class Igi2SoldierLevelTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void Mission_one_s_soldiers_stand_as_I_G_I_2_soldiers_minded_and_ticking()
    {
        string igi1 = GameData.RequireRoot();
        string missionDirectory = Path.Combine(igi1, "missions", "location0", "level1");
        (LevelTask level, WorldScene scene, TerrainData terrain) = Load(igi1);
        Dictionary<int, GraphFile> graphs = NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance);
        HumanPlayerTask player = PlayerSpawn.FindGameplayTask(level)!;

        Igi2SoldierKit kit = Igi2SoldierKit.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped);
        kit.Guest.FileSystem.RegisterDevice(new Igi1MissionDevice(missionDirectory, kit.Scripts));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());

        Igi2SoldierLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, graphs, world, player, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance);

        int mapped = level.Descendants().OfType<HumanSoldierTask>().Count(static s => s.Driver == GameTitle.Igi2);
        output.WriteLine($"{mapped} handed over, {soldiers.HandedOver.Count} bodies");
        Assert.True(mapped > 10, $"only {mapped} soldiers handed over");
        Assert.Equal(mapped, soldiers.HandedOver.Count);
        foreach (Igi2HumanSoldier body in soldiers.HandedOver.Values)
        {
            Igi2HumanAi mind = Assert.IsType<Igi2HumanAi>(body.Mind);
            Assert.NotNull(mind.Script);
            Assert.NotNull(mind.Graph);
        }

        for (int tick = 0; tick < 150; tick++)
        {
            soldiers.Tick();
        }

        Assert.All(soldiers.HandedOver.Values, static body =>
        {
            Assert.False(body.IsDead);
            Assert.True(double.IsFinite(body.Position.Z));
        });
        Assert.All(soldiers.HandedOver.Values, static body => Assert.True(((Igi2HumanAi)body.Mind!).HasStarted));
    }

    [RequiresTitleDataFact("igi2")]
    public void A_soldier_detects_the_Project_IGI_player_standing_before_it()
    {
        string igi1 = GameData.RequireRoot();
        string missionDirectory = Path.Combine(igi1, "missions", "location0", "level1");
        (LevelTask level, WorldScene scene, TerrainData terrain) = Load(igi1);
        Dictionary<int, GraphFile> graphs = NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance);
        HumanPlayerTask player = PlayerSpawn.FindGameplayTask(level)!;

        Igi2SoldierKit kit = Igi2SoldierKit.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped);
        kit.Guest.FileSystem.RegisterDevice(new Igi1MissionDevice(missionDirectory, kit.Scripts));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());
        Igi2SoldierLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, graphs, world, player, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance);
        for (int tick = 0; tick < 30; tick++)
        {
            soldiers.Tick();
        }

        // The player steps out ten metres in front of a guard, on the ground there.
        Igi2HumanSoldier guard = soldiers.HandedOver.Values.First();
        Matrix3f facing = guard.Orientation;
        Vector3d ahead = new(guard.Position.X + (facing.M12 * 40960.0), guard.Position.Y + (facing.M22 * 40960.0), guard.Position.Z);
        double ground = terrain.TryGetGround(ahead.X, ahead.Y, ahead.Z + 8192.0, aboveUnits: 0.0, out TerrainGround found) ? found.Height : ahead.Z;
        player.Body = new StandingBody(new Vector3d(ahead.X, ahead.Y, ground));
        RecordingEvents struck = new();
        player.Events = struck;

        Igi2HumanAi mind = (Igi2HumanAi)guard.Mind!;
        int seenOn = -1;
        for (int tick = 0; tick < 300 && seenOn < 0; tick++)
        {
            soldiers.Tick();
            if (mind.Detection.Enemies.Any(item => ReferenceEquals(item.Target, player)))
            {
                seenOn = tick;
            }
        }

        output.WriteLine($"the guard listed the player after {seenOn} ticks");
        Assert.True(seenOn >= 0, "the guard never listed the player");

        // And fights: within twenty seconds it has its weapon up and rounds going the player's way.
        Assert.NotNull(guard.Weapon.Inventory?.Current);
        int firedOn = -1;
        for (int tick = 0; tick < 600 && firedOn < 0; tick++)
        {
            soldiers.Tick();
            if (guard.Weapon.Inventory!.TotalBulletsFired > 0)
            {
                firedOn = tick;
            }
        }

        output.WriteLine($"the guard fired after {firedOn} more ticks, holding {guard.Weapon.Inventory!.Current!.WeaponType.WeaponId}");
        Assert.True(firedOn >= 0, "the guard never fired");

        // Its rounds strike the player's task, which takes the engine's WeaponHit with I.G.I. 2's round inside.
        for (int tick = 0; tick < 300 && struck.Hits.Count == 0; tick++)
        {
            soldiers.Tick();
        }

        output.WriteLine($"{struck.Hits.Count} round(s) struck the player, the first for {(struck.Hits.Count > 0 ? struck.Hits[0].Damage : 0f)}");
        WeaponHit first = Assert.Single(struck.Hits.Take(1));
        Assert.IsType<Game.Igi2.Weapons.Igi2WeaponHit>(first.Context);
        Assert.True(first.Damage > 0f);
    }

    private sealed class RecordingEvents : IHumanEvents
    {
        public List<WeaponHit> Hits { get; } = [];

        public bool EvaluateWeaponCollision(HumanTask human, in WeaponCollisionQuery query) => true;

        public void WeaponHit(HumanTask human, in WeaponHit hit) => Hits.Add(hit);

        public void ExplosionHit(HumanTask human, in ExplosionHit hit)
        {
        }
    }

    private sealed class StandingBody(Vector3d feet) : IHumanBodyState
    {
        public Vector3d Feet => feet;

        public Vector3d Eyes => new(feet.X, feet.Y, feet.Z + AiSoldier.DetectionMarkAboveFeet(crouching: false));

        public bool IsKeepingLow => false;

        public bool IsActive => true;
    }

    private static (LevelTask, WorldScene, TerrainData) Load(string root)
    {
        VirtualFileSystem fileSystem = new();
        fileSystem.RegisterDevice(new LocalFileDevice(root));
        ResourceRegistry resources = new(fileSystem);
        GameLoop loop = new(new ManualTimeSource());
        PlayerTaskTypes.RegisterAll(loop.Types);
        loop.Types.Register<ConditionalContainerTask>(ConditionalContainerTask.TypeName);
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
        return (levelRoot, WorldScene.Build(mission, levelRoot, magic), mission.Terrain!);
    }
}
