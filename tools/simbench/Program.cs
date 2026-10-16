using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CepaCero.AI;
using CepaCero.Building;
using CepaCero.Net;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Lore;
using CepaCero.Net.Protocol;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Map;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.SimBench
{
    /// <summary>
    /// Stress scenario for milestone 7 ("100 zombies + 2 players"), headless:
    /// two players hold a small wooden fort in the middle of the map while a
    /// horde besieges it. Reports host CPU per tick and bandwidth per client.
    /// </summary>
    public static class Program
    {
        private const int TickRate = 20;

        public static int Main(string[] args)
        {
            Options o;
            try
            {
                o = Options.Parse(args);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine($"error: {e.Message}\n\n{Options.Usage}");
                return 2;
            }
            if (o.Help)
            {
                Console.WriteLine(Options.Usage);
                return 0;
            }

            string repo = FindRepoRoot();
            string[] argsCopy = args;
            ConstructionData construction = null;
            if (o.Levels)
            {
                construction = JsonSerializer.Deserialize<ConstructionData>(File.ReadAllText(Path.Combine(repo, "data/generated/construction.json")), new JsonSerializerOptions { IncludeFields = true });
                o.Players = Math.Max(o.Players, 3);
            }
            _ = argsCopy;
            var registry = o.Levels ? BlockRegistry.CreateDefault(construction) : BlockRegistry.CreateDefault();
            BlockWorld blocks;
            GroundLayer ground;
            string mapName;
            if (o.MapPath != null)
            {
                var mapData = JsonSerializer.Deserialize<CepaMapData>(File.ReadAllText(o.MapPath), new JsonSerializerOptions { IncludeFields = true });
                MapImportResult map = MapImporter.Import(mapData, registry);
                blocks = map.Blocks;
                ground = map.Ground;
                mapName = $"{map.Name} ({o.MapPath})";
            }
            else
            {
                (blocks, ground) = SyntheticCity.Build(o.Width, o.Height, registry);
                mapName = $"synthetic city {o.Width}x{o.Height}";
            }

            var json = new JsonSerializerOptions { IncludeFields = true };
            T Load<T>(string file) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(repo, "data/generated", file)), json);
            var items = new ItemCatalog(Load<ItemsData>("items.json"));
            var data = new GameData(new ZombieCatalog(Load<BestiaryData>("bestiary.json")), items, Load<RecipesData>("recipes.json"),
                Load<LootData>("loot.json"), Load<DiaryData>("diary.json"), construction);
            ZombieCatalog catalog = data.Zombies;

            Float2 center = FindOpenCenter(blocks, ground);
            int fortBlocks = BuildFort(blocks, ground, registry, center.Tile, radius: 3);
            int blocksDestroyed = 0;
            blocks.TypeChanged += (x, y, before, after) => blocksDestroyed += after == BlockRegistry.AirId ? 1 : 0;

            // A whole match on the host, as in the game: no horde planning (one endless day),
            // the zombies are placed by hand so every run besieges the same fort.
            var settings = new GameSettings { DaySeconds = 1_000_000, HordeBase = 0, Seed = o.Seed };
            var session = new GameSession(settings, new PlayerSettings { MaxHealth = 60000 }, blocks, ground, data,
                zombieCapacity: Math.Max(512, o.Zombies * 2));
            var underground = new List<Float2>();
            var upstairs = new List<Float2>();
            if (o.Levels)
            {
                // Player 0 holds the fort; player 1 digs in below a pit; player 2 is on a floor reached by stairs (D-022 phase G5).
                Int2 c = center.Tile;
                Int2 pit = new Int2(c.X + 4, c.Y), stairs = new Int2(c.X - 4, c.Y);
                blocks.SetBlock(pit.X, pit.Y, 0, BuildLayer.Structure, registry.Get("pit").Id);
                for (int x = c.X - 3; x <= pit.X; x++)
                {
                    blocks.SetBlock(x, c.Y, -1, BuildLayer.Floor, registry.Get("cave").Id);
                    underground.Add(new Float2(x + 0.5f, c.Y + 0.5f));
                }
                blocks.SetBlock(stairs.X, stairs.Y, 0, BuildLayer.Structure, registry.Get("stairs_wood").Id);
                for (int y = c.Y; y >= c.Y - 6; y--)
                {
                    if (y != c.Y)
                    {
                        blocks.SetBlock(stairs.X, y, 1, BuildLayer.Floor, registry.Get("floor_wood").Id);
                    }
                    upstairs.Add(new Float2(stairs.X + 0.5f, y + 0.5f));
                }
                session.AddPlayer(new Float2(center.X, center.Y));
                session.AddPlayer(new Float2(pit.X + 0.5f, pit.Y + 0.5f));
                session.AddPlayer(new Float2(stairs.X + 0.5f, stairs.Y + 0.5f));
                blocks.DrainChanges(new List<BlockChange>());
                session.QueueIntent(1, PlayerIntent.Climb(false));
                session.QueueIntent(2, PlayerIntent.Climb(true));
                for (int k = 0; k < 4; k++)
                {
                    session.Step();
                }
                for (int p = 3; p < o.Players; p++)
                {
                    session.AddPlayer(new Float2(center.X + p, center.Y));
                }
            }
            else
            {
                for (int p = 0; p < o.Players; p++)
                {
                    session.AddPlayer(new Float2(center.X + p * 1.0f, center.Y));
                }
            }
            blocks.DrainChanges(new List<BlockChange>());
            session.Step(); // builds the first flow field
            var players = session.Players.Select(pl => pl.Position).ToList();
            var rng = new DeterministicRandom(o.Seed);
            byte[] types = { catalog.IndexOf("walker"), catalog.IndexOf("walker"), catalog.IndexOf("walker"), catalog.IndexOf("runner") };
            int spawned = 0;
            for (int attempt = 0; spawned < o.Zombies && attempt < o.Zombies * 20; attempt++)
            {
                if (SpawnFinder.TryFind(ground, blocks, session.Zombies.Field, players, rng, 18f, 45f, out Float2 pos)
                    && session.Zombies.Spawn(types[rng.Range(0, types.Length)], pos, o.Players) >= 0)
                {
                    spawned++;
                }
            }

            int below = 0, above = 0;
            for (int k = 0; o.Levels && k < Math.Max(8, o.Zombies / 6); k++)
            {
                below += session.Zombies.Spawn(catalog.IndexOf("walker"), underground[k % underground.Count], o.Players, level: -1) >= 0 ? 1 : 0;
                above += session.Zombies.Spawn(catalog.IndexOf("walker"), upstairs[1 + k % (upstairs.Count - 1)], o.Players, level: 1) >= 0 ? 1 : 0;
            }
            if (o.Levels)
            {
                Console.WriteLine($"Levels: player levels {string.Join("/", session.Players.Select(pl => pl.Level))}; extra zombies placed: {below} underground, {above} upstairs");
            }

            var broadcaster = new HostBroadcaster(session, mapName);
            var writer = new ByteWriter(16384);
            long joinBytes = 0;
            for (int p = 1; p < o.Players; p++) // player 0 is the host: nothing to send
            {
                writer.Clear();
                broadcaster.WriteJoin(p, writer);
                joinBytes = Math.Max(joinBytes, writer.Length);
            }
            broadcaster.CollectTick();

            Console.WriteLine($"Map: {mapName}, {blocks.Width}x{blocks.Height} tiles");
            Console.WriteLine($"Players: {o.Players} inside a {fortBlocks}-block wooden fort. Zombies: {spawned}/{o.Zombies}. " +
                              $"Simulating {o.Seconds}s at {TickRate} Hz (full GameSession + real protocol)…");

            int ticks = o.Seconds * TickRate;
            var tickMs = new double[ticks];
            long bytesToClients = 0;
            int playerHits = 0;
            int wallsBefore = CountKey(blocks, "wood_wall");
            long maxTickBytes = 0;
            var sw = new Stopwatch();

            for (int t = 0; t < ticks; t++)
            {
                sw.Restart();
                session.Step();
                broadcaster.CollectTick();
                for (int p = 1; p < o.Players; p++)
                {
                    writer.Clear();
                    broadcaster.WriteFor(p, writer);
                    bytesToClients += writer.Length;
                    maxTickBytes = Math.Max(maxTickBytes, writer.Length);
                }
                sw.Stop();
                tickMs[t] = sw.Elapsed.TotalMilliseconds;
                playerHits += broadcaster.LastEvents.Count(e => e.Type == SessionEventType.PlayerHurt);
            }

            int slowTicks = 0;
            int firstSlow = -1;
            int lastSlow = -1;
            for (int t = 0; t < ticks; t++)
            {
                if (tickMs[t] > 2.0)
                {
                    slowTicks++;
                    firstSlow = firstSlow < 0 ? t : firstSlow;
                    lastSlow = t;
                }
            }
            Array.Sort(tickMs);
            double avg = tickMs.Average();
            double p95 = tickMs[(int)(ticks * 0.95)];
            double p99 = tickMs[(int)(ticks * 0.99)];
            double max = tickMs[ticks - 1];
            int clients = Math.Max(1, o.Players - 1);
            double kbPerClient = bytesToClients / (double)clients / o.Seconds / 1024.0;
            double budgetMs = 1000.0 / TickRate;

            Console.WriteLine();
            Console.WriteLine($"Host tick (whole match + protocol encoding), budget {budgetMs:0} ms:");
            Console.WriteLine($"  avg {avg:0.000} ms | p95 {p95:0.000} ms | p99 {p99:0.000} ms | max {max:0.000} ms");
            Console.WriteLine($"  flow field rebuilds: {session.Zombies.FlowRebuilds}; ticks over 2 ms: {slowTicks} (first #{firstSlow}, last #{lastSlow})");
            Console.WriteLine($"Network per client: {kbPerClient:0.0} KB/s (all messages); biggest tick {maxTickBytes} B; join {joinBytes / 1024.0:0.0} KB");
            if (o.Levels)
            {
                var perLevel = new SortedDictionary<int, int>();
                for (int i = 0; i < session.Zombies.Capacity; i++)
                {
                    if (session.Zombies.IsAlive(i))
                    {
                        int l = session.Zombies.GetLevel(i);
                        perLevel[l] = perLevel.TryGetValue(l, out int n) ? n + 1 : 1;
                    }
                }
                Console.WriteLine($"Zombies per level at the end: {string.Join(", ", perLevel.Select(kv => $"{kv.Key}: {kv.Value}"))}; levels with a field: {session.Zombies.BuiltLevelCount}; managed memory {GC.GetTotalMemory(true) / 1048576.0:0.0} MB");
            }
            Console.WriteLine($"Siege: {blocksDestroyed} blocks destroyed ({wallsBefore - CountKey(blocks, "wood_wall")} fort walls), {playerHits} hits on players");

            bool ok = p95 < budgetMs * 0.25 && kbPerClient < 64;
            Console.WriteLine(ok
                ? "RESULT: OK (p95 < 25% of the tick budget and < 64 KB/s per client)"
                : "RESULT: OVER TARGET (see docs/plan.md, week 7)");

            if (o.JsonOut != null)
            {
                var report = new Dictionary<string, object>
                {
                    ["map"] = mapName, ["width"] = blocks.Width, ["height"] = blocks.Height,
                    ["players"] = o.Players, ["zombies"] = spawned, ["seconds"] = o.Seconds,
                    ["tick_ms_avg"] = avg, ["tick_ms_p95"] = p95, ["tick_ms_p99"] = p99, ["tick_ms_max"] = max,
                    ["flow_rebuilds"] = session.Zombies.FlowRebuilds, ["kb_per_s_per_client"] = kbPerClient,
                    ["join_kb"] = joinBytes / 1024.0, ["max_tick_bytes"] = maxTickBytes,
                    ["blocks_destroyed"] = blocksDestroyed, ["player_hits"] = playerHits, ["ok"] = ok,
                };
                File.WriteAllText(o.JsonOut, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            }
            return ok ? 0 : 1;
        }

        private static Float2 FindOpenCenter(BlockWorld blocks, GroundLayer ground)
        {
            // Spiral out from the middle until a 9x9 patch of open outdoor ground is found.
            int cx = blocks.Width / 2;
            int cy = blocks.Height / 2;
            for (int r = 0; r < Math.Max(blocks.Width, blocks.Height); r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && IsOpenPatch(blocks, ground, cx + dx, cy + dy, 4))
                        {
                            return new Float2(cx + dx + 0.5f, cy + dy + 0.5f);
                        }
                    }
                }
            }
            throw new InvalidOperationException("No open area for the players.");
        }

        private static bool IsOpenPatch(BlockWorld blocks, GroundLayer ground, int x, int y, int half)
        {
            for (int yy = y - half; yy <= y + half; yy++)
            {
                for (int xx = x - half; xx <= x + half; xx++)
                {
                    if (!blocks.InBounds(xx, yy) || blocks.IsSolid(xx, yy))
                    {
                        return false;
                    }
                    GroundType g = ground.Get(xx, yy);
                    if (g == GroundType.Water || g == GroundType.Floor)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static int BuildFort(BlockWorld blocks, GroundLayer ground, BlockRegistry registry, Int2 c, int radius)
        {
            ushort wood = registry.Get("wood_wall").Id;
            int placed = 0;
            for (int y = c.Y - radius; y <= c.Y + radius; y++)
            {
                for (int x = c.X - radius; x <= c.X + radius; x++)
                {
                    bool edge = Math.Abs(x - c.X) == radius || Math.Abs(y - c.Y) == radius;
                    if (edge && blocks.TryPlace(x, y, wood, ground) == PlaceResult.Placed)
                    {
                        placed++;
                    }
                }
            }
            return placed;
        }

        private static int CountKey(BlockWorld blocks, string key)
        {
            ushort id = blocks.Registry.Get(key).Id;
            int n = 0;
            for (int y = 0; y < blocks.Height; y++)
            {
                for (int x = 0; x < blocks.Width; x++)
                {
                    n += blocks.GetTypeId(x, y) == id ? 1 : 0;
                }
            }
            return n;
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data/generated/bestiary.json")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new FileNotFoundException("Run inside the repository (data/generated/bestiary.json not found).");
        }

        private sealed class Options
        {
            public string MapPath;
            public int Zombies = 100;
            public int Players = 2;
            public int Seconds = 60;
            public int Width = 375;
            public int Height = 335;
            public ulong Seed = 1;
            public string JsonOut;
            public bool Help;
            public bool Levels;

            public const string Usage =
                "simbench [--map file.map.json] [--zombies 100] [--players 2] [--seconds 60]\n" +
                "         [--size 375x335] [--seed 1] [--json report.json] [--levels]\n" +
                "--levels adds a pit with a tunnel under the fort and stairs to a floor above, with a player and zombies on each level.\n" +
                "Without --map, a synthetic city of --size tiles is generated.";

            public static Options Parse(string[] args)
            {
                var o = new Options();
                for (int i = 0; i < args.Length; i++)
                {
                    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
                    switch (args[i])
                    {
                        case "--map": o.MapPath = Next(); break;
                        case "--zombies": o.Zombies = PositiveInt(Next(), "--zombies"); break;
                        case "--players": o.Players = PositiveInt(Next(), "--players"); break;
                        case "--seconds": o.Seconds = PositiveInt(Next(), "--seconds"); break;
                        case "--seed": o.Seed = ulong.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--json": o.JsonOut = Next(); break;
                        case "--levels": o.Levels = true; break;
                        case "--size":
                            string[] parts = Next().Split('x');
                            if (parts.Length != 2)
                            {
                                throw new ArgumentException("--size must look like 375x335");
                            }
                            o.Width = PositiveInt(parts[0], "--size");
                            o.Height = PositiveInt(parts[1], "--size");
                            break;
                        case "-h":
                        case "--help": o.Help = true; break;
                        default: throw new ArgumentException($"unknown option {args[i]}");
                    }
                }
                if (o.Players > 4)
                {
                    throw new ArgumentException("--players is 1..4");
                }
                return o;
            }

            private static int PositiveInt(string s, string name) =>
                int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0
                    ? v
                    : throw new ArgumentException($"{name} must be a positive integer");
        }
    }
}
