using System.Collections.Generic;
using System.Linq;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Net;
using CepaCero.Net.Protocol;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using NUnit.Framework;

namespace CepaCero.Tests.Core
{
    /// <summary>Hunger and thirst (D-015): bars, penalties, eating, network and save.</summary>
    public class SurvivalTests
    {
        private const float Tolerance = 0.01f;

        private GameData _data;

        [SetUp]
        public void SetUp()
        {
            _data = Fixtures.GameData();
        }

        /// <summary>A 2-second day cycle: with FoodLastsDays 1 a full bar empties in 40 ticks.</summary>
        private static GameSettings Quick(SurvivalMode mode) => new GameSettings
        {
            TickRate = 20, DaySeconds = 1, DuskSeconds = 0, NightSeconds = 1, DawnSeconds = 0, HordeBase = 0,
            Survival = mode, FoodLastsDays = 1f, WaterLastsDays = 1f, StarvingSecondsPerHealth = 0.5f, ThirstSecondsPerHealth = 0.5f,
        };

        private static AsciiMap Open(int size) => new AsciiMap(Enumerable.Repeat(new string('.', size), size).ToArray());

        private GameSession Session(GameSettings settings, AsciiMap map = null)
        {
            map ??= Open(16);
            return new GameSession(settings, new PlayerSettings(), map.Blocks, map.Ground, _data);
        }

        private static List<SessionEvent> Step(GameSession s, int ticks)
        {
            var events = new List<SessionEvent>();
            for (int t = 0; t < ticks; t++)
            {
                s.Step();
                s.DrainEvents(events);
            }
            return events;
        }

        private void Equip(GameSession s, int player, string id, int count)
        {
            Inventory inv = s.Inventories[player];
            ushort item = _data.Items.IndexOf(id);
            inv.Add(item, count);
            inv.MoveToHotbar(inv.Bag.ToList().FindIndex(st => st.Item == item), 0);
            inv.SelectHotbar(0);
        }

        private static IntentResult Use(GameSession s, int player)
        {
            s.QueueIntent(player, PlayerIntent.Use());
            List<SessionEvent> events = Step(s, 1);
            return events.First(e => e.Type == SessionEventType.IntentResolved && e.Player == player).Result;
        }

        [Test]
        public void BarsDrainAtTheConfiguredPace()
        {
            var settings = new GameSettings(); // defaults: Hard, food 3 days, water 2 days, 15-minute cycle
            Assert.That(settings.Survival, Is.EqualTo(SurvivalMode.Hard), "D-015: hard by default");
            GameSession s = Session(settings);
            int p = s.AddPlayer(new Float2(8.5f, 8.5f));
            int ticks = 60 * settings.TickRate; // one minute
            Step(s, ticks);
            float cycle = settings.DaySeconds + settings.DuskSeconds + settings.NightSeconds + settings.DawnSeconds;
            Assert.That(s.Players[p].Food, Is.EqualTo(100f - 100f * 60f / (3f * cycle)).Within(Tolerance));
            Assert.That(s.Players[p].Water, Is.EqualTo(100f - 100f * 60f / (2f * cycle)).Within(Tolerance));
        }

        [Test]
        public void HardModeStarvesYou()
        {
            GameSession s = Session(Quick(SurvivalMode.Hard));
            int p = s.AddPlayer(new Float2(8.5f, 8.5f));
            List<SessionEvent> events = Step(s, 40);
            Assert.That(s.Players[p].Food, Is.EqualTo(0f));
            Assert.That(events.Any(e => e.Type == SessionEventType.Hungry && e.Value == 0), "low warning first");
            Assert.That(events.Any(e => e.Type == SessionEventType.Hungry && e.Value == 1), "then empty");
            Assert.That(events.Any(e => e.Type == SessionEventType.Thirsty && e.Value == 1));
            Assert.That(s.Players[p].Health, Is.EqualTo(75), "hunger caps health at 75 %");

            Step(s, 100); // 5 s: 1 point every 0.5 s for each empty bar
            Assert.That(s.Players[p].Health, Is.EqualTo(75 - 20).Within(2));

            Step(s, 20 * 20);
            Assert.That(s.Players[p].IsDead, Is.True, "alone, starving to 0 is death");
        }

        [Test]
        public void SoftModeWeakensButNeverKills()
        {
            GameSession s = Session(Quick(SurvivalMode.Soft));
            int p = s.AddPlayer(new Float2(8.5f, 8.5f));
            Step(s, 20 * 30);
            PlayerState me = s.Players[p];
            Assert.That(me.Food, Is.EqualTo(0f));
            Assert.That(me.Health, Is.EqualTo(75));
            Assert.That(me.IsDowned, Is.False);
            Assert.That(SurvivalRules.StaminaRegenFactor(me, s.Settings), Is.EqualTo(0.5f));
        }

