using System;
using CepaCero.AI;
using CepaCero.Tests.TestSupport;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.AI
{
    public class ZombieCatalogTests
    {
        [Test]
        public void LoadsTheGeneratedBestiary()
        {
            ZombieCatalog catalog = Fixtures.Catalog();
            Assert.That(catalog.Count, Is.GreaterThanOrEqualTo(5));
            ZombieDefinition walker = catalog[catalog.IndexOf("walker")];
            Assert.That(walker.name, Is.EqualTo("Caminante"));
            Assert.That(walker.health, Is.EqualTo(60));
            Assert.That(walker.speed, Is.EqualTo(1.2f));
            Assert.That(walker.damage_taken.fire, Is.EqualTo(1.5f));
            Assert.That(walker.HasBehavior("chase"), Is.True);
        }

        [Test]
        public void SpitterCorrodesWoodAndBarelyScratchesConcrete()
        {
            ZombieCatalog catalog = Fixtures.Catalog();
            ZombieDefinition spitter = catalog[catalog.IndexOf("spitter")];
            Assert.That(ZombieCatalog.StructureDamage(spitter, BlockMaterial.Wood), Is.EqualTo(30));
            Assert.That(ZombieCatalog.StructureDamage(spitter, BlockMaterial.Concrete), Is.EqualTo(3));
        }

        [Test]
        public void BossHealthScalesWithPlayers()
        {
            ZombieCatalog catalog = Fixtures.Catalog();
            ZombieDefinition brute = catalog[catalog.IndexOf("brute")];
            Assert.That(brute.HealthFor(1), Is.EqualTo(1500));
            Assert.That(brute.HealthFor(4), Is.EqualTo(3750));
        }

        [Test]
        public void RejectsBadDocuments()
        {
            BestiaryData wrong = Fixtures.LoadBestiary();
            wrong.version = 9;
            Assert.Throws<FormatException>(() => new ZombieCatalog(wrong));

            BestiaryData dupes = Fixtures.LoadBestiary();
            dupes.zombies[1].id = dupes.zombies[0].id;
            Assert.Throws<FormatException>(() => new ZombieCatalog(dupes));
        }
    }
}
