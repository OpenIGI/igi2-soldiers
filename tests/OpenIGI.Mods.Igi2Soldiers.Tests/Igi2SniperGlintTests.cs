using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Tasks;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi2.Ai.Runtime;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Player;
using OpenIGI.Game.Igi2.Scripting;
using OpenIGI.Game.Igi2.Soldiers;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.Modding.Testing;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>Mission one's sniper on the water tower, his Dragunov's scope glinting at I.G.I. 2's player where it points his way.</summary>
public sealed class Igi2SniperGlintTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void The_tower_sniper_glints_at_jones_standing_where_he_looks()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = Igi2SoldierLevelTests.LoadMission(igi1, 1);
        Igi2GuestContent kit = Igi2GuestContent.Load(
            GameDataSource.Igi2.RequireRoot(),
            SoldierTable.Shipped.Models.Append(Igi2GuestPlayerStart.JonesModel).Append(Igi2Gameplay.FirstPersonFallbackModel));
        kit.Guest.FileSystem.RegisterDevice(new Igi2HostMissionDevice(missionDirectory, kit.Scripts));
        HumanPlayerTask task = PlayerSpawn.FindGameplayTask(level)!;
        Assert.True(PlayerSpawn.TryFindGameplay(level, out PlayerSpawnPoint spawn));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());
        Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
        Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal) { WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit) });
        Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: guest);

        Igi2HumanSoldier[] snipers = [.. soldiers.HandedOver.Values.Where(static s => Game.Weapons.ScopedWeapons.IsScoped(s.Weapon.Inventory?.Current?.WeaponType.WeaponId))];
        output.WriteLine($"{snipers.Length} scoped of {soldiers.HandedOver.Count}: {string.Join(", ", snipers.Select(static s => $"{s.Weapon.Inventory?.Current?.WeaponType.WeaponId} at {s.Position}"))}");
        Assert.NotEmpty(snipers);
        Igi2HumanSoldier sniper = snipers[0];
        // His post faces into the tower's own frame (the level's gamma for him is nought, as Project I.G.I.'s own sniper's is),
        // so he is turned about to look out over the compound, where nothing stands between him and Jones.
        for (int tick = 0; tick < 30; tick++)
        {
            soldiers.Tick();
        }

        sniper.PopOrientation(MathF.PI);
        for (int tick = 0; tick < 2; tick++)
        {
            soldiers.Tick();
        }

        Igi2HumanAi mind = (Igi2HumanAi)sniper.Mind!;
        float[] m = mind.DetectionView.Orientation;
        double fx = m[1], fy = m[4];
        double flat = Math.Max(1e-6, Math.Sqrt((fx * fx) + (fy * fy)));
        output.WriteLine($"he looks along ({fx:F2}, {fy:F2}, {m[7]:F2})");

        // Level with him sixty metres out along his view: the glass shows him, full.
        jones.Human.PopPosition(new Vector3d(sniper.Position.X + (fx / flat * 245760.0), sniper.Position.Y + (fy / flat * 245760.0), sniper.Position.Z));
        jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
        (Vector3d Position, float Strength) glint = Assert.Single(jones.ScopeGlints());
        output.WriteLine($"glint at {glint.Position}, strength {glint.Strength}");
        Assert.True(glint.Strength > 0.9f);

        // Forty degrees off his aim, nothing.
        double side = 0.7;
        jones.Human.PopPosition(new Vector3d(
            sniper.Position.X + (((fx * Math.Cos(side)) - (fy * Math.Sin(side))) / flat * 245760.0),
            sniper.Position.Y + (((fx * Math.Sin(side)) + (fy * Math.Cos(side))) / flat * 245760.0),
            sniper.Position.Z));
        jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
        Assert.Empty(jones.ScopeGlints());
    }
}
