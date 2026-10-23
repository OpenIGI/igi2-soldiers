using OpenIGI.Engine.Flow;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Time;
using OpenIGI.Engine.Vfs;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Igi2.Weapons;
using OpenIGI.Game.Weapons;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.TestSupport;
using Xunit;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>Project I.G.I.'s ammunition, by calibre, into I.G.I. 2's pouches, by clip.</summary>
public sealed class Igi1AmmoForIgi2Tests
{
    [RequiresTitleDataFact("igi2")]
    public void A_calibre_goes_to_the_clip_of_a_weapon_that_fires_it_and_a_medipack_charge_is_a_medipack()
    {
        (WeaponTypeRegistry weapons, AmmoTypeRegistry ammunition) = Igi1Tables();
        Igi2GuestContent kit = Igi2GuestContent.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped.Models);
        Igi2HumanWeapons arms = kit.Guest.Session.Services.Require<Igi2HumanWeapons>();
        Igi2Inventory inventory = arms.CreateInventory([]);
        arms.RunWeaponScript(inventory, "Human_AddWeapon(\"WEAPON_ID_MP5SD\")");

        // 9 mm: the MP5SD Jones carries fires it, so its clip takes the rounds.
        AmmoType nine = ammunition.Types.Single(static a => a.ScriptId == "AMMO_ID_919");
        int before = inventory.FindAmmo("AMMO_ID_MP5CLIP")!.Count;
        Assert.Equal("AMMO_ID_MP5CLIP", Igi1AmmoForIgi2.Add(inventory, nine, 30, weapons, ammunition));
        Assert.Equal(before + 30, inventory.FindAmmo("AMMO_ID_MP5CLIP")!.Count);

        // A medipack's charges: the same ammunition in both games, and the medipack carried with them.
        AmmoType medipack = ammunition.Types.Single(static a => a.ScriptId == "AMMO_ID_MEDIPACK");
        Assert.Equal("AMMO_ID_MEDIPACK", Igi1AmmoForIgi2.Add(inventory, medipack, 1, weapons, ammunition));
        Assert.Contains(inventory.Weapons, static w => w is { IsActive: true } && w.WeaponType.WeaponId == "WEAPON_ID_MEDIPACK");
    }

    private static (WeaponTypeRegistry, AmmoTypeRegistry) Igi1Tables()
    {
        VirtualFileSystem files = new();
        files.RegisterDevice(new LocalFileDevice(GameData.RequireRoot()));
        ResourceRegistry resources = new(files);
        GameLoop loop = new(new ManualTimeSource());
        WeaponTypeRegistry weapons = new();
        weapons.Register(loop.Natives);
        weapons.Load(files, resources, loop.Natives);
        AmmoTypeRegistry ammo = new();
        ammo.Register(loop.Natives);
        ammo.Load(files, resources, loop.Natives);
        return (weapons, ammo);
    }
}
