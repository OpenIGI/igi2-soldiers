using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Tasks;
using OpenIGI.Formats.Ai;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Level;
using OpenIGI.Game.Igi2.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi2.Ai.Runtime;
using OpenIGI.Game.Igi2.Audio;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Detection;
using OpenIGI.Game.Igi2.Flow;
using OpenIGI.Game.Igi2.Humans;
using OpenIGI.Game.Igi2.Interaction;
using OpenIGI.Game.Igi2.Player;
using OpenIGI.Game.Igi2.Scripting;
using OpenIGI.Game.Igi2.Soldiers;
using OpenIGI.Game.Igi2.Weapons;
using OpenIGI.Game.Igi2.World;
using OpenIGI.Game.Player;
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
    private readonly Igi2LevelRun _run;
    private int _tick;

    private readonly Dictionary<HumanTask, Igi2AiSquadTask> _squads;
    private readonly TaskTree _tasks;
    private Igi1Carriers? _carriers;

    private Igi2SoldierLevel(
        TaskTree tasks,
        LevelTask root,
        Igi2LevelRun run,
        SoldierBodies soldiers,
        Igi2AiWorld ai,
        IReadOnlyDictionary<HumanTask, Igi2HumanSoldier> handedOver,
        Dictionary<HumanTask, Igi2AiSquadTask> squads)
    {
        _tasks = tasks;
        Root = root;
        _run = run;
        _squads = squads;
        Soldiers = soldiers;
        Ai = ai;
        HandedOver = handedOver;
    }

    /// <summary>Gets the guest's level.</summary>
    public LevelTask Root { get; }

    /// <summary>Gets the soldiers.</summary>
    public SoldierBodies Soldiers { get; }

    /// <summary>Gets the level's AI.</summary>
    public Igi2AiWorld Ai { get; }

    /// <summary>Gets each Project I.G.I. soldier handed over, and the I.G.I. 2 soldier that stands for it.</summary>
    public IReadOnlyDictionary<HumanTask, Igi2HumanSoldier> HandedOver { get; }

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
    /// <param name="difficulty">The session's difficulty.</param>
    /// <param name="random">The soldiers' run stream.</param>
    /// <param name="logger">Where diagnostics go.</param>
    /// <param name="carriers">
    /// The level's moving models, which the soldiers' solids follow and whose graphs ride them; <see langword="null"/> for a
    /// level taken as it was built.
    /// </param>
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
        Igi1Carriers? carriers = null)
    {
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(graphs);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(random);
        IOpenIgiLogger log = logger ?? NullLogger.Instance;
        Engine.Flow.GameLoop guest = kit.Guest.Loop;
        Engine.Titles.IServiceCatalog services = kit.Guest.Session.Services;
        Igi2ScriptFunctions functions = services.Require<Igi2ScriptFunctions>();

        LevelTask root = (LevelTask)guest.Tasks.New(guest.Tasks.Root, guest.Types.Get(EngineTaskTypes.Level));
        GameTask dynamic = guest.Tasks.New(root, guest.Types.Get(EngineTaskTypes.Dynamic));
        Dictionary<Igi2HumanSoldierTask, HumanTask> handedOver = [];
        Dictionary<HumanTask, Igi2AiSquadTask> squads = [];
        int spare = 4095;

        // Each guest task keeps the Project I.G.I. task's id when it has one the guest has not given out, else the next spare id
        // from the top: a mind finds its script by its id, and one without an id would run none.
        void Bind(GameTask task, int preferred)
        {
            if (preferred >= 0 && guest.Tasks.FromId(preferred) is null && guest.Tasks.TryBindId(preferred, task))
            {
                return;
            }

            while (guest.Tasks.FromId(spare) is not null)
            {
                spare--;
            }

            guest.Tasks.TryBindId(spare--, task);
        }

        foreach (HumanSoldierTask igi1 in level.Descendants().OfType<HumanSoldierTask>())
        {
            HumanAiTask? mind = MindOf(igi1);
            if (table.For(AiTypeOf(mind)) is not { } mapping)
            {
                continue;
            }

            Igi2AiSquadTask squad = (Igi2AiSquadTask)guest.Tasks.New(dynamic, guest.Types.Get(Igi2AiSquadTask.TypeName));
            Bind(squad, -1);
            Igi2HumanSoldierTask soldier = (Igi2HumanSoldierTask)guest.Tasks.New(squad, guest.Types.Get(Igi2HumanSoldierTask.TypeName));
            Bind(soldier, igi1.Id);
            soldier.Position = igi1.Position;
            soldier.Gamma = igi1.Gamma;
            soldier.Team = igi1.Team;
            soldier.Model = mapping.ModelFor(igi1.Id);
            soldier.WeaponScript = mapping.Weapon;
            soldier.BoneHierarchy = 1;
            Igi2HumanAiTask ai = (Igi2HumanAiTask)guest.Tasks.New(soldier, guest.Types.Get(Igi2HumanAiTask.TypeName));
            Bind(ai, mind?.Id ?? -1);

            ai.AiTypeName = mapping.AiType;
            ai.AnimTypeName = mapping.AnimType;
            ai.GraphId = mind?.GraphId ?? -1;
            igi1.Driver = GameTitle.Igi2;
            handedOver[soldier] = igi1;
            squads[igi1] = squad;
        }

        SoldierBodies soldiers = Igi2Gameplay.SpawnSoldiers(
            root,
            services,
            world,
            kit.Clips,
            kit.Models,
            services.TryGet(out Igi2PlayerTuning? tuning) ? tuning : null,
            new Igi2Sounds(kit.SoundDefinitions, null),
            random,
            difficulty,
            natives: guest.Natives,
            functions: functions);

        // The player is a body I.G.I. 2's rounds strike and its soldiers' sight is stopped by.
        world.AddCollider(new Collision.Igi1HumanCollider(player));

        Igi2SoldierLevel? built = null;
        Igi2DetectionWorld detection = new(world, services.Require<Igi2GameMaterials>())
        {
            FlowTicks = () => built?._tick ?? 0,
            LevelTick = () => built?._tick ?? 0,
        };
        detection.Detectables.Add(player);
        foreach (Igi2HumanSoldier body in soldiers.Bodies)
        {
            detection.Detectables.Add(body);
            Igi2Gameplay.SeeThrough(body, detection);
        }

        // HumanSoldier_UpdateInternalDataAfterLoadHandler: a living soldier of team 1 is no business of the detectors; it is listed
        // again as it dies.
        foreach (Igi2HumanSoldier body in soldiers.Bodies)
        {
            if (body is { Team: 1, IsDead: false })
            {
                detection.Remove(body);
            }
        }

        Igi2AiGraphs placed = Igi2AiGraphs.Of(graphs.Select(entry =>
        {
            // A graph on a train is held in the train's frame and rides it.
            IIgi2AttachParent? carrier = level is LevelTask placed && NavGraphs.CarrierOf(placed, entry.Key) is { } model
                ? carriers?.Carrier(model)
                : null;
            Igi2AiGraph walked = Igi2AiGraph.From(entry.Value, text => Igi2LineScript.Evaluate(text, guest.Natives, functions), carrier);
            walked.Id = entry.Key;
            return walked;
        }));
        Igi2AiWorld mindWorld = Igi2LevelAi.Build(new Igi2LevelAiSetup(
            root,
            kit.Guest.Resources,
            guest.Natives,
            functions,
            player,
            [.. soldiers.Bodies.Cast<Igi2Human>()],
            random,
            () => built?._tick ?? 0)
        {
            Detection = detection,
            FireTaskTypes = services.TryGet(out Igi2HumanWeapons? armoury) ? armoury.FireTaskTypes : null,
            Config = services.TryGet(out Igi2HumanAiConfig? config) ? config : null,
            Difficulty = (int)difficulty,
            Logger = log,
            World = services.Require<Igi2AiWorld>(),
            Graphs = placed,
        });
        Igi2Gameplay.HearAiEvents(soldiers.Bodies, () => mindWorld);

        Dictionary<HumanTask, Igi2HumanSoldier> bodies = [];
        foreach ((Igi2HumanSoldierTask task, HumanTask igi1) in handedOver)
        {
            if (task.Body is { } body)
            {
                bodies[igi1] = body;
            }
        }

        Igi2LevelRun run = new(root, guest.Natives, functions) { Dynamic = new Igi2DynamicTasks() };
        built = new Igi2SoldierLevel(guest.Tasks, root, run, soldiers, mindWorld, bodies, squads) { _carriers = carriers };
        log.Information("Mods", $"{bodies.Count} of the mission's soldiers are I.G.I. 2's.");
        return built;
    }

    /// <summary>
    /// Puts the soldiers in the Project I.G.I. mission's level: among the bodies its shots meet, and on its explosion and human
    /// lists after its own.
    /// </summary>
    /// <param name="mission">The mission.</param>
    public void JoinTo(MissionState mission)
    {
        ArgumentNullException.ThrowIfNull(mission);
        mission.Bodies.Add(new Igi2ShootableSoldiers(Root.Descendants().OfType<Igi2HumanSoldierTask>()));
        foreach (Igi2HumanSoldierTask soldier in Root.Descendants().OfType<Igi2HumanSoldierTask>())
        {
            mission.Joined.JoinExplosions(soldier);
            mission.Joined.JoinHumans(soldier);
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
        foreach ((HumanTask igi1, Igi2AiSquadTask squad) in _squads)
        {
            walk.Bind(igi1, () => _run.Run(squad));
        }

        walk.AddTickEnd(EndTick);
    }

    /// <summary>Takes the level down as its mission is left: the guest's tasks are deleted, and the ids they kept are free.</summary>
    public void Leave()
    {
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

    private void Finish()
    {
        _carriers?.Follow();
        _tick++;
        foreach ((HumanTask igi1, Igi2HumanSoldier body) in HandedOver)
        {
            if (body.IsDead && !igi1.IsDead)
            {
                igi1.IsDead = true;
            }
        }
    }
}
