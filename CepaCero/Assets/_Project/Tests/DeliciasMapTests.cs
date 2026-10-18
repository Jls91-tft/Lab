#if UNITY_INCLUDE_TESTS
using System.Linq;
using CepaCero.Map;
using CepaCero.Unity.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace CepaCero.Unity.Tests
{
    /// <summary>The real map (osm2tiles, cepacero.map v2) loads through RepoData as the prototype does.</summary>
    public class DeliciasMapTests
    {
        [Test]
        public void DeliciasLoadsWithFloorsBasementsAndRoofs()
        {
            RepoData.LoadGameData(); // the block registry needs the construction data
            MapImportResult map = RepoData.LoadMap(RepoData.DeliciasMap);

            Assert.That(map.Name, Is.EqualTo("delicias-centro"));
            Assert.That((map.Blocks.Width, map.Blocks.Height), Is.EqualTo((349, 334)));
            Assert.That(map.Buildings.Count, Is.GreaterThan(300));
            Assert.That(map.Buildings.Any(b => b.Levels > 1), "no building with upper floors");
            Assert.That(map.Buildings.Any(b => !string.IsNullOrEmpty(b.Roof)), "no roofs");
            Debug.Log($"Delicias: {map.Buildings.Count} buildings, {map.Buildings.Count(b => b.Underground > 0)} with basements, " +
                      $"{map.Warnings.Count} import warnings.");
        }
    }
}
#endif
