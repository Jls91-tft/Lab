using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CepaCero.AI;
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
        private const int SnapshotEveryTicks = 2; // 10 Hz
        private const float InterestRadius = 40f;

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
            var registry = BlockRegistry.CreateDefault();
            BlockWorld blocks;
            GroundLayer ground;
            string mapName;
            if (o.MapPath != null)
            {
                var data = JsonSerializer.Deserialize<CepaMapData>(File.ReadAllText(o.MapPath), new JsonSerializerOptions { IncludeFields = true });
                MapImportResult map = MapImporter.Import(data, registry);
                blocks = map.Blocks;
                ground = map.Ground;
                mapName = $"{map.Name} ({o.MapPath})";
            }
            else
            {
                (blocks, ground) = SyntheticCity.Build(o.Width, o.Height, registry);
                mapName = $"synthetic city {o.Width}x{o.Height}";
            }

            var bestiary = JsonSerializer.Deserialize<BestiaryData>(
                File.ReadAllText(Path.Combine(repo, "data/generated/bestiary.json")), new JsonSerializerOptions { IncludeFields = true });
            var catalog = new ZombieCatalog(bestiary);

            Float2 center = FindOpenCenter(blocks, ground);
            var players = new List<Float2>();
            for (int p = 0; p < o.Players; p++)
            {
                players.Add(new Float2(center.X + p * 1.0f, center.Y));
            }
            int fortBlocks = BuildFort(blocks, ground, registry, center.Tile, radius: 3);
            blocks.DrainChanges(new List<BlockChange>());

            var sim = new ZombieSimulation(blocks, ground, catalog, capacity: Math.Max(512, o.Zombies * 2));
            sim.Tick(1f / TickRate, players); // builds the first flow field
            var rng = new DeterministicRandom(o.Seed);
            byte[] types = { catalog.IndexOf("walker"), catalog.IndexOf("walker"), catalog.IndexOf("walker"), catalog.IndexOf("runner") };
            int spawned = 0;
            for (int attempt = 0; spawned < o.Zombies && attempt < o.Zombies * 20; attempt++)
            {
                if (SpawnFinder.TryFind(ground, blocks, sim.Field, players, rng, 18f, 45f, out Float2 pos)
                    && sim.Spawn(types[rng.Range(0, types.Length)], pos, o.Players) >= 0)
                {
                    spawned++;
                }
            }

            Console.WriteLine($"Map: {mapName}, {blocks.Width}x{blocks.Height} tiles");
            Console.WriteLine($"Players: {o.Players} inside a {fortBlocks}-block wooden fort. Zombies: {spawned}/{o.Zombies}. " +
                              $"Simulating {o.Seconds}s at {TickRate} Hz…");

            int ticks = o.Seconds * TickRate;
            var tickMs = new double[ticks];
            var writer = new ByteWriter(4096);
            var scratch = new List<int>();
            var changes = new List<BlockChange>();
            var hits = new List<PlayerHit>();
            long snapshotBytes = 0;
            long blockDeltaBytes = 0;
            int playerHits = 0;
            int blocksDestroyed = 0;
            int wallsBefore = CountKey(blocks, "wood_wall");
            int maxSentToOne = 0;
            var sw = new Stopwatch();

            for (int t = 0; t < ticks; t++)
            {
                sw.Restart();
                sim.Tick(1f / TickRate, players);

                // What the host would send this tick.
                blocks.DrainChanges(changes);
                if (changes.Count > 0)
                {
                    writer.Clear();
                    BlockChange.WriteBatch(changes, writer);
                    blockDeltaBytes += writer.Length * (long)Math.Max(0, o.Players - 1);
                    blocksDestroyed += changes.Count(c => c.Type == BlockRegistry.AirId);
                    changes.Clear();
                }
                if (t % SnapshotEveryTicks == 0)
                {
                    for (int p = 1; p < o.Players; p++) // player 0 is the host: nothing to send
                    {
                        writer.Clear();
                        int sent = ZombieSnapshotCodec.Write(sim, (uint)t, players[p], InterestRadius, writer, scratch);
                        maxSentToOne = Math.Max(maxSentToOne, sent);
                        snapshotBytes += writer.Length;
                    }
                }
                sim.DrainPlayerHits(hits);
                playerHits += hits.Count;
                hits.Clear();
                sw.Stop();
                tickMs[t] = sw.Elapsed.TotalMilliseconds;
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
            double kbPerClient = (snapshotBytes + blockDeltaBytes) / (double)clients / o.Seconds / 1024.0;
            double budgetMs = 1000.0 / TickRate;

            Console.WriteLine();
            Console.WriteLine($"Host tick (simulation + encoding), budget {budgetMs:0} ms:");
            Console.WriteLine($"  avg {avg:0.000} ms | p95 {p95:0.000} ms | p99 {p99:0.000} ms | max {max:0.000} ms");
            Console.WriteLine($"  flow field rebuilds: {sim.FlowRebuilds}; ticks over 2 ms: {slowTicks} (first #{firstSlow}, last #{lastSlow})");
            Console.WriteLine($"Network per client: {kbPerClient:0.0} KB/s (zombie snapshots at 10 Hz + block deltas); " +
                              $"max zombies in one snapshot: {maxSentToOne}");
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
                    ["flow_rebuilds"] = sim.FlowRebuilds, ["kb_per_s_per_client"] = kbPerClient,
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

            public const string Usage =
                "simbench [--map file.map.json] [--zombies 100] [--players 2] [--seconds 60]\n" +
                "         [--size 375x335] [--seed 1] [--json report.json]\n" +
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
