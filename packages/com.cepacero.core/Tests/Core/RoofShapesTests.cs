using System;
using System.Linq;
using CepaCero.Building;
using NUnit.Framework;

namespace CepaCero.Tests.Core
{
    public class RoofShapesTests
    {
        private static Func<int, int, bool> Rect(int x0, int y0, int x1, int y1) => (x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;

        [Test]
        public void EdgesSlopeAndCornersTurn()
        {
            Func<int, int, bool> roof = Rect(0, 0, 9, 9);
            Assert.That(RoofShapes.At(roof, 5, 9), Is.EqualTo(RoofShape.SlopeN));
            Assert.That(RoofShapes.At(roof, 5, 0), Is.EqualTo(RoofShape.SlopeS));
            Assert.That(RoofShapes.At(roof, 9, 5), Is.EqualTo(RoofShape.SlopeE));
            Assert.That(RoofShapes.At(roof, 0, 5), Is.EqualTo(RoofShape.SlopeW));
            Assert.That(RoofShapes.At(roof, 9, 9), Is.EqualTo(RoofShape.CornerNE));
            Assert.That(RoofShapes.At(roof, 0, 9), Is.EqualTo(RoofShape.CornerNW));
            Assert.That(RoofShapes.At(roof, 9, 0), Is.EqualTo(RoofShape.CornerSE));
            Assert.That(RoofShapes.At(roof, 0, 0), Is.EqualTo(RoofShape.CornerSW));
        }

        [Test]
        public void TheMiddleOfAWideRoofIsFlatAndOfALongStripHasARidgeAlongIt()
        {
            Assert.That(RoofShapes.At(Rect(0, 0, 19, 19), 10, 10), Is.EqualTo(RoofShape.Flat));
            Func<int, int, bool> strip = Rect(0, 0, 29, 5);             // long in x, 6 high
            Assert.That(RoofShapes.At(strip, 15, 2), Is.EqualTo(RoofShape.RidgeH));
            Func<int, int, bool> tall = Rect(0, 0, 5, 29);
            Assert.That(RoofShapes.At(tall, 2, 15), Is.EqualTo(RoofShape.RidgeV));
        }

        [Test]
        public void EveryShapeHasADistinctSpriteSuffixOfTheSpritegenSet()
        {
            var suffixes = Enum.GetValues(typeof(RoofShape)).Cast<RoofShape>().Select(RoofShapes.SpriteSuffix).ToList();
            Assert.That(suffixes.Distinct().Count(), Is.EqualTo(11));
            CollectionAssert.AreEquivalent(new[] { "flat", "slope_n", "slope_e", "slope_s", "slope_w", "corner_ne", "corner_nw", "corner_se", "corner_sw", "ridge_h", "ridge_v" }, suffixes);
        }
    }
}
