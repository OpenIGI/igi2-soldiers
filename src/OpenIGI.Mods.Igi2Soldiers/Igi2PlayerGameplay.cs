using OpenIGI.Engine.Titles;
using OpenIGI.Game.Igi2.Player;
using OpenIGI.Game.Player;

namespace OpenIGI.Mods.Igi2Soldiers;

/// <summary>
/// I.G.I. 2's player in Project I.G.I.'s missions: the gameplay the session provides in the game's own stead
/// (<see cref="ITitleGameplay"/>), which stands David Jones where the mission stands its player, or declines a mission and
/// leaves it to the game.
/// </summary>
/// <param name="session">The mod in the run, which stands the player.</param>
internal sealed class Igi2PlayerGameplay(Igi2SoldiersSession session) : ITitleGameplay
{
    private static readonly Igi2Gameplay Igi2 = new();

    /// <inheritdoc/>
    /// <remarks>I.G.I. 2's own: its player reads the keys I.G.I. 2's does.</remarks>
    public IReadOnlyList<TitleInputBinding> InputBindings => Igi2.InputBindings;

    /// <inheritdoc/>
    public ITitlePlayer? CreatePlayer(in TitlePlayerRequest request) => session.CreatePlayer(request);
}

/// <summary>Provides the session's player (<see cref="Igi2PlayerGameplay"/>) as the content the game's sessions are made of.</summary>
/// <param name="gameplay">The player.</param>
internal sealed class Igi2PlayerModule(ITitleGameplay gameplay) : IContentModule
{
    /// <inheritdoc/>
    public string Name => "Igi2Soldiers.Player";

    /// <inheritdoc/>
    public void Register(ContentRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // A session made from another inherits what that one was given: the player is provided once.
        context.Services.GetOrProvide(() => gameplay);
    }

    /// <inheritdoc/>
    public void LoadTables(ContentLoadContext context)
    {
    }
}
