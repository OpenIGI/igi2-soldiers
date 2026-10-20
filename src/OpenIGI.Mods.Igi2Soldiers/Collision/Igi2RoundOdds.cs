using OpenIGI.Core.Numerics;
using OpenIGI.Game.Igi2.Humans;
using OpenIGI.Game.Igi2.Weapons;
using OpenIGI.Game.Player;
using OpenIGI.Game.Weapons;

namespace OpenIGI.Mods.Igi2Soldiers.Collision;

/// <summary>The Project I.G.I. player's hit events with I.G.I. 2's odds of a round striking the player.</summary>
/// <remarks>
/// <para>
/// Both games give a round fired at the player a chance of striking (<see cref="WeaponCollisionQuery.HitProbability"/>), but roll
/// it on different sides. Project I.G.I.'s cast rolls it before asking (the player's own evaluate answers yes to everything);
/// I.G.I. 2's cast asks, and the player's evaluate rolls it: struck when a draw is no more than the probability
/// (<c>Human_WeaponCollisionEvaluateHandler</c>, <c>igi2 0x4FF4E0</c>). An I.G.I. 2 round asking a Project I.G.I. player would be
/// rolled by neither and strike every time, so for a round an I.G.I. 2 human fired this rolls it as I.G.I. 2's player would,
/// on the soldiers' run stream, before the player's own handler answers.
/// </para>
/// <para>
/// A round or blast of I.G.I. 2's reaches the player's own events with its damage multiplied by the table's
/// <see cref="Mapping.SoldierTable.DamageFromIgi2"/>; every other question, and every other hit, is the player's own events'.
/// </para>
/// </remarks>
/// <param name="inner">The player's own events, or <see langword="null"/> for a player with none, which takes every round.</param>
/// <param name="random">The stream the roll draws from.</param>
/// <param name="damageScale">What an I.G.I. 2 round's or blast's damage is multiplied by.</param>
internal sealed class Igi2RoundOdds(IHumanEvents? inner, IgiRandom random, float damageScale = 1f) : IHumanEvents
{
    /// <summary>Gets the player's own events.</summary>
    public IHumanEvents? Inner { get; } = inner;

    /// <inheritdoc/>
    public bool EvaluateWeaponCollision(HumanTask human, in WeaponCollisionQuery query) =>
        (query.Shooter is not Igi2Human || !(random.NextDouble() > query.HitProbability))
        && (Inner?.EvaluateWeaponCollision(human, query) ?? true);

    /// <inheritdoc/>
    public void WeaponHit(HumanTask human, in WeaponHit hit) =>
        Inner?.WeaponHit(human, hit.Context is Igi2WeaponHit && damageScale != 1f ? hit with { Damage = hit.Damage * damageScale } : hit);

    /// <inheritdoc/>
    public void ExplosionHit(HumanTask human, in ExplosionHit hit) =>
        Inner?.ExplosionHit(human, hit.Context is Igi2ExplosionHit && damageScale != 1f ? hit with { Damage = hit.Damage * damageScale } : hit);
}
