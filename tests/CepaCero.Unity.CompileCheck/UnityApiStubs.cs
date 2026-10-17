// Hand-written signatures of the small part of the Unity API that com.cepacero.unity uses.
// Not Unity code: only lets `dotnet build` catch C# and core-API mistakes without the editor.
// Real Unity compilation is still the final word (see docs/setup-unity.md).
using System;
namespace UnityEngine
{
    public class Object { public string name; public static void Destroy(Object o) {} public static implicit operator bool(Object o) => o != null; }
    public class Component : Object { public GameObject gameObject => null; public Transform transform => null; public T GetComponent<T>() where T : Component => null; }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { }
    public class GameObject : Object { public GameObject(string n) {} public Transform transform => null; public string tag; public T AddComponent<T>() where T : Component => null; public bool activeSelf => true; public void SetActive(bool v) {} }
    public class Transform : Component { public Transform parent => null; public Quaternion localRotation; public void SetParent(Transform p, bool w) {} public Vector3 position; public Vector3 localPosition; public Vector3 localScale; }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero => default; public static Vector2 operator /(Vector2 a, float d) => a; }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static Vector3 one => new Vector3(1f, 1f, 1f); public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a; public static Vector3 operator *(Vector3 a, float b) => a; }
    public struct Vector3Int { public int x, y, z; public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; } }
    public struct BoundsInt { public BoundsInt(int x, int y, int z, int sx, int sy, int sz) {} }
    public struct Rect { public float x, y, width, height; public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; } }
    public class RectOffset { public RectOffset(int l, int r, int t, int b) {} }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject => null; }
    public class TextAsset : Object { public string text => ""; }
    public static class Resources { public static T Load<T>(string path) where T : Object => null; }
    public class GridLayout : Behaviour { public enum CellLayout { Rectangle, Hexagon, Isometric, IsometricZAsY } }
    public class Grid : GridLayout { public CellLayout cellLayout; public Vector3 cellSize; }
    public enum SpriteMeshType { FullRect, Tight }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white => default; public static Color black => default; public static Color red => default; public static Color Lerp(Color a, Color b, float t) => a; }
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator Color(Color32 c) => default; }
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point, Bilinear }
    public enum TextureWrapMode { Clamp }
    public class Texture : Object { public int width, height; public FilterMode filterMode; public TextureWrapMode wrapMode; }
    public class Texture2D : Texture { public Texture2D(int w, int h, TextureFormat f, bool m) {} public Texture2D(int w, int h, TextureFormat f, bool m, bool linear) {} public void LoadRawTextureData(byte[] d) {} public byte[] EncodeToPNG() => null; public static Texture2D whiteTexture => null; public void SetPixels32(Color32[] p) {} public void SetPixel(int x, int y, Color c) {} public void Apply(bool m) {} }
    public class Sprite : Object { public static Sprite Create(Texture2D t, Rect r, Vector2 p, float ppu) => null; public static Sprite Create(Texture2D t, Rect r, Vector2 p, float ppu, uint extrude, SpriteMeshType mesh) => null; }
    public class Shader : Object { public static Shader Find(string n) => null; }
    public class Material : Object { public Material(Shader s) {} }
    public class Renderer : Component { public Material sharedMaterial; public int sortingOrder; public bool enabled; }
    public enum SpriteSortPoint { Center, Pivot }
    public class SpriteRenderer : Renderer { public Sprite sprite; public Color color; public bool flipX; public SpriteSortPoint spriteSortPoint; }
    public struct Quaternion { public static Quaternion Euler(float x, float y, float z) => default; }
    public enum CameraClearFlags { SolidColor }
    public class Camera : Behaviour { public static Camera main => null; public bool orthographic; public float orthographicSize; public float aspect; public CameraClearFlags clearFlags; public Color backgroundColor; public Vector3 ScreenToWorldPoint(Vector3 p) => p; }
    public static class Mathf { public static float Clamp(float v, float a, float b) => v; public static float Lerp(float a, float b, float t) => a; public static float Max(float a, float b) => a; public static float Min(float a, float b) => a; public static int Min(int a, int b) => a; public static float PingPong(float t, float l) => t; public static int Max(int a, int b) => a; public static float Round(float v) => v; public static int RoundToInt(float v) => 0; public static float Clamp01(float v) => v; public static float Abs(float v) => v; public static int Clamp(int v, int a, int b) => v; public static float MoveTowards(float c, float t, float d) => c; }
    public static class Time { public static float time; public static float fixedDeltaTime; public static float deltaTime; public static float fixedTime; }
    public static class Debug { public static void Log(object o) {} public static void LogWarning(object o) {} public static void LogException(Exception e) {} public static bool isDebugBuild => true; }
    public static class ScreenCapture { public static void CaptureScreenshot(string path) {} }
    public static class Application { public static string dataPath => ""; public static string persistentDataPath => ""; public static bool runInBackground; }
    public static class JsonUtility { public static T FromJson<T>(string s) => default; }
    public static class Screen { public static int width, height; }
    public enum TextAnchor { UpperLeft, MiddleLeft, MiddleCenter, LowerRight }
    public class GUIStyleState { public Color textColor; }
    public class Font : Object { }
    public class AudioClip : Object { public static AudioClip Create(string n, int len, int ch, int rate, bool stream) => null; public bool SetData(float[] d, int off) => true; }
    public class AudioSource : Behaviour { public AudioClip clip; public bool playOnAwake; public bool loop; public float spatialBlend; public float volume; public float pitch; public float panStereo; public bool isPlaying => false; public void Play() {} }
    public class GUIStyle { public GUIStyle(GUIStyle o) {} public Font font; public int fontSize; public bool richText; public TextAnchor alignment; public bool wordWrap; public RectOffset padding; public RectOffset margin; public GUIStyleState normal => null; }
    public class GUISkin { public GUIStyle label; public GUIStyle box; }
    public static class GUI { public static GUISkin skin; public static Color color; public static void Box(Rect r, string t, GUIStyle s) {} public static void Label(Rect r, string t, GUIStyle s) {} public static void DrawTextureWithTexCoords(Rect r, Texture t, Rect uv) {} }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string t) {} }
    public class SerializeField : Attribute { }
}
namespace UnityEngine.Rendering
{
    public class SortingGroup : UnityEngine.Behaviour { public int sortingOrder; }
}

