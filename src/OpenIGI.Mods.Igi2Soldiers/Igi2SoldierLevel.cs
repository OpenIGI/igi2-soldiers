using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Tasks;
using OpenIGI.Formats.Ai;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2.Ai;
using OpenIGI.Game.Igi2.Ai.Runtime;
using OpenIGI.Game.Igi2.Audio;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Flow;
using OpenIGI.Game.Igi2.Soldiers;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Level;
using OpenIGI.Game.Player;
using OpenIGI.Game.Weapons;
using OpenIGI.Game.World;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using SoldierBodies = OpenIGI.Game.Igi2.Soldiers.Igi2Soldiers;

namespace OpenIGI.Mods.Igi2Soldiers;

/// <summary>
/// The I.G.I. 2 soldiers a Project I.G.I. mission spawns, in a level of the guest's own: each in a squad of its own, with its
/// mind walking the mission's graphs and its detectors looking out for the player.
/// </summary>
/// <remarks>
/// <para>
/// Each soldier the table maps is handed over: its Project I.G.I. task stays, driven by I.G.I. 2 (<see cref="HumanTask.Driver"/>),
/// and an I.G.I. 2 soldier stands where it stood, facing as it faced, on its side, dressed and armed as the table says, its mind of
/// the table's type walking the graph its Project I.G.I. mind walked. The guest's tasks keep the Project I.G.I. tasks' ids.
/// </para>
/// <para>
/// The level is built as I.G.I. 2 builds one (its <c>Detectors</c> and <c>Ai</c> systems): the bodies spawned, the detectors
/// listing them and the player, the minds given their scripts and graphs, and every human's AI events sent to the level's AI.
/// Each soldier's squad is its own, since Project I.G.I. authored no squads; a squad is what lets a mind hear at all.
/// </para>
/// </remarks>
public sealed class Igi2SoldierLevel
{
    /// <summary>The I.G.I. 2 material row a human is, which the player's body reads as to the soldiers' rounds and sight.</summary>
    public const int HumanMaterial = 21;

    private readonly Igi2LevelRun _run;
    private int _tick;

    private readonly Dictionary<HumanTask, Igi2AiSquadTask> _squads;
    private readonly TaskTree _tasks;
    private Igi1Carriers? _carriers;
    private HumanPlayerTask? _player;
    private Detection.Igi1PlayerVisibility? _visibility;
    private Collision.Igi2RoundOdds? _odds;
    private Igi2Sounds? _sounds;
    private float _damageFromIgi1 = 1f;
    private readonly Dictionary<HumanTask, bool> _inWorld = [];
    private readonly Dictionary<HumanTask, Igi2HumanSoldier> _handedOver;
    private TitleWorldModel[] _drawn = [];
    private int _drawnTick = -1;

    private Igi2SoldierLevel(
        TaskTree tasks,
        LevelTask root,
        Igi2LevelRun run,
        SoldierBodies soldiers,
        Igi2AiWorld ai,
        Dictionary<HumanTask, Igi2HumanSoldier> handedOver,
        Dictionary<HumanTask, Igi2AiSquadTask> squads)
    {
        _tasks = tasks;
        Root = root;
        _run = run;
        _squads = squads;
        Soldiers = soldiers;
        Ai = ai;
        _handedOver = handedOver;
    }

    /// <summary>Gets the guest's level.</summary>
    public LevelTask Root { get; }

    /// <summary>Gets the soldiers.</summary>
    public SoldierBodies Soldiers { get; }

    /// <summary>Gets the level's AI.</summary>
    public Igi2AiWorld Ai { get; }

    /// <summary>Gets each Project I.G.I. soldier handed over, and the I.G.I. 2 soldier that stands for it.</summary>
    public IReadOnlyDictionary<HumanTask, Igi2HumanSoldier> HandedOver => _handedOver;

