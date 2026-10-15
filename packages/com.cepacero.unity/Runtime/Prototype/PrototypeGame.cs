using System;
using System.Collections.Generic;
using System.IO;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Lore;
using CepaCero.Map;
using CepaCero.Net;
using CepaCero.Net.Protocol;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Util;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Debug prototype: plays the whole core simulation with the generated placeholder
    /// art (tools/artgen, D-018) and a pixel-perfect camera (16 px tiles, 640×360 base).
    /// Add it to an empty GameObject in an empty scene and press Play.
    ///
    /// Two modes:
    /// - Host (default): you are player 0 and the view reads the simulation.
    /// - Loopback client (<see cref="_playAsRemoteClient"/>): you are player 1 of a
    ///   host running in the same process. Your input travels as protocol bytes and
    ///   the view draws only the client replica, exactly what FishNet will carry.
    /// Not the real game: placeholder art, no real network, prototype HUD.
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

        [Tooltip("Screen pixels per art pixel. 0 = automatic: the largest whole number that keeps about 360 art pixels of height.")]
        [SerializeField] private int _pixelZoom;

        [Tooltip("Play as a remote client of an in-process host: everything goes through the network protocol.")]
        [SerializeField] private bool _playAsRemoteClient;

        private readonly PrototypeInput _input = new PrototypeInput();
        private readonly PlayerSettings _playerSettings = new PlayerSettings();
        private PrototypeArt _art;
        private PrototypeHud _hud;
        private readonly List<SessionEvent> _events = new List<SessionEvent>();
        private readonly ByteWriter _up = new ByteWriter(256);
        private readonly ByteWriter _down = new ByteWriter(16 * 1024);

        private GameData _data;
        private MapImportResult _map;
        private GameSession _session;
        private HostBroadcaster _broadcaster;
        private ClientMatchState _client;
        private PrototypeWorldView _world;
        private PrototypeEntityView _entities;
        private PrototypeDecorView _decor;
        private Transform _viewRoot;
        private Camera _camera;
        private int _me;
        private int _build;
        private int _zombiesShown;
        private double _netTime;
        private long _bytesDown;
        private long _bytesUp;
        private string _message = "";
        private float _messageUntil;
        private string _diaryText = "";
        private float _diaryUntil;

        private bool Remote => _client != null;

        private void Awake()
        {
            try
            {
                _art = PrototypeArt.Load();
                _hud = new PrototypeHud(_art);
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
            _session = new GameSession(_settings, _playerSettings, _map.Blocks, _map.Ground, _data, _map.Buildings);
            _broadcaster = null;
            _client = null;
            Time.fixedDeltaTime = 1f / _settings.TickRate;
            _me = _playAsRemoteClient ? 1 : 0;

            if (loadSave)
            {
                SaveGame.Load(File.ReadAllBytes(SavePath), _session, _mapPath);
            }
            else
            {
                Float2 spawn = FindSpawn();
                _session.AddPlayer(spawn);
                if (_playAsRemoteClient)
                {
                    _session.AddPlayer(spawn + new Float2(1.5f, 0f)); // you; player 0 stays at the fort
                }
                if (_starterKit)
                {
                    GiveStarterKit(_session.Inventories[_me]);
                }
                if (_populateMap)
                {
                    Debug.Log($"Cepa Cero prototype: {_session.PopulateMap()} zombies placed in the map.");
                }
            }

            _viewRoot = new GameObject("Cepa Cero (prototype view)").transform;
            BlockWorld viewBlocks = _map.Blocks;
            GroundLayer viewGround = _map.Ground;
            if (_playAsRemoteClient && _session.Players.Count > 1)
            {
                // The client loads the same map file on its own and learns the rest from the host.
                MapImportResult clientMap = RepoData.LoadMap(_mapPath);
                _broadcaster = new HostBroadcaster(_session, _mapPath);
                _client = new ClientMatchState(_mapPath, clientMap.Blocks, clientMap.Ground, _data);
                _down.Clear();
                _broadcaster.WriteJoin(_me, _down);
                _client.Receive(_down.Segment, 0);
                Debug.Log($"Cepa Cero prototype: joined as player {_client.MyPlayer} ({_down.Length} bytes).");
                viewBlocks = clientMap.Blocks;
                viewGround = clientMap.Ground;
                _netTime = 0;
                _bytesDown = 0;
                _bytesUp = 0;
            }
            else
            {
                _me = 0;
            }
            _world = new PrototypeWorldView(viewBlocks, viewGround, _art, _viewRoot);
            // Same map + same seed = the same street for every player, with nothing sent.
            List<DecorItem> decor = DecorPlanner.Plan(viewBlocks, viewGround, _map.Buildings, DecorPlanner.SeedFor(_map.Name));
            _decor = new PrototypeDecorView(decor, _art, _viewRoot);
            Debug.Log($"Cepa Cero prototype: {_decor.Count} pieces of decoration.");
            _entities = new PrototypeEntityView(_viewRoot, _data.Zombies, _art);
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

            if (Remote)
            {
                _up.Clear();
                Messages.WriteInput(_up, (uint)_session.Clock.Tick, PlayerInput.FromVector(_input.Move, _input.Sprint));
            }
            else
            {
                _session.SetInput(_me, PlayerInput.FromVector(_input.Move, _input.Sprint));
            }
            QueueActions();
            _input.ClearPresses();

            if (Remote)
            {
                _bytesUp += _up.Length;
                _broadcaster.Receive(_me, _up.Segment);
            }
            _session.Step();
            _events.Clear();
            if (Remote)
            {
                _broadcaster.CollectTick();
                _down.Clear();
                _broadcaster.WriteFor(_me, _down);
                _bytesDown += _down.Length;
                _netTime += Time.fixedDeltaTime;
                _client.Receive(_down.Segment, _netTime);
                _client.DrainEvents(_events);
            }
            else
            {
                _session.DrainEvents(_events);
            }
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
            float ambient = Remote ? _client.Clock.AmbientLight : _session.Clock.AmbientLight;
            _world.Refresh();
            _world.SetAmbient(ambient);
            _decor.SetAmbient(ambient);
            // Clients render one snapshot interval in the past to interpolate smoothly.
            _zombiesShown = Remote
                ? _entities.Draw(_client, _netTime - 2.0 / _settings.TickRate, ambient)
                : _entities.Draw(_session, _me, ambient);
            UpdateCursor();
            PlaceCamera(MyState.Position);
        }

        /// <summary>Same reach rule as the host (tile centre within PlayerSettings.Reach).</summary>
        private void UpdateCursor()
        {
            PlayerState me = MyState;
            Float2 mouse = _input.MouseWorld;
            var tile = new Int2((int)Math.Floor(mouse.X), (int)Math.Floor(mouse.Y));
            bool visible = !me.IsDowned && _map.Blocks.InBounds(tile.X, tile.Y);
            Float2 toTile = new Float2(tile.X + 0.5f, tile.Y + 0.5f) - me.Position;
            _entities.SetCursor(visible, tile, toTile.LengthSquared <= _playerSettings.Reach * _playerSettings.Reach);
        }

        /// <summary>
        /// Pixel-perfect camera by hand (no URP component needed): each art pixel covers a
        /// whole number of screen pixels, and the camera only moves in whole screen pixels.
        /// </summary>
        private void PlaceCamera(Float2 target)
        {
            int screenHeight = Screen.height > 0 ? Screen.height : PrototypeArt.BaseHeight; // 0 in batch mode
            int zoom = _pixelZoom > 0 ? _pixelZoom : PrototypeArt.Zoom(screenHeight);
            float screenPixelsPerUnit = _art.PixelsPerUnit * zoom;
            _camera.orthographicSize = screenHeight / (2f * screenPixelsPerUnit);
            float x = Mathf.Round(target.X * screenPixelsPerUnit) / screenPixelsPerUnit;
            float y = Mathf.Round(target.Y * screenPixelsPerUnit) / screenPixelsPerUnit;
            _camera.transform.position = new Vector3(x, y, -10f);
        }

        private void OnGUI()
        {
            if (_session == null)
            {
                return;
            }
            DayNightCycle clock = Remote ? _client.Clock : _session.Clock;
            double seconds = Math.Max(1.0, _netTime);
            var model = new HudModel
            {
                Day = clock.Day,
                Phase = clock.Phase,
                PhaseProgress = clock.PhaseProgress,
                NightsSurvived = Remote ? -1 : _session.NightsSurvived,
                ZombiesShown = _zombiesShown,
                ZombiesKilled = Remote ? -1 : _session.ZombiesKilled,
                Me = MyState,
                MaxHealth = _playerSettings.MaxHealth,
                MaxStamina = _playerSettings.MaxStamina,
                ShowHelp = _input.ShowHelp,
                Food = _settings.Survival == SurvivalMode.Off ? -1 : Remote ? _client.MyFood : Messages.QuantizeBar(MyState.Food),
                Water = _settings.Survival == SurvivalMode.Off ? -1 : Remote ? _client.MyWater : Messages.QuantizeBar(MyState.Water),
                Inventory = MyInventory,
                OpenContainer = Remote ? _client.OpenContainerContents : _session.OpenContainerContents(_me),
                Building = BuildKeys[_build],
                Message = Time.time < _messageUntil ? _message : "",
                Diary = Time.time < _diaryUntil ? _diaryText : "",
                Network = Remote
                    ? $"Cliente en bucle: ↓ {_bytesDown / seconds / 1024.0:0.0} KB/s · ↑ {_bytesUp / seconds / 1024.0:0.00} KB/s"
                    : "",
            };
            _hud.Draw(model);
        }

        // ------------------------------------------------------------- actions

        private PlayerState MyState
        {
            get
            {
                IReadOnlyList<PlayerState> players = Remote ? _client.Players : _session.Players;
                return _me < players.Count ? players[_me] : default;
            }
        }

        private Inventory MyInventory => Remote ? _client.MyInventory : _session.Inventories[_me];

        private void Send(PlayerIntent intent)
        {
            if (Remote)
            {
                Messages.WriteIntent(_up, intent);
            }
            else
            {
                _session.QueueIntent(_me, intent);
            }
        }

        private void QueueActions()
        {
            PlayerState me = MyState;
            Int2 tile = _input.MouseWorld.Tile;
            Float2 aim = (_input.MouseWorld - me.Position).Normalized;
            Inventory inv = MyInventory;
            BlockWorld blocks = _map.Blocks; // same registry and size on host and client

            if (_input.Hotbar >= 0)
            {
                Send(PlayerIntent.Select(_input.Hotbar));
            }
            if (_input.CycleBuild)
            {
                _build = (_build + 1) % BuildKeys.Length;
            }
            if (_input.Attack && aim.LengthSquared > 0f)
            {
                ItemDefinition weapon = inv.EquippedDefinition;
                Send(weapon != null && weapon.IsRanged ? PlayerIntent.Ranged(aim) : PlayerIntent.Melee(aim));
            }
            if (_input.Place && blocks.InBounds(tile.X, tile.Y))
            {
                Send(PlayerIntent.Place(tile.X, tile.Y, blocks.Registry.Get(BuildKeys[_build]).Id));
            }
            if (_input.Harvest && blocks.InBounds(tile.X, tile.Y))
            {
                Send(PlayerIntent.Hit(tile.X, tile.Y));
            }
            if (_input.Interact && blocks.InBounds(tile.X, tile.Y))
            {
                bool isContainer = Remote ? ContainsCell(_client.ContainerCells, tile) : _session.Containers != null && _session.Containers.Has(tile);
                Send(isContainer ? PlayerIntent.Open(tile.X, tile.Y) : PlayerIntent.Door(tile.X, tile.Y));
            }
            if (_input.Take)
            {
                Int2 box = Remote ? _client.OpenContainer : _session.OpenContainerOf(_me);
                if (box != GameSession.NoContainer)
                {
                    Send(PlayerIntent.Take(box.X, box.Y, 0));
                }
            }
            if (_input.PickUp)
            {
                Send(PlayerIntent.PickUpNearest());
            }
            if (_input.Use)
            {
                Send(PlayerIntent.Use());
            }
            if (_input.EquipFromBag && inv.Bag.Count > 0)
            {
                Send(PlayerIntent.ToHotbar(0, inv.SelectedHotbar));
            }
            if (_input.CraftBandage)
            {
                // Recipe indices come from the same data file on both sides.
                Send(PlayerIntent.CraftRecipe(new RecipeBook(_data.RecipesData, _data.Items).IndexOf("craft_bandage")));
            }
            if (_input.Revive)
            {
                int downed = NearestDownedTeammate(me.Position);
                if (downed >= 0)
                {
                    Send(PlayerIntent.ReviveTeammate(downed));
                }
            }
        }

        private int NearestDownedTeammate(Float2 position)
        {
            IReadOnlyList<PlayerState> players = Remote ? _client.Players : _session.Players;
            for (int p = 0; p < players.Count; p++)
            {
                if (p != _me && players[p].IsDowned && !players[p].IsDead && (players[p].Position - position).Length <= 1.5f)
                {
                    return p;
                }
            }
            return -1;
        }

        private static bool ContainsCell(IReadOnlyCollection<Int2> cells, Int2 cell)
        {
            foreach (Int2 c in cells)
            {
                if (c == cell)
                {
                    return true;
                }
            }
            return false;
        }

        private void HandleMetaKeys()
        {
            if (_input.Save)
            {
                var writer = new ByteWriter(64 * 1024);
                SaveGame.Write(_session, _mapPath, writer); // the host saves, also in loopback mode
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
                    Say($"¡Noche {e.Value}! Llega la horda");
                    break;
                case SessionEventType.PhaseChanged when e.Phase == DayPhase.Dawn:
                    Say("Amanece");
                    break;
                case SessionEventType.PlayerDowned when e.Player != _me:
                    Say("¡Tu compañero ha caído! Mantén R a su lado para levantarlo");
                    break;
                case SessionEventType.PlayerDied when e.Player == _me:
                    Say("Has muerto. Reapareces al amanecer; tu mochila se queda donde caíste.");
                    break;
                case SessionEventType.PlayerRevived:
                    Say(e.Player == _me ? "Te han levantado" : "Has levantado a tu compañero");
                    break;
                case SessionEventType.PlayerRespawned when e.Player == _me:
                    Say("Has vuelto al fuerte");
                    break;
                case SessionEventType.Hungry when e.Player == _me:
                    Say(e.Value == 0 ? "Tienes hambre: come algo (5 y Q)" : "Te mueres de hambre: tu vida máxima baja y vas perdiendo vida");
                    break;
                case SessionEventType.Thirsty when e.Player == _me:
                    Say(e.Value == 0 ? "Tienes sed: bebe algo (V pone la botella en la mano, Q bebe)" : "Deshidratado: te cansas antes y vas perdiendo vida");
                    break;
                case SessionEventType.RecipeUnlocked:
                    Say("Muestra analizada: recetas nuevas para el grupo");
                    break;
                case SessionEventType.DiaryUnlocked:
                    TeamDiary diary = Remote ? _client.Diary : _session.Diary;
                    if (diary != null && e.Value >= 0 && e.Value < diary.Entries.Count)
                    {
                        DiaryEntry entry = diary.Entries[e.Value];
                        _diaryText = $"<i>{entry.source}</i>\n{entry.text}";
                        _diaryUntil = Time.time + 12f;
                    }
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
                case IntentResult.InvalidTarget: return "Objetivo no válido (apunta con el ratón a un bloque)";
                case IntentResult.NotAllowed: return "No permitido";
                case IntentResult.TooSoon: return "Aún no";
                case IntentResult.PlayerDowned: return "Estás caído";
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
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || !_map.Blocks.InBounds(x, y) || !_map.Blocks.InBounds(x + 2, y))
                        {
                            continue;
                        }
                        if (IsOpenOutdoor(x, y) && IsOpenOutdoor(x + 1, y) && IsOpenOutdoor(x + 2, y))
                        {
                            return new Float2(x + 0.5f, y + 0.5f);
                        }
                    }
                }
            }
            return new Float2(cx + 0.5f, cy + 0.5f);
        }

        private bool IsOpenOutdoor(int x, int y)
        {
            GroundType g = _map.Ground.Get(x, y);
            return !_map.Blocks.IsSolid(x, y) && g != GroundType.Water && g != GroundType.Floor;
        }

        private void GiveStarterKit(Inventory inv)
        {
            ItemCatalog items = _data.Items;
            void Hotbar(string id, int count, int slot)
            {
                inv.Add(items.IndexOf(id), count);
                inv.MoveToHotbar(inv.Bag.Count - 1, slot);
            }
            Hotbar("bat", 1, 0);
            Hotbar("axe", 1, 1);
            Hotbar("pistol", 1, 2);
            Hotbar("bandage", 3, 3);
            Hotbar("food_can", 2, 4);
            inv.Add(items.IndexOf("water_bottle"), 2); // first in the bag, so V swaps it into your hand
            inv.Add(items.IndexOf("pistol_ammo"), 12);
            inv.Add(items.IndexOf("wood"), 40);
            inv.Add(items.IndexOf("nails"), 20);
            inv.Add(items.IndexOf("scrap_cloth"), 6);
            inv.SelectHotbar(0);
        }
    }
}
