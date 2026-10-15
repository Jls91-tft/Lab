// Hand-written signatures of the small part of the Unity API that com.cepacero.unity uses.
// Not Unity code: only lets `dotnet build` catch C# and core-API mistakes without the editor.
// Real Unity compilation is still the final word (see docs/setup-unity.md).
using System;
namespace UnityEngine
{
    public class Object { public string name; public static void Destroy(Object o) {} public static implicit operator bool(Object o) => o != null; }
    public class Component : Object { public GameObject gameObject => null; public Transform transform => null; }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { }
    public class GameObject : Object { public GameObject(string n) {} public Transform transform => null; public string tag; public T AddComponent<T>() where T : Component => null; public bool activeSelf => true; public void SetActive(bool v) {} }
    public class Transform : Component { public void SetParent(Transform p, bool w) {} public Vector3 position; public Vector3 localPosition; public Vector3 localScale; }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero => default; }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public struct Rect { public Rect(float x, float y, float w, float h) {} }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white => default; public static Color black => default; public static Color red => default; public static Color Lerp(Color a, Color b, float t) => a; }
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator Color(Color32 c) => default; }
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point }
    public enum TextureWrapMode { Clamp }
    public class Texture : Object { public int width, height; public FilterMode filterMode; public TextureWrapMode wrapMode; }
    public class Texture2D : Texture { public Texture2D(int w, int h, TextureFormat f, bool m) {} public static Texture2D whiteTexture => null; public void SetPixels32(Color32[] p) {} public void SetPixel(int x, int y, Color c) {} public void Apply(bool m) {} }
    public class Sprite : Object { public static Sprite Create(Texture2D t, Rect r, Vector2 p, float ppu) => null; }
    public class Shader : Object { public static Shader Find(string n) => null; }
    public class Material : Object { public Material(Shader s) {} }
    public class Renderer : Component { public Material sharedMaterial; public int sortingOrder; }
    public class SpriteRenderer : Renderer { public Sprite sprite; public Color color; }
    public enum CameraClearFlags { SolidColor }
    public class Camera : Behaviour { public static Camera main => null; public bool orthographic; public float orthographicSize; public CameraClearFlags clearFlags; public Color backgroundColor; public Vector3 ScreenToWorldPoint(Vector3 p) => p; }
    public static class Mathf { public static float Lerp(float a, float b, float t) => a; public static float Max(float a, float b) => a; public static float Min(float a, float b) => a; public static int Min(int a, int b) => a; public static float PingPong(float t, float l) => t; }
    public static class Time { public static float time; public static float fixedDeltaTime; }
    public static class Debug { public static void Log(object o) {} public static void LogWarning(object o) {} public static void LogException(Exception e) {} }
    public static class Application { public static string dataPath => ""; public static string persistentDataPath => ""; }
    public static class JsonUtility { public static T FromJson<T>(string s) => default; }
    public static class Screen { public static int width, height; }
    public enum TextAnchor { UpperLeft }
    public class GUIStyle { public GUIStyle(GUIStyle o) {} public int fontSize; public bool richText; public TextAnchor alignment; public bool wordWrap; }
    public class GUISkin { public GUIStyle label; public GUIStyle box; }
    public static class GUI { public static GUISkin skin; public static void Box(Rect r, string t, GUIStyle s) {} public static void Label(Rect r, string t, GUIStyle s) {} }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string t) {} }
    public class SerializeField : Attribute { }
}
namespace UnityEngine.InputSystem
{
    public class ButtonControl { public bool isPressed; public bool wasPressedThisFrame; }
    public class Vector2Control { public UnityEngine.Vector2 ReadValue() => default; }
    public class Keyboard { public static Keyboard current; public ButtonControl aKey, dKey, wKey, sKey, leftArrowKey, rightArrowKey, upArrowKey, downArrowKey, leftShiftKey, fKey, eKey, tKey, gKey, qKey, bKey, vKey, cKey, f5Key, f9Key, digit1Key, digit2Key, digit3Key, digit4Key, digit5Key; }
    public class Mouse { public static Mouse current; public ButtonControl leftButton, rightButton; public Vector2Control position; }
}
