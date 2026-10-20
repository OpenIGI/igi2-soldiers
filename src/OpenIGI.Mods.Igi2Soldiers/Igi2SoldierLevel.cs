using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Tasks;
using OpenIGI.Formats.Ai;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi2.Ai;
using OpenIGI.Game.Igi2.Ai.Runtime;
using OpenIGI.Game.Igi2.Audio;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Detection;
using OpenIGI.Game.Igi2.Flow;
using OpenIGI.Game.Igi2.Humans;
using OpenIGI.Game.Igi2.Player;
using OpenIGI.Game.Igi2.Scripting;
using OpenIGI.Game.Igi2.Soldiers;
using OpenIGI.Game.Igi2.Weapons;
using OpenIGI.Game.Igi2.World;
using OpenIGI.Game.Player;
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

    private Igi2SoldierLevel(LevelTask root, Igi2LevelRun run, SoldierBodies soldiers, Igi2AiWorld ai, IReadOnlyDictionary<HumanTask, Igi2HumanSoldier> handedOver)
    {
        Root = root;
        _run = run;
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
        IOpenIgiLogger? logger = null)
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
            DeclaredTask? mind = igi1.Children.OfType<DeclaredTask>().FirstOrDefault(static c => c.Type.Name == "HumanAI");
            string aiType = mind is not null && mind.TryGetValue("AI Type", out object? type) && type is string name ? name : string.Empty;
            if (table.For(aiType) is not { } mapping)
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
            ai.GraphId = mind is not null && mind.TryGetValue("Graph ID", out object? graph) && graph is int id ? id : -1;
            igi1.Driver = GameTitle.Igi2;
            handedOver[soldier] = igi1;
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

        Igi2AiGraphs placed = Igi2AiGraphs.Of(graphs.Select(entry =>
        {
            Igi2AiGraph walked = Igi2AiGraph.From(entry.Value, text => Igi2LineScript.Evaluate(text, guest.Natives, functions));
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
        built = new Igi2SoldierLevel(root, run, soldiers, mindWorld, bodies);
        log.Information("Mods", $"{bodies.Count} of the mission's soldiers are I.G.I. 2's.");
        return built;
    }

    /// <summary>Runs the level a tick: every squad and its soldier, then each soldier's body and mind.</summary>
    public void Tick()
    {
        _run.Tick();
        _tick++;
    }
}
