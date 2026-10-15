#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using CepaCero.Unity.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CepaCero.Unity.Tests
{
    /// <summary>
    /// Runs the debug prototype for a few seconds in both modes (normal and loopback
    /// client) and checks that it starts, keeps ticking and logs no exceptions.
    /// It does not replace playing it: visuals and feel still need a human.
    /// </summary>
    public class PrototypeSmokeTests
    {
        private const float RunSeconds = 20f;

        private int _errors;
        private string _firstError;

        [UnityTest]
        public IEnumerator NormalModeRunsWithoutErrors()
        {
            yield return Run(remoteClient: false);
        }

        [UnityTest]
        public IEnumerator RemoteLoopbackRunsWithoutErrors()
        {
            yield return Run(remoteClient: true);
        }

        private IEnumerator Run(bool remoteClient)
        {
            _errors = 0;
            _firstError = null;
            Application.logMessageReceived += OnLog;
            try
            {
                // Inactive, so Awake runs only after the mode flag is set.
                var go = new GameObject("Prototype (smoke test)");
                go.SetActive(false);
                var game = go.AddComponent<PrototypeGame>();
                SetField(game, "_playAsRemoteClient", remoteClient);
                go.SetActive(true);

                Assert.IsTrue(game.enabled, "PrototypeGame disabled itself: Awake threw an exception.");

                yield return new WaitForSeconds(RunSeconds);

                object session = GetField(game, "_session");
                Assert.IsNotNull(session, "No session was created.");
                object clock = GetProperty(session, "Clock");
                long tick = (long)GetProperty(clock, "Tick");
                Assert.Greater(tick, 100L, "The simulation barely ticked.");

                if (remoteClient)
                {
                    Assert.IsNotNull(GetField(game, "_client"), "Loopback mode did not create a client.");
                    Assert.Greater((long)GetField(game, "_bytesDown"), 0L, "No bytes came down from the host.");
                    Assert.Greater((long)GetField(game, "_bytesUp"), 0L, "No input went up to the host.");
                }

                Object.Destroy(go);
                yield return null;
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
            Assert.AreEqual(0, _errors, "Errors logged while running the prototype. First: " + _firstError);
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error)
            {
                return;
            }
            _errors++;
            _firstError ??= message;
        }

        private static FieldInfo Field(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = Field(target, name);
            Assert.IsNotNull(field, "Missing field " + name);
            field.SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = Field(target, name);
            Assert.IsNotNull(field, "Missing field " + name);
            return field.GetValue(target);
        }

        private static object GetProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(property, "Missing property " + name);
            return property.GetValue(target);
        }
    }
}
#endif