        [Test]
        public void OffModeChangesNothing()
        {
            GameSession s = Session(Quick(SurvivalMode.Off));
            int p = s.AddPlayer(new Float2(8.5f, 8.5f));
            Step(s, 20 * 30);
            Assert.That(s.Players[p].Food, Is.EqualTo(100f));
            Assert.That(s.Players[p].Health, Is.EqualTo(100));

            Equip(s, p, "food_can", 1);
            Assert.That(Use(s, p), Is.EqualTo(IntentResult.NothingToUse), "full health and no bars: nothing to eat for");
        }

        [Test]
        public void EatingAndDrinkingRefillTheBarsAndLiftTheCap()
        {
            GameSession s = Session(Quick(SurvivalMode.Hard));
            int p = s.AddPlayer(new Float2(8.5f, 8.5f));
            Step(s, 40);
            Assert.That(s.Players[p].Health, Is.EqualTo(75));

            Equip(s, p, "food_can", 1);
            Assert.That(Use(s, p), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Players[p].Food, Is.GreaterThan(35f).And.LessThanOrEqualTo(40f));
            Assert.That(s.Players[p].Health, Is.GreaterThanOrEqualTo(75 + 5 - 2), "cap lifted: the can's heal counts");
            Assert.That(s.Inventories[p].Count(_data.Items.IndexOf("food_can")), Is.Zero);

            Equip(s, p, "water_bottle", 2);
            Assert.That(Use(s, p), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Players[p].Water, Is.GreaterThan(35f));
        }

        [Test]
        public void ThirstSlowsStaminaRegeneration()
        {
            var settings = new GameSettings();
            var thirsty = new PlayerState { Water = 0f, Food = 50f };
            var fine = new PlayerState { Water = 1f, Food = 50f };
            Assert.That(SurvivalRules.StaminaRegenFactor(thirsty, settings), Is.EqualTo(0.5f));
            Assert.That(SurvivalRules.StaminaRegenFactor(fine, settings), Is.EqualTo(1f));
            Assert.That(SurvivalRules.MaxHealth(new PlayerState { Food = 0f }, settings, new PlayerSettings()), Is.EqualTo(75));
        }

        [Test]
        public void TheOwnerGetsItsBarsOverTheNetwork()
        {
            AsciiMap hostMap = Open(16);
            GameSession host = Session(Quick(SurvivalMode.Hard), hostMap);
            host.AddPlayer(new Float2(4.5f, 4.5f));
            int remote = host.AddPlayer(new Float2(8.5f, 8.5f));
            var broadcaster = new HostBroadcaster(host, "open");
            AsciiMap clientMap = Open(16);
            var client = new ClientMatchState("open", clientMap.Blocks, clientMap.Ground, _data);
            var w = new ByteWriter();
            broadcaster.WriteJoin(remote, w);
            client.Receive(w.Segment, 0);
            Assert.That(client.MyFood, Is.EqualTo(100));

            int vitalsMessages = 0;
            for (int t = 0; t < 20; t++)
            {
                host.Step();
                broadcaster.CollectTick();
                w.Clear();
                broadcaster.WriteFor(remote, w);
                vitalsMessages += CountMessages(w, MessageType.Vitals);
                client.Receive(w.Segment, t * 0.05);
            }
            Assert.That(client.MyFood, Is.EqualTo(Messages.QuantizeBar(host.Players[remote].Food)));
            Assert.That(client.MyWater, Is.EqualTo(Messages.QuantizeBar(host.Players[remote].Water)));
            Assert.That(client.MyFood, Is.LessThan(100));
            Assert.That(vitalsMessages, Is.LessThanOrEqualTo(20), "only sent when a whole percent changes");
        }

        [Test]
        public void SavesKeepTheBars()
        {
            GameSession original = Session(Quick(SurvivalMode.Hard));
            int p = original.AddPlayer(new Float2(8.5f, 8.5f));
            Step(original, 15);
            var writer = new ByteWriter();
            SaveGame.Write(original, "open", writer);

            GameSession loaded = Session(Quick(SurvivalMode.Hard));
            SaveGame.Load(writer.ToArray(), loaded, "open");
            Assert.That(loaded.Players[p].Food, Is.EqualTo(original.Players[p].Food));
            Assert.That(loaded.Players[p].Water, Is.EqualTo(original.Players[p].Water));
        }

        [Test]
        public void RespawningRefillsTheBars()
        {
            Assert.That(PlayerState.Spawn(default, new PlayerSettings()).Food, Is.EqualTo(SurvivalRules.Full));
            Assert.That(PlayerState.Spawn(default, new PlayerSettings()).Water, Is.EqualTo(SurvivalRules.Full));
        }

        private static int CountMessages(ByteWriter w, MessageType wanted)
        {
            var r = new ByteReader(w.Segment);
            int count = 0;
            while (r.Remaining > 0)
            {
                var type = (MessageType)r.ReadByte();
                int length = r.ReadUShort();
                r.Skip(length);
                if (type == wanted)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
