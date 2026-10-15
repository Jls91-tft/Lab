using System.IO;
using CepaCero.Map;
using NUnit.Framework;

namespace CepaCero.Tests.TestSupport
{
    /// <summary>Locates shared fixture files both under `dotnet test` and inside the Unity editor.</summary>
    public static class Fixtures
    {
        private const string RelativeDir = "packages/com.cepacero.core/Tests/Fixtures";

        public static string Path(string fileName)
        {
            foreach (string start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = System.IO.Path.Combine(dir.FullName, RelativeDir, fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                    dir = dir.Parent;
                }
            }
            Assert.Fail($"Fixture '{fileName}' not found above {TestContext.CurrentContext.TestDirectory}.");
            return null;
        }

        /// <summary>Parses a cepacero.map JSON with the same rules Unity's JsonUtility uses (public fields, exact names).</summary>
        public static CepaMapData LoadMap(string fileName)
        {
            string json = File.ReadAllText(Path(fileName));
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<CepaMapData>(json);
#else
            var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            return System.Text.Json.JsonSerializer.Deserialize<CepaMapData>(json, options);
#endif
        }
    }
}
