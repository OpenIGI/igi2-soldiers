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
    public void The_tower_sniper_looks_out_from_his_post_and_glints_at_jones_where_he_looks()
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
        output.WriteLine($"spawned facing gamma {sniper.Gamma}, body task {(soldiers.HandedOver.First(p => ReferenceEquals(p.Value, sniper)).Key.Name)}");
        // His post faces into the tower's own frame (the level's gamma for him is nought), so the mod turns him to look out
        // from it over open ground, with a sniper's hundred metres of sight.
        for (int tick = 0; tick < 30; tick++)
        {
            soldiers.Tick();
        }

        output.WriteLine($"after 30 ticks gamma {sniper.Gamma}");
        Assert.True(((Igi2HumanAi)sniper.Mind!).Tweaks[Igi2AiTweakIndex.IdleViewCone1Length] >= Igi2SniperPosts.SightUnits);

        Igi2HumanAi mind = (Igi2HumanAi)sniper.Mind!;
        float[] m = mind.DetectionView.Orientation;
        double fx = m[1], fy = m[4];
        double flat = Math.Max(1e-6, Math.Sqrt((fx * fx) + (fy * fy)));
        output.WriteLine($"he looks along ({fx:F2}, {fy:F2}, {m[7]:F2})");
        Vector3d eye = mind.DetectionView.Position;
        Game.Physics.WorldLineOfSight ahead = new(eye, new Vector3d(eye.X + (fx / flat * 245760.0), eye.Y + (fy / flat * 245760.0), eye.Z), mind.Detection.World.Materials)
        {
            Includes = owner => !ReferenceEquals(owner, sniper),
        };
        jones.CollisionWorld.LineOfSight(ahead, uint.MaxValue);
        Assert.False(ahead.IsBlocked, "He still looks into something.");

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

    [RequiresTitleDataFact("igi2")]
    public void The_tower_sniper_sees_jones_on_the_ground_in_front_of_his_post()
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

        foreach (double metres in new[] { 40.0, 60.0 })
        {
            Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
            Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal) { WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit) });
            Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
                kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
                world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: guest);
            Igi2HumanSoldier sniper = soldiers.HandedOver.Values.First(static s => Game.Weapons.ScopedWeapons.IsScoped(s.Weapon.Inventory?.Current?.WeaponType.WeaponId));
            for (int tick = 0; tick < 30; tick++)
            {
                soldiers.Tick();
            }

            Igi2HumanAi mind = (Igi2HumanAi)sniper.Mind!;
            float[] m = mind.DetectionView.Orientation;
            double fx = m[1], fy = m[4];
            double flat = Math.Max(1e-6, Math.Sqrt((fx * fx) + (fy * fy)));
            jones.Human.PopPosition(new Vector3d(sniper.Position.X + (fx / flat * metres * 4096.0), sniper.Position.Y + (fy / flat * metres * 4096.0), spawn.Position.Z + 8192.0));
            bool seen = false, inList = false;
            int tickSeen = -1;
            for (int tick = 0; tick < 450 && !seen; tick++)
            {
                jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
                soldiers.Tick();
                foreach (var item in mind.Detection.Enemies)
                {
                    if (ReferenceEquals(item.Target, jones.Human))
                    {
                        inList = true;
                        if (item.IsFullVisible)
                        {
                            seen = true;
                            tickSeen = tick;
                        }
                    }
                }
            }

            Vector3d eye = mind.DetectionView.Position;
            Game.Physics.WorldLineOfSight line = new(eye, jones.EyePosition, mind.Detection.World.Materials) { Includes = owner => !ReferenceEquals(owner, sniper) && !ReferenceEquals(owner, jones.Human) };
            jones.CollisionWorld.LineOfSight(line, uint.MaxValue);
            double drop = Math.Atan2(eye.Z - jones.EyePosition.Z, metres * 4096.0) * 180.0 / Math.PI;
            output.WriteLine($"{metres} m: seen {seen} (tick {tickSeen}), in his list {inList}, cone length {mind.View.ConeLength / 4096.0:F0} m alpha {mind.View.ConeAlpha * 180 / Math.PI:F0} deg gamma {mind.View.ConeGamma * 180 / Math.PI:F0} deg, tweak {mind.Tweaks[Igi2AiTweakIndex.IdleViewCone1Length] / 4096.0:F0} m, Jones {drop:F0} deg below his eye, line blocked {line.IsBlocked}");
            soldiers.Leave();
            if (!line.IsBlocked)
            {
                Assert.True(seen, $"The tower sniper never saw Jones standing {metres} metres in front of his post.");
            }
        }
    }
}
