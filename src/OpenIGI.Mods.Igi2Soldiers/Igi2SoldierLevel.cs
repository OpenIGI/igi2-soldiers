using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Tasks;
using OpenIGI.Formats.Ai;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi1.Player;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2.Ai;
using OpenIGI.Game.Igi2.Ai.Runtime;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Scripting;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Igi2.Weapons;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.Scripting.Qvm;

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
/// The level is brought up as I.G.I. 2 brings its own (<see cref="Igi2GuestLevel"/>), and runs inside the mission's
/// (<see cref="Igi2HostedLevel"/>). Project I.G.I. authored no squads, so its guards are put in squads as I.G.I. 2's levels
/// have them: those one container holds who walk one graph on one side, eight at most, so they hear, warn and fight as one.
/// </para>
/// </remarks>
public static class Igi2SoldierLevel
{
    /// <summary>The I.G.I. 2 material row a human is, which the player's body reads as to the soldiers' rounds and sight.</summary>
    public const int HumanMaterial = 21;

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
    /// <param name="content">What they are made of.</param>
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
    /// <param name="scripts">
    /// The guest's <c>MISSION:</c> device, which each soldier's script is written into from its Project I.G.I. orders;
    /// <see langword="null"/> to leave every soldier the generic script.
    /// </param>
    /// <param name="missionDirectory">The Project I.G.I. mission's directory, whose <c>ai/</c> scripts give the orders.</param>
    /// <param name="alarms">
    /// The mission's alarms, which the soldiers raise and hear as theirs; <see langword="null"/> for soldiers that know no alarm.
    /// </param>
    /// <param name="doors">The mission's doors, which the soldiers open on their way; <see langword="null"/> for none.</param>
    /// <returns>The level, begun.</returns>
    public static Igi2HostedLevel Spawn(
        Igi2GuestContent content,
        SoldierTable table,
        GameTask level,
        IReadOnlyDictionary<int, GraphFile> graphs,
        Igi2CollisionWorld world,
        HumanPlayerTask player,
        AiDifficulty difficulty,
        IgiRandom random,
        IOpenIgiLogger? logger = null,
        Igi2PlacedCarriers? carriers = null,
        ITitleAudio? audio = null,
        Igi1ObjectLight? objectLight = null,
        Igi2HostMissionDevice? scripts = null,
        string? missionDirectory = null,
        Igi1Alarms? alarms = null,
        IReadOnlyList<IAiDoor>? doors = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(graphs);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(random);
        IOpenIgiLogger log = logger ?? NullLogger.Instance;
        Igi2GuestLevel guest = Igi2GuestLevel.Open(content);

        // The weapons I.G.I. 2 has, by the ids both games give them: a guard is armed with its own where they are here.
        HashSet<string> igi2Weapons = content.Guest.Session.Services.TryGet(out Igi2HumanWeapons? arms)
            ? [.. arms.Tables.WeaponTypes.Select(static w => w.WeaponId)]
            : [];

        // Each guard's own orders -- the path it patrols, the gun it mans, how far it sees -- and the paths themselves, carried
        // into the guest's level under their own ids for the soldiers' scripts to send them on.
        AiPatrolPathRegistry patrols = AiPatrolPathRegistry.Build(level);
        HashSet<int> carried = [];
        HashSet<int> joined = [];
        Igi2SoldierOrders OrdersFor(HumanAiTask? mind, Igi2AiSquadTask squad)
        {
            Igi1GuardOrders own = OrdersOf(mind, missionDirectory);
            JoinAlarm(own, squad);
            Igi2SoldierOrders orders = Translate(own);
            if (!Carry(orders.PatrolPathId))
            {
                orders = orders with { PatrolPathId = null };
            }

            if (!Carry(orders.AlarmPatrolPathId))
            {
                orders = orders with { AlarmPatrolPathId = null };
            }

            return orders;
        }

        bool Carry(int? id)
        {
            if (id is not { } path || carried.Contains(path))
            {
                return true;
            }

            if (patrols.Find(path) is { } found && guest.AddPatrolPath(path, found.Commands) is not null)
            {
                carried.Add(path);
                return true;
            }

            log.Warning("Mods", $"Patrol path {path} cannot be carried into I.G.I. 2's level; its guards stand idle.");
            return false;
        }

        // A squad runs for the alarm its first guard with one does; that alarm is carried into the guest's level once, its
        // control following the mission's alarm and its trigger, where it has one, pressing the mission's. A guard that only
        // hears the alarm names no trigger, and its squad sends no one to press one.
        void JoinAlarm(Igi1GuardOrders own, Igi2AiSquadTask squad)
        {
            if (alarms is null || own.AlarmControlId is not { } control || squad.AlarmControlId != -1)
            {
                return;
            }

            (int, Vector3d, Action)? trigger = own.AlarmTriggerId is { } id && TryPosition(level, id, out Vector3d at)
                ? (id, at, () => alarms.Press(id))
                : null;
            if (!joined.Contains(control))
            {
                if (!guest.AddAlarm(control, () => alarms.IsOn(control), trigger))
                {
                    return;
                }

                joined.Add(control);
            }

            squad.AlarmControlId = control;
            squad.AlarmTriggerId = trigger?.Item1 ?? -1;
        }

        List<(HumanTask Igi1, Igi2AiSquadTask Squad, Igi2HumanSoldierTask Soldier)> handedOver = [];

        // The lives of each generator's template, beyond the first: a GuardGenerator (0x487EE0) clones its template as often as
        // it may, so every life it may spawn is made here, out of the world until the generator puts it in. Each soldier and
        // mind keeps its Project I.G.I. task's id where it can.
        Dictionary<HumanTask, List<(Igi2AiSquadTask Squad, Igi2HumanSoldierTask Soldier)>> lives = [];

        // Squads, as I.G.I. 2's levels have them: the guards one container holds who walk one graph on one side, eight at most
        // (Igi2AiSquadTeam.Capacity). Project I.G.I. authored no squads; these are its guards' own groups.
        Dictionary<(GameTask? Parent, int Graph, int Team), (Igi2AiSquadTask Squad, int Members)> squads = [];
        Igi2AiSquadTask SquadFor(HumanSoldierTask igi1, HumanAiTask? mind)
        {
            (GameTask? Parent, int Graph, int Team) key = (igi1.Parent, mind?.GraphId ?? -1, igi1.Team);
            if (squads.TryGetValue(key, out (Igi2AiSquadTask Squad, int Members) open) && open.Members < Igi2AiSquadTeam.Capacity)
            {
                squads[key] = (open.Squad, open.Members + 1);
                return open.Squad;
            }

            Igi2AiSquadTask squad = guest.AddSquad();
            squads[key] = (squad, 1);
            return squad;
        }

        foreach (HumanSoldierTask igi1 in level.Descendants().OfType<HumanSoldierTask>())
        {
            HumanAiTask? mind = MindOf(igi1);
            if (table.For(AiTypeOf(mind)) is not { } mapping)
            {
                continue;
            }

            Igi2SoldierOrders? orders = null;
            int count = igi1.Parent is GuardGeneratorTask generator ? Math.Max(1, generator.MaxSpawns) : 1;
            for (int life = 0; life < count; life++)
            {
                // A generator's lives stand in the world one at a time: each is a squad of its own.
                Igi2AiSquadTask squad = igi1.Parent is GuardGeneratorTask ? guest.AddSquad() : SquadFor(igi1, mind);
                if (scripts is not null && life == 0)
                {
                    orders = OrdersFor(mind, squad);
                }

                Igi2HumanSoldierTask soldier = guest.AddSoldier(squad, life == 0 ? igi1.Id : -1);
                soldier.Position = igi1.Position;
                soldier.Gamma = igi1.Gamma;
                soldier.Team = igi1.Team;
                soldier.Model = mapping.ModelFor(igi1.Id);
                soldier.WeaponScript = WeaponScriptOf(igi1, mapping, igi2Weapons);
                soldier.BoneHierarchy = 1;
                Igi2HumanAiTask ai = guest.AddMind(soldier, life == 0 ? mind?.Id ?? -1 : -1);
                ai.AiTypeName = mapping.AiType;
                ai.AnimTypeName = mapping.AnimType;
                ai.GraphId = mind?.GraphId ?? -1;
                if (orders is not null && scripts is not null)
                {
                    scripts.SoldierScripts[ai.Id] = Igi2SoldierScript.Build(orders);
                }

                if (life == 0)
                {
                    handedOver.Add((igi1, squad, soldier));
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
        Igi2RoundOdds odds = new(player.Events, random, table.DamageFromIgi2);
        player.Events = odds;
        Igi2HostedLevel hosted = guest.Start(new Igi2GuestLevelStart(world, random, difficulty, player)
        {
            Audio = audio,
            Colliders = [new Igi1HumanLineCollider(player, Igi2CollisionWorld.HumanGroup, HumanMaterial)],
            Doors = doors ?? [],

            // The guards Project I.G.I. keeps driving are seen as I.G.I. 2's own see one another: a friend, or a friend lying dead.
            Others = [.. level.Descendants().OfType<HumanSoldierTask>().Where(static guard => guard.Driver != GameTitle.Igi2)],
            Graphs = graphs,

            // A graph on a train is held in the train's frame and rides it.
            CarrierOf = id => level is LevelTask placed && NavGraphs.CarrierOf(placed, id) is { } model ? carriers?.Carrier(model) : null,

            // The player is seen by the light the level bakes under its feet: a building's lit floor, else the ground.
            Visibility = table.Visibility ?? new(),
            Light = [.. new IBakedLight?[] { objectLight, Igi1GroundLight.Of(world.Ground as TerrainData, level) }.OfType<IBakedLight>()],
            Logger = log,
        });

        foreach ((HumanTask igi1, Igi2AiSquadTask squad, Igi2HumanSoldierTask soldier) in handedOver)
        {
            hosted.StandFor(igi1, squad, soldier);
        }

        // A generator's later lives wait out of the world; the generator's update spawns them.
        foreach ((HumanTask igi1, List<(Igi2AiSquadTask Squad, Igi2HumanSoldierTask Soldier)> made) in lives)
        {
            if (igi1.Parent is GuardGeneratorTask generator && hosted.AddLives(igi1, made) is { } pool)
            {
                generator.DrivenLives = pool;
            }
        }

        if (carriers is not null)
        {
            hosted.EachTick(carriers.Follow);
        }

        // The player's own events again, for a mission played on without the soldiers.
        hosted.OnLeave(() =>
        {
            if (ReferenceEquals(player.Events, odds))
            {
                player.Events = odds.Inner;
            }
        });
        hosted.Begin();
        log.Information("Mods", $"{hosted.HandedOver.Count} of the mission's soldiers are I.G.I. 2's, at {difficulty}.");
        return hosted;
    }

    // Where a task of the level stands, as a Project I.G.I. guard finds what it is sent to (SoldierBrainBinder).
    private static bool TryPosition(GameTask level, int id, out Vector3d position)
    {
        foreach (GameTask task in level.DescendantsAndSelf())
        {
            if (task.Id == id && task is DeclaredTask declared && declared.TryGetValue("Position", out object? value) && value is Vector3d at)
            {
                position = at;
                return true;
            }
        }

        position = default;
        return false;
    }

    // What a guard carries into the fight: its own Project I.G.I. weapons, each by the id I.G.I. 2 knows it by, so a guard
    // with no grenade throws none; a guard that lists none takes the table's, its grenades and flashbangs left out, as it has
    // none of its own.
    private static string WeaponScriptOf(HumanTask igi1, SoldierMapping mapping, HashSet<string> igi2Weapons)
    {
        string[] own = [.. PlayerLoadout.WeaponsOf(igi1).Select(static w => w.WeaponId).Where(igi2Weapons.Contains).Distinct()];
        if (own.Length > 0)
        {
            return string.Join(", ", own.Select(static id => $"{Igi2HumanWeapons.AddWeaponNative}(\"{id}\")"));
        }

        return string.Join(", ", mapping.Weapon.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(static call => !call.Contains("GRENADE", StringComparison.Ordinal) && !call.Contains("FLASHBANG", StringComparison.Ordinal)));
    }

    // A guard's orders off its own script, MISSION:AI/<mind id>.qsc, read from its compiled form beside it.
    private static Igi1GuardOrders OrdersOf(HumanAiTask? mind, string? missionDirectory)
    {
        if (missionDirectory is null || mind is not { Id: >= 0 })
        {
            return Igi1GuardOrders.None;
        }

        string path = Path.Combine(missionDirectory, "ai", $"{mind.Id}.qvm");
        return File.Exists(path) ? Igi1GuardScript.Read(QvmProgram.Read(File.ReadAllBytes(path))) : Igi1GuardOrders.None;
    }

    // The orders in I.G.I. 2's terms; the alarm ids are the squad's there, not the soldier's.
    private static Igi2SoldierOrders Translate(Igi1GuardOrders orders) => new()
    {
        PatrolPathId = orders.PatrolPathId,
        PatrolStart = orders.PatrolStart,
        AlarmPatrolPathId = orders.AlarmPatrolPathId,
        AlarmPatrolStart = orders.AlarmPatrolStart,
        GunnerId = orders.GunnerId,
        AlarmAccess = orders.AlarmAccess,
        ViewLength = orders.ViewLength,
        ViewGamma = orders.ViewGamma,
        IsInvulnerable = orders.IsInvulnerable,
    };

    // The soldier's Project I.G.I. mind, whose AI type picks its I.G.I. 2 counterpart and whose graph it walks.
    private static HumanAiTask? MindOf(GameTask soldier) => soldier.Children.OfType<HumanAiTask>().FirstOrDefault();

    private static string AiTypeOf(HumanAiTask? mind) => mind?.AiTypeName ?? string.Empty;
}
