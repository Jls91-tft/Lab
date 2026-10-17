#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CepaCero.Map;
using CepaCero.Unity.Prototype;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CepaCero.Unity.Tests
{
    /// <summary>The debug bridge (D-023): its command language, its path finding, and a real HTTP round trip.</summary>
    public class DebugBridgeTests
    {
        private const int Port = 7791; // not the default, so a running editor does not clash

        [Test]
        public void ParsesScriptsAndExplainsMistakes()
        {
            var commands = new List<DebugCommand>();
            Assert.That(DebugCommand.ParseScript("select 7; walk 10 12 run\n# comment\nkey down\n\ndrag 1 2 5 2 room", commands), Is.Null);
            Assert.That(commands.Count, Is.EqualTo(4));
            Assert.That(commands[1].Verb, Is.EqualTo("walk"));
            Assert.That(commands[1].Int(1), Is.EqualTo(12));

            commands.Clear();
            Assert.That(DebugCommand.ParseScript("select 12", commands), Does.Contain("select N"));
            Assert.That(DebugCommand.ParseScript("key jump", commands), Does.Contain("key "));
            Assert.That(DebugCommand.ParseScript("fly 1 2", commands), Does.Contain("unknown verb"));
            Assert.That(DebugCommand.ParseScript("screenshot ../../evil", commands), Is.Not.Null, "screenshot names cannot leave the folder");
        }

        [Test]
        public void PathsOnlyCrossWalkableCellsOneStepAtATime()
        {
            RepoData.LoadGameData();
            MapImportResult map = RepoData.LoadMap(RepoData.SyntheticMap);
            Int2 a = FindWalkable(map, 2, 2);
            Int2 b = FindWalkable(map, map.Blocks.Width - 3, map.Blocks.Height - 3);
            List<Int2> path = DebugPathfinder.Find(map.Blocks, map.Ground, 0, 0.35f, a, b);
            Assert.That(path, Is.Not.Null, $"no path {a.X},{a.Y} -> {b.X},{b.Y}");
            Int2 previous = a;
            foreach (Int2 c in path)
            {
                Assert.That(DebugPathfinder.Walkable(map.Blocks, map.Ground, 0, 0.35f, c), $"{c.X},{c.Y} is not walkable");
                Assert.That(System.Math.Abs(c.X - previous.X) + System.Math.Abs(c.Y - previous.Y), Is.EqualTo(1));
                previous = c;
            }
            Assert.That(previous, Is.EqualTo(b));
        }

        [UnityTest]
        public IEnumerator AnsweresOverHttpAndDrivesTheRealInput()
        {
            var go = new GameObject("Prototype (bridge test)");
            go.SetActive(false);
            var game = go.AddComponent<PrototypeGame>();
            Set(game, "_mapPath", RepoData.SyntheticMap);
            Set(game, "_populateMap", false);
            Set(game, "_debugBridgePort", Port);
            go.SetActive(true);
            Assert.IsTrue(game.enabled, "PrototypeGame failed to start");

            string reply = null;
            yield return Post("select 7; state", r => reply = r);
            StringAssert.Contains("\"ok\":true", reply);
            StringAssert.Contains("\"slot\":7", reply);

            // Walk three cells to the east if they are free, and check the player really moved there.
            var host = (IPrototypeDebugHost)game;
            Int2 start = host.Me.Position.Tile;
            Int2 goal = new Int2(start.X + 3, start.Y);
            if (DebugPathfinder.Walkable(host.ViewBlocks, host.ViewGround, 0, 0.35f, goal))
            {
                yield return Post($"walk {goal.X} {goal.Y}", r => reply = r);
                StringAssert.Contains("\"ok\":true", reply);
                Assert.That(host.Me.Position.Tile, Is.EqualTo(goal));
            }

            yield return Post("fly away", r => reply = r);
            StringAssert.Contains("unknown verb", reply);

            Object.Destroy(go);
            yield return null;
        }

        private static IEnumerator Post(string script, System.Action<string> done)
        {
            Task<string> task = Task.Run(async () =>
            {
                using (var http = new HttpClient())
                {
                    var body = new StringContent(script, Encoding.UTF8, "text/plain");
                    HttpResponseMessage response = await http.PostAsync($"http://localhost:{Port}/", body);
                    return await response.Content.ReadAsStringAsync();
                }
            });
            float until = Time.realtimeSinceStartup + 30f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
            Assert.That(task.IsCompleted, Is.True, "the bridge did not answer in 30 s");
            done(task.Result);
        }

        private static Int2 FindWalkable(MapImportResult map, int x0, int y0)
        {
            for (int r = 0; r < 40; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var c = new Int2(x0 + dx, y0 + dy);
                        if (DebugPathfinder.Walkable(map.Blocks, map.Ground, 0, 0.35f, c))
                        {
                            return c;
                        }
                    }
                }
            }
            Assert.Fail("no walkable cell");
            return default;
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "missing field " + field);
            f.SetValue(target, value);
        }
    }
}
#endif
