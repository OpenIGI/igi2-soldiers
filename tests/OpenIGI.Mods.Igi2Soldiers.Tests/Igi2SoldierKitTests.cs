using OpenIGI.Formats.World;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.TestSupport;
using Xunit;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>What the mod's soldiers are made of, read from the I.G.I. 2 installation.</summary>
public sealed class Igi2SoldierKitTests
{
    [RequiresTitleDataFact("igi2")]
    public void The_kit_opens_the_packs_the_table_dresses_soldiers_from_and_their_clips()
    {
        Igi2GuestContent kit = Igi2GuestContent.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped.Models);

        // The global packs, then Russia's and Libya's: the shipped table dresses nobody from China.
        Assert.Equal(["common", "new", "location1", "location2"], kit.Packs.Select(static p => p.Name.ToLowerInvariant()));
        Assert.True(kit.Clips.RigCount > 0 && kit.Clips.ClipCount > 100, $"{kit.Clips.RigCount} rigs, {kit.Clips.ClipCount} clips");

        foreach (string model in SoldierTable.Shipped.Soldiers.Values.Append(SoldierTable.Shipped.Default).OfType<SoldierMapping>().SelectMany(static m => m.Models).Distinct())
        {
            MefModel? body = kit.Models(model);
            Assert.True(body is { BoneHierarchy: not null }, $"{model} is not a boned body");
        }
    }
}
