using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Net;
using CepaCero.Player;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Net
{
    public class HostIntentRulesTests
    {
        private AsciiMap _map;
        private ZombieSimulation _zombies;
        private HostIntentRules _rules;
        private List<PlayerState> _players;
        private PlayerSettings _settings;
        private ushort _wood;

        [SetUp]
        public void SetUp()
        {
            _map = new AsciiMap(
                "..........",
                "..........",
                ".....D....",
                "..........",
                "..........");
            _settings = new PlayerSettings();
            _zombies = new ZombieSimulation(_map.Blocks, _map.Ground, Fixtures.Catalog());
            _rules = new HostIntentRules(_map.Blocks, _map.Ground, _zombies, _settings);
            _players = new List<PlayerState> { PlayerState.Spawn(new Float2(3.5f, 2.5f), _settings) };
            _wood = _map.Registry.Get("wood_wall").Id;
            _map.Blocks.DrainChanges(new List<BlockChange>());
        }

        [Test]
        public void IntentsRoundTripThroughBytes()
        {
            var w = new ByteWriter();
            PlayerIntent.Place(12, 345, 6).Write(w);
            PlayerIntent.Melee(new Float2(0, -1)).Write(w);
            Assert.That(w.Length, Is.EqualTo(2 * PlayerIntent.Bytes));
            var r = new ByteReader(w.ToArray());
            PlayerIntent place = PlayerIntent.Read(r);
            Assert.That((place.Type, place.X, place.Y, place.Arg), Is.EqualTo((IntentType.PlaceBlock, (ushort)12, (ushort)345, (ushort)6)));
            Float2 aim = PlayerIntent.Read(r).AimDirection;
            Assert.That(aim.X, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(aim.Y, Is.EqualTo(-1f).Within(1e-3f));
        }

        [Test]
        public void PlacingIsValidatedAndRecordedAsADelta()
        {
            Assert.That(_rules.Apply(0, PlayerIntent.Place(5, 1, _wood), _players, 0f), Is.EqualTo(IntentResult.Ok));
            var changes = new List<BlockChange>();
            _map.Blocks.DrainChanges(changes);
            Assert.That(changes.Count, Is.EqualTo(1), "the placement becomes the delta sent to every client");

            Assert.That(_rules.Apply(0, PlayerIntent.Place(9, 4, _wood), _players, 1f), Is.EqualTo(IntentResult.OutOfReach));
            Assert.That(_rules.Apply(0, PlayerIntent.Place(3, 2, _wood), _players, 2f), Is.EqualTo(IntentResult.Occupied), "not on top of yourself");
            Assert.That(_rules.Apply(0, PlayerIntent.Place(4, 1, _map.Registry.Get("building_wall").Id), _players, 3f),
                Is.EqualTo(IntentResult.InvalidTarget), "players cannot place map-only blocks");
        }

        [Test]
        public void ActionsAreRateLimited()
        {
            Assert.That(_rules.Apply(0, PlayerIntent.Place(4, 1, _wood), _players, 0f), Is.EqualTo(IntentResult.Ok));
            Assert.That(_rules.Apply(0, PlayerIntent.Place(4, 3, _wood), _players, 0.1f), Is.EqualTo(IntentResult.TooSoon));
            Assert.That(_rules.Apply(0, PlayerIntent.Place(4, 3, _wood), _players, 0.4f), Is.EqualTo(IntentResult.Ok));
        }

        [Test]
        public void InventoryHookCanRefuse()
        {
            _rules.CanAffordBlock = (player, type) => false;
            Assert.That(_rules.Apply(0, PlayerIntent.Place(4, 1, _wood), _players, 0f), Is.EqualTo(IntentResult.MissingIngredients));
            Assert.That(_map.Blocks.GetBlock(4, 1).IsAir, Is.True);
        }

        [Test]
        public void HittingBlocksUsesHostDamageNotClientDamage()
        {
            _map.Blocks.SetBlock(4, 2, _wood);
            var forged = PlayerIntent.Hit(4, 2);
            forged.Arg = 9999; // a cheating client cannot choose the damage
            Assert.That(_rules.Apply(0, forged, _players, 0f), Is.EqualTo(IntentResult.Ok));
            Assert.That(_map.Blocks.GetHealth(4, 2), Is.EqualTo(100 - _settings.BlockHitDamage));
            Assert.That(_rules.LastNoise, Is.GreaterThan(0f));
        }

        [Test]
        public void DoorsCannotBeClosedOnSomeone()
        {
            Assert.That(_rules.Apply(0, PlayerIntent.Door(5, 2), _players, 0f), Is.EqualTo(IntentResult.Ok));
            Assert.That(_map.Blocks.IsSolid(5, 2), Is.False);
            _players.Add(PlayerState.Spawn(new Float2(5.5f, 2.5f), _settings)); // someone steps into the doorway
            Assert.That(_rules.Apply(0, PlayerIntent.Door(5, 2), _players, 1f), Is.EqualTo(IntentResult.Occupied));
        }

        [Test]
        public void MeleeHitsZombiesInTheArcOnly()
        {
            byte walker = Fixtures.Catalog().IndexOf("walker");
            int front = _zombies.Spawn(walker, new Float2(4.5f, 2.5f));
            int behind = _zombies.Spawn(walker, new Float2(2.5f, 2.5f));
            Assert.That(_rules.Apply(0, PlayerIntent.Melee(new Float2(1, 0)), _players, 0f), Is.EqualTo(IntentResult.Ok));
            Assert.That(_rules.LastMeleeHits, Is.EqualTo(1));
            Assert.That(_zombies.GetHealth(front), Is.EqualTo(60 - _settings.MeleeDamage));
            Assert.That(_zombies.GetHealth(behind), Is.EqualTo(60));
            Assert.That(_rules.Apply(0, PlayerIntent.Melee(new Float2(0, 1)), _players, 1f), Is.EqualTo(IntentResult.Missed));
        }

        [Test]
        public void DownedPlayersCannotAct()
        {
            PlayerState downed = _players[0];
            downed.Health = 0;
            _players[0] = downed;
            Assert.That(_rules.Apply(0, PlayerIntent.Place(4, 1, _wood), _players, 0f), Is.EqualTo(IntentResult.PlayerDowned));
            Assert.That(_rules.Apply(3, PlayerIntent.Place(4, 1, _wood), _players, 0f), Is.EqualTo(IntentResult.NotAllowed), "unknown player index");
        }
    }
}
