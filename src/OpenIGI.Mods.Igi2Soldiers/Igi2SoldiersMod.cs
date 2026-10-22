using OpenIGI.Game.Hosting;
using OpenIGI.Modding;
using OpenIGI.Modding.Presentation;
using OpenIGI.Mods.Igi2Soldiers.Mapping;

namespace OpenIGI.Mods.Igi2Soldiers;

/// <summary>Project I.G.I.'s missions spawn I.G.I. 2's soldiers, which run I.G.I. 2's AI.</summary>
/// <remarks>
/// <para>
/// Two systems join Project I.G.I.'s mission build. <see cref="ClaimSystem"/> runs before the game's soldiers are given their
/// bodies and minds, and claims those the table maps; <see cref="SpawnSystem"/> runs once the player stands, and spawns the
/// I.G.I. 2 soldiers that stand for them, puts them in the level and has its walk run them.
/// </para>
/// <para>
/// The table is <c>igi2soldiers.json</c> beside the mod's assembly when there is one, else the one the mod ships. Without
/// I.G.I. 2 installed nothing is claimed, and the missions play as they ship.
/// </para>
/// </remarks>
public sealed class Igi2SoldiersMod : IOpenIgiMod
{
    /// <summary>The system that claims the soldiers.</summary>
    public const string ClaimSystem = "Igi2Soldiers.Claim";

    /// <summary>The system that spawns them.</summary>
    public const string SpawnSystem = "Igi2Soldiers.Spawn";

    /// <inheritdoc/>
    /// <remarks>Its manifest is for Project I.G.I. alone, so this runs for that game alone.</remarks>
    public void Configure(IModBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Igi2SoldiersSession session = new(SoldierTable.Load(builder.Directory));

        builder.LevelSystems<MissionLevelBuild>()
            .InsertBefore(MissionSystemNames.Soldiers, ClaimSystem, session.Claim)
            .InsertAfter(MissionSystemNames.Player, SpawnSystem, session.Spawn);
        builder.OnStarted(session.Start);
        builder.Presenters(presenters => presenters.ForLevel(session.Presenter));
    }
}
