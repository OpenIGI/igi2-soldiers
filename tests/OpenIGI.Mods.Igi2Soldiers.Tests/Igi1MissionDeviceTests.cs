using System.Text;
using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Titles;
using OpenIGI.Game.Igi2;
using OpenIGI.Game.Igi2.Ai;
using OpenIGI.Game.Igi2.Ai.Runtime;
using OpenIGI.Mods.Igi2Soldiers.Scripts;
using OpenIGI.Scripting.Qvm;
using OpenIGI.TestSupport;
using Xunit;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>I.G.I. 2's <c>MISSION:</c> device over a Project I.G.I. mission.</summary>
public sealed class Igi1MissionDeviceTests
{
    [RequiresTitleDataFact("igi2")]
    public void The_generic_scripts_leave_every_event_to_the_default_handlers()
    {
        Igi2GenericScripts scripts = Igi2GenericScripts.Find(GameDataSource.Igi2.RequireRoot());
        Assert.Contains("AIFunction_DefaultHandler", Encoding.Latin1.GetString(scripts.Soldier), StringComparison.Ordinal);
        Assert.Contains("AISquad_DefaultHandler", Encoding.Latin1.GetString(scripts.Squad), StringComparison.Ordinal);
    }

    [RequiresTitleDataFact("igi2")]
    public void A_Project_IGI_mission_gives_I_G_I_2s_minds_their_scripts_and_graphs()
    {
        string igi2 = GameDataSource.Igi2.RequireRoot();
        GuestTitle guest = GuestTitle.Start(new Igi2Runtime(), igi2);
        string mission = Path.Combine(GameData.RequireRoot(), "missions", "location0", "level1");
        guest.FileSystem.RegisterDevice(new Igi1MissionDevice(mission, Igi2GenericScripts.Find(igi2)));

        // Any soldier and any squad, by I.G.I. 2's own names for their scripts.
        QvmProgram? soldier = Igi2AiWorld.Load(guest.Resources, Igi2HumanAiTask.ScriptNameFor(2205));
        Assert.NotNull(soldier);
        Assert.NotNull(Igi2AiWorld.Load(guest.Resources, "MISSION:AI/Squad_4.qsc"));

        // The mission's own graph, read as I.G.I. 2 reads its graphs.
        Assert.True(guest.Resources.TryLoad("MISSION:graphs/graph1.dat", out ReadOnlyMemory<byte> bytes));
        Igi2AiGraph graph = Igi2AiGraph.Read(bytes, new Igi2AiGraphPlacement(1000, IsRelative: true, Vector3d.Zero), static _ => 0);
        Assert.NotEmpty(graph.Nodes);
    }
}
