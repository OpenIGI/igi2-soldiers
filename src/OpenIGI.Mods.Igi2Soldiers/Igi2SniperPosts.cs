using OpenIGI.Core.Numerics;
using OpenIGI.Game.Igi2.Ai.Runtime;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Humans;
using OpenIGI.Game.Igi2.Soldiers;
using OpenIGI.Game.Physics;

namespace OpenIGI.Mods.Igi2Soldiers;

/// <summary>
/// Project I.G.I.'s snipers as I.G.I. 2's soldiers stand them: with a sniper's sight, and on their posts looking out over the
/// ground they watch.
/// </summary>
/// <remarks>
/// <para>
/// I.G.I. 2 has no sniper type: its levels' snipers are ordinary soldiers whose scripts lengthen and widen their cones and
/// sharpen their aim (<see cref="Tunings"/>), each standing alone in a defensive squad. Project I.G.I.'s snipers are made so.
/// </para>
/// <para>
/// A post is placed with a heading, and a sniper's often faces into the very tower he stands on -- mission one's looks into
/// the water tank. Project I.G.I.'s sniper stands so too, its sight passing where I.G.I. 2's collision stops it. So each
/// sniper is turned to the widest clear view from his eyes, found by casting lines round him at eye height, and his idle
/// head sweep (I.G.I. 2's own, forty-five degrees either way) then crosses open ground; that heading is the one he returns
/// to after an alarm.
/// </para>
/// </remarks>
public static class Igi2SniperPosts
{
    /// <summary>Project I.G.I.'s sniper type, as a guard's AI names it.</summary>
    public const string SniperType = "AITYPE_SNIPER";

    /// <summary>How far a sniper sees, in metres: the furthest I.G.I. 2's own snipers are given.</summary>
    public const float SightMetres = 150f;

    /// <summary>The I.G.I. 2 squad type a sniper stands in alone, <c>AIType_Defensive</c>.</summary>
    public const int DefensiveSquad = 0;

    /// <summary>
    /// What I.G.I. 2's own snipers' scripts set on creation, after the default handler: cones of thirty, forty-five and sixty
    /// degrees idle, investigating and in combat, all reaching as far as he sees; eighty percent accuracy near, half at the
    /// furthest; and the furthest range his aim is judged at, as far as he sees. Its levels' snipers are ordinary soldiers
    /// tuned so (I.G.I. 2 has no sniper type), each in a one-man defensive squad, which takes cover and looks toward a shot
    /// instead of sending him off to search.
    /// </summary>
    public static IReadOnlyList<(string Native, float Value)> Tunings { get; } =
    [
        ("AIFunction_SetIdleViewCone1Alpha", 30f),
        ("AIFunction_SetInvestigateViewCone1Alpha", 45f),
        ("AIFunction_SetCombatViewCone1Alpha", 60f),
        ("AIFunction_SetIdleViewCone1Length", SightMetres),
        ("AIFunction_SetInvestigateViewCone1Length", SightMetres),
        ("AIFunction_SetCombatViewCone1Length", SightMetres),
        ("AIFunction_SetMinRangeAccuracy", 80f),
        ("AIFunction_SetMaxRangeAccuracy", 50f),
        ("AIFunction_SetAccuracyMaxRange", SightMetres),
    ];

    /// <summary>How far a line round a post is cast to judge the view that way, in world units: eighty metres.</summary>
    public const double JudgedReach = 80.0 * 4096.0;

    /// <summary>How many headings round a post are judged.</summary>
    public const int Headings = 36;

    /// <summary>Turns a sniper to the widest clear view from his post.</summary>
    /// <param name="soldier">The sniper.</param>
    /// <param name="world">The collision the view is judged against.</param>
    /// <returns>The heading he was turned to, in radians.</returns>
    public static float Post(Igi2HumanSoldier soldier, Igi2CollisionWorld world)
    {
        ArgumentNullException.ThrowIfNull(soldier);
        ArgumentNullException.ThrowIfNull(world);
        // How far the eye sees each way, at eye height and a little below, as a watcher looks down on the ground.
        Vector3d eye = Igi2Human.EyesAt(soldier.Position, soldier.Pose);
        Span<double> clear = stackalloc double[Headings];
        for (int i = 0; i < Headings; i++)
        {
            double angle = i * Math.Tau / Headings;
            double level = Reach(world, eye, angle, 0.0, soldier), down = Reach(world, eye, angle, -0.25, soldier);
            clear[i] = Math.Max(level, down);
        }

        // The widest clear view: each heading scored with its neighbours, so a gap between two posts of a railing is not taken
        // for open ground.
        int best = 0;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < Headings; i++)
        {
            double score = 0.0;
            for (int k = -3; k <= 3; k++)
            {
                score += clear[(i + k + Headings) % Headings] * (4 - Math.Abs(k));
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        double heading = best * Math.Tau / Headings;
        float gamma = (float)Math.Atan2(-Math.Cos(heading), Math.Sin(heading));
        soldier.PopOrientation(gamma);
        if (soldier.Mind is Igi2HumanAi mind)
        {
            mind.OriginalViewDirection = new Vector3f((float)Math.Cos(heading), (float)Math.Sin(heading), 0f);
        }

        return gamma;
    }

    /// <summary>
    /// How far out from a sniper's eye his lines of sight start, in world units: two and a half metres, past the railing and the
    /// walkway edge of the post he leans over. I.G.I. 2's sight treats them as walls, and mission one's tower post, seen from the
    /// rooftops below, hides a shooter behind them at every few centimetres his head moves; I.G.I. 2 itself sees whatever stands
    /// nearer than that, whatever is between.
    /// </summary>
    public const double SightClearance = 2.5 * 4096.0;

    /// <summary>
    /// Keeps snipers looking past their posts: each tick, a sniper whose sight is set up and starts at his eye is given
    /// <see cref="SightClearance"/>. A soldier's sight is made with his mind's first run, after he is posted.
    /// </summary>
    /// <param name="snipers">The snipers.</param>
    /// <returns>What runs each tick.</returns>
    public static Action LookPastPosts(IReadOnlyList<Igi2HumanSoldier> snipers)
    {
        ArgumentNullException.ThrowIfNull(snipers);
        return () =>
        {
            foreach (Igi2HumanSoldier sniper in snipers)
            {
                if (sniper is { Mind: Igi2HumanAi ai } && ai.HasDetection && ai.Detection.SightClearance < SightClearance)
                {
                    ai.Detection.SightClearance = SightClearance;
                }
            }
        };
    }

    // How far a line of sight from the eye runs a way, tilted, before something stops it, as far as it is judged: through every
    // group a soldier's sight is stopped by, everything counted solid.
    private static double Reach(Igi2CollisionWorld world, Vector3d eye, double angle, double tilt, Igi2HumanSoldier soldier)
    {
        double across = Math.Cos(tilt);
        Vector3d end = new(
            eye.X + (Math.Cos(angle) * across * JudgedReach),
            eye.Y + (Math.Sin(angle) * across * JudgedReach),
            eye.Z + (Math.Sin(tilt) * JudgedReach));
        WorldLineOfSight sight = new(eye, end, Solid.Instance) { Includes = owner => !ReferenceEquals(owner, soldier) };
        world.LineOfSight(sight, uint.MaxValue);
        return sight.IsHit ? sight.T * JudgedReach : JudgedReach;
    }

    // Every material stops sight: a view judged clear is clear whatever it looks through.
    private sealed class Solid : IWorldMaterialTransparency
    {
        public static Solid Instance { get; } = new();

        public float TransparencyOf(int material) => 0f;
    }
}
