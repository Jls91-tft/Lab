using System.Collections.Generic;
using System.Linq;
using CepaCero.Core;
using CepaCero.Lore;
using CepaCero.Map;
using CepaCero.Net;
using CepaCero.Net.Protocol;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Core
{
    /// <summary>The shared diary of docs/lore.md, unlocked by what happens in the match.</summary>
    public class DiaryTests
    {
        private GameData _data;

        [SetUp]
        public void SetUp() => _data = Fixtures.GameData();

        private static GameSettings Fast() => new GameSettings
        {
            TickRate = 20, DaySeconds = 3, DuskSeconds = 1, NightSeconds = 3, DawnSeconds = 1, HordeBase = 0,
        };

        private static List<string> Ids(GameSession s) => s.Diary.Unlocked.Select(i => s.Diary.Entries[i].id).ToList();

        [Test]
        public void TriggersFireOnceAndInOrder()
        {
            var diary = new TeamDiary(Fixtures.DiaryData());
            var fresh = new List<int>();
            diary.Fire("match_start", fresh);
            Assert.That(fresh.Count, Is.EqualTo(1));
            fresh.Clear();
            diary.Fire("match_start", fresh);
            Assert.That(fresh, Is.Empty, "a trigger fires once");
            diary.Fire("nothing_matches", fresh);
            Assert.That(fresh, Is.Empty);
            Assert.That(diary.IsUnlocked("diary_001"), Is.True);
        }

        [Test]
        public void TheFirstNightsWriteTheDiary()
        {
            var rows = new string[40];
            for (int r = 0; r < rows.Length; r++)
            {
                rows[r] = new string('.', 40);
            }
            var map = new AsciiMap(rows);
            var s = new GameSession(Fast(), new PlayerSettings(), map.Blocks, map.Ground, _data);
            s.AddPlayer(new Float2(20.5f, 20.5f));
            var events = new List<SessionEvent>();
            while (s.NightsSurvived < 1)
            {
                s.Step();
                s.DrainEvents(events);
            }
            Assert.That(Ids(s), Is.EqualTo(new[] { "diary_001", "diary_003", "diary_004" }), "start, first night, first dawn");
            Assert.That(events.Count(e => e.Type == SessionEventType.DiaryUnlocked), Is.EqualTo(3));
        }

        [Test]
        public void LootingBuildingsAndAnalysingSamplesRevealTheStory()
        {
            MapImportResult map = MapImporter.Import(Fixtures.LoadMap("synthetic.map.json"), BlockRegistry.CreateDefault());
            var s = new GameSession(Fast(), new PlayerSettings(), map.Blocks, map.Ground, _data, map.Buildings);
            // The synthetic map has a hospital: find one of its containers and stand there.
            Int2 box = s.Containers.Positions.First(c => s.Containers.KindAt(c) == "medical");
            int p = s.AddPlayer(new Float2(box.X + 0.5f, box.Y + 0.5f));
            s.QueueIntent(p, PlayerIntent.Open(box.X, box.Y));
            s.Step();
            Assert.That(Ids(s), Does.Contain("diary_002"), "first container opened");
            Assert.That(Ids(s), Does.Contain("diary_007"), "hospital recording");

            s.Inventories[p].Add(_data.Items.IndexOf("sample_brute"), 1);
            s.Inventories[p].MoveToHotbar(s.Inventories[p].Bag.Count - 1, 0);
            s.Inventories[p].SelectHotbar(0);
            s.QueueIntent(p, PlayerIntent.Use());
            s.Step();
            Assert.That(Ids(s), Does.Contain("diary_005"), "La Mole's sample analysed");
        }

        [Test]
        public void TheDiarySurvivesSavesAndReachesLateJoiners()
        {
            var rows = new string[30];
            for (int r = 0; r < rows.Length; r++)
            {
                rows[r] = new string('.', 30);
            }
            var map = new AsciiMap(rows);
            var s = new GameSession(Fast(), new PlayerSettings(), map.Blocks, map.Ground, _data);
            s.AddPlayer(new Float2(10.5f, 10.5f));
            s.AddPlayer(new Float2(12.5f, 10.5f));
            for (int t = 0; t < 20 * 5; t++)
            {
                s.Step();
            }
            List<string> before = Ids(s);
            Assert.That(before.Count, Is.GreaterThanOrEqualTo(2));

            var w = new ByteWriter();
            SaveGame.Write(s, "m", w);
            var map2 = new AsciiMap(rows);
            var loaded = new GameSession(Fast(), new PlayerSettings(), map2.Blocks, map2.Ground, _data);
            SaveGame.Load(w.ToArray(), loaded, "m");
            Assert.That(Ids(loaded), Is.EqualTo(before));

            var broadcaster = new HostBroadcaster(s, "m");
            var clientMap = new AsciiMap(rows);
            var client = new ClientMatchState("m", clientMap.Blocks, clientMap.Ground, _data);
            var join = new ByteWriter();
            broadcaster.WriteJoin(1, join);
            client.Receive(join.Segment, 0);
            Assert.That(client.Diary.Unlocked.Select(i => client.Diary.Entries[i].id), Is.EqualTo(before));
        }
    }
}
