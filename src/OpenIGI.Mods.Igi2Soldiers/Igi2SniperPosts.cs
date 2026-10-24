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
/// Project I.G.I. gives its sniper type a hundred metres of sight in a narrow cone, against forty for a guard; I.G.I. 2 has
/// no sniper type, and the ordinary soldier the table maps one to sees as far as any other. The sight is set where I.G.I. 2
/// keeps it, the soldier's idle, investigating and combat cones (the tuning each tick's view is taken from), so it holds.
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

    /// <summary>How far a sniper sees, in world units: Project I.G.I.'s hundred metres.</summary>
    public const float SightUnits = 100f * 4096f;

    /// <summary>How far a line round a post is cast to judge the view that way, in world units: eighty metres.</summary>
    public const double JudgedReach = 80.0 * 4096.0;

    /// <summary>How many headings round a post are judged.</summary>
    public const int Headings = 36;

    /// <summary>Gives a sniper his sight and turns him to the widest clear view from his post.</summary>
    /// <param name="soldier">The sniper.</param>
    /// <param name="world">The collision the view is judged against.</param>
    /// <returns>The heading he was turned to, in radians.</returns>
    public static float Post(Igi2HumanSoldier soldier, Igi2CollisionWorld world)
    {
        ArgumentNullException.ThrowIfNull(soldier);
        ArgumentNullException.ThrowIfNull(world);
        if (soldier.Mind is Igi2HumanAi ai)
        {
            foreach (int length in (int[])[Igi2AiTweakIndex.IdleViewCone1Length, Igi2AiTweakIndex.InvestigateViewCone1Length, Igi2AiTweakIndex.CombatViewCone1Length])
            {
                ai.Tweaks[length] = Math.Max(ai.Tweaks[length], SightUnits);
            }
        }

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
