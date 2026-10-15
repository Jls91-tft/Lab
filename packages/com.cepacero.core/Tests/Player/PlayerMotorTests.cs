using CepaCero.Player;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Player
{
    public class PlayerMotorTests
    {
        private const float Dt = 0.05f;
        private readonly PlayerSettings _settings = new PlayerSettings();

        private static AsciiMap Open(int w, int h)
        {
            var rows = new string[h];
            for (int r = 0; r < h; r++)
            {
                rows[r] = new string('.', w);
            }
            return new AsciiMap(rows);
        }

        [Test]
        public void WalksAndSprintsAtTheReferenceSpeeds()
        {
            AsciiMap map = Open(40, 3);
            var walker = PlayerState.Spawn(new Float2(1.5f, 1.5f), _settings);
            var sprinter = walker;
            PlayerInput walk = PlayerInput.FromVector(new Float2(1, 0), sprint: false);
            PlayerInput run = PlayerInput.FromVector(new Float2(1, 0), sprint: true);
            for (int t = 0; t < 20; t++)
            {
                PlayerMotor.Step(ref walker, walk, Dt, map.Blocks, map.Ground, _settings);
                PlayerMotor.Step(ref sprinter, run, Dt, map.Blocks, map.Ground, _settings);
            }
            Assert.That(walker.Position.X - 1.5f, Is.EqualTo(3.5f).Within(0.01f));
            Assert.That(sprinter.Position.X - 1.5f, Is.EqualTo(5.5f).Within(0.01f));
            Assert.That(sprinter.Stamina, Is.EqualTo(80f).Within(0.01f));
        }

        [Test]
        public void SprintStopsWhenStaminaRunsOutAndNeedsAMinimumToRestart()
        {
            AsciiMap map = Open(200, 3);
            var state = PlayerState.Spawn(new Float2(1.5f, 1.5f), _settings);
            PlayerInput run = PlayerInput.FromVector(new Float2(1, 0), sprint: true);
            for (int t = 0; t < 120; t++) // 6 s > 5 s of stamina
            {
                PlayerMotor.Step(ref state, run, Dt, map.Blocks, map.Ground, _settings);
            }
            Assert.That(state.IsSprinting, Is.False);
            PlayerMotor.Step(ref state, run, Dt, map.Blocks, map.Ground, _settings);
            Assert.That(state.IsSprinting, Is.False, "0.6 stamina is not enough to start sprinting again");
        }

        [Test]
        public void SlidesAlongWallsAndNeverEntersThem()
        {
            var map = new AsciiMap(
                "..........",
                "....#.....",
                "....#.....",
                "..........");
            var state = PlayerState.Spawn(new Float2(2.5f, 2.0f), _settings);
            PlayerInput diagonal = PlayerInput.FromVector(new Float2(1, 0.2f), sprint: true);
            for (int t = 0; t < 60; t++)
            {
                PlayerMotor.Step(ref state, diagonal, Dt, map.Blocks, map.Ground, _settings);
                Assert.That(TileCollision.IsFree(state.Position, _settings.Radius, map.Blocks, map.Ground), Is.True, $"overlapping a wall at {state.Position}");
            }
            Assert.That(state.Position.X, Is.GreaterThan(5f), "slid past the wall instead of getting stuck");
        }

        [Test]
        public void ThinWallsCannotBeTunnelledAtHighSpeed()
        {
            var map = new AsciiMap("..#..");
            var state = PlayerState.Spawn(new Float2(1.5f, 0.5f), new PlayerSettings { Radius = 0.3f });
            var fast = new PlayerSettings { Radius = 0.3f, WalkSpeed = 60f };
            PlayerMotor.Step(ref state, PlayerInput.FromVector(new Float2(1, 0), false), 0.1f, map.Blocks, map.Ground, fast);
            Assert.That(state.Position.X, Is.LessThan(2f));
        }

        [Test]
        public void SameInputsGiveTheSameResultForPrediction()
        {
            AsciiMap map = Open(30, 30);
            var a = PlayerState.Spawn(new Float2(15, 15), _settings);
            var b = a;
            var rng = new DeterministicRandom(5);
            for (int t = 0; t < 200; t++)
            {
                var input = PlayerInput.FromVector(new Float2(rng.NextFloat() * 2 - 1, rng.NextFloat() * 2 - 1), rng.Chance(0.5f));
                var w = new ByteWriter();
                input.Write(w);
                PlayerInput replayed = PlayerInput.Read(new ByteReader(w.ToArray()));
                PlayerMotor.Step(ref a, input, Dt, map.Blocks, map.Ground, _settings);
                PlayerMotor.Step(ref b, replayed, Dt, map.Blocks, map.Ground, _settings);
            }
            Assert.That(b.Position, Is.EqualTo(a.Position));
            Assert.That(b.Stamina, Is.EqualTo(a.Stamina));
        }

        [Test]
        public void SprintingMakesNoiseAndDownedPlayersDoNotMove()
        {
            AsciiMap map = Open(10, 3);
            var state = PlayerState.Spawn(new Float2(1.5f, 1.5f), _settings);
            Assert.That(PlayerMotor.Step(ref state, PlayerInput.FromVector(new Float2(1, 0), true), Dt, map.Blocks, map.Ground, _settings),
                Is.EqualTo(_settings.SprintNoise));
            state.Health = 0;
            Float2 before = state.Position;
            PlayerMotor.Step(ref state, PlayerInput.FromVector(new Float2(1, 0), true), Dt, map.Blocks, map.Ground, _settings);
            Assert.That(state.Position, Is.EqualTo(before));
        }
    }
}
