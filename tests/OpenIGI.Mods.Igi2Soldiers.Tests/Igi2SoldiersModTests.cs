using OpenIGI.Core.Diagnostics;
using OpenIGI.Engine.Tasks;
using OpenIGI.Engine.Titles;
using OpenIGI.Game.Hosting;
using OpenIGI.Game.Igi1;
using OpenIGI.Game.Igi2;
using OpenIGI.Game.Level;
using OpenIGI.Modding;
using OpenIGI.Scripting.Runtime;
using Xunit;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>The mod as a game configures it.</summary>
public sealed class Igi2SoldiersModTests
{
    [Fact]
    public void Project_IGI_claims_before_its_soldiers_and_spawns_after_its_player()
    {
        IReadOnlyList<string> names = Configured(new Igi1Runtime());
        int claim = Index(names, Igi2SoldiersMod.ClaimSystem);
        int spawn = Index(names, Igi2SoldiersMod.SpawnSystem);
        Assert.Equal(Index(names, MissionSystemNames.Soldiers) - 1, claim);
        Assert.Equal(Index(names, MissionSystemNames.Player) + 1, spawn);
    }

    [Fact]
    public void I_G_I_2_is_left_as_it_ships()
    {
        Igi2Runtime runtime = new();
        IGameTitleRuntime modded = Mods().Extend(runtime);
        Assert.Equal(runtime.Modules.Count, modded.Modules.Count);
    }

    [Fact]
    public void The_manifest_is_for_Project_IGI_and_names_the_mods_assembly()
    {
        ModManifest manifest = ModManifest.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ModManifest.FileName)));
        Assert.Equal(ModApi.Version, manifest.ApiVersion);
        Assert.True(manifest.AppliesTo(Core.Titles.GameTitle.Igi1));
        Assert.False(manifest.AppliesTo(Core.Titles.GameTitle.Igi2));
        Assert.Equal(Path.GetFileName(typeof(Igi2SoldiersMod).Assembly.Location), manifest.Assembly);
    }

    private static ModSet Mods() =>
        new([new InstalledMod(new ModManifest("igi2soldiers", "I.G.I. 2 soldiers", "1.0", ModApi.Version, []), string.Empty, new Igi2SoldiersMod())]);

    // The mission systems a stand-in host lists, as the mod edits them.
    private static IReadOnlyList<string> Configured(Igi1Runtime runtime)
    {
        IGameTitleRuntime modded = Mods().Extend(runtime);
        NativeRegistry natives = new();
        ServiceCatalog services = new();
        ContentRegistrationContext registration = new(
            new TaskTypeRegistry(), natives, new ScriptVarRegistry(natives), services, NullLogger.Instance);
        foreach (IContentModule module in modded.Modules.Skip(runtime.Modules.Count))
        {
            module.Register(registration);
        }

        Assert.True(services.TryGet(out LevelSystemEdits? edits));
        LevelSystems<MissionLevelBuild> host = new();
        foreach (string name in new[]
        {
            MissionSystemNames.Sounds, MissionSystemNames.Scene, MissionSystemNames.Soldiers, MissionSystemNames.Picture,
            MissionSystemNames.Player, MissionSystemNames.Adornments,
        })
        {
            host.Add(new LevelSystem<MissionLevelBuild>(name, static _ => { }));
        }

        return [.. edits.Apply(host).Select(static s => s.Name)];
    }

    private static int Index(IReadOnlyList<string> names, string name)
    {
        int index = names.ToList().IndexOf(name);
        Assert.True(index >= 0, $"{name} is not among the systems");
        return index;
    }
}
