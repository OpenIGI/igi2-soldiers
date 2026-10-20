using OpenIGI.Core.Numerics;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Player;

namespace OpenIGI.Mods.Igi2Soldiers.Collision;

/// <summary>A Project I.G.I. human in I.G.I. 2's collision world: what I.G.I. 2's rounds strike and its soldiers' sight is stopped by.</summary>
/// <remarks>
/// <para>
/// A line meets the human as Project I.G.I.'s own stand-in cast meets one (<see cref="HumanHitTest.TryCast"/>): its body from its
/// feet up, the zone found the way Project I.G.I. finds it. The struck task is the human's, which takes the engine's
/// <c>WeaponHit</c> and reads I.G.I. 2's round in its own game's terms. The face a round enters is the human collision material,
/// which both games' material tables hold in the same row.
/// </para>
/// <para>A dead human, or one whose body is not in the level, stops nothing.</para>
/// </remarks>
public sealed class Igi1HumanCollider : IIgi2LineCollider
{
    /// <summary>The game-material row a round entering a human reports, <c>HumanCollision</c>.</summary>
    public const int HumanMaterial = 21;

    private readonly HumanTask _human;

    /// <summary>Initializes a new instance of the <see cref="Igi1HumanCollider"/> class.</summary>
    /// <param name="human">The human, its body given by its game.</param>
    public Igi1HumanCollider(HumanTask human) => _human = human ?? throw new ArgumentNullException(nameof(human));

    /// <inheritdoc/>
    public object Owner => _human;

    /// <inheritdoc/>
    public object? Handler => _human;

    /// <inheritdoc/>
    public uint Group => Igi2CollisionWorld.HumanGroup;

    /// <inheritdoc/>
    public void NextZ(Igi2LineTrace trace)
    {
    }

    /// <inheritdoc/>
    public void TraceLines(Igi2LineTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        if (Stands() is not { } feet)
        {
            return;
        }

        for (int i = 0; i < trace.Count; i++)
        {
            if (!HumanHitTest.TryCast(trace.Starts[i], trace.Ends[i], feet, Facing, pose: null, out HumanHit met))
            {
                continue;
            }

            Igi2Intersection hit = trace.Intersections[i];
            if (hit.IsHit && !(met.Distance < hit.Distance))
            {
                continue;
            }

            Vector3d line = trace.Ends[i] - trace.Starts[i];
            double length = line.Length;
            Vector3f back = length > 0.0 ? new Vector3f((float)(-line.X / length), (float)(-line.Y / length), (float)(-line.Z / length)) : default;
            hit.IsHit = true;
            hit.HitObject = this;
            hit.Position = met.Position;
            hit.LocalPosition = new Vector3f((float)(met.Position.X - feet.X), (float)(met.Position.Y - feet.Y), (float)(met.Position.Z - feet.Z));
            hit.Normal = back;
            hit.LocalNormal = back;
            hit.Distance = met.Distance;
            hit.EnterMaterial = HumanMaterial;
            hit.LeaveMaterial = null;
            hit.IsDoubleFace = false;
            hit.Sector = met.Bone;
            hit.Light = 1f;
        }
    }

    /// <inheritdoc/>
    public void LineOfSight(Igi2LineOfSight sight)
    {
        ArgumentNullException.ThrowIfNull(sight);
        if (Stands() is { } feet && HumanHitTest.TryCast(sight.Start, sight.End, feet, Facing, pose: null, out _))
        {
            sight.IsHit = true;
            sight.Transparency = 0f;
            sight.Material = HumanMaterial;
        }
    }

    // The way the stand-in faces: it is round, so only its zones turn with it.
    private static Vector3f Facing => new(0f, 1f, 0f);

    // Where the human stands, while it lives in the level.
    private Vector3d? Stands() => _human is { IsDead: false, Body: { IsActive: true } body } ? body.Feet : null;
}
