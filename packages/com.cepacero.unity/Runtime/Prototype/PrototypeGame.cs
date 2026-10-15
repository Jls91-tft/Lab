using System;
using System.Collections.Generic;
using System.IO;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Map;
using CepaCero.Net;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Util;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Debug prototype: plays the whole core simulation (one local player, who is
    /// the host) with coloured squares. Add it to an empty GameObject in an empty
    /// scene and press Play. Not the real game: no art, no network, debug HUD.
    /// </summary>
    public sealed class PrototypeGame : MonoBehaviour
    {
        private const string SaveFile = "prototype.sav";

        private static readonly string[] BuildKeys = { "wood_wall", "metal_wall", "door", "spike_trap", "fence", "chest", "workbench", "concrete_wall" };

        [Tooltip("Map relative to the repository root: an osm2tiles output or the test fixture.")]
        [SerializeField] private string _mapPath = "packages/com.cepacero.core/Tests/Fixtures/synthetic.map.json";

        [Tooltip("Match options. Short days by default so a night comes quickly.")]
        [SerializeField] private GameSettings _settings = new GameSettings { DaySeconds = 90, DuskSeconds = 15, NightSeconds = 90, DawnSeconds = 10 };

        [SerializeField] private bool _populateMap = true;
        [SerializeField] private bool _starterKit = true;
        [SerializeField] private float _cameraSize = 14f;

        private readonly PrototypeInput _input = new PrototypeInput();
        private readonly PrototypeHud _hud = new PrototypeHud();
        private readonly List<SessionEvent> _events = new List<SessionEvent>();

        private GameData _data;
        private MapImportResult _map;
        private GameSession _session;
        private PrototypeWorldView _world;
        private PrototypeEntityView _entities;
        private Transform _viewRoot;
        private Camera _camera;
        private int _build;
        private string _message = "";
        private float _messageUntil;
        private string _diaryText = "";
        private float _diaryUntil;

        private void Awake()
        {
            try
            {
                _data = RepoData.LoadGameData();
                StartMatch(loadSave: false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                enabled = false;
            }
        }

        private void StartMatch(bool loadSave)
        {
            if (_viewRoot != null)
            {
                Destroy(_viewRoot.gameObject);
            }
            _map = RepoData.LoadMap(_mapPath);
            _session = new GameSession(_settings, new PlayerSettings(), _map.Blocks, _map.Ground, _data, _map.Buildings);
            Time.fixedDeltaTime = 1f / _settings.TickRate;

            if (loadSave)
            {
                SaveGame.Load(File.ReadAllBytes(SavePath), _session, _mapPath);
            }
            else
            {
                _session.AddPlayer(FindSpawn());
                if (_starterKit)
                {
                    GiveStarterKit(_session.Inventories[0]);
                }
                if (_populateMap)
                {
                    int placed = _session.PopulateMap();
                    Debug.Log($"Cepa Cero prototype: {placed} zombies placed in the map.");
                }
            }

            _viewRoot = new GameObject("Cepa Cero (prototype view)").transform;
            _world = new PrototypeWorldView(_map.Blocks, _map.Ground, _viewRoot);
            _entities = new PrototypeEntityView(_viewRoot);
            SetupCamera();
            Say(loadSave ? "Partida cargada" : $"Mapa: {_map.Name} ({_map.Blocks.Width}×{_map.Blocks.Height}). {_map.Attribution}");
        }

        private void Update()
        {
            _input.Poll(_camera);
        }

        private void FixedUpdate()
        {
            if (_session == null)
            {
                return;
            }
            HandleMetaKeys();
            if (_session == null)
            {
                return;
            }
            _session.SetInput(0, PlayerInput.FromVector(_input.Move, _input.Sprint));
            QueueActions();
            _input.ClearPresses();

            _session.Step();
            _events.Clear();
            _session.DrainEvents(_events);
            foreach (SessionEvent e in _events)
            {
                Report(e);
            }
        }

        private void LateUpdate()
        {
            if (_session == null)
            {
                return;
            }
            float ambient = _session.Clock.AmbientLight;
            _world.Refresh();
            _world.SetAmbient(ambient);
            _entities.Draw(_session, ambient);
            Float2 me = _session.Players[0].Position;
            _camera.transform.position = new Vector3(me.X, me.Y, -10f);
        }

        private void OnGUI()
        {
            if (_session == null)
            {
                return;
            }
            _hud.Draw(_session, BuildKeys[_build], Time.time < _messageUntil ? _message : "", Time.time < _diaryUntil ? _diaryText : "");
        }

        // ------------------------------------------------------------- actions

        private void QueueActions()
        {
            PlayerState me = _session.Players[0];
            Int2 tile = _input.MouseWorld.Tile;
            Float2 aim = (_input.MouseWorld - me.Position).Normalized;
            Inventory inv = _session.Inventories[0];

            if (_input.Hotbar >= 0)
            {
                _session.QueueIntent(0, PlayerIntent.Select(_input.Hotbar));
            }
            if (_input.CycleBuild)
            {
                _build = (_build + 1) % BuildKeys.Length;
            }
            if (_input.Attack && aim.LengthSquared > 0f)
            {
                ItemDefinition weapon = inv.EquippedDefinition;
                _session.QueueIntent(0, weapon != null && weapon.IsRanged ? PlayerIntent.Ranged(aim) : PlayerIntent.Melee(aim));
            }
            if (_input.Place && _map.Blocks.InBounds(tile.X, tile.Y))
            {
                _session.QueueIntent(0, PlayerIntent.Place(tile.X, tile.Y, _map.Blocks.Registry.Get(BuildKeys[_build]).Id));
            }
            if (_input.Harvest && _map.Blocks.InBounds(tile.X, tile.Y))
            {
                _session.QueueIntent(0, PlayerIntent.Hit(tile.X, tile.Y));
            }
            if (_input.Interact && _map.Blocks.InBounds(tile.X, tile.Y))
            {
                bool isContainer = _session.Containers != null && _session.Containers.Has(tile);
                _session.QueueIntent(0, isContainer ? PlayerIntent.Open(tile.X, tile.Y) : PlayerIntent.Door(tile.X, tile.Y));
            }
            if (_input.Take)
            {
                Int2 box = _session.OpenContainerOf(0);
                if (box != GameSession.NoContainer)
                {
                    _session.QueueIntent(0, PlayerIntent.Take(box.X, box.Y, 0));
                }
            }
            if (_input.PickUp)
            {
                _session.QueueIntent(0, PlayerIntent.PickUpNearest());
            }
            if (_input.Use)
            {
                _session.QueueIntent(0, PlayerIntent.Use());
            }
            if (_input.EquipFromBag && inv.Bag.Count > 0)
            {
                _session.QueueIntent(0, PlayerIntent.ToHotbar(0, inv.SelectedHotbar));
            }
            if (_input.CraftBandage)
            {
                _session.QueueIntent(0, PlayerIntent.CraftRecipe(_session.Recipes.IndexOf("craft_bandage")));
            }
        }

        private void HandleMetaKeys()
        {
            if (_input.Save)
            {
                var writer = new ByteWriter(64 * 1024);
                SaveGame.Write(_session, _mapPath, writer);
                File.WriteAllBytes(SavePath, writer.ToArray());
                Say($"Guardado ({writer.Length} bytes) en {SavePath}");
            }
            if (_input.Load)
            {
                _input.ClearPresses();
                if (File.Exists(SavePath))
                {
                    StartMatch(loadSave: true);
                }
                else
                {
                    Say("No hay partida guardada (F5 para guardar)");
                }
            }
        }

        private void Report(SessionEvent e)
        {
            switch (e.Type)
            {
                case SessionEventType.IntentResolved when e.Result != IntentResult.Ok && e.Result != IntentResult.TooSoon:
                    Say(ResultText(e.Result));
                    break;
                case SessionEventType.NightStarted:
                    Say($"¡Noche {e.Value}! Llega la horda: {_session.CurrentPlan.Total} zombis");
                    break;
                case SessionEventType.PhaseChanged when e.Phase == DayPhase.Dawn:
                    Say("Amanece");
                    break;
                case SessionEventType.PlayerDied:
                    Say("Has muerto. Reapareces al amanecer; tu mochila se queda donde caíste.");
                    break;
                case SessionEventType.PlayerRespawned:
                    Say("Has vuelto al fuerte");
                    break;
                case SessionEventType.RecipeUnlocked:
                    Say("Muestra analizada: recetas nuevas para el grupo");
                    break;
                case SessionEventType.DiaryUnlocked:
                    var entry = _session.Diary.Entries[e.Value];
                    _diaryText = $"<i>{entry.source}</i>\n{entry.text}";
                    _diaryUntil = Time.time + 12f;
                    break;
            }
        }

        private static string ResultText(IntentResult r)
        {
            switch (r)
            {
                case IntentResult.OutOfReach: return "Demasiado lejos";
                case IntentResult.Occupied: return "Ocupado";
                case IntentResult.MissingIngredients: return "Te faltan materiales";
                case IntentResult.NeedsWorkbench: return "Necesitas un banco de trabajo cerca";
                case IntentResult.Locked: return "Receta bloqueada: analiza la muestra";
                case IntentResult.NoAmmo: return "Sin munición";
                case IntentResult.NoRoom: return "No cabe";
                case IntentResult.NothingToUse: return "Nada que usar";
                case IntentResult.Missed: return "Fallas";
                default: return r.ToString();
            }
        }

        // -------------------------------------------------------------- setup

        private void Say(string text)
        {
            _message = text;
            _messageUntil = Time.time + 4f;
            Debug.Log($"Cepa Cero: {text}");
        }

        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFile);

        private void SetupCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = go.AddComponent<Camera>();
            }
            _camera.orthographic = true;
            _camera.orthographicSize = _cameraSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
        }

        private Float2 FindSpawn()
        {
            int cx = _map.Blocks.Width / 2;
            int cy = _map.Blocks.Height / 2;
            for (int r = 0; r < Math.Max(_map.Blocks.Width, _map.Blocks.Height); r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || !_map.Blocks.InBounds(x, y))
                        {
                            continue;
                        }
                        GroundType g = _map.Ground.Get(x, y);
                        if (!_map.Blocks.IsSolid(x, y) && g != GroundType.Water && g != GroundType.Floor)
                        {
                            return new Float2(x + 0.5f, y + 0.5f);
                        }
                    }
                }
            }
            return new Float2(cx + 0.5f, cy + 0.5f);
        }

        private void GiveStarterKit(Inventory inv)
        {
            ItemCatalog items = _data.Items;
            void Hotbar(string id, int slot)
            {
                inv.Add(items.IndexOf(id), 1);
                inv.MoveToHotbar(inv.Bag.Count - 1, slot);
            }
            Hotbar("bat", 0);
            Hotbar("axe", 1);
            Hotbar("pistol", 2);
            inv.Add(items.IndexOf("pistol_ammo"), 12);
            inv.Add(items.IndexOf("wood"), 40);
            inv.Add(items.IndexOf("nails"), 20);
            inv.Add(items.IndexOf("bandage"), 3);
            inv.MoveToHotbar(inv.Bag.Count - 1, 3);
            inv.Add(items.IndexOf("scrap_cloth"), 6);
            inv.SelectHotbar(0);
        }
    }
}
