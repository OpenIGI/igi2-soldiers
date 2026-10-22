using OpenIGI.Formats;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.World;

namespace OpenIGI.Mods.Igi2Soldiers.Collision;

/// <summary>
/// I.G.I. 2's collision world built of a Project I.G.I. level (<see cref="Igi2PlacedCollision"/>): its placements read in
/// Project I.G.I.'s collision layout, and its ground Project I.G.I.'s own answering I.G.I. 2's lines (<see cref="Igi1GroundCollider"/>).
/// </summary>
public static class Igi1CollisionWorld
{
    /// <summary>Builds the world.</summary>
    /// <param name="placements">The level's placed models, as its world scene placed them.</param>
    /// <param name="terrain">The level's terrain, or <see langword="null"/> for none.</param>
    /// <param name="material">Translates a Project I.G.I. game material into I.G.I. 2's row, or <see langword="null"/> for none.</param>
    /// <param name="terrainOwner">What a hit on the ground reports as struck.</param>
    /// <param name="solids">Receives each placement's solid, for <see cref="Igi2PlacedCarriers"/>; <see langword="null"/> for none.</param>
    /// <returns>The world.</returns>
    public static Igi2CollisionWorld Build(
        IEnumerable<WorldPlacement> placements,
        TerrainData? terrain,
        Func<int, int?> material,
        object terrainOwner,
        IDictionary<WorldPlacement, Igi2RigidObject>? solids = null)
    {
        ArgumentNullException.ThrowIfNull(terrainOwner);
        Igi1GroundCollider? ground = terrain is null ? null : new Igi1GroundCollider(terrain, material, terrainOwner);
        return Igi2PlacedCollision.Build(placements, AssetFormatProfile.Igi1.Collision, ground, terrain, material, solids);
    }
}
