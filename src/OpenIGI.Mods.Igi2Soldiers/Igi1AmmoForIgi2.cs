using OpenIGI.Game.Igi2.Weapons;
using OpenIGI.Game.Weapons;

namespace OpenIGI.Mods.Igi2Soldiers;

/// <summary>
/// Project I.G.I.'s ammunition into I.G.I. 2's pouches: Project I.G.I. keeps rounds by calibre (<c>AMMO_ID_919</c>), I.G.I. 2 by
/// each weapon's clip (<c>AMMO_ID_MP5CLIP</c>), so a calibre's rounds go to the clip of a weapon that fires it.
/// </summary>
/// <remarks>
/// <para>
/// Ammunition both games name alike -- grenades, mines, medipacks, the M203's rounds -- goes in as it is; and, as I.G.I. 2 carries
/// those as the weapon its ammunition is, the weapon is carried too, so a medipack's charges are a medipack to use.
/// </para>
/// <para>
/// A calibre's rounds go to the clip of an I.G.I. 2 weapon whose Project I.G.I. weapon of the same id fires that calibre: one the
/// player carries, the one in hand first, else the first that fires it, for when the player picks one up.
/// </para>
/// </remarks>
public static class Igi1AmmoForIgi2
{
    /// <summary>Puts rounds of a Project I.G.I. ammunition in I.G.I. 2's player's pouches.</summary>
    /// <param name="inventory">The player's inventory.</param>
    /// <param name="ammo">The Project I.G.I. ammunition.</param>
    /// <param name="rounds">How many.</param>
    /// <param name="weapons">Project I.G.I.'s weapons, which say what fires the calibre.</param>
    /// <param name="ammunition">Project I.G.I.'s ammunition, by calibre.</param>
    /// <returns>The I.G.I. 2 ammunition the rounds went to, or <see langword="null"/> where no weapon of I.G.I. 2's fires them.</returns>
    public static string? Add(Igi2Inventory inventory, AmmoType ammo, int rounds, WeaponTypeRegistry? weapons, AmmoTypeRegistry? ammunition)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(ammo);
        Igi2WeaponTables tables = inventory.Tables;
        if (inventory.FindAmmo(ammo.ScriptId) is not null)
        {
            inventory.AddAmmo(ammo.ScriptId, rounds);
            if (!inventory.Weapons.Any(w => w is { IsActive: true } && ClipOf(w.WeaponType, tables) == ammo.ScriptId)
                && tables.WeaponTypes.FirstOrDefault(w => ClipOf(w, tables) == ammo.ScriptId) is { } user)
            {
                inventory.EnableWeaponType(user.WeaponId, addAmmo: false);
            }

            return ammo.ScriptId;
        }

        if (weapons is null || ammunition is null)
        {
            return null;
        }

        HashSet<string> firing = [.. weapons.Weapons
            .Where(w => ammunition.TryGetByCalibre(w.CalibreId, out AmmoType fired) && fired.ScriptId == ammo.ScriptId)
            .Select(static w => w.ScriptId)];
        Igi2WeaponTypeTask? target =
            (inventory.Current is { IsActive: true } held && firing.Contains(held.WeaponType.WeaponId) ? held.WeaponType : null)
            ?? inventory.Weapons.Where(w => w is { IsActive: true } && firing.Contains(w.WeaponType.WeaponId)).Select(static w => w.WeaponType).FirstOrDefault()
            ?? tables.WeaponTypes.FirstOrDefault(w => firing.Contains(w.WeaponId));
        if (target is null || ClipOf(target, tables) is not { } clip)
        {
            return null;
        }

        inventory.AddAmmo(clip, rounds);
        return clip;
    }

    // The ammunition a weapon's primary fire takes.
    private static string? ClipOf(Igi2WeaponTypeTask weapon, Igi2WeaponTables tables) => weapon.AmmoInfo(0, tables).AmmoType?.AmmoId;
}
