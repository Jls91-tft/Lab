using System;
using System.Collections.Generic;
using System.Linq;
using CepaCero.Rules;
using NUnit.Framework;

namespace CepaCero.Tests.Rules
{
    public class DayNightCycleTests
    {
        private static GameSettings ShortDays() => new GameSettings
        {
            TickRate = 10, DaySeconds = 10, DuskSeconds = 2, NightSeconds = 6, DawnSeconds = 2,
        };

        [Test]
        public void StartsOnTheMorningOfDayOne()
        {
            var cycle = new DayNightCycle(ShortDays());
            Assert.That(cycle.Day, Is.EqualTo(1));
            Assert.That(cycle.Phase, Is.EqualTo(DayPhase.Day));
            Assert.That(cycle.AmbientLight, Is.EqualTo(1f));
        }

        [Test]
        public void GoesThroughEveryPhaseInOrderAndWraps()
        {
            var cycle = new DayNightCycle(ShortDays());
            var phases = new List<DayPhase> { cycle.Phase };
            for (int i = 0; i < 200; i++)
            {
                if (cycle.Step())
                {
                    phases.Add(cycle.Phase);
                }
            }
            Assert.That(phases.Take(5), Is.EqualTo(new[] { DayPhase.Day, DayPhase.Dusk, DayPhase.Night, DayPhase.Dawn, DayPhase.Day }));
            Assert.That(cycle.Day, Is.EqualTo(2), "200 ticks = one full 20 s cycle at 10 Hz → day 2 begins");
        }

        [Test]
        public void LightFadesAtDuskAndReturnsAtDawn()
        {
            var cycle = new DayNightCycle(ShortDays());
            cycle.SetTick(100 + 10); // halfway through dusk
            Assert.That(cycle.Phase, Is.EqualTo(DayPhase.Dusk));
            Assert.That(cycle.AmbientLight, Is.InRange(0.3f, 0.8f));
            cycle.SetTick(100 + 20 + 30);
            Assert.That(cycle.Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(cycle.AmbientLight, Is.LessThan(0.1f));
        }

        [Test]
        public void ClientCanSyncFromTheHostTick()
        {
            var host = new DayNightCycle(ShortDays());
            for (int i = 0; i < 437; i++)
            {
                host.Step();
            }
            var client = new DayNightCycle(ShortDays());
            client.SetTick(host.Tick);
            Assert.That(client.Day, Is.EqualTo(host.Day));
            Assert.That(client.Phase, Is.EqualTo(host.Phase));
            Assert.That(client.PhaseProgress, Is.EqualTo(host.PhaseProgress));
        }

        [Test]
        public void InvalidSettingsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DayNightCycle(new GameSettings { NightSeconds = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DayNightCycle(new GameSettings { HordeGrowth = 0.5f }));
        }
    }

    public class HordeDirectorTests
    {
        [Test]
        public void HordeGrowsEveryNight()
        {
            var director = new HordeDirector(new GameSettings());
            int previous = 0;
            for (int night = 1; night <= 8; night++)
            {
                int total = director.PlanNight(night).Total;
                Assert.That(total, Is.GreaterThan(previous), $"night {night}");
                previous = total;
            }
        }

        [Test]
        public void FirstNightIsOnlyWalkers()
        {
            HordePlan plan = new HordeDirector(new GameSettings()).PlanNight(1);
            Assert.That(plan.Total, Is.EqualTo(12));
            Assert.That(plan.CountOf(HordeDirector.Walker), Is.EqualTo(12));
        }

        [Test]
        public void SpecialsAndBossAppearLater()
        {
            var director = new HordeDirector(new GameSettings());
            HordePlan night2 = director.PlanNight(2);
            Assert.That(night2.CountOf(HordeDirector.Screamer), Is.GreaterThanOrEqualTo(1));
            Assert.That(night2.CountOf(HordeDirector.Runner), Is.GreaterThanOrEqualTo(1));
            Assert.That(night2.CountOf(HordeDirector.Brute), Is.EqualTo(0));
            HordePlan night5 = director.PlanNight(5);
            Assert.That(night5.CountOf(HordeDirector.Brute), Is.EqualTo(1));
            Assert.That(night5.CountOf(HordeDirector.Spitter), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void NoiseBringsMoreZombiesAndMaxCaps()
        {
            var director = new HordeDirector(new GameSettings { HordeMax = 50 });
            Assert.That(director.PlanNight(3, noiseBonus: 1f).Total, Is.GreaterThan(director.PlanNight(3).Total));
            Assert.That(director.PlanNight(30).Total, Is.EqualTo(50));
        }

        [Test]
        public void ArrivalsAreSpreadOverTheSpawnWindowAndRespectTheAliveCap()
        {
            var settings = new GameSettings { MaxAliveZombies = 10, SpawnWindow = 0.5f };
            var director = new HordeDirector(settings);
            HordePlan plan = director.PlanNight(5);
            director.BeginNight(plan);

            var spawned = new List<string>();
            director.TakeDue(0.1f, alive: 0, spawned);
            Assert.That(spawned.Count, Is.InRange(1, 10), "only the first slice arrives early");
            int total = spawned.Count;

            spawned.Clear();
            director.TakeDue(0.5f, alive: 0, spawned);
            Assert.That(spawned.Count, Is.LessThanOrEqualTo(10), "never more than the alive cap at once");
            total += spawned.Count;

            for (int i = 0; i < 100 && director.Remaining > 0; i++)
            {
                spawned.Clear();
                director.TakeDue(0.6f, alive: 0, spawned);
                total += spawned.Count;
            }
            Assert.That(director.Remaining, Is.EqualTo(0));
            Assert.That(total, Is.EqualTo(plan.Total), "everyone planned arrives, nobody twice");
        }

        [Test]
        public void ArrivalOrderIsReproducibleAndBossComesLast()
        {
            List<string> Order(ulong seed)
            {
                var director = new HordeDirector(new GameSettings { Seed = seed, MaxAliveZombies = 1000 });
                director.BeginNight(director.PlanNight(5));
                var all = new List<string>();
                director.TakeDue(1f, 0, all);
                return all;
            }
            List<string> a = Order(7);
            Assert.That(a, Is.EqualTo(Order(7)));
            Assert.That(a, Is.Not.EqualTo(Order(8)));
            Assert.That(a.Last(), Is.EqualTo(HordeDirector.Brute));
        }
    }
}
