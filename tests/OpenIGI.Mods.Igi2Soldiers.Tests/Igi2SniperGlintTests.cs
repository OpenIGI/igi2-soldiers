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
        Igi2HostMissionDevice device = new(missionDirectory, kit.Scripts);
        kit.Guest.FileSystem.RegisterDevice(device);
        HumanPlayerTask task = PlayerSpawn.FindGameplayTask(level)!;
        Assert.True(PlayerSpawn.TryFindGameplay(level, out PlayerSpawnPoint spawn));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());
        Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
        Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal) { WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit) });
        Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: guest, scripts: device);

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
        Assert.True(((Igi2HumanAi)sniper.Mind!).Tweaks[Igi2AiTweakIndex.IdleViewCone1Length] >= Igi2SniperPosts.SightMetres * 4096f);

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

        // Level with him sixty metres out along his view. Until he has made Jones out, his scope shows nothing; once he has, and
        // aims at him, the glass shows him, full, on the rifle in his hands rather than at his eye.
        Vector3d spot = new(sniper.Position.X + (fx / flat * 245760.0), sniper.Position.Y + (fy / flat * 245760.0), sniper.Position.Z);
        jones.Human.PopPosition(spot);
        jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
        Assert.Empty(jones.ScopeGlints());
        for (int tick = 0; tick < 300 && !mind.Detection.Enemies.Any(e => ReferenceEquals(e.Target, jones.Human) && e.HasBeenFullVisible); tick++)
        {
            jones.Human.PopPosition(spot);
            jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
            soldiers.Tick();
        }

        jones.Human.PopPosition(spot);
        jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
        (Vector3d Position, float Strength) glint = Assert.Single(jones.ScopeGlints());
        Vector3d sniperEye = mind.DetectionView.Position;
        double fromEye = Math.Sqrt(Math.Pow(glint.Position.X - sniperEye.X, 2) + Math.Pow(glint.Position.Y - sniperEye.Y, 2) + Math.Pow(glint.Position.Z - sniperEye.Z, 2)) / 4096.0;
        output.WriteLine($"glint at {glint.Position}, strength {glint.Strength}, {fromEye:F2} m from his eye");
        Assert.True(glint.Strength > 0.9f);
        Assert.InRange(fromEye, 0.05, 1.0);

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
        Igi2HostMissionDevice device = new(missionDirectory, kit.Scripts);
        kit.Guest.FileSystem.RegisterDevice(device);
        HumanPlayerTask task = PlayerSpawn.FindGameplayTask(level)!;
        Assert.True(PlayerSpawn.TryFindGameplay(level, out PlayerSpawnPoint spawn));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());

        foreach (double metres in new[] { 40.0, 60.0 })
        {
            Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
            Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal) { WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit) });
            Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
                kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
                world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: guest, scripts: device);
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

    [RequiresTitleDataFact("igi2")]
    public void The_tower_sniper_shoots_at_jones_once_he_sees_him()
    {
        // Fifty and seventy metres out on the ground in front of his post, standing still: he sees Jones and fires.
        foreach (double metres in new[] { 50.0, 70.0 })
        {
            Assert.True(ShootsAt(metres) > 0, $"The tower sniper never fired at Jones {metres} metres in front of him.");
        }
    }

    private int ShootsAt(double metres)
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = Igi2SoldierLevelTests.LoadMission(igi1, 1);
        Igi2GuestContent kit = Igi2GuestContent.Load(
            GameDataSource.Igi2.RequireRoot(),
            SoldierTable.Shipped.Models.Append(Igi2GuestPlayerStart.JonesModel).Append(Igi2Gameplay.FirstPersonFallbackModel));
        Igi2HostMissionDevice device = new(missionDirectory, kit.Scripts);
        kit.Guest.FileSystem.RegisterDevice(device);
        HumanPlayerTask task = PlayerSpawn.FindGameplayTask(level)!;
        Assert.True(PlayerSpawn.TryFindGameplay(level, out PlayerSpawnPoint spawn));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());
        Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
        Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal) { WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit) });
        Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: guest, scripts: device);
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
        Game.Igi2.Weapons.Igi2Inventory inventory = sniper.Weapon.Inventory!;
        float health = jones.Human.Damage.Damage;
        List<string> actions = [];
        int fired = 0;
        for (int tick = 0; tick < 900; tick++)
        {
            jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
            soldiers.Tick();
            string top = mind.Actions.Top?.Init?.Method.Name ?? "(none)";
            if (actions.Count == 0 || actions[^1] != top)
            {
                actions.Add(top);
                output.WriteLine($"{metres} m tick {tick}: {top}; fired {inventory.TotalBulletsFired}, loaded {(inventory.Current is { } held ? inventory.FireTypeRounds(held, 0).Loaded : -1)}/{(inventory.Current is { } h2 ? inventory.FireTypeRounds(h2, 0).Available : -1)}, gamma {sniper.Gamma:F2}");
            }

            fired = Math.Max(fired, inventory.TotalBulletsFired);
        }

        bool sawHim = mind.Detection.Enemies.Any(e => ReferenceEquals(e.Target, jones.Human));
        output.WriteLine($"{metres} m: fired {fired}, Jones's damage {health} -> {jones.Human.Damage.Damage}, still in his list {sawHim}");
        soldiers.Leave();
        return fired;
    }

    [RequiresTitleDataFact("igi2")]
    public void The_tower_sniper_answers_jones_firing_from_a_roof_he_can_see()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = Igi2SoldierLevelTests.LoadMission(igi1, 1);
        Igi2GuestContent kit = Igi2GuestContent.Load(
            GameDataSource.Igi2.RequireRoot(),
            SoldierTable.Shipped.Models.Append(Igi2GuestPlayerStart.JonesModel).Append(Igi2Gameplay.FirstPersonFallbackModel));
        Igi2HostMissionDevice device = new(missionDirectory, kit.Scripts);
        kit.Guest.FileSystem.RegisterDevice(device);
        HumanPlayerTask task = PlayerSpawn.FindGameplayTask(level)!;
        Assert.True(PlayerSpawn.TryFindGameplay(level, out PlayerSpawnPoint spawn));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());

        // The roofs round the tower: a first surface well above the ground, from which the sniper's eye is in plain view.
        Igi2GuestLevel look = Igi2GuestLevel.Open(kit);
        Igi2TitlePlayer probe = look.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal) { WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit) });
        Igi2HostedLevel first = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: look, scripts: device);
        Igi2HumanSoldier tower = first.HandedOver.Values.First(static s => Game.Weapons.ScopedWeapons.IsScoped(s.Weapon.Inventory?.Current?.WeaponType.WeaponId));
        for (int tick = 0; tick < 30; tick++)
        {
            first.Tick();
        }

        Vector3d towerEye = ((Igi2HumanAi)tower.Mind!).DetectionView.Position;
        List<Vector3d> roofs = [];
        int cast = 0, high = 0, flat = 0;
        for (double gx = -150.0; gx <= 150.0; gx += 7.5)
        {
            for (double gy = -150.0; gy <= 150.0; gy += 7.5)
            {
                double x = tower.Position.X + (gx * 4096.0), y = tower.Position.Y + (gy * 4096.0);
                double flatDistance = Math.Sqrt((gx * gx) + (gy * gy));
                if (flatDistance < 25.0
                    || !world.Cast(new Vector3d(x, y, tower.Position.Z + (80.0 * 4096.0)), new Vector3d(x, y, tower.Position.Z - (200.0 * 4096.0)), out Game.World.WorldRayHit top)
                    || top.OnTerrain)
                {
                    continue;
                }

                cast++;

                double groundZ = terrain.TryGetGround(x, y, top.Position.Z, 1000.0 * 4096.0, out TerrainGround g0) ? g0.Height : double.NaN;
                if (double.IsNaN(groundZ) || top.Position.Z - groundZ < 3.0 * 4096.0)
                {
                    continue;
                }

                high++;
                if (top.Normal.Z < 0.9f)
                {
                    continue;
                }

                flat++;

                Vector3d eyeThere = new(top.Position.X, top.Position.Y, top.Position.Z + (1.7 * 4096.0));
                Game.Physics.WorldLineOfSight line = new(eyeThere, towerEye, ((Igi2HumanAi)tower.Mind!).Detection.World.Materials) { Includes = owner => !ReferenceEquals(owner, tower) && !ReferenceEquals(owner, probe.Human) };
                world.LineOfSight(line, uint.MaxValue);
                if (!line.IsBlocked)
                {
                    roofs.Add(top.Position);
                }
                else
                {
                    double total = Math.Sqrt(Math.Pow(towerEye.X - eyeThere.X, 2) + Math.Pow(towerEye.Y - eyeThere.Y, 2) + Math.Pow(towerEye.Z - eyeThere.Z, 2)) / 4096.0;
                    output.WriteLine($"roof at ({gx}, {gy}) {(top.Position.Z - groundZ) / 4096.0:F1} m up, {(eyeThere.Z - towerEye.Z) / 4096.0:F1} m against his eye: blocked {(1.0 - line.T) * total:F1} m from his eye of {total:F1}, material {line.Material}");
                }
            }
        }

        first.Leave();
        output.WriteLine($"{cast} object tops, {high} high, {flat} flat, {roofs.Count} roof spots see his eye.");
        int answered = 0, tried = 0;
        foreach (Vector3d roof in roofs.Where((_, i) => i % Math.Max(1, roofs.Count / 10) == 0).Take(10))
        {
            tried++;
            Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
            Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal) { WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit) });
            Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
                kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
                world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: guest, scripts: device);
            Igi2HumanSoldier sniper = soldiers.HandedOver.Values.First(static s => Game.Weapons.ScopedWeapons.IsScoped(s.Weapon.Inventory?.Current?.WeaponType.WeaponId));
            for (int tick = 0; tick < 30; tick++)
            {
                soldiers.Tick();
            }

            Igi2HumanAi mind = (Igi2HumanAi)sniper.Mind!;
            Vector3d tower0 = sniper.Position;
            jones.Human.PopPosition(new Vector3d(roof.X, roof.Y, roof.Z + 400.0));

            // Turned to look at the sniper, and firing a metre and a half wide of his head.
            jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
            Vector3d head = mind.DetectionView.Position;
            double dx = head.X - jones.EyePosition.X, dy = head.Y - jones.EyePosition.Y, dz = head.Z - jones.EyePosition.Z;
            double across = Math.Sqrt((dx * dx) + (dy * dy));
            jones.Human.PopOrientation((float)(Math.Atan2(-dx, dy) + Math.Asin(1.5 * 4096.0 / across)));
            jones.Human.View.IdealPitch = jones.Human.View.Pitch = (float)Math.Atan2(dz, across);
            jones.Tick(new TitlePlayerInput(0f, 0f, default, default, 1f, false));
            int listed = -1, full = -1, fired = -1, byShot = -1;
            List<string> tops = [];
            for (int tick = 0; tick < 600 && fired < 0; tick++)
            {
                jones.Tick(new TitlePlayerInput(0f, 0f, tick < 30 ? PlayerAction.Fire : default, tick % 6 == 0 && tick < 30 ? PlayerAction.Fire : default, 1f, false));
                soldiers.Tick();
                foreach (var item in mind.Detection.Enemies)
                {
                    if (ReferenceEquals(item.Target, jones.Human))
                    {
                        listed = listed < 0 ? tick : listed;
                        byShot = byShot < 0 && item.IsCreatedByGunShot ? tick : byShot;
                        full = full < 0 && item.IsFullVisible ? tick : full;
                    }
                }

                fired = fired < 0 && sniper.Weapon.Inventory!.TotalBulletsFired > 0 ? tick : fired;
                string now = mind.Actions.Top?.Init?.Method.Name ?? "-";
                if (tops.Count == 0 || tops[^1] != now)
                {
                    tops.Add(now);
                }
            }

            Game.Physics.WorldLineOfSight end = new(mind.Detection.ClearOf(mind.DetectionView.Position, jones.EyePosition), jones.EyePosition, mind.Detection.World.Materials) { Includes = owner => !ReferenceEquals(owner, sniper) && !ReferenceEquals(owner, jones.Human) };
            jones.CollisionWorld.LineOfSight(end, uint.MaxValue);
            output.WriteLine($"   at the end: in his cone {mind.DetectionView.IsInside(jones.EyePosition)}, visibility {((IAiDetectable)jones.Human).VisibilityFactor}, cone {mind.View.ConeLength / 4096.0:F0} m (tweak {mind.Tweaks[Igi2AiTweakIndex.IdleViewCone1Length] / 4096.0:F0}/{mind.Tweaks[Igi2AiTweakIndex.InvestigateViewCone1Length] / 4096.0:F0}/{mind.Tweaks[Igi2AiTweakIndex.CombatViewCone1Length] / 4096.0:F0}, squad {(sniper.Mind as Igi2HumanAi)?.Squad?.SquadType}), line blocked {end.IsBlocked} (clearance {mind.Detection.SightClearance / 4096.0} m, by {end.Material} at {end.T:F2}), he stands {Math.Sqrt(Math.Pow(sniper.Position.X - tower0.X, 2) + Math.Pow(sniper.Position.Y - tower0.Y, 2)) / 4096.0:F1} m from his post; locomotion {sniper.Locomotion.CurrentState}, allowed to fire {mind.IsAllowedToFire}, sees a target {mind.GetTarget(visibleOnly: true) is not null}, ammo {mind.AmmoInWeapon()}");
            output.WriteLine($"   by a shot from {byShot}; actions {string.Join(" > ", tops.Select(static t => t.Replace("Init", string.Empty, StringComparison.Ordinal)))}");
            double far = Math.Sqrt((dx * dx) + (dy * dy)) / 4096.0;
            answered += fired >= 0 ? 1 : 0;
            output.WriteLine($"roof {far:F0} m off, {(roof.Z - sniper.Position.Z) / 4096.0:F0} m up: Jones fired {jones.Human.Weapon.Inventory?.TotalBulletsFired}, listed {listed}, plain sight {full}, sniper fired {fired}, top {mind.Actions.Top?.Init?.Method.Name}");
            soldiers.Leave();
        }

        output.WriteLine($"{answered} of {tried} answered.");

        // As I.G.I. 2's own snipers answer: most take cover, look toward the shots and engage; some posts' edges still hide a
        // shooter just below them.
        Assert.True(tried >= 8 && answered * 2 > tried, $"The tower sniper answered {answered} of {tried} rooftop shooters.");
    }
}
