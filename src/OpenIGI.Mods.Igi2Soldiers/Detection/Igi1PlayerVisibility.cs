using OpenIGI.Core.Numerics;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi2.Weapons;
using OpenIGI.Game.Player;
using OpenIGI.Mods.Igi2Soldiers.Mapping;

namespace OpenIGI.Mods.Igi2Soldiers.Detection;

/// <summary>
/// How well the I.G.I. 2 soldiers see the Project I.G.I. player: I.G.I. 2's own measure of its player, worked out for the
/// Project I.G.I. one each tick.
/// </summary>
/// <remarks>
/// <para>
/// I.G.I. 2's detectors see a human only as far as its visibility factor times their cone's length (<c>AIDetection_RunDetection</c>),
/// and its player's factor is never one: <c>HumanPlayer_UpdateVisibilityFactor</c> takes half the light the player stands in to
/// the level's visibility gamma, four fifths of that crouched, more while moving (three twentieths running, a tenth walking), and
/// keeps it between the level's minimum and maximum; for a while after a loud shot the light does not count. Project I.G.I.
/// measures nothing of the kind, so without this its player would be seen at a cone's full length, and a soldier 70 metres off
/// opens fire as a level starts where Project I.G.I.'s own guards, which see 40, do not.
/// </para>
/// <para>
/// The light is what the level bakes into the ground under the player's feet (<see cref="Igi1GroundLight"/>), as I.G.I. 2 reads
/// its own player's from the lightmap it stands on; where the level bakes none, the table's (<see cref="PlayerVisibility.Light"/>).
/// Moving is read from how far the feet go in a tick; turning on the spot, which I.G.I. 2 counts a
/// twentieth, is not seen through the body. A loud shot is one of the player's own gunshots that reaches further than I.G.I. 2's
/// loud range, heard as the level broadcasts it.
/// </para>
/// </remarks>
/// <param name="player">The Project I.G.I. player.</param>
/// <param name="tuning">The visibility parameters.</param>
/// <param name="ground">The level's ground light, or <see langword="null"/> where it bakes none.</param>
internal sealed class Igi1PlayerVisibility(HumanPlayerTask player, PlayerVisibility tuning, Igi1GroundLight? ground = null)
    : IHumanAiEventTarget
{
    /// <summary>How fast, in metres a second, the feet go at the least that counts as moving.</summary>
    public const double MovingSpeed = 0.3;

    /// <summary>How fast the feet go at the least that counts as running: between Project I.G.I.'s walk and its 4.9 m/s run.</summary>
    public const double RunningSpeed = 3.0;

    private Vector3d? _lastFeet;
    private int _ignoreLight;

    /// <summary>Gets the player.</summary>
    public HumanPlayerTask Player => player;

    /// <summary>Gets the light the player stood in at the last update, on I.G.I. 2's scale.</summary>
    public float Light { get; private set; } = tuning.Light;

    /// <inheritdoc/>
    /// <remarks>The player's own gunshot past I.G.I. 2's loud range lights it for <see cref="Igi2GunFire.LoudVisibilityTicks"/>.</remarks>
    public void SendEvent(in HumanAiEvent aiEvent)
    {
        if (aiEvent.Type == (int)AiEvent.Gunshot && ReferenceEquals(aiEvent.Source, player) && aiEvent.Radius > Igi2GunFire.LoudRange)
        {
            _ignoreLight = Igi2GunFire.LoudVisibilityTicks;
        }
    }

    /// <summary>Works the factor out for the tick just run and gives it to the player.</summary>
    /// <returns>The factor.</returns>
    public float Update()
    {
        Vector3d feet = player.Body?.Feet ?? player.Position;
        double speed = 0.0;
        if (_lastFeet is { } last)
        {
            double dx = feet.X - last.X, dy = feet.Y - last.Y;
            speed = Math.Sqrt((dx * dx) + (dy * dy)) * 30.0 / 4096.0;
        }

        _lastFeet = feet;
        Light = ground?.At(feet) ?? tuning.Light;
        float factor = Measure(_ignoreLight != 0, player.Body?.IsKeepingLow ?? false, speed, tuning, Light);
        if (_ignoreLight > 0)
        {
            _ignoreLight--;
        }

        player.VisibilityFactor = factor;
        return factor;
    }

    /// <summary>The factor for a stance and a speed, <c>HumanPlayer_UpdateVisibilityFactor</c>'s steps.</summary>
    /// <param name="lit">Whether a loud shot keeps the player seen whatever the light.</param>
    /// <param name="low">Whether the player keeps low.</param>
    /// <param name="speed">How fast the feet go, in metres a second.</param>
    /// <param name="tuning">The visibility parameters.</param>
    /// <param name="light">The light the player stands in, on I.G.I. 2's scale; <see langword="null"/> for the table's.</param>
    /// <returns>The factor.</returns>
    public static float Measure(bool lit, bool low, double speed, PlayerVisibility tuning, float? light = null)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        float factor = 1f;
        if (!lit)
        {
            double half = Math.Clamp((light ?? tuning.Light) * 0.5, 0.0, 1.0);
            factor = (float)Math.Pow(half, tuning.Gamma);
        }

        if (low)
        {
            factor = (float)(factor * 0.800000011920929);
        }

        if (speed >= RunningSpeed)
        {
            factor += 0.15000000596046448f;
        }
        else if (speed >= MovingSpeed)
        {
            factor += 0.10000000149011612f;
        }

        return factor < tuning.Minimum ? tuning.Minimum : factor > tuning.Maximum ? tuning.Maximum : factor;
    }

    /// <summary>Gives the player back the factor Project I.G.I. sees it at, as the mod leaves the level.</summary>
    public void Reset() => player.VisibilityFactor = 1f;
}
