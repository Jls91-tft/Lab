using System.IO;
using CepaCero.AI;
using CepaCero.Items;
using CepaCero.Map;
using NUnit.Framework;

namespace CepaCero.Tests.TestSupport
{
    /// <summary>Locates repository files both under `dotnet test` and inside the Unity editor.</summary>
    public static class Fixtures
    {
        private const string FixtureDir = "packages/com.cepacero.core/Tests/Fixtures";

        public static string Path(string fileName) => RepoFile(FixtureDir + "/" + fileName);

        /// <summary>Walks up from the test and working directories until <paramref name="relativePath"/> exists.</summary>
        public static string RepoFile(string relativePath)
        {
            foreach (string start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = System.IO.Path.Combine(dir.FullName, relativePath);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                    dir = dir.Parent;
                }
            }
            Assert.Fail($"'{relativePath}' not found above {TestContext.CurrentContext.TestDirectory}.");
            return null;
        }

        public static CepaMapData LoadMap(string fileName) => FromJson<CepaMapData>(File.ReadAllText(Path(fileName)));

        public static BestiaryData LoadBestiary() => FromJson<BestiaryData>(File.ReadAllText(RepoFile("data/generated/bestiary.json")));

        public static ZombieCatalog Catalog() => new ZombieCatalog(LoadBestiary());

        public static ItemCatalog Items() =>
            new ItemCatalog(FromJson<ItemsData>(File.ReadAllText(RepoFile("data/generated/items.json"))));

        public static RecipeBook Recipes(ItemCatalog items) =>
            new RecipeBook(FromJson<RecipesData>(File.ReadAllText(RepoFile("data/generated/recipes.json"))), items);

        public static LootRoller Loot(ItemCatalog items) =>
            new LootRoller(FromJson<LootData>(File.ReadAllText(RepoFile("data/generated/loot.json"))), items);

        /// <summary>Parses JSON with the same rules Unity's JsonUtility uses (public fields, exact names).</summary>
        public static T FromJson<T>(string json)
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<T>(json);
#else
            var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, options);
#endif
        }
    }
}