    /// <summary>Gets the models the soldiers in the world draw as of the last tick: each body posed, its weapon in hand.</summary>
    /// <returns>The models.</returns>
    /// <remarks>
    /// As <c>Igi2Soldiers.Models</c> builds them, for the soldiers whose Project I.G.I. tasks are in the world: one a container
    /// holds out of it is not there to be seen.
    /// </remarks>
    public IReadOnlyList<TitleWorldModel> Models()
    {
        if (_drawnTick != _tick)
        {
            List<TitleWorldModel> models = [];
            foreach ((HumanTask igi1, Igi2HumanSoldier body) in HandedOver)
            {
                if (_inWorld.GetValueOrDefault(igi1) && body.Model is { } model)
                {
                    models.AddRange(Game.Igi2.ViewModel.Igi2BodyFrame.Build(
                        body,
                        body.BodyPose(),
                        model.Name,
                        body.Weapon.Inventory?.Current?.WeaponType.FixedModel,
                        _tick,
                        body.Weapon.Firing?.Flashes));
                }
            }

            _drawn = [.. models];
            _drawnTick = _tick;
        }

        return _drawn;
    }

    /// <summary>Gets the ticks the level has run.</summary>
    public int Ticks => _tick;

    /// <summary>
    /// Claims the soldiers the table maps for I.G.I. 2, before Project I.G.I. gives its own soldiers their bodies and minds: a
    /// claimed soldier is given neither, and is not bound into the level's walk.
    /// </summary>
    /// <param name="level">The Project I.G.I. mission's level.</param>
    /// <param name="table">Which soldiers become which.</param>
    /// <returns>How many were claimed.</returns>
    public static int Claim(GameTask level, SoldierTable table)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(table);
        int claimed = 0;
        foreach (HumanSoldierTask igi1 in level.Descendants().OfType<HumanSoldierTask>())
        {
            if (table.For(AiTypeOf(MindOf(igi1))) is not null)
            {
                igi1.Driver = GameTitle.Igi2;
                claimed++;
            }
        }

