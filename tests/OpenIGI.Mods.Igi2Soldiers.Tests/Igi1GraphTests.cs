using OpenIGI.Core.Diagnostics;
using OpenIGI.Engine.Flow;
using OpenIGI.Engine.Level;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Tasks;
using OpenIGI.Engine.Time;
using OpenIGI.Engine.Vfs;
using OpenIGI.Formats.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2.Ai;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.TestSupport;
using Xunit;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>Project I.G.I.'s graphs, as I.G.I. 2's AI walks them.</summary>
public sealed class Igi1GraphTests
{
    [RequiresGameDataFact]
    public void A_graph_Project_IGI_placed_is_the_same_graph_to_I_G_I_2s_AI()
    {
        LevelTask level = Level(1);
        string directory = Path.Combine(GameData.RequireRoot(), "missions", "location0", "level1", "graphs");
        Dictionary<int, GraphFile> placed = NavGraphs.Read(directory, level, NullLogger.Instance);
        Assert.NotEmpty(placed);

        foreach ((int id, GraphFile file) in placed)
        {
            Igi2AiGraph graph = Igi2AiGraph.From(file, static _ => 0);
            Assert.Equal(file.Nodes.Count, graph.Nodes.Count);
            Assert.Equal(file.Links.Count, graph.Links.Count);
            foreach (GraphNode node in file.Nodes)
            {
                Igi2AiGraphNode walked = graph.Nodes[node.Id];
                Assert.Equal(node.Position, walked.Position);
                Assert.Equal(node.Radius, walked.Radius);
            }

            foreach (Igi2AiGraphLink link in graph.Links)
            {
                double distance = (graph.Nodes[link.NodeA].Position - graph.Nodes[link.NodeB].Position).Length;
                Assert.Equal(distance, link.Length, 1.0);
            }
        }
    }

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
