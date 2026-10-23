using OpenIGI.Core.Configuration;

namespace OpenIGI.Mods.Igi2Soldiers;

/// <summary>What of I.G.I. 2 the player brings into Project I.G.I.'s missions, each its own setting.</summary>
/// <param name="Soldiers">Whether Project I.G.I.'s guards are I.G.I. 2's soldiers.</param>
/// <param name="Player">Whether the player is I.G.I. 2's David Jones, moving, fighting and seeing as I.G.I. 2's player does.</param>
/// <param name="Pickups">Whether the weapons and ammunition lying about are I.G.I. 2's, for I.G.I. 2's player to pick up.</param>
/// <remarks>Each is read as a mission is built, so a change takes effect from the next mission.</remarks>
public sealed record Igi2SoldiersOptions(BoolCVar Soldiers, BoolCVar Player, BoolCVar Pickups);
