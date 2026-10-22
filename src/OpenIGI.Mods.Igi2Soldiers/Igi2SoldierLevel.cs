using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Tasks;
using OpenIGI.Formats.Ai;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2.Ai;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;

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
/// (<see cref="Igi2HostedLevel"/>). Each soldier's squad is its own, since Project I.G.I. authored no squads; a squad is what lets a
/// mind hear at all.
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
        Igi1ObjectLight? objectLight = null)
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
        List<(HumanTask Igi1, Igi2AiSquadTask Squad, Igi2HumanSoldierTask Soldier)> handedOver = [];

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

    // The soldier's Project I.G.I. mind, whose AI type picks its I.G.I. 2 counterpart and whose graph it walks.
    private static HumanAiTask? MindOf(GameTask soldier) => soldier.Children.OfType<HumanAiTask>().FirstOrDefault();

    private static string AiTypeOf(HumanAiTask? mind) => mind?.AiTypeName ?? string.Empty;
}