namespace UnityEngine.Tilemaps
{
    public class TileBase : UnityEngine.ScriptableObject { }
    public class Tile : TileBase { public UnityEngine.Sprite sprite; public UnityEngine.Color color; }
    public class Tilemap : UnityEngine.GridLayout { public UnityEngine.Color color; public UnityEngine.Vector3 tileAnchor; public void SetTile(UnityEngine.Vector3Int p, TileBase t) {} public T GetTile<T>(UnityEngine.Vector3Int p) where T : TileBase => null; public void SetTilesBlock(UnityEngine.BoundsInt b, TileBase[] t) {} public void ClearAllTiles() {} }
    public class TilemapRenderer : UnityEngine.Renderer { public enum Mode { Chunk, Individual, SRPBatch } public Mode mode; }
}
namespace UnityEngine.InputSystem
{
    public class ButtonControl { public bool isPressed; public bool wasPressedThisFrame; public bool wasReleasedThisFrame; }
    public class Vector2Control { public UnityEngine.Vector2 ReadValue() => default; }
    public class Keyboard { public static Keyboard current; public ButtonControl rKey, aKey, dKey, wKey, sKey, leftArrowKey, rightArrowKey, upArrowKey, downArrowKey, leftShiftKey, fKey, eKey, tKey, gKey, qKey, bKey, vKey, cKey, hKey, tabKey, f5Key, f9Key, digit1Key, digit2Key, digit3Key, digit4Key, digit5Key, digit6Key, digit7Key, digit8Key, digit9Key, escapeKey, leftCtrlKey, rightCtrlKey, leftBracketKey, rightBracketKey, commaKey, periodKey, zKey, leftAltKey, rightAltKey, lKey, pageUpKey, pageDownKey, f6Key; }
    public class Mouse { public static Mouse current; public ButtonControl leftButton, rightButton, middleButton; public Vector2Control position; public Vector2Control scroll; }
}