        return claimed;
    }

    /// <summary>Spawns a mission's I.G.I. 2 soldiers.</summary>
    /// <param name="kit">What they are made of.</param>
    /// <param name="table">Which Project I.G.I. soldiers become which.</param>
    /// <param name="level">The Project I.G.I. mission's level.</param>
    /// <param name="graphs">The mission's graphs, as Project I.G.I. placed them, by their task ids.</param>
    /// <param name="world">The mission's collision, built for I.G.I. 2.</param>
    /// <param name="player">The player, whom the soldiers look out for.</param>
    /// <param name="difficulty">
    /// The session's difficulty, which the soldiers fight at as I.G.I. 2's own do at its: each reads its type's tweaks scaled by
    /// that difficulty's row of the AI config (<c>DIFF_SCALE_</c>), so an easy soldier is slower to identify an enemy and worse
    /// at hitting one, and a hard one quicker and better.
    /// </param>
    /// <param name="random">The soldiers' run stream.</param>
    /// <param name="logger">Where diagnostics go.</param>
    /// <param name="carriers">
    /// The level's moving models, which the soldiers' solids follow and whose graphs ride them; <see langword="null"/> for a
    /// level taken as it was built.
    /// </param>
    /// <param name="audio">Where the soldiers' sounds are heard, I.G.I. 2's own; <see langword="null"/> where nothing is.</param>
    /// <param name="objectLight">The light the level bakes into its buildings' floors, which the soldiers see the player by indoors.</param>
    /// <returns>The level.</returns>
    public static Igi2SoldierLevel Spawn(
        Igi2SoldierKit kit,
        SoldierTable table,
        GameTask level,
        IReadOnlyDictionary<int, GraphFile> graphs,
        Igi2CollisionWorld world,
        HumanPlayerTask player,
        AiDifficulty difficulty,
        IgiRandom random,
        IOpenIgiLogger? logger = null,
        Igi1Carriers? carriers = null,
        ITitleAudio? audio = null,
        Detection.Igi1ObjectLight? objectLight = null)
    {
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(graphs);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(random);
        IOpenIgiLogger log = logger ?? NullLogger.Instance;
        Igi2GuestLevel guest = Igi2GuestLevel.Open(kit.Guest);
        Dictionary<Igi2HumanSoldierTask, HumanTask> handedOver = [];
        Dictionary<HumanTask, Igi2AiSquadTask> squads = [];

        // The lives of each generator's template, beyond the first: a GuardGenerator (0x487EE0) clones its template as often as
        // it may, so every life it may spawn is made here, out of the world until the generator puts it in. Each soldier and
        // mind keeps its Project I.G.I. task's id where it can.
        Dictionary<HumanTask, List<(Igi2AiSquadTask Squad, Igi2HumanSoldierTask Soldier)>> lives = [];
        foreach (HumanSoldierTask igi1 in level.Descendants().OfType<HumanSoldierTask>())
        {
            HumanAiTask? mind = MindOf(igi1);
            if (table.For(AiTypeOf(mind)) is not { } mapping)
            {
                continue;
            }

            int count = igi1.Parent is GuardGeneratorTask generator ? Math.Max(1, generator.MaxSpawns) : 1;
            for (int life = 0; life < count; life++)
            {
                Igi2AiSquadTask squad = guest.AddSquad();
                Igi2HumanSoldierTask soldier = guest.AddSoldier(squad, life == 0 ? igi1.Id : -1);
                soldier.Position = igi1.Position;
                soldier.Gamma = igi1.Gamma;
                soldier.Team = igi1.Team;
                soldier.Model = mapping.ModelFor(igi1.Id);
                soldier.WeaponScript = mapping.Weapon;
                soldier.BoneHierarchy = 1;
                Igi2HumanAiTask ai = guest.AddMind(soldier, life == 0 ? mind?.Id ?? -1 : -1);
                ai.AiTypeName = mapping.AiType;
                ai.AnimTypeName = mapping.AnimType;
                ai.GraphId = mind?.GraphId ?? -1;
                if (life == 0)
                {
                    handedOver[soldier] = igi1;
                    squads[igi1] = squad;
                }

                if (igi1.Parent is GuardGeneratorTask)
                {
                    (lives.TryGetValue(igi1, out var made) ? made : lives[igi1] = []).Add((squad, soldier));
                }
            }

            igi1.Driver = GameTitle.Igi2;
        }

        // The player is a body I.G.I. 2's rounds strike and its soldiers' sight is stopped by, at I.G.I. 2's odds of a round
        // striking: it joins the guest's world after the soldiers' bodies.
        Collision.Igi2RoundOdds odds = new(player.Events, random, table.DamageFromIgi2);
        player.Events = odds;

        Igi2SoldierLevel? built = null;
        Igi2Sounds sounds = new(kit.SoundDefinitions, audio);
        Igi2GuestLevelRun started = guest.Start(new Igi2GuestLevelStart(
            world,
            kit.Clips,
            kit.Models,
            sounds,
            random,
            difficulty,
            player,
            () => built?._tick ?? 0)
        {
            Colliders = [new Igi1HumanLineCollider(player, Igi2CollisionWorld.HumanGroup, HumanMaterial)],
            Graphs = graphs,

            // A graph on a train is held in the train's frame and rides it.
            CarrierOf = id => level is LevelTask placed && NavGraphs.CarrierOf(placed, id) is { } model ? carriers?.Carrier(model) : null,
            Logger = log,
        });

        Dictionary<HumanTask, Igi2HumanSoldier> bodies = [];
        foreach ((Igi2HumanSoldierTask task, HumanTask igi1) in handedOver)
        {
            if (task.Body is { } body)
            {
                bodies[igi1] = body;
            }
        }

        built = new Igi2SoldierLevel(kit.Guest.Loop.Tasks, guest.Root, started.Run, started.Soldiers, started.Ai, bodies, squads)
        {
            _carriers = carriers,
            _player = player,
            _odds = odds,
            _sounds = sounds,
            _damageFromIgi1 = table.DamageFromIgi1,
        };

        // The soldiers see the player as I.G.I. 2 sees its own, not at their cones' full length -- from their first look.
        if (player is not null)
        {
            built._visibility = new Detection.Igi1PlayerVisibility(
                player,
                table.Visibility ?? new PlayerVisibility(),
                Detection.Igi1GroundLight.Of(world.Ground as TerrainData, level),
                objectLight);
            built._visibility.Update();
        }

        // A generator's later lives wait out of the world; the generator's update spawns them.
        foreach ((HumanTask igi1, List<(Igi2AiSquadTask Squad, Igi2HumanSoldierTask Soldier)> made) in lives)
        {
            List<(Igi2AiSquadTask, Igi2HumanSoldier)> pool = [];
            foreach ((Igi2AiSquadTask squad, Igi2HumanSoldierTask soldier) in made)
            {
                if (soldier.Body is { } body)
                {
                    pool.Add((squad, body));
                    body.IsActive = false;
                }
            }

            if (pool.Count > 0 && igi1.Parent is GuardGeneratorTask generator)
            {
                generator.DrivenLives = new GuardLives(built, igi1, pool);
            }
        }

        built.FollowWorld(initial: true);
        log.Information("Mods", $"{bodies.Count} of the mission's soldiers are I.G.I. 2's, at {difficulty}.");
        return built;
    }

    /// <summary>
    /// Puts the soldiers in the Project I.G.I. mission's level: among the bodies its shots meet, and on its explosion and human
    /// lists after its own.
    /// </summary>
    /// <param name="mission">The mission.</param>
    /// <param name="levelRoot">
    /// The mission's level, whose humans hear the soldiers' AI events after the soldiers' own, as the soldiers hear theirs.
    /// </param>
    public void JoinTo(MissionState mission, GameTask levelRoot)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(levelRoot);
        Ai.OtherHumans = [.. levelRoot.DescendantsAndSelf().OfType<IHumanAiEventTarget>()];
        // Project I.G.I.'s rounds and blasts strike them at the table's DamageFromIgi1.
        mission.Bodies.Add(new Igi2ShootableSoldiers(Root.Descendants().OfType<Igi2HumanSoldierTask>()) { DamageScale = _damageFromIgi1 });
        mission.Joined.ExplosionDamageScale = _damageFromIgi1;
        foreach (Igi2HumanSoldierTask soldier in Root.Descendants().OfType<Igi2HumanSoldierTask>())
        {
            // A soldier out of the world -- held by its container, or a generator's life not yet spawned or taken out -- is on no
            // list a blast walks.
            mission.Joined.JoinExplosions(new InWorld(soldier));
            mission.Joined.JoinHumans(soldier);
        }

        // It hears the level's broadcasts for the player's loud shots.
        if (_visibility is not null)
        {
            mission.Joined.JoinHumans(_visibility);
        }
    }

    /// <summary>
    /// Has the Project I.G.I. level's walk run the soldiers: each squad where the walk reaches the task its soldier stands for,
    /// so the containers that held the Project I.G.I. soldier hold the I.G.I. 2 one, and the level's tick finished as the walk's
    /// ends.
    /// </summary>
    /// <param name="walk">The Project I.G.I. level's walk.</param>
    public void BindTo(LevelRun walk)
    {
        ArgumentNullException.ThrowIfNull(walk);
        foreach (HumanTask igi1 in _squads.Keys)
        {
            walk.Bind(igi1, () => _run.Run(_squads[igi1]));
        }

        walk.AddTickEnd(EndTick);
    }

    /// <summary>
    /// Takes the level down as its mission is left: the guest's tasks are deleted, the ids they kept are free, and the player's
    /// own hit events are given back.
    /// </summary>
    public void Leave()
    {
        // The player's own events again, for a mission played on without the soldiers.
        if (_player is not null && _odds is not null && ReferenceEquals(_player.Events, _odds))
        {
            _player.Events = _odds.Inner;
        }

        _visibility?.Reset();
        if (Root.Parent is not null)
        {
            _tasks.Delete(Root);
        }
    }

    /// <summary>Runs the level a tick on its own: every squad and its soldier, then each soldier's body and mind.</summary>
    /// <remarks>
    /// A Project I.G.I. task handed over dies with its I.G.I. 2 soldier, the latch the mission's scripts read
    /// (<c>HumanSoldier_N.isDead</c>).
    /// </remarks>
    public void Tick()
    {
        _run.Tick();
        Finish();
    }

    // The soldier's Project I.G.I. mind, whose AI type picks its I.G.I. 2 counterpart and whose graph it walks.
    private static HumanAiTask? MindOf(GameTask soldier) => soldier.Children.OfType<HumanAiTask>().FirstOrDefault();

    private static string AiTypeOf(HumanAiTask? mind) => mind?.AiTypeName ?? string.Empty;

    private void EndTick()
    {
        _run.EndHostedTick();
        Finish();
    }

    // A soldier is in the world while the Project I.G.I. task it stands for is (Human+3318 as its containers put it in and take it
    // out); the body takes part as that changes, and its script may switch it between.
    private void FollowWorld(bool initial)
    {
        foreach ((HumanTask igi1, Igi2HumanSoldier body) in HandedOver)
        {
            bool now = ConditionalContainerGate.IsTaskActive(igi1);
            if (initial || _inWorld.GetValueOrDefault(igi1) != now)
            {
                _inWorld[igi1] = now;
                body.IsActive = now;
            }
        }
    }

    private void Finish()
    {
        _carriers?.Follow();
        FollowWorld(initial: false);

        // The player's factor for the tick just run, which the soldiers' detection reads on the next.
        _visibility?.Update();

        // The soldiers' sounds run once a tick after the level, as the game's own do: each follows what it hangs off, and one
        // that has ended is let go.
        _sounds?.Run();
        _tick++;
        foreach ((HumanTask igi1, Igi2HumanSoldier body) in HandedOver)
        {
            if (body.IsDead && !igi1.IsDead)
            {
                igi1.IsDead = true;
            }
        }
    }

    // The template's life in the world is now another: the walk runs its squad, the Project I.G.I. task stands for its body and
    // lives again, and it is in the world as the task is.
    private void Wear(HumanTask igi1, Igi2AiSquadTask squad, Igi2HumanSoldier body)
    {
        _squads[igi1] = squad;
        _handedOver[igi1] = body;
        igi1.IsDead = false;
        bool now = ConditionalContainerGate.IsTaskActive(igi1);
        _inWorld[igi1] = now;
        body.IsActive = now;
        _drawnTick = -1;
    }

    // A generator's lives of an I.G.I. 2 soldier: the first is the one handed over, the rest wait out of the world.
    private sealed class GuardLives(Igi2SoldierLevel level, HumanTask template, List<(Igi2AiSquadTask Squad, Igi2HumanSoldier Body)> lives)
        : IGuardLives
    {
        private int _current = -1;

        // Replaced as Project I.G.I.'s own generator replaces its guard: once the dead body's death has played out (0x44F410 takes
        // a generator's guard out of the world then).
        public bool IsDead => _current >= 0 && lives[_current].Body is { IsDead: true, IsDeathAnimationDone: true };

        public void Spawn()
        {
            // 0x487EE0: the dead life is deleted (0x4015F0) as the next is cloned (0x488000).
            if (_current >= 0)
            {
                lives[_current].Body.IsActive = false;
            }

            if (_current + 1 >= lives.Count)
            {
                return;
            }

            _current++;
            level.Wear(template, lives[_current].Squad, lives[_current].Body);
        }
    }

    // A soldier as a blast finds it: only while it is in the world.
    private sealed class InWorld(Igi2HumanSoldierTask soldier) : IExplosionHitTarget
    {
        public void ExplosionHit(in ExplosionHit hit)
        {
            if (soldier.Body is { IsActive: true })
            {
                soldier.ExplosionHit(hit);
            }
        }
    }
}
