using System;
using System.Collections.Generic;
using System.IO;
using CepaCero.Building;
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
        private PrototypeBuilder _builder;
        private float _nextHoldAction;
        private PrototypeBuildView _buildView;
        private RecipeBook _recipes;
        private BlockWorld _viewBlocks;
        private GroundLayer _viewGround;
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
                _builder = new PrototypeBuilder(new BuildPalette(_data.Construction));
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
            _viewBlocks = viewBlocks;
            _viewGround = viewGround;
            // The host pays from the session's book (it knows the unlocked recipes); a client builds its own.
            _recipes = Remote ? new RecipeBook(_data.RecipesData, _data.Items) : _session.Recipes;
            _world = new PrototypeWorldView(viewBlocks, viewGround, _art, _viewRoot);
            _buildView = new PrototypeBuildView(_viewRoot, _world, _art, viewBlocks.Registry);
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
            _world.SetLevel(MyState.Level);
            _world.SetInside(_world.HasRoofOver(MyState.Position.Tile.X, MyState.Position.Tile.Y));
            _entities.ViewLevel = MyState.Level;
            _decor.SetVisible(MyState.Level == 0);
            _builder.SetHand(MyInventory.EquippedDefinition);
            _builder.UpdatePreview(_input.MouseWorld.Tile, BuildContext());
            _buildView.Draw(_builder);
            UpdateCursor();
            PlaceCamera(MyState.Position);
        }

        /// <summary>Same reach rule as the host (tile centre within PlayerSettings.Reach).</summary>
        private void UpdateCursor()
        {
            PlayerState me = MyState;
            Float2 mouse = _input.MouseWorld;
            var tile = new Int2((int)Math.Floor(mouse.X), (int)Math.Floor(mouse.Y));
            bool visible = !me.IsDowned && !_builder.Active && _map.Blocks.InBounds(tile.X, tile.Y); // the ghost replaces it
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
                Level = MyState.Level,
                MaxHealth = _playerSettings.MaxHealth,
                MaxStamina = _playerSettings.MaxStamina,
                ShowHelp = _input.ShowHelp,
                Food = _settings.Survival == SurvivalMode.Off ? -1 : Remote ? _client.MyFood : Messages.QuantizeBar(MyState.Food),
                Water = _settings.Survival == SurvivalMode.Off ? -1 : Remote ? _client.MyWater : Messages.QuantizeBar(MyState.Water),
                Inventory = MyInventory,
                OpenContainer = Remote ? _client.OpenContainerContents : _session.OpenContainerContents(_me),
                Build = _builder,
                BuildTitle = BuildTitle(),
                BuildHint = BuildHint(),
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

        // ------------------------------------------------------- building with the hand (D-022)

        private PrototypeBuilder.Context BuildContext() => new PrototypeBuilder.Context(
            _viewBlocks, _viewGround, _playerSettings, MyState.Position,
            Remote ? _client.Players : _session.Players, MyInventory, _settings.BuildCost, MyState.Level);

        /// <summary>"Muro de ladrillo · 3 Ladrillo", or "" when the hand builds nothing.</summary>
        private string BuildTitle()
        {
            if (_builder.Pasting)
            {
                return $"Plano {_builder.Clipboard.Width}×{_builder.Clipboard.Height} · {_builder.Clipboard.Pieces.Count} piezas";
            }
            if (!_builder.Active || !_builder.Piece.HasValue)
            {
                return "";
            }
            BuildPiece piece = _builder.Piece.Value;
            string cost = PrototypeBuilder.CostText(_viewBlocks.Registry.Get(piece.Key), _data.Items, _settings.BuildCost);
            return string.IsNullOrEmpty(cost) ? piece.Name : $"{piece.Name} · {cost}";
        }

        private string BuildHint()
        {
            if (_builder.Pasting)
            {
                return PasteHint();
            }
            if (!_builder.Active)
            {
                return "";
            }
            foreach (PrototypeBuilder.Placement p in _builder.Preview)
            {
                if (p.Verdict != PrototypeBuilder.Verdict.Ok && p.Verdict != PrototypeBuilder.Verdict.Replace)
                {
                    return PrototypeBuilder.Explain(p.Verdict);
                }
            }
            string queued = _builder.Queue.Count > 0 ? $" · En cola: {_builder.Queue.Count}" : "";
            return _builder.DragStart.HasValue
                ? $"{_builder.Preview.Count} piezas: suelta para construir{queued}"
                : $"Clic: poner · Arrastra: línea · Mayús: contorno · Ctrl: habitación · Q/E forma · R girar · [ ] color{queued}";
        }

        private string PasteHint()
        {
            string missing = "";
            List<ItemShortfall> short_ = _builder.Clipboard.Shortfall(_viewBlocks.Registry,
                id => _data.Items.TryGetIndex(id, out ushort item) ? MyInventory.Count(item) : 0, _settings.BuildCost);
            if (short_.Count > 0)
            {
                var parts = new List<string>();
                foreach (ItemShortfall m in short_)
                {
                    parts.Add($"{m.Missing} {(_data.Items.TryGetIndex(m.Item, out ushort item) ? _data.Items[item].name : m.Item)}");
                }
                missing = " · Faltan: " + string.Join(", ", parts);
            }
            return $"Clic: pegar · R girar · Clic der./Esc: soltar{missing}";
        }

        private static string PlansDirectory => Path.Combine(Application.persistentDataPath, "plans");

        private void SavePlan()
        {
            if (_builder.Clipboard == null || _builder.Clipboard.IsEmpty)
            {
                Say("No hay nada copiado: Alt + arrastrar y Ctrl+C");
                return;
            }
            Directory.CreateDirectory(PlansDirectory);
            string path = Path.Combine(PlansDirectory, $"plano_{DateTime.Now:yyyyMMdd_HHmmss}.cepaplan");
            File.WriteAllText(path, _builder.Clipboard.ToText());
            Say("Plano guardado: " + path);
        }

        private int _planIndex = -1;

        /// <summary>Ctrl+L: the next saved blueprint becomes the clipboard (files from other people work too).</summary>
        private void LoadNextPlan()
        {
            string[] files = Directory.Exists(PlansDirectory) ? Directory.GetFiles(PlansDirectory, "*.cepaplan") : new string[0];
            if (files.Length == 0)
            {
                Say("No hay planos guardados en " + PlansDirectory);
                return;
            }
            Array.Sort(files, StringComparer.Ordinal);
            _planIndex = (_planIndex + 1) % files.Length;
            try
            {
                _builder.Clipboard = Blueprint.FromText(File.ReadAllText(files[_planIndex]));
                Say($"Plano cargado: {Path.GetFileName(files[_planIndex])} · Ctrl+V para pegarlo");
            }
            catch (FormatException e)
            {
                Say($"{Path.GetFileName(files[_planIndex])}: {e.Message}");
            }
        }

        /// <summary>Plans: Alt + drag selects, Ctrl+C copies, Ctrl+V pastes (R turns, click confirms), Ctrl+S saves, Ctrl+L loads the next saved one.</summary>
        private void HandlePlans(Int2 tile)
        {
            if (MyState.Level != 0)
            {
                if (_input.CopyPlan || _input.PastePlan || (_input.Alt && _input.Attack))
                {
                    Say("Los planos solo funcionan en la calle (nivel 0)");
                }
                _builder.CancelPaste();
                return;
            }
            if (_input.SavePlan)
            {
                SavePlan();
            }
            if (_input.LoadPlan)
            {
                LoadNextPlan();
            }
            if (_input.PastePlan && !_builder.StartPaste())
            {
                Say("No hay ningún plano: Alt + arrastrar y Ctrl+C");
            }
            if (_input.CopyPlan && _builder.Clipboard != null)
            {
                Say($"Plano en el portapapeles: {_builder.Clipboard.Pieces.Count} piezas · Ctrl+V pega · Ctrl+S guarda");
            }
            if (_input.Alt && _input.Attack)
            {
                _builder.BeginSelect(tile);
            }
            if (_input.AttackReleased && _builder.SelectStart.HasValue)
            {
                int n = _builder.EndSelect(tile, _viewBlocks);
                Say(n == 0 ? "No hay nada construido ahí" : $"{n} piezas copiadas · Ctrl+V pega · Ctrl+S guarda");
            }
        }

        /// <summary>What the cell under the cursor offers the click (see <see cref="HandResolver"/>).</summary>
        private ClickTarget TargetAt(Int2 tile)
        {
            BlockWorld blocks = _viewBlocks;
            int z = MyState.Level;
            if (!blocks.InBounds(tile.X, tile.Y))
            {
                return default;
            }
            BlockType structure = blocks.GetBlock(tile.X, tile.Y, z, BuildLayer.Structure);
            bool isContainer = z == 0 && (Remote ? ContainsCell(_client.ContainerCells, tile) : _session.Containers != null && _session.Containers.Has(tile));
            bool hasBlock = !structure.IsAir || !blocks.GetBlock(tile.X, tile.Y, z, BuildLayer.Object).IsAir;
            bool mine = false;
            for (int l = 0; l < 4; l++)
            {
                BlockType piece = blocks.GetBlock(tile.X, tile.Y, z, (BuildLayer)l);
                mine |= piece.Build != null && piece.Build.Shape != "natural";
            }
            bool diggable = LevelRules.DigMode(blocks, _viewGround, tile.X, tile.Y, z) != LevelRules.NotDiggable;
            return new ClickTarget(hasBlock, isContainer || structure.ToggleTo != 0, mine, diggable);
        }

        /// <summary>Middle click: copy shape, colour and rotation of the piece under the cursor and put its material in your hand.</summary>
        private void PickPiece(Int2 tile, Inventory inv)
        {
            foreach (BuildLayer layer in new[] { BuildLayer.Structure, BuildLayer.Object, BuildLayer.Floor, BuildLayer.Roof })
            {
                BlockType type = _viewBlocks.GetBlock(tile.X, tile.Y, MyState.Level, layer);
                if (type.IsAir || type.Build == null || type.Build.Shape == "natural")
                {
                    continue;
                }
                _builder.Cursor.Copy(type, _viewBlocks.GetVariant(tile.X, tile.Y, MyState.Level, layer));
                string item = _builder.Palette.ItemOf(type);
                for (int h = 0; item != null && h < inv.HotbarSlots; h++)
                {
                    ItemStack st = inv.GetHotbar(h);
                    if (!st.IsEmpty && inv.Catalog[st.Item].id == item)
                    {
                        Send(PlayerIntent.Select(h));
                        return;
                    }
                }
                Say(item == null ? "Copiada" : $"Copiada: pon {inv.Catalog[inv.Catalog.IndexOf(item)].name} en la barra");
                return;
            }
        }

        private void QueueActions()
        {
            PlayerState me = MyState;
            Int2 tile = _input.MouseWorld.Tile;
            Float2 aim = (_input.MouseWorld - me.Position).Normalized;
            Inventory inv = MyInventory;
            ItemDefinition held = inv.EquippedDefinition;
            bool inBounds = _viewBlocks.InBounds(tile.X, tile.Y);
            ClickTarget target = TargetAt(tile);

            // The host takes one action per tick: the last Send of the tick wins, so the order here is the priority.
            if (_input.ShapeStep != 0)
            {
                _builder.CycleShape(_input.ShapeStep);
            }
            if (_input.ColorStep != 0)
            {
                _builder.Cursor.CycleColor(_input.ColorStep);
            }
            if (_input.Rotate && !_builder.Pasting)
            {
                _builder.Cursor.Rotate(1);
            }

            HandlePlans(tile);
            if (_builder.Pasting)
            {
                if (_input.Secondary || _input.Cancel)
                {
                    _builder.Cancel();
                }
                else if (_input.Attack)
                {
                    if (_builder.ConfirmPaste() == 0)
                    {
                        Say(BuildHint());
                    }
                }
                if (_input.Rotate)
                {
                    _builder.RotatePaste();
                }
                if (_builder.TryNext(Time.time, BuildContext(), out PlayerIntent pasted))
                {
                    Send(pasted);
                }
            }
            else if (_input.Alt || _builder.SelectStart.HasValue)
            {
                if (_input.Cancel || _input.Secondary)
                {
                    _builder.Cancel();
                }
            }
            else if (_builder.Active)
            {
                if (_input.Secondary || _input.Cancel)
                {
                    if (_builder.DragStart.HasValue || _input.Cancel)
                    {
                        _builder.Cancel();
                    }
                    else
                    {
                        SendSecondary(held, true, target, tile);
                    }
                }
                else
                {
                    if (_input.Attack)
                    {
                        _builder.BeginDrag(tile, _input.Ctrl ? PrototypeBuilder.DragKind.Room : _input.Sprint ? PrototypeBuilder.DragKind.Outline : PrototypeBuilder.DragKind.Line);
                    }
                    if (_input.AttackReleased && _builder.DragStart.HasValue)
                    {
                        _builder.UpdatePreview(tile, BuildContext());
                        if (_builder.EndDrag() == 0)
                        {
                            Say(BuildHint());
                        }
                    }
                }
                if (_builder.TryNext(Time.time, BuildContext(), out PlayerIntent place))
                {
                    Send(place);
                }
            }
            else
            {
                HandAction action = HandResolver.Resolve(held, false, target, false);
                bool continuous = action == HandAction.HitBlock || action == HandAction.Dismantle || action == HandAction.Dig;
                bool fire = _input.Attack || (continuous && _input.AttackHeld && Time.time >= _nextHoldAction);
                if (fire && inBounds)
                {
                    switch (action)
                    {
                        case HandAction.MeleeAttack when aim.LengthSquared > 0f:
                            Send(PlayerIntent.Melee(aim));
                            break;
                        case HandAction.RangedAttack when aim.LengthSquared > 0f:
                            Send(PlayerIntent.Ranged(aim));
                            break;
                        case HandAction.HitBlock:
                            Send(PlayerIntent.Hit(tile.X, tile.Y, me.Level));
                            break;
                        case HandAction.Dismantle:
                            Send(PlayerIntent.Dismantle(tile.X, tile.Y, 0, me.Level));
                            break;
                        case HandAction.Dig:
                            Send(PlayerIntent.Dig(tile.X, tile.Y, me.Level));
                            break;
                        case HandAction.UseItem:
                            Send(PlayerIntent.Use());
                            break;
                    }
                    _nextHoldAction = Time.time + _playerSettings.ActionCooldown + 0.02f;
                }
                if (_input.Secondary && inBounds)
                {
                    SendSecondary(held, false, target, tile);
                }
            }
            if (_input.ClimbStep != 0)
            {
                Send(PlayerIntent.Climb(_input.ClimbStep > 0));
            }
            if (_input.Pick && inBounds)
            {
                PickPiece(tile, inv);
            }
            if (_input.Undo)
            {
                Send(PlayerIntent.Undo());
            }
            if (_input.Hotbar >= 0 && _input.Hotbar < inv.HotbarSlots)
            {
                Send(PlayerIntent.Select(_input.Hotbar));
            }
            else if (_input.Scroll != 0 && inv.HotbarSlots > 0)
            {
                Send(PlayerIntent.Select(((inv.SelectedHotbar + _input.Scroll) % inv.HotbarSlots + inv.HotbarSlots) % inv.HotbarSlots));
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

        /// <summary>Right click: open the door or chest, or (material in hand) take the piece down.</summary>
        private void SendSecondary(ItemDefinition held, bool builds, in ClickTarget target, Int2 tile)
        {
            switch (HandResolver.Resolve(held, builds, target, true))
            {
                case HandAction.Interact:
                    bool isContainer = Remote ? ContainsCell(_client.ContainerCells, tile) : _session.Containers != null && _session.Containers.Has(tile);
                    Send(isContainer ? PlayerIntent.Open(tile.X, tile.Y) : PlayerIntent.Door(tile.X, tile.Y, MyState.Level));
                    break;
                case HandAction.Dismantle:
                    Send(PlayerIntent.Dismantle(tile.X, tile.Y, 0, MyState.Level));
                    break;
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
            if (_input.CycleCost)
            {
                // The host's setting, read by every client through the match options; in this prototype both share it.
                _settings.BuildCost = (BuildCostMode)(((int)_settings.BuildCost + 1) % 3);
                Say("Coste de construir: " + (_settings.BuildCost == BuildCostMode.Normal ? "normal" : _settings.BuildCost == BuildCostMode.Cheap ? "barato (la mitad)" : "gratis (creativo)"));
            }
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
                case SessionEventType.PlayerFell when e.Player == _me:
                    Say($"¡Caes {e.Value} nivel{(e.Value > 1 ? "es" : "")}!");
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
            Hotbar("hammer", 1, 5);
            Hotbar("shovel", 1, 6);
            Hotbar("pickaxe", 1, 7);
            inv.Add(items.IndexOf("water_bottle"), 2); // first in the bag, so V swaps it into your hand
            inv.Add(items.IndexOf("pistol_ammo"), 12);
            Hotbar("wood", 60, 8);                       // the building material goes in the last quick slot
            inv.Add(items.IndexOf("nails"), 30);
            foreach (string material in new[] { "brick", "log", "pallet", "plasterboard", "glass_pane", "stone", "concrete_mix", "straw" })
            {
                inv.Add(items.IndexOf(material), 40);
            }
            inv.Add(items.IndexOf("scrap_cloth"), 6);
            inv.SelectHotbar(0);
        }
    }
}
