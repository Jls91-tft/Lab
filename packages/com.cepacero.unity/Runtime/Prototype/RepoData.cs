using System.IO;
using CepaCero.AI;
using CepaCero.Building;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Lore;
using CepaCero.Map;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Loads design data and maps straight from the repository (editor only, for the
    /// prototype). The real game will ship them as Addressables or StreamingAssets.
    /// </summary>
    public static class RepoData
    {
        /// <summary>Repository root: the Unity project lives in &lt;repo&gt;/CepaCero, so two levels above Assets.</summary>
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>
        /// Real map of the prototype: Delicias (Zaragoza), cepacero.map v2 with floors, basements and
        /// roofs per building. A pinned copy of `python -m osm2tiles --zone delicias-centro --meters-per-tile 1` (ODbL).
        /// </summary>
        public const string DeliciasMap = "data/maps/delicias-centro.map.json";

        /// <summary>The prototype's map since D-029: Zaragoza's old town around the Pilar, built on a square grid (osm2tiles --zone casco-pilar).</summary>
        public const string CascoMap = "data/maps/casco-pilar.map.json";

        /// <summary>Tiny hand-made map of the core tests: quick to load, one street.</summary>
        public const string SyntheticMap = "packages/com.cepacero.core/Tests/Fixtures/synthetic.map.json";

        public static string PathOf(string relative) => Path.Combine(RepoRoot, relative);

        public static T LoadJson<T>(string relative)
        {
            string path = PathOf(relative);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Cepa Cero: no encuentro {path}. ¿Está el proyecto Unity en <repo>/CepaCero?", path);
            }
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }

        private static ConstructionData _construction;

        public static GameData LoadGameData()
        {
            _construction = LoadJson<ConstructionData>("data/generated/construction.json");
            var items = new ItemCatalog(LoadJson<ItemsData>("data/generated/items.json"));
            return new GameData(
                new ZombieCatalog(LoadJson<BestiaryData>("data/generated/bestiary.json")),
                items,
                LoadJson<RecipesData>("data/generated/recipes.json"),
                LoadJson<LootData>("data/generated/loot.json"),
                LoadJson<DiaryData>("data/generated/diary.json"),
                _construction);
        }

        public static MapImportResult LoadMap(string relative)
        {
            MapImportResult map = MapImporter.Import(LoadJson<CepaMapData>(relative), BlockRegistry.CreateDefault(_construction));
            foreach (string warning in map.Warnings)
            {
                Debug.LogWarning($"Cepa Cero map: {warning}");
            }
            return map;
        }
    }
}
