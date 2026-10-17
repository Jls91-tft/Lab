using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CepaCero.EditorTools
{
    /// <summary>
    /// Windows development build of the prototype for the debug bridge (D-023):
    ///   Unity -batchmode -quit -projectPath CepaCero -executeMethod CepaCero.EditorTools.DevBuild.BuildWindows
    /// It goes to &lt;repo&gt;/devbuild/ (ignored by Git): the prototype reads the repository data two
    /// folders above its Data folder, so the build must sit right under the repository root.
    /// </summary>
    public static class DevBuild
    {
        private const string Scene = "Assets/_Project/Scenes/SCN_Prototype.unity";

        [MenuItem("Cepa Cero/Build de desarrollo (Windows)")]
        public static void BuildWindows()
        {
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string exe = Path.Combine(repo, "devbuild", "CepaCero.exe");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"Cepa Cero dev build: {report.summary.result} -> {exe} ({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors)");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
