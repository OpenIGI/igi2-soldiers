using OpenIGI.Formats;
using OpenIGI.Formats.World;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.World;

namespace OpenIGI.Mods.Igi2Soldiers.Collision;

/// <summary>
/// I.G.I. 2's collision world built of a Project I.G.I. level: the solids its soldiers stand, look and shoot through, and the
/// ground under them.
/// </summary>
/// <remarks>
/// <para>
/// Both games trace placed models through one engine handler, over the same collision mesh levels and the same row-major
/// local-to-world frame, so each placement becomes an I.G.I. 2 solid as it stands: its model's collision levels read in
/// Project I.G.I.'s layout, its position in units, its orientation copied.
/// </para>
/// <para>
/// Not yet: each solid stands where the level placed it, so a door or a lift that moves, or a placement whose container
/// closes, is not followed; Project I.G.I.'s own index follows both.
/// </para>
/// <para>
/// The games number their game materials differently, so every face's material is translated into the row I.G.I. 2's table
/// gives it; a material it has no row for reads as none, which I.G.I. 2 treats as a plain solid.
/// </para>
/// </remarks>
public static class Igi1CollisionWorld
{
    /// <summary>Builds the world.</summary>
    /// <param name="placements">The level's placed models, as its world scene placed them.</param>
    /// <param name="terrain">The level's terrain, or <see langword="null"/> for none.</param>
    /// <param name="material">Translates a Project I.G.I. game material into I.G.I. 2's row, or <see langword="null"/> for none.</param>
    /// <param name="terrainOwner">What a hit on the ground reports as struck.</param>
    /// <param name="solids">Receives each placement's solid, for <see cref="Igi1Carriers"/>; <see langword="null"/> for none.</param>
    /// <returns>The world.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static Igi2CollisionWorld Build(
        IEnumerable<WorldPlacement> placements,
        TerrainData? terrain,
        Func<int, int?> material,
        object terrainOwner,
        IDictionary<WorldPlacement, Igi2RigidObject>? solids = null)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(terrainOwner);

        List<Igi2RigidObject> made = [];
        foreach (WorldPlacement placement in placements)
        {
            if (Solid(placement, material) is { } solid)
            {
                made.Add(solid);
                solids?.Add(placement, solid);
            }
        }

        Igi1GroundCollider? ground = terrain is null ? null : new Igi1GroundCollider(terrain, material, terrainOwner);
        return new Igi2CollisionWorld(made, ground) { Ground = terrain };
    }

    /// <summary>One placement as an I.G.I. 2 solid, or <see langword="null"/> when its model has no collision.</summary>
    /// <param name="placement">The placement.</param>
    /// <param name="material">Translates a Project I.G.I. game material into I.G.I. 2's row.</param>
    /// <returns>The solid.</returns>
    public static Igi2RigidObject? Solid(WorldPlacement placement, Func<int, int?> material)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(material);
        Igi2CollisionMesh?[] levels = new Igi2CollisionMesh?[MefCollisionMesh.MeshLevels];
        bool any = false;
        for (int level = 0; level < levels.Length; level++)
        {
            if (MefCollisionMesh.TryRead(placement.Model.Container, level, out MefCollisionMesh? mesh, AssetFormatProfile.Igi1.Collision)
                && mesh is { Faces.Length: > 0 })
            {
                Igi2CollisionMesh solid = new(mesh);
                for (int row = 0; row < solid.Rows.Length; row++)
                {
                    solid.Rows[row] = solid.Rows[row] == 0xFFFF ? 0xFFFF : material(solid.Rows[row]) ?? 0xFFFF;
                }

                levels[level] = solid;
                any = true;
            }
        }

        if (!any)
        {
            return null;
        }

        Igi2RigidObject placed = new(
            new Igi2CollisionOwner(placement.Owner.Type.Name, placement.Owner.Id, placement.Instance.Name, placement.IsAttached ? 1 : 0),
            levels,
            placement.Model.NativeMarkerBound,
            isBuildingMesh: false)
        {
            Position = placement.Position * WorldScene.UnitsPerMetre,
        };
        placement.Orientation.CopyTo(placed.Orientation);
        return placed;
    }
}
