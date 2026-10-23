using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Tasks;
using OpenIGI.Formats.Ai;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi2.Ai.Runtime;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Interaction;
using OpenIGI.Game.Igi2.Scripting;
using OpenIGI.Game.Igi2.Soldiers;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Player;
using OpenIGI.Game.Weapons;
using OpenIGI.Game.World;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.Scripting.Qvm;
using OpenIGI.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>Project I.G.I.'s guards as I.G.I. 2's soldiers keep their orders: squads, patrols, alarms and what a fight leaves.</summary>
public sealed class Igi2SoldierOrdersTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void Mission_one_s_guards_fight_in_squads_walk_their_patrols_and_raise_their_alarms()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = Igi2SoldierLevelTests.LoadMission(igi1, 1);
        Dictionary<int, GraphFile> graphs = NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance);
        HumanPlayerTask player = PlayerSpawn.FindGameplayTask(level)!;

        Igi2GuestContent kit = Igi2GuestContent.Load(GameDataSource.Igi2.RequireRoot(), SoldierTable.Shipped.Models);
        Igi2HostMissionDevice scripts = new(missionDirectory, kit.Scripts);
        kit.Guest.FileSystem.RegisterDevice(scripts);
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());

        bool sounding = false;
        List<int> pressed = [];
        // The sniper is left to Project I.G.I., as a table may leave a type.
        SoldierTable table = SoldierTable.Shipped with
        {
            Soldiers = new Dictionary<string, SoldierMapping?>(SoldierTable.Shipped.Soldiers) { ["AITYPE_SNIPER"] = null },
        };
        Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, table, level, graphs, world, player, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance,
            scripts: scripts, missionDirectory: missionDirectory, alarms: new Igi1Alarms(_ => sounding, pressed.Add));

        // Squads: the guards Project I.G.I. placed together fight together, no more than a squad holds, on one graph.
        Igi2HumanAi[] minds = [.. soldiers.HandedOver.Values.Select(static body => (Igi2HumanAi)body.Mind!)];
        IGrouping<Igi2AiSquad?, Igi2HumanAi>[] squads = [.. minds.GroupBy(static mind => mind.Squad)];
        output.WriteLine($"{minds.Length} soldiers in {squads.Length} squads: {string.Join(", ", squads.Select(static s => s.Count()))}");
        Assert.DoesNotContain(squads, static squad => squad.Key is null);
        Assert.All(squads, static squad => Assert.InRange(squad.Count(), 1, Igi2AiSquadTeam.Capacity));
        Assert.All(squads, static squad => Assert.Single(squad.Select(static mind => mind.Graph).Distinct()));
        Assert.Contains(squads, static squad => squad.Count() > 1);

        // Orders: each guard's own script, read off its Project I.G.I. one, written as I.G.I. 2 writes a soldier's.
        Dictionary<HumanTask, Igi1GuardOrders> orders = soldiers.HandedOver.Keys.ToDictionary(static task => task, task => OrdersOf(task, missionDirectory));
        int patrolling = orders.Values.Count(static o => o.PatrolPathId is not null);
        output.WriteLine($"{scripts.SoldierScripts.Count} scripts written, {patrolling} patrolling");
        Assert.NotEmpty(scripts.SoldierScripts);
        Assert.True(patrolling > 0, "no guard of mission one patrols");

        // The guards Project I.G.I. keeps driving are seen as I.G.I. 2's own see one another.
        HumanSoldierTask[] kept = [.. level.Descendants().OfType<HumanSoldierTask>().Where(static guard => guard.Driver != GameTitle.Igi2)];
        output.WriteLine($"{kept.Length} guards kept by Project I.G.I.");
        Assert.NotEmpty(kept);
        Assert.All(kept, guard => Assert.Contains(guard, soldiers.Detection.Detectables));

        // The world a fight needs: grenades, the guns the dead drop, the casings the guns throw.
        Assert.NotNull(soldiers.Grenades);
        Assert.NotNull(soldiers.Pickups);
        Assert.NotNull(soldiers.Casings);

        Dictionary<HumanTask, Vector3d> start = soldiers.HandedOver.ToDictionary(static p => p.Key, static p => p.Value.Position);
        for (int tick = 0; tick < 600; tick++)
        {
            soldiers.Tick();
        }

        // Their guns time the trigger by the game's tick, as the player's does: each soldier's clock is the tick it last ran in.
        int now = soldiers.Ai.LevelTick();
        Assert.All(
            soldiers.HandedOver.Values.Where(static body => body.IsActive && !body.IsDead),
            body => Assert.InRange(body.Ticks, now - 1, now));

        // Patrols: a guard sent on its path walks it.
        int walked = soldiers.HandedOver.Count(p => orders[p.Key].PatrolPathId is not null && Distance(p.Value.Position, start[p.Key]) > 1024.0);
        output.WriteLine($"{walked} of {patrolling} patrolling guards walked");
        Assert.True(walked * 2 >= patrolling, $"only {walked} of {patrolling} patrolling guards walked");

        // Alarms: a squad's alarm is the mission's. Mission one's guards only hear theirs, and name no trigger to press: as it
        // sounds, the control tells them, and each with a patrol for the alarm walks that one instead.
        (HumanTask alarmed, Igi1GuardOrders alarm) = orders.First(static p => p.Value is { AlarmControlId: not null, AlarmPatrolPathId: not null });
        Igi2AlarmControlTask control = Assert.Single(soldiers.Alarms, a => a.Id == alarm.AlarmControlId);
        Igi2HumanAi listener = (Igi2HumanAi)soldiers.HandedOver[alarmed].Mind!;
        Assert.Equal(alarm.AlarmControlId, listener.Squad!.Task.AlarmControlId);
        Assert.Equal(-1, listener.Squad.Task.AlarmTriggerId);
        Assert.False(control.IsAlarm);
        sounding = true;
        for (int tick = 0; tick < 30; tick++)
        {
            soldiers.Tick();
        }

        Assert.True(control.IsAlarm);
        Assert.Contains(
            listener.Actions.Items,
            item => item.Context is Igi2AiContent.PatrolContext patrol && patrol.PatrolPathId == alarm.AlarmPatrolPathId);
        Assert.Empty(pressed);

        // A guard that dies drops its gun.
        int lying = soldiers.Pickups!.Items.Count;
        // One that walked, so stands in the world, and that its script leaves mortal: mission one keeps a few guards from harm
        // (AIFunction_SetInvulnerability), and they stay so.
        Assert.Contains(orders.Values, static o => o.IsInvulnerable == true);
        Assert.All(
            soldiers.HandedOver.Where(p => orders[p.Key].IsInvulnerable == true),
            static p => Assert.True(((Igi2HumanAi)p.Value.Mind!).IsInvulnerable));
        Igi2HumanSoldier killed = soldiers.HandedOver
            .First(p => orders[p.Key].IsInvulnerable != true && Distance(p.Value.Position, start[p.Key]) > 1024.0).Value;
        ((IWeaponHitTarget)killed).WeaponHit(new WeaponHit(killed.Position, default, 1000f, new Vector3f(1f, 0f, 0f), null, new object()));
        for (int tick = 0; tick < 200 && !killed.IsDeathAnimationDone; tick++)
        {
            soldiers.Tick();
        }

        Assert.True(soldiers.Pickups.Items.Count > lying, "the dead guard dropped nothing");
    }

    private static Igi1GuardOrders OrdersOf(HumanTask task, string missionDirectory)
    {
        HumanAiTask? mind = task.Children.OfType<HumanAiTask>().FirstOrDefault();
        string path = Path.Combine(missionDirectory, "ai", $"{mind?.Id}.qvm");
        return mind is not null && File.Exists(path) ? Igi1GuardScript.Read(QvmProgram.Read(File.ReadAllBytes(path))) : Igi1GuardOrders.None;
    }

    private static double Distance(Vector3d a, Vector3d b)
    {
        Vector3d d = a - b;
        return Math.Sqrt((d.X * d.X) + (d.Y * d.Y) + (d.Z * d.Z));
    }
}
