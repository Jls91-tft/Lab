using System.Collections.Generic;
using System.Linq;
using CepaCero.AI;
using CepaCero.Core;
using CepaCero.Net;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Core
{
    public class GameSessionTests
    {
        private static GameSettings FastDays(ulong seed = 1) => new GameSettings
        {
            TickRate = 20, DaySeconds = 5, DuskSeconds = 1, NightSeconds = 20, DawnSeconds = 1,
            HordeBase = 6, MaxAliveZombies = 100, Seed = seed,
        };

        private static AsciiMap OpenMap(int size)
        {
            var rows = new string[size];
            for (int r = 0; r < size; r++)
            {
                rows[r] = new string('.', size);
            }
            return new AsciiMap(rows);
        }

        private static GameSession NewSession(AsciiMap map, GameSettings settings, PlayerSettings players)
        {
            return new GameSession(settings, players, map.Blocks, map.Ground, Fixtures.Catalog());
        }

        private static List<SessionEvent> RunUntil(GameSession s, System.Func<bool> stop, int maxTicks = 20 * 60 * 10)
        {
            var events = new List<SessionEvent>();
            for (int t = 0; t < maxTicks && !stop(); t++)
            {
                s.Step();
                s.DrainEvents(events);
            }
            return events;
        }

        [Test]
        public void ThreeNightsInARowEachHarderThanTheLast()
        {
            // Milestone 5 acceptance, headless: survive 3 nights, each with more zombies.
            AsciiMap map = OpenMap(100);
            var session = NewSession(map, FastDays(), new PlayerSettings { MaxHealth = 1_000_000 });
            session.AddPlayer(new Float2(50.5f, 50.5f));
            session.AddPlayer(new Float2(51.5f, 50.5f));

            var spawnedPerNight = new List<int>();
            var plannedPerNight = new List<int>();
            var events = new List<SessionEvent>();
            for (int t = 0; t < 20 * 3 * 27 + 100; t++)
            {
                session.Step();
                session.DrainEvents(events);
                if (session.Clock.Phase == DayPhase.Dawn && session.Clock.TicksIntoPhase == 0)
                {
                    spawnedPerNight.Add(session.SpawnedTonight);
                    plannedPerNight.Add(session.CurrentPlan.Total);
                }
            }

            Assert.That(events.Count(e => e.Type == SessionEventType.NightStarted), Is.EqualTo(3));
            Assert.That(session.NightsSurvived, Is.EqualTo(3));
            Assert.That(plannedPerNight.Count, Is.EqualTo(3));
            Assert.That(plannedPerNight[1], Is.GreaterThan(plannedPerNight[0]));
            Assert.That(plannedPerNight[2], Is.GreaterThan(plannedPerNight[1]));
            Assert.That(spawnedPerNight, Is.EqualTo(plannedPerNight), "every planned zombie entered during its night");
            Assert.That(events.Any(e => e.Type == SessionEventType.PlayerHurt), Is.True, "the horde reached the players");
        }

        [Test]
        public void NightHalvesZombieSight()
        {
            AsciiMap map = OpenMap(40);
            var session = NewSession(map, FastDays(), new PlayerSettings());
            session.AddPlayer(new Float2(20.5f, 20.5f));
            Assert.That(session.Zombies.SightMultiplier, Is.EqualTo(1f));
            RunUntil(session, () => session.Clock.Phase == DayPhase.Dusk);
            Assert.That(session.Zombies.SightMultiplier, Is.EqualTo(0.5f));
        }

        [Test]
        public void IntentsGoThroughTheHostRulesAndProduceDeltas()
        {
            AsciiMap map = OpenMap(30);
            var session = NewSession(map, FastDays(), new PlayerSettings());
            int p = session.AddPlayer(new Float2(10.5f, 10.5f));
            ushort wood = map.Registry.Get("wood_wall").Id;
            session.QueueIntent(p, PlayerIntent.Place(12, 10, wood));
            session.QueueIntent(p, PlayerIntent.Place(25, 25, wood));
            session.Step();

            var events = new List<SessionEvent>();
            session.DrainEvents(events);
            IntentResult[] results = events.Where(e => e.Type == SessionEventType.IntentResolved).Select(e => e.Result).ToArray();
            Assert.That(results, Is.EqualTo(new[] { IntentResult.Ok, IntentResult.TooSoon }), "the second one is rate limited first");
            var changes = new List<BlockChange>();
            map.Blocks.DrainChanges(changes);
            Assert.That(changes.Count, Is.EqualTo(1));
        }

        [Test]
        public void PlayersMoveWithTheirInputs()
        {
            AsciiMap map = OpenMap(30);
            var session = NewSession(map, FastDays(), new PlayerSettings());
            int p = session.AddPlayer(new Float2(10.5f, 10.5f));
            session.SetInput(p, PlayerInput.FromVector(new Float2(0, 1), sprint: false));
            for (int t = 0; t < 20; t++)
            {
                session.Step();
            }
            Assert.That(session.Players[p].Position.Y, Is.EqualTo(14f).Within(0.05f));
        }

        [Test]
        public void TheMatchEndsWhenEveryoneIsDowned()
        {
            AsciiMap map = OpenMap(60);
            var session = NewSession(map, FastDays(), new PlayerSettings { MaxHealth = 30 });
            session.AddPlayer(new Float2(30.5f, 30.5f));
            List<SessionEvent> events = RunUntil(session, () => session.IsGameOver);
            Assert.That(session.IsGameOver, Is.True);
            Assert.That(events.Any(e => e.Type == SessionEventType.PlayerDowned), Is.True);
            Assert.That(events.Last().Type, Is.EqualTo(SessionEventType.GameOver));
            int tick = (int)session.Clock.Tick;
            session.Step();
            Assert.That(session.Clock.Tick, Is.EqualTo(tick), "a finished match does not advance");
        }

        [Test]
        public void LoudDaysBringBiggerNights()
        {
            int NightOneTotal(bool loud)
            {
                AsciiMap map = OpenMap(40);
                var session = NewSession(map, FastDays(), new PlayerSettings { ActionCooldown = 0f });
                int p = session.AddPlayer(new Float2(20.5f, 20.5f));
                ushort wood = map.Registry.Get("wood_wall").Id;
                map.Blocks.SetBlock(21, 20, wood);
                RunUntil(session, () =>
                {
                    if (loud && session.Clock.Phase == DayPhase.Day)
                    {
                        session.QueueIntent(p, PlayerIntent.Hit(21, 20)); // banging on a wall all day
                        if (map.Blocks.GetBlock(21, 20).IsAir)
                        {
                            map.Blocks.SetBlock(21, 20, wood);
                        }
                    }
                    return session.Clock.Phase == DayPhase.Night;
                });
                return session.CurrentPlan.Total;
            }
            Assert.That(NightOneTotal(loud: true), Is.GreaterThan(NightOneTotal(loud: false)));
        }

        [Test]
        public void SameSeedSameMatch()
        {
            string Fingerprint(ulong seed)
            {
                AsciiMap map = OpenMap(80);
                var session = NewSession(map, FastDays(seed), new PlayerSettings { MaxHealth = 1_000_000 });
                session.AddPlayer(new Float2(40.5f, 40.5f));
                for (int t = 0; t < 20 * 15; t++)
                {
                    session.Step();
                }
                var parts = new List<string>();
                for (int i = 0; i < session.Zombies.Capacity; i++)
                {
                    if (session.Zombies.IsAlive(i))
                    {
                        parts.Add(session.Zombies.GetPosition(i).ToString());
                    }
                }
                return string.Join(";", parts);
            }
            string a = Fingerprint(11);
            Assert.That(a, Is.Not.Empty);
            Assert.That(Fingerprint(11), Is.EqualTo(a), "reproducible: useful to replay bug reports");
            Assert.That(Fingerprint(12), Is.Not.EqualTo(a));
        }
    }
}
