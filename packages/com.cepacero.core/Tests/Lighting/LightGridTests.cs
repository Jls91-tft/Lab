using System.Linq;
using CepaCero.Lighting;
using CepaCero.Tests.TestSupport;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Lighting
{
    public class LightGridTests
    {
        private static LightGrid Lit(AsciiMap map, in LightSource light, int size = 21)
        {
            var grid = new LightGrid();
            grid.Begin(0, 0, size, size);
            grid.Add(map.Blocks, 0, light);
            return grid;
        }

        private static AsciiMap OpenField(int size = 21) => new AsciiMap(Enumerable.Repeat(new string('.', size), size).ToArray());

        [Test]
        public void ALampLightsAroundItAndFadesWithDistance()
        {
            AsciiMap map = OpenField();
            LightGrid g = Lit(map, LightSource.Omni(10.5f, 10.5f, 6f, 1f, 1f, 1f, 1f));
            float centre = g.Brightness(10, 10);
            float near = g.Brightness(12, 10);
            float far = g.Brightness(15, 10);
            Assert.That(centre, Is.GreaterThan(near));
            Assert.That(near, Is.GreaterThan(far));
            Assert.That(far, Is.GreaterThan(0f));
            Assert.That(g.Brightness(17, 10), Is.Zero, "beyond the radius nothing");
            Assert.That(g.Brightness(10, 17), Is.Zero);
        }

        [Test]
        public void WhatThePlayerCannotSeeStaysDarkButTheirOwnWallsShow()
        {
            var rows = new string[21];
            for (int i = 0; i < 21; i++)
            {
                rows[i] = new string('.', 12) + "#" + new string('.', 8);
            }
            var map = new AsciiMap(rows);
            var grid = new LightGrid();
            grid.Begin(0, 0, 21, 21);
            var rgba = new byte[21 * 21 * 4];
            grid.Resolve(1f, 1f, 1f, rgba);
            grid.ApplySight(map.Blocks, 0, 10, 10, 0.25f, rgba);
            Assert.That(rgba[(10 * 21 + 11) * 4], Is.EqualTo(255), "in front of you");
            Assert.That(rgba[(10 * 21 + 12) * 4], Is.EqualTo(255), "the wall you look at");
            Assert.That(rgba[(10 * 21 + 15) * 4], Is.LessThan(100), "behind the wall: dark");
        }

        [Test]
        public void AWallBlocksLightAndCastsAShadow()
        {
            // A wall column at x = 12; the lamp is at x = 10.
            var rows = new string[21];
            for (int i = 0; i < 21; i++)
            {
                rows[i] = new string('.', 12) + "#" + new string('.', 8);
            }
            var map = new AsciiMap(rows);
            LightGrid g = Lit(map, LightSource.Omni(10.5f, 10.5f, 9f, 1f, 1f, 1f, 1f));
            Assert.That(g.Brightness(11, 10), Is.GreaterThan(0f), "in front of the wall");
            Assert.That(g.Brightness(12, 10), Is.GreaterThan(0f), "the wall itself is lit: it is what you see");
            Assert.That(g.Brightness(13, 10), Is.Zero, "behind the wall: shadow");
            Assert.That(g.Brightness(14, 12), Is.Zero, "and a long shadow, not just one tile");
        }

        [Test]
        public void ADoorBlocksLightUntilItIsOpenAndGlassNeverDoes()
        {
            BlockRegistry registry = BlockRegistry.CreateDefault(Fixtures.Construction());
            var rows = new string[21];
            for (int i = 0; i < 21; i++)
            {
                rows[i] = new string('.', 12) + "D" + new string('.', 8);
            }
            var map = new AsciiMap(registry, rows);
            var light = LightSource.Omni(10.5f, 10.5f, 9f, 1f, 1f, 1f, 1f);
            Assert.That(Lit(map, light).Brightness(14, 10), Is.Zero, "a closed door");

            map.Blocks.SetBlock(12, 10, registry.Get("door_open").Id);
            Assert.That(Lit(map, light).Brightness(14, 10), Is.GreaterThan(0f), "an open door lets it through");

            map.Blocks.SetBlock(12, 10, 0, BuildLayer.Structure, registry.Get("wall_glass").Id);
            Assert.That(Lit(map, light).Brightness(14, 10), Is.GreaterThan(0f), "glass is solid but see-through");
        }

        [Test]
        public void ATorchConeOnlyLightsWhereItPoints()
        {
            AsciiMap map = OpenField();
            LightGrid g = Lit(map, LightSource.Cone(10.5f, 10.5f, 8f, 1f, 0f, 0.85f, 1f, 1f, 1f, 1f));
            Assert.That(g.Brightness(14, 10), Is.GreaterThan(0.1f), "ahead");
            Assert.That(g.Brightness(6, 10), Is.Zero, "behind");
            Assert.That(g.Brightness(10, 14), Is.Zero, "to the side, outside the beam");
        }

        [Test]
        public void UndergroundNeverLeaksIntoUndugGround()
        {
            BlockRegistry registry = BlockRegistry.CreateDefault(Fixtures.Construction());
            var map = new AsciiMap(registry, Enumerable.Repeat(new string('.', 11), 11).ToArray());
            // Level −1 is solid ground except what is dug: dig a straight tunnel along y = 5.
            for (int x = 2; x <= 8; x++)
            {
                map.Blocks.SetBlock(x, 5, -1, BuildLayer.Floor, registry.Get("cave").Id);
            }
            var grid = new LightGrid();
            grid.Begin(0, 0, 11, 11);
            grid.Add(map.Blocks, -1, LightSource.Omni(3.5f, 5.5f, 6f, 1f, 1f, 1f, 1f));
            Assert.That(grid.Brightness(7, 5), Is.GreaterThan(0f), "along the tunnel");
            Assert.That(grid.Brightness(3, 8), Is.Zero, "the rock beside it is dark");
        }

        [Test]
        public void ResolveAddsTheAmbientAndNeverGoesBeyondFullBrightness()
        {
            AsciiMap map = OpenField();
            LightGrid g = Lit(map, LightSource.Omni(10.5f, 10.5f, 5f, 1f, 0.5f, 0f, 1f));
            var rgba = new byte[21 * 21 * 4];
            g.Resolve(0.1f, 0.1f, 0.2f, rgba);
            int far = (0 * 21 + 0) * 4;
            Assert.That(rgba[far], Is.EqualTo((byte)26).Within(1), "ambient only: 0.1 of 255");
            Assert.That(rgba[far + 2], Is.EqualTo((byte)51).Within(1));
            int core = (10 * 21 + 10) * 4;
            Assert.That(rgba[core], Is.GreaterThan(rgba[far]), "the lamp raises it");
            Assert.That(rgba[core + 2], Is.LessThan(rgba[core]), "and keeps its warm colour: more red than blue");
            Assert.That(rgba[core + 3], Is.EqualTo((byte)255));
            g.Resolve(1f, 1f, 1f, rgba);
            Assert.That(rgba[far], Is.EqualTo((byte)255), "full day: no darkening at all");
        }

        [Test]
        public void SmoothingSpreadsALightButKeepsItsTotalRoughlyTheSame()
        {
            AsciiMap map = OpenField();
            LightGrid g = Lit(map, LightSource.Omni(10.5f, 10.5f, 4f, 1f, 1f, 1f, 1f));
            float before = 0f, after = 0f;
            for (int y = 0; y < 21; y++)
            {
                for (int x = 0; x < 21; x++)
                {
                    before += g.Brightness(x, y);
                }
            }
            float peak = g.Brightness(10, 10);
            g.Smooth();
            for (int y = 0; y < 21; y++)
            {
                for (int x = 0; x < 21; x++)
                {
                    after += g.Brightness(x, y);
                }
            }
            Assert.That(g.Brightness(10, 10), Is.LessThan(peak));
            Assert.That(after, Is.EqualTo(before).Within(before * 0.1f));
        }
    }
}
