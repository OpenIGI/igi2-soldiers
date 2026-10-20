using System.Text;
using System.Text.RegularExpressions;
using OpenIGI.Engine.Flow;
using OpenIGI.Engine.Level;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Tasks;
using OpenIGI.Engine.Time;
using OpenIGI.Engine.Titles;
using OpenIGI.Engine.Vfs;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2;
using OpenIGI.Game.Igi2.Weapons;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.TestSupport;
using Xunit;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>The shipped mapping table, held against both games' data.</summary>
public sealed partial class SoldierTableTests
{
    [Fact]
    public void A_type_mapped_to_nothing_stays_and_an_unnamed_one_takes_the_default()
    {
        SoldierTable table = SoldierTable.Shipped;
        Assert.Null(table.For("AITYPE_CIVILIAN"));
        Assert.Same(table.Default, table.For("AITYPE_NOT_IN_THE_TABLE"));
        Assert.Equal("HUMANAI_TYPE_C2_TOUGH_SOLDIER", table.For("AITYPE_SPETNAZ_GUARD_AK")!.AiType);
        SoldierMapping patrol = table.For("AITYPE_PATROL_AK")!;
        Assert.Equal(patrol.ModelFor(1203), patrol.ModelFor(1203));
    }

    [Fact]
    public void A_table_that_gives_a_soldier_no_model_is_refused() =>
        Assert.Throws<System.Text.Json.JsonException>(() => SoldierTable.Read(
            """{ "soldiers": { "AITYPE_X": { "aiType": "a", "animType": "b", "weapon": "", "models": [] } }, "default": null }"""));

    [RequiresGameDataFact]
    public void Every_soldier_Project_IGI_places_is_covered()
    {
        SoldierTable table = SoldierTable.Shipped;
        HashSet<string> types = [];
        for (int missionId = 1; missionId <= 14; missionId++)
        {
            foreach (HumanSoldierTask soldier in Level(missionId).DescendantsAndSelf().OfType<HumanSoldierTask>())
            {
                if (soldier.Children.OfType<DeclaredTask>().FirstOrDefault(static c => c.Type.Name == "HumanAI") is { } ai
                    && ai.TryGetValue("AI Type", out object? type) && type is string name)
                {
                    types.Add(name);
                }
            }
        }

        Assert.True(types.Count > 20, $"only {types.Count} AI types found");
        Assert.All(types, type => Assert.True(table.Soldiers.ContainsKey(type), $"{type} falls to the default"));
    }

    [RequiresTitleDataFact("igi2")]
    public void Everything_the_table_names_is_I_G_I_2s()
    {
        string root = GameDataSource.Igi2.RequireRoot();
        GuestTitle guest = GuestTitle.Start(new Igi2Runtime(), root);
        HashSet<string> weapons = [.. guest.Session.Services.Require<Igi2WeaponTables>().WeaponTypes.Select(static w => w.WeaponId)];
        string packs = string.Concat(Directory.GetFiles(Path.Combine(root, "MISSIONS"), "location*.mtp", SearchOption.AllDirectories)
            .Select(static p => Encoding.Latin1.GetString(File.ReadAllBytes(p))));

        foreach (SoldierMapping mapping in SoldierTable.Shipped.Soldiers.Values.Append(SoldierTable.Shipped.Default).OfType<SoldierMapping>())
        {
            // The names level data gives a HumanAI are constants of the game's own (HumanAI_Open registers them, aliases too).
            Assert.True(guest.Loop.Natives.TryResolve(mapping.AiType, out _), $"{mapping.AiType} is no I.G.I. 2 constant");
            Assert.True(guest.Loop.Natives.TryResolve(mapping.AnimType, out _), $"{mapping.AnimType} is no I.G.I. 2 constant");
            Assert.All(WeaponId().Matches(mapping.Weapon), m => Assert.Contains(m.Value, weapons));
            Assert.All(mapping.Models, model => Assert.Contains(model, packs, StringComparison.Ordinal));
        }
    }

    [GeneratedRegex("WEAPON_ID_[A-Z0-9_]+")]
    private static partial Regex WeaponId();

    private static LevelTask Level(int missionId)
    {
        VirtualFileSystem fileSystem = new();
        fileSystem.RegisterDevice(new LocalFileDevice(GameData.RequireRoot()));
        ResourceRegistry resources = new(fileSystem);
        GameLoop loop = new(new ManualTimeSource());
        PlayerTaskTypes.RegisterAll(loop.Types);
        loop.Types.Register<ConditionalContainerTask>(ConditionalContainerTask.TypeName);
        LevelTask levelRoot = (LevelTask)loop.Tasks.New(loop.Tasks.Root, loop.Types.Get(EngineTaskTypes.Level));
        MissionRegistry registry = new();
        registry.Register(loop.Natives);
        registry.Scan(fileSystem, resources, loop.Natives);
        LevelLoader levelLoader = new(levelRoot, loop.TaskCommands, loop.Natives, resources);
        _ = new MissionLoader(registry, fileSystem, resources, levelLoader).Load(missionId);
        return levelRoot;
    }
}
