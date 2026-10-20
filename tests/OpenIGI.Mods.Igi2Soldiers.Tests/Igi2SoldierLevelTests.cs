using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Audio;
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
using OpenIGI.Game.Igi2.Ai;
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

        RecordingAudio audio = new();
        Igi2SoldierLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, graphs, world, player, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, audio: audio);
        Assert.Contains(kit.SoundBanks, static bank => bank.EndsWith("/ENGLISH", StringComparison.Ordinal));

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

        // They are heard: I.G.I. 2's own definitions, played where they stand.
        output.WriteLine($"{audio.Played.Count} sound(s): {string.Join(", ", audio.Played.Distinct().Take(8))}");
        Assert.NotEmpty(audio.Played);

        Assert.All(soldiers.HandedOver.Values, static body =>
        {
            Assert.False(body.IsDead);
            Assert.True(double.IsFinite(body.Position.Z));
        });
        Assert.All(soldiers.HandedOver.Values, static body => Assert.True(((Igi2HumanAi)body.Mind!).HasStarted));

        // In the mission's level: its shots meet the soldiers, its blasts and AI events reach their tasks, and a Project I.G.I.
        // task dies with the soldier that stands for it.
        MissionState mission = new();
        soldiers.JoinTo(mission, level);
        Assert.Equal(mapped, mission.Bodies.Sources.Single().Bodies().Count());
        Assert.Equal(mapped, mission.Joined.ExplosionTargets.Count);
        Assert.Equal(mapped, mission.Joined.Humans.Count);

        // And the other way: an event the soldiers' world raises reaches the level's own humans after them, in that game's
        // numbering, the soldiers' event whole as its context. I.G.I. 2's explosion is its twentieth, Project I.G.I.'s its
        // twenty-first; a near miss is not given.
        Assert.Contains(player, soldiers.Ai.OtherHumans!);
        RecordingEvents heard = new();
        player.Events = heard;
        Igi2AiEvent blast = new(20, -1, 2, 40960f, player.Position, default, null);
        soldiers.Ai.SendEvent(blast);
        soldiers.Ai.SendEvent(new Igi2AiEvent(Igi2AiEvent.GunShotMiss, -1, 0, 0f, player.Position, default, null));
        HumanAiEvent shared = Assert.Single(heard.Events);
        Assert.Equal((int)AiEvent.Explosion, shared.Type);
        Assert.Equal(blast, shared.Context);

        (HumanTask task, Igi2HumanSoldier killed) = soldiers.HandedOver.First();
        ((IWeaponHitTarget)killed).WeaponHit(new WeaponHit(killed.Position, default, 1000f, new Vector3f(1f, 0f, 0f), null, new object()));
        soldiers.Tick();
        Assert.True(killed.IsDead);
        Assert.True(task.IsDead);
    }

    [RequiresTitleDataFact("igi2")]
    public void A_guard_generator_spawns_its_I_G_I_2_lives_one_at_a_time_each_as_the_last_dies()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = LoadMission(igi1, 4);
        Dictionary<int, GraphFile> graphs = NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance);
        HumanPlayerTask player = PlayerSpawn.FindGameplayTask(level)!;
        Igi2SoldierKit kit = Igi2SoldierKit.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped);
        kit.Guest.FileSystem.RegisterDevice(new Igi1MissionDevice(missionDirectory, kit.Scripts));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());
        Igi2SoldierLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, graphs, world, player, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance);

        // Mission 4's GuardGenerator_600 generates while "1", four lives at most.
        GuardGeneratorTask generator = level.Descendants().OfType<GuardGeneratorTask>().First(static g => g.Name == "GuardGenerator_600");
        HumanTask template = generator.Children.OfType<HumanTask>().Single();
        IGuardLives lives = Assert.IsAssignableFrom<IGuardLives>(generator.DrivenLives);

        // Not in the world until the generator spawns it (0x487EE0 has no child before its first clone).
        Assert.False(soldiers.HandedOver[template].IsActive);
        Assert.Equal(GuardSpawnDecision.FirstSpawn, generator.Advance(isOn: true, childIsDead: lives.IsDead));
        lives.Spawn();
        soldiers.Tick();
        Igi2HumanSoldier first = soldiers.HandedOver[template];
        Assert.True(first.IsActive);
        Assert.False(lives.IsDead);

        // It dies; the generator's next update takes it out and puts the next life in, fresh, where the template stands.
        ((IWeaponHitTarget)first).WeaponHit(new WeaponHit(first.Position, default, 1000f, new Vector3f(1f, 0f, 0f), null, new object()));
        soldiers.Tick();
        Assert.True(template.IsDead);
        Assert.True(lives.IsDead);
        Assert.Equal(GuardSpawnDecision.Respawn, generator.Advance(isOn: true, childIsDead: lives.IsDead));
        lives.Spawn();
        Igi2HumanSoldier second = soldiers.HandedOver[template];
        Assert.NotSame(first, second);
        Assert.False(first.IsActive);
        Assert.True(second.IsActive);
        Assert.False(second.IsDead);
        Assert.False(template.IsDead);
        // Where the template stands, its feet on the ground under the authored point.
        Assert.Equal(template.Position.X, second.Position.X);
        Assert.Equal(template.Position.Y, second.Position.Y);
        Assert.InRange(template.Position.Z - second.Position.Z, 0.0, 1.5 * 4096.0);
        Assert.Equal(2, generator.SpawnCount);
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

    [RequiresTitleDataFact("igi2")]
    public void The_soldiers_fight_at_the_session_s_difficulty_as_I_G_I_2_s_own_do()
    {
        string igi1 = GameData.RequireRoot();
        string missionDirectory = Path.Combine(igi1, "missions", "location0", "level1");
        (LevelTask level, WorldScene scene, TerrainData terrain) = Load(igi1);
        Dictionary<int, GraphFile> graphs = NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance);
        Igi2SoldierKit kit = Igi2SoldierKit.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped);
        kit.Guest.FileSystem.RegisterDevice(new Igi1MissionDevice(missionDirectory, kit.Scripts));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());

        // The player stands where the mission starts him, in the open, the guards about the compound in sight of him.
        (float Accuracy, int FirstShot) Fight(AiDifficulty difficulty)
        {
            HumanPlayerTask player = PlayerSpawn.FindGameplayTask(level)!;
            player.Body = new StandingBody(player.Position);
            Igi2SoldierLevel soldiers = Igi2SoldierLevel.Spawn(
                kit, SoldierTable.Shipped, level, graphs, world, player, difficulty, new IgiRandom(), NullLogger.Instance);
            float accuracy = 0f;
            for (int tick = 0; tick < 450; tick++)
            {
                soldiers.Tick();
                foreach (Igi2HumanSoldier guard in soldiers.HandedOver.Values)
                {
                    Igi2HumanAi mind = (Igi2HumanAi)guard.Mind!;
                    Assert.Equal((int)difficulty, mind.World.Difficulty);
                    accuracy = mind.HasStarted ? mind.Tweak(Igi2AiTweakIndex.MinRangeAccuracy) : accuracy;
                    if (guard.Weapon.Inventory?.TotalBulletsFired > 0)
                    {
                        soldiers.Leave();
                        return (accuracy, tick);
                    }
                }
            }

            soldiers.Leave();
            return (accuracy, int.MaxValue);
        }

        (float Accuracy, int FirstShot) easy = Fight(AiDifficulty.Easy);
        (float Accuracy, int FirstShot) normal = Fight(AiDifficulty.Normal);
        (float Accuracy, int FirstShot) hard = Fight(AiDifficulty.Hard);
        output.WriteLine($"first shot: easy {easy.FirstShot}, normal {normal.FirstShot}, hard {hard.FirstShot}; close accuracy {easy.Accuracy}, {normal.Accuracy}, {hard.Accuracy}");
        Assert.True(easy.FirstShot > normal.FirstShot && normal.FirstShot > hard.FirstShot, "harder soldiers open fire sooner");
        Assert.True(easy.Accuracy < normal.Accuracy && normal.Accuracy < hard.Accuracy, "harder soldiers shoot straighter");
    }

    [RequiresTitleDataFact("igi2")]
    public void A_round_strikes_the_player_at_I_G_I_2_s_odds_and_once()
    {
        string igi1 = GameData.RequireRoot();
        string missionDirectory = Path.Combine(igi1, "missions", "location0", "level1");
        (LevelTask level, WorldScene scene, TerrainData terrain) = Load(igi1);
        Igi2SoldierKit kit = Igi2SoldierKit.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped);
        kit.Guest.FileSystem.RegisterDevice(new Igi1MissionDevice(missionDirectory, kit.Scripts));

        // The player stands where the mission starts him, and guard 2003, seventy metres off across the compound, opens fire.
        HumanPlayerTask player = PlayerSpawn.FindGameplayTask(level)!;
        player.Body = new StandingBody(player.Position);
        RecordingEvents own = new();
        player.Events = own;
        Igi2SoldierLevel soldiers = Igi2SoldierLevel.Spawn(
            kit,
            SoldierTable.Shipped,
            level,
            NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object()),
            player,
            AiDifficulty.Normal,
            new IgiRandom(),
            NullLogger.Instance);
        for (int tick = 0; tick < 450; tick++)
        {
            soldiers.Tick();
        }

        int fired = soldiers.HandedOver.Values.Sum(static guard => guard.Weapon.Inventory?.TotalBulletsFired ?? 0);
        output.WriteLine($"{fired} rounds fired, {own.Hits.Count} struck");
        Assert.InRange(own.Hits.Count, 1, fired / 2);

        // Leaving gives the player its own events back.
        soldiers.Leave();
        Assert.Same(own, player.Events);
    }

    [RequiresTitleDataFact("igi2")]
    public void Claimed_soldiers_run_where_the_Project_IGI_walk_reaches_them()
    {
        string igi1 = GameData.RequireRoot();
        string missionDirectory = Path.Combine(igi1, "missions", "location0", "level1");
        (LevelTask level, WorldScene scene, TerrainData terrain) = Load(igi1);
        int claimed = Igi2SoldierLevel.Claim(level, SoldierTable.Shipped);
        Dictionary<int, GraphFile> graphs = NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance);
        Igi2SoldierKit kit = Igi2SoldierKit.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped);
        kit.Guest.FileSystem.RegisterDevice(new Igi1MissionDevice(missionDirectory, kit.Scripts));
        Igi2SoldierLevel soldiers = Igi2SoldierLevel.Spawn(
            kit,
            SoldierTable.Shipped,
            level,
            graphs,
            Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object()),
            PlayerSpawn.FindGameplayTask(level)!,
            AiDifficulty.Normal,
            new IgiRandom(),
            NullLogger.Instance);
        Assert.Equal(claimed, soldiers.HandedOver.Count);

        LevelUpdateWalker walk = new(level);
        soldiers.BindTo(walk);
        for (int tick = 0; tick < 60; tick++)
        {
            walk.Tick();
        }

        Assert.Equal(60, soldiers.Ticks);
        int started = soldiers.HandedOver.Values.Count(static body => ((Igi2HumanAi)body.Mind!).HasStarted);
        output.WriteLine($"{claimed} claimed, {started} minds started under the walk");
        Assert.True(started > 0, "no soldier ran under the walk");

        // A container takes its soldiers out of the world: their bodies take no part and are not drawn, until it puts them back.
        (HumanTask held, Igi2HumanSoldier body) = soldiers.HandedOver.First(static p => p.Key.Id == 2003);
        ConditionalContainerTask container = held.Ancestors().OfType<ConditionalContainerTask>().First();
        int drawn = soldiers.Models().Count;
        container.IsActive = false;
        soldiers.Tick();
        int heldOut = soldiers.HandedOver.Count(p => p.Key.Ancestors().Contains(container));
        Assert.False(body.IsActive);
        Assert.False(body.DetectionInfo().IsActive);
        Assert.DoesNotContain(soldiers.Models(), m => ReferenceEquals(m.Instance, body));
        Assert.True(soldiers.Models().Count < drawn, $"{heldOut} held out, yet {soldiers.Models().Count} of {drawn} models still drawn");

        container.IsActive = true;
        soldiers.Tick();
        Assert.True(body.IsActive);
        Assert.Contains(soldiers.Models(), m => ReferenceEquals(m.Instance, body));
    }

    [RequiresTitleDataFact("igi2")]
    public void Soldiers_riding_a_train_stand_on_it_and_their_graph_rides_with_it()
    {
        string igi1 = GameData.RequireRoot();
        string missionDirectory = Path.Combine(igi1, "missions", "location0", "level1");
        (LevelTask level, WorldScene scene, TerrainData terrain) = Load(igi1);
        Dictionary<WorldPlacement, Igi2RigidObject> solids = [];
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object(), solids);

        // Project I.G.I. moves the trains: here, every placement stands where it was built until it is moved by hand.
        Dictionary<WorldPlacement, (Vector3d, Matrix3f)> moved = [];
        Igi1Carriers carriers = new(solids, p => moved.TryGetValue(p, out (Vector3d At, Matrix3f Turn) pose)
            ? (true, pose.At, pose.Turn)
            : (true, p.Position, p.Orientation));
        Igi2SoldierKit kit = Igi2SoldierKit.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped);
        kit.Guest.FileSystem.RegisterDevice(new Igi1MissionDevice(missionDirectory, kit.Scripts));
        Igi2SoldierLevel soldiers = Igi2SoldierLevel.Spawn(
            kit,
            SoldierTable.Shipped,
            level,
            NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            world,
            PlayerSpawn.FindGameplayTask(level)!,
            AiDifficulty.Normal,
            new IgiRandom(),
            NullLogger.Instance,
            carriers);
        Dictionary<HumanTask, double> spawned = soldiers.HandedOver.ToDictionary(static p => p.Key, static p => p.Value.Position.Z);
        for (int tick = 0; tick < 150; tick++)
        {
            soldiers.Tick();
        }

        // The guards on the trains stand on their graphs, which are in the trains' frames and ride them: none sinks.
        List<(HumanTask Task, Igi2HumanSoldier Body)> riders = [.. soldiers.HandedOver
            .Where(p => NavGraphs.CarrierOf(level, p.Key.Children.OfType<HumanAiTask>().Single().GraphId) is not null)
            .Select(static p => (p.Key, p.Value))];
        output.WriteLine($"{riders.Count} ride: {string.Join(", ", riders.Select(static r => r.Task.Id))}");
        Assert.NotEmpty(riders);
        foreach ((HumanTask task, Igi2HumanSoldier body) in riders)
        {
            Assert.True(body.Position.Z > spawned[task] - (3 * 4096.0), $"{task.Id} sank from {spawned[task] / 4096:F1} m to {body.Position.Z / 4096:F1} m");
        }

        // The train moves ten metres: its solid follows, and so does the graph its guard walks.
        (HumanTask rider, Igi2HumanSoldier guard) = soldiers.HandedOver.First(static p => p.Key.Id == 3031);
        GameTask train = NavGraphs.CarrierOf(level, rider.Children.OfType<HumanAiTask>().Single().GraphId)!;
        WorldPlacement car = solids.Keys.First(p => ReferenceEquals(p.Owner, train) && !p.IsAttached);
        Igi2AiGraphNode node = ((Igi2HumanAi)guard.Mind!).Graph!.Nodes.Values.First();
        Vector3d before = node.Position;
        moved[car] = (car.Position + new Vector3d(10.0, 0.0, 0.0), car.Orientation);
        soldiers.Tick();
        Assert.Equal(before.X + (10.0 * 4096.0), node.Position.X, 1);
        Assert.Equal(car.Position.X + 10.0, solids[car].Position.X / 4096.0, 3);
    }

    private sealed class RecordingAudio : ITitleAudio
    {
        public List<string> Played { get; } = [];

        public void UseDefinitions(ISoundDefinitionLookup definitions, IReadOnlyList<string> bankDirectories)
        {
        }

        public ITitleVoice? Play(string definition, Vector3d positionInUnits, bool? looping = null)
        {
            Played.Add(definition);
            return null;
        }
    }

    private sealed class RecordingEvents : IHumanEvents
    {
        public List<WeaponHit> Hits { get; } = [];

        public bool EvaluateWeaponCollision(HumanTask human, in WeaponCollisionQuery query) => true;

        public void WeaponHit(HumanTask human, in WeaponHit hit) => Hits.Add(hit);

        public void ExplosionHit(HumanTask human, in ExplosionHit hit)
        {
        }

        public List<HumanAiEvent> Events { get; } = [];

        public void SendEvent(HumanTask human, in HumanAiEvent aiEvent) => Events.Add(aiEvent);
    }

    private sealed class StandingBody(Vector3d feet) : IHumanBodyState
    {
        public Vector3d Feet => feet;

        public Vector3d Eyes => new(feet.X, feet.Y, feet.Z + Igi1HumanAi.DetectionMarkAboveFeet(crouching: false));

        public bool IsKeepingLow => false;

        public bool IsActive => true;
    }

    internal static (LevelTask, WorldScene, TerrainData) Load(string root)
    {
        (LevelTask level, WorldScene scene, TerrainData terrain, _) = LoadMission(root, 1);
        return (level, scene, terrain);
    }

    internal static (LevelTask, WorldScene, TerrainData, string) LoadMission(string root, int missionId)
    {
        VirtualFileSystem fileSystem = new();
        fileSystem.RegisterDevice(new LocalFileDevice(root));
        ResourceRegistry resources = new(fileSystem);
        GameLoop loop = new(new ManualTimeSource());
        PlayerTaskTypes.RegisterAll(loop.Types);
        AiTaskTypes.RegisterAll(loop.Types);
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
        LoadedMission mission = new MissionLoader(registry, fileSystem, resources, levelLoader, null, terrainLoader).Load(missionId);
        return (levelRoot, WorldScene.Build(mission, levelRoot, magic), mission.Terrain!, Path.Combine(root, mission.Descriptor.MissionDirectory));
    }
}
