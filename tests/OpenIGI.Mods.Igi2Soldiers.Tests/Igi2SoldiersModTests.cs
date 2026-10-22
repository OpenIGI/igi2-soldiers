using OpenIGI.Core.Content;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Titles;
using OpenIGI.Game.Hosting;
using OpenIGI.Game.Igi1;
using OpenIGI.Game.Igi2;
using OpenIGI.Modding;
using OpenIGI.Modding.Testing;
using Xunit;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>The mod as a game configures it.</summary>
public sealed class Igi2SoldiersModTests
{
    // The mission systems a Project I.G.I. host lists, in its order, which the mod's own go among.
    private static readonly string[] MissionSystems =
    [
        MissionSystemNames.Sounds, MissionSystemNames.Scene, MissionSystemNames.Soldiers, MissionSystemNames.Picture,
        MissionSystemNames.Player, MissionSystemNames.Adornments,
    ];

    [Fact]
    public void Project_IGI_claims_before_its_soldiers_and_spawns_after_its_player()
    {
        IReadOnlyList<string> names = Harness().Start(new Igi1Runtime()).LevelSystems<MissionLevelBuild>(MissionSystems);

        int claim = Index(names, Igi2SoldiersMod.ClaimSystem);
        int spawn = Index(names, Igi2SoldiersMod.SpawnSystem);
        Assert.Equal(Index(names, MissionSystemNames.Soldiers) - 1, claim);
        Assert.Equal(Index(names, MissionSystemNames.Player) + 1, spawn);
    }

    [Fact]
    public void I_G_I_2_is_left_as_it_ships()
    {
        // The mod as installed, its manifest for Project I.G.I. alone.
        Igi2Runtime runtime = new();
        IGameTitleRuntime modded = ModHarness.Installed("igi2soldiers").Extend(runtime);
        Assert.Equal(runtime.Modules.Count, modded.Modules.Count);
    }

    [Fact]
    public void The_manifest_is_for_Project_IGI_reads_I_G_I_2_and_names_the_mods_assembly()
    {
        // Written from the project's properties into the package the launcher installs.
        ModManifest manifest = ModManifest.Parse(File.ReadAllText(Path.Combine(Installed, ModManifest.FileName)));
        Assert.Equal(ModApi.Version, manifest.ApiVersion);
        Assert.True(manifest.AppliesTo(GameTitle.Igi1));
        Assert.False(manifest.AppliesTo(GameTitle.Igi2));
        Assert.Equal([GameTitle.Igi2.Id], manifest.RequiresTitleData);
        Assert.Equal(Path.GetFileName(typeof(Igi2SoldiersMod).Assembly.Location), manifest.Assembly);
    }

    [Fact]
    public void Its_package_carries_none_of_the_games_assemblies_and_loads_through_the_loader()
    {
        Assert.DoesNotContain(
            Directory.EnumerateFiles(Installed, "*.dll", SearchOption.AllDirectories),
            static file => Path.GetFileName(file) != "OpenIGI.Mods.Igi2Soldiers.dll");

        ModHarness harness = ModHarness.Installed("igi2soldiers");
        Assert.NotNull(harness.Mod.Code);
        Igi1Runtime runtime = new();
        Assert.True(harness.Extend(runtime).Modules.Count > runtime.Modules.Count);
    }

    // The package as the SDK installs it beside the tests.
    private static string Installed => Path.Combine(AppContext.BaseDirectory, ModHarness.ModsDirectoryName, "igi2soldiers");

    private static ModHarness Harness() => ModHarness.Of(new Igi2SoldiersMod(), "igi2soldiers");

    private static int Index(IReadOnlyList<string> names, string name)
    {
        int index = names.ToList().IndexOf(name);
        Assert.True(index >= 0, $"{name} is not among the systems");
        return index;
    }
}
