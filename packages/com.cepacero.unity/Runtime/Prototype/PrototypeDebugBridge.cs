using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using CepaCero.AI;
using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Util;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Debug bridge (D-023): text commands over http://localhost:PORT/ turned into the same
    /// keyboard and mouse input a player would give (<see cref="PrototypeInput"/>), so every
    /// action goes through the real game code and the host's validation. Editor and development
    /// builds only; loopback connections only. When no command runs it leaves the input alone.
    /// Commands: tools/gamebridge/README.md.
    /// </summary>
    public sealed class PrototypeDebugBridge : IDisposable
    {
        private const int RequestTimeoutMs = 300000;
        private const int MaxMessages = 12;
        private const float DragTicks = 6f;

        private readonly IPrototypeDebugHost _host;
        private readonly string _screenshotDir;
        private readonly ConcurrentQueue<Job> _incoming = new ConcurrentQueue<Job>();
        private readonly List<string> _messages = new List<string>();
        private readonly HttpListener _listener;
        private readonly Thread _thread;
        private volatile bool _running;

        // The command in progress.
        private Job _job;
        private int _index;
        private int _phase;
        private float _started;
        private List<Int2> _path;
        private float _bestDistance;
        private float _lastProgress;
        private bool _replanned;
        private string _pendingShot;

        // Held input while a command runs.
        private bool _controlling;
        private Float2 _move;
        private Float2? _aim;
        private bool _held;
        private bool _run;
        private bool _ctrl;
        private bool _alt;

        private sealed class Job
        {
            public readonly List<DebugCommand> Commands;
            public readonly List<string> Results = new List<string>();
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
            public bool Ok = true;
            public string Response;

            public Job(List<DebugCommand> commands)
            {
                Commands = commands;
            }
        }

        public PrototypeDebugBridge(IPrototypeDebugHost host, int port, string screenshotDir)
        {
            _host = host;
            _screenshotDir = screenshotDir;
            Port = port;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            _running = true;
            _thread = new Thread(Serve) { IsBackground = true, Name = "Cepa Cero debug bridge" };
            _thread.Start();
        }

        public int Port { get; }

        /// <summary>Starts the bridge when this is an editor or development build; null otherwise or if the port is taken.</summary>
        public static PrototypeDebugBridge TryStart(IPrototypeDebugHost host, int port, string screenshotDir)
        {
            if (port <= 0 || !Debug.isDebugBuild)
            {
                return null;
            }
            try
            {
                var bridge = new PrototypeDebugBridge(host, port, screenshotDir);
                // Driven from outside: keep simulating when the window loses focus to the terminal.
                Application.runInBackground = true;
                Debug.Log($"Cepa Cero debug bridge: http://localhost:{port}/ (D-023, tools/gamebridge/README.md)");
                return bridge;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Cepa Cero debug bridge: could not listen on port {port}: {e.Message}");
                return null;
            }
        }

        /// <summary>Messages shown to the player, so a script can read what the game said.</summary>
        public void Log(string message)
        {
            lock (_messages)
            {
                _messages.Add(message);
                if (_messages.Count > MaxMessages)
                {
                    _messages.RemoveAt(0);
                }
            }
        }

        /// <summary>Every frame, after the keyboard is read: held input while a command runs.</summary>
        public void ApplyContinuous(PrototypeInput input)
        {
            if (!_controlling)
            {
                return;
            }
            input.Move = _move;
            input.Sprint = _run;
            input.Ctrl = _ctrl;
            input.Alt = _alt;
            input.AttackHeld = _held;
            if (_aim.HasValue)
            {
                input.MouseWorld = _aim.Value;
            }
        }

        /// <summary>Every simulation tick, before the game reads the input: advances the command in progress.</summary>
        public void Step(PrototypeInput input)
        {
            if (_job == null && _incoming.TryDequeue(out Job next))
            {
                _job = next;
                _index = 0;
                Begin();
            }
            if (_job == null)
            {
                return;
            }
            _controlling = true;
            ApplyContinuous(input);
            try
            {
                Advance(input, _job.Commands[_index]);
            }
            catch (Exception e)
            {
                Fail($"{_job.Commands[_index].Line}: {e.Message}");
            }
        }

        public void Dispose()
        {
            _running = false;
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception)
            {
                // closing anyway
            }
            while (_incoming.TryDequeue(out Job j))
            {
                j.Response = "{\"ok\":false,\"error\":\"the game stopped\"}";
                j.Done.Set();
            }
            if (_job != null)
            {
                _job.Response = "{\"ok\":false,\"error\":\"the game stopped\"}";
                _job.Done.Set();
            }
        }

        // ------------------------------------------------------------ commands

        private void Begin()
        {
            _phase = 0;
            _started = Time.fixedTime;
            _path = null;
            _replanned = false;
        }

        private float Elapsed => Time.fixedTime - _started;

        private void Advance(PrototypeInput input, in DebugCommand c)
        {
            if (_phase >= SettlePhase)
            {
                Settle(null); // the action is done: only waiting for its effect to show
                return;
            }
            switch (c.Verb)
            {
                case "state":
                    Next("ok");
                    break;
                case "select":
                    input.Hotbar = c.Int(0) - 1;
                    Settle($"slot {c.Int(0)}");
                    break;
                case "aim":
                    AimAt(c);
                    Next(AimText());
                    break;
                case "click":
                    if (_phase == 0)
                    {
                        AimAt(c);
                        input.Attack = true;
                        _held = true;
                        _phase = 1;
                    }
                    else
                    {
                        _held = false;
                        input.AttackReleased = true;
                        Settle("clicked " + AimText());
                    }
                    break;
                case "rclick":
                    AimAt(c);
                    input.Secondary = true;
                    Settle("right-clicked " + AimText());
                    break;
                case "mclick":
                    AimAt(c);
                    input.Pick = true;
                    Settle("middle-clicked " + AimText());
                    break;
                case "hold":
                    if (_phase == 0)
                    {
                        _aim = Centre(c.Int(0), c.Int(1));
                        input.Attack = true;
                        _held = true;
                        _phase = 1;
                    }
                    else if (Elapsed >= c.Number(2))
                    {
                        _held = false;
                        input.AttackReleased = true;
                        Settle($"held {c.Number(2):0.##} s on {AimText()}");
                    }
                    break;
                case "drag":
                    Drag(input, c);
                    break;
                case "key":
                    PressKey(input, c.Args[0].ToLowerInvariant());
                    Settle("key " + c.Args[0]);
                    break;
                case "move":
                    if (_phase == 0)
                    {
                        _move = new Float2(c.Number(0), c.Number(1)).Normalized;
                        _run = c.Count == 4;
                        _phase = 1;
                    }
                    else if (Elapsed >= c.Number(2))
                    {
                        _move = default;
                        _run = false;
                        Next($"moved to {Where()}");
                    }
                    break;
                case "walk":
                    Walk(c);
                    break;
                case "wait":
                    if (Elapsed >= c.Number(0))
                    {
                        Next($"waited {c.Number(0):0.##} s");
                    }
                    break;
                case "screenshot":
                    Screenshot(c.Args[0]);
                    break;
                case "give":
                    Give(c);
                    break;
                case "spawn":
                    Spawn(c);
                    break;
                case "time":
                    SetTime(c.Args[0] == "night");
                    Settle("time " + c.Args[0]);
                    break;
                default:
                    Fail("unknown verb " + c.Verb);
                    break;
            }
        }

        private const int SettlePhase = 100;

        /// <summary>
        /// Instant commands still wait a short moment so the reply shows their effect. The first
        /// call records the result; later ticks only wait (<see cref="Advance"/> skips the action).
        /// </summary>
        private void Settle(string result)
        {
            if (_phase < SettlePhase)
            {
                _phase = SettlePhase;
                _started = Time.fixedTime;
                _pendingResult = result;
                return;
            }
            if (Elapsed >= 0.15f)
            {
                Next(_pendingResult);
            }
        }

        private string _pendingResult;

        private void Drag(PrototypeInput input, in DebugCommand c)
        {
            Float2 from = Centre(c.Int(0), c.Int(1));
            Float2 to = Centre(c.Int(2), c.Int(3));
            string kind = c.Count == 5 ? c.Args[4] : "line";
            if (_phase == 0)
            {
                _ctrl = kind == "room";
                _run = kind == "outline";
                _alt = kind == "select";
                _aim = from;
                input.Attack = true;
                _held = true;
                _phase = 1;
                return;
            }
            if (_phase <= DragTicks)
            {
                _aim = from + (to - from) * (_phase / DragTicks);
                _phase++;
                return;
            }
            if (_phase == DragTicks + 1)
            {
                _aim = to;
                _held = false;
                input.AttackReleased = true;
                _phase++;
                return;
            }
            _ctrl = _run = _alt = false;
            Settle($"dragged {kind} {c.Int(0)},{c.Int(1)} -> {c.Int(2)},{c.Int(3)}");
        }

        private static void PressKey(PrototypeInput input, string key)
        {
            switch (key)
            {
                case "up": input.ClimbStep += 1; break;
                case "down": input.ClimbStep -= 1; break;
                case "shape+": input.ShapeStep += 1; break;
                case "shape-": input.ShapeStep -= 1; break;
                case "color+": input.ColorStep += 1; break;
                case "color-": input.ColorStep -= 1; break;
                case "rotate": input.Rotate = true; break;
                case "undo": input.Undo = true; break;
                case "copy": input.CopyPlan = true; break;
                case "paste": input.PastePlan = true; break;
                case "saveplan": input.SavePlan = true; break;
                case "loadplan": input.LoadPlan = true; break;
                case "esc": input.Cancel = true; break;
                case "take": input.Take = true; break;
                case "pickup": input.PickUp = true; break;
                case "equip": input.EquipFromBag = true; break;
                case "bandage": input.CraftBandage = true; break;
                case "save": input.Save = true; break;
                case "load": input.Load = true; break;
                case "cost": input.CycleCost = true; break;
                case "help": input.ShowHelp = !input.ShowHelp; break;
            }
        }

        private void Walk(in DebugCommand c)
        {
            var target = new Int2(c.Int(0), c.Int(1));
            PlayerState me = _host.Me;
            float radius = _host.PlayerSettings.Radius;
            if (_phase == 0 || (_path == null && !_replanned))
            {
                _path = DebugPathfinder.Find(_host.ViewBlocks, _host.ViewGround, me.Level, radius, me.Position.Tile, target);
                if (_path == null)
                {
                    Fail($"no way to {target.X},{target.Y} on level {me.Level} from {Where()}");
                    return;
                }
                _run = c.Count == 3;
                _phase = 1;
                _bestDistance = float.MaxValue;
                _lastProgress = Time.fixedTime;
            }
            if (_path.Count == 0)
            {
                _move = default;
                _run = false;
                Next($"arrived at {Where()}");
                return;
            }
            Float2 waypoint = Centre(_path[0].X, _path[0].Y);
            Float2 toWaypoint = waypoint - me.Position;
            float distance = toWaypoint.Length;
            if (distance < 0.2f)
            {
                _path.RemoveAt(0);
                _bestDistance = float.MaxValue;
                _lastProgress = Time.fixedTime;
                return;
            }
            if (distance < _bestDistance - 0.01f)
            {
                _bestDistance = distance;
                _lastProgress = Time.fixedTime;
            }
            else if (Time.fixedTime - _lastProgress > 1.5f)
            {
                if (_replanned)
                {
                    _move = default;
                    _run = false;
                    Fail($"stuck at {Where()} on the way to {target.X},{target.Y}");
                    return;
                }
                _replanned = true;
                _path = DebugPathfinder.Find(_host.ViewBlocks, _host.ViewGround, me.Level, radius, me.Position.Tile, target);
                _lastProgress = Time.fixedTime;
                if (_path == null)
                {
                    Fail($"lost the way to {target.X},{target.Y} at {Where()}");
                }
                return;
            }
            _move = toWaypoint.Normalized;
        }

        private void Screenshot(string name)
        {
            if (_phase == 0)
            {
                Directory.CreateDirectory(_screenshotDir);
                _pendingShot = Path.Combine(_screenshotDir, name + ".png");
                if (File.Exists(_pendingShot))
                {
                    File.Delete(_pendingShot);
                }
                ScreenCapture.CaptureScreenshot(_pendingShot);
                _phase = 1;
                return;
            }
            if (File.Exists(_pendingShot))
            {
                Next(_pendingShot.Replace('\\', '/'));
            }
            else if (Elapsed > 3f)
            {
                Fail("no image written (batch mode without graphics?)");
            }
        }

        private void Give(in DebugCommand c)
        {
            Inventory inv = _host.HostSession.Inventories[_host.MeIndex];
            if (!inv.Catalog.TryGetIndex(c.Args[0], out ushort item))
            {
                Fail("unknown item " + c.Args[0]);
                return;
            }
            int count = c.Count == 2 ? c.Int(1) : 1;
            int added = inv.Add(item, count);
            Settle($"gave {added} {c.Args[0]}");
        }

        private void Spawn(in DebugCommand c)
        {
            ZombieCatalog catalog = _host.HostSession.Catalog;
            if (!catalog.TryGetIndex(c.Args[0], out byte type))
            {
                Fail("unknown zombie " + c.Args[0]);
                return;
            }
            int level = c.Count == 4 ? c.Int(3) : 0;
            int slot = _host.HostSession.Zombies.Spawn(type, Centre(c.Int(1), c.Int(2)), 1, true, level);
            if (slot < 0)
            {
                Fail("no room for another zombie");
                return;
            }
            Settle($"spawned {c.Args[0]} at {c.Int(1)},{c.Int(2)} level {level}");
        }

        private void SetTime(bool night)
        {
            var s = _host.HostSession.Settings;
            int rate = _host.HostSession.Clock.TickRate;
            long cycle = (long)(s.DaySeconds + s.DuskSeconds + s.NightSeconds + s.DawnSeconds) * rate;
            long start = (_host.HostSession.Clock.Tick / cycle) * cycle;
            _host.HostSession.Clock.SetTick(night ? start + (long)(s.DaySeconds + s.DuskSeconds) * rate : start);
        }

        private void AimAt(in DebugCommand c)
        {
            if (c.Count >= 2)
            {
                _aim = Centre(c.Int(0), c.Int(1));
            }
        }

        private static Float2 Centre(int x, int y) => new Float2(x + 0.5f, y + 0.5f);

        private string AimText() => _aim.HasValue ? $"{_aim.Value.Tile.X},{_aim.Value.Tile.Y}" : "(cursor)";

        private string Where()
        {
            PlayerState me = _host.Me;
            return $"{me.Position.Tile.X},{me.Position.Tile.Y} level {me.Level}";
        }

        private void Next(string result)
        {
            _job.Results.Add($"{_job.Commands[_index].Line} -> {result}");
            _index++;
            if (_index >= _job.Commands.Count)
            {
                Finish();
                return;
            }
            Begin();
        }

        private void Fail(string error)
        {
            _job.Ok = false;
            _job.Results.Add("ERROR " + error);
            Finish();
        }

        private void Finish()
        {
            _move = default;
            _aim = null;
            _held = _run = _ctrl = _alt = false;
            _controlling = false;
            var json = new StringBuilder(2048);
            json.Append("{\"ok\":").Append(_job.Ok ? "true" : "false").Append(",\"results\":[");
            for (int i = 0; i < _job.Results.Count; i++)
            {
                json.Append(i > 0 ? "," : "").Append(Quote(_job.Results[i]));
            }
            json.Append("],\"state\":");
            AppendState(json);
            json.Append('}');
            _job.Response = json.ToString();
            _job.Done.Set();
            _job = null;
        }

        // --------------------------------------------------------------- state

        private void AppendState(StringBuilder j)
        {
            PlayerState me = _host.Me;
            Inventory inv = _host.MyInventory;
            var clock = _host.ViewClock;
            j.Append("{\"day\":").Append(clock.Day).Append(",\"phase\":").Append(Quote(clock.Phase.ToString()));
            j.Append(",\"remote_client\":").Append(_host.IsRemoteClient ? "true" : "false");
            j.Append(",\"player\":{\"x\":").Append(F(me.Position.X)).Append(",\"y\":").Append(F(me.Position.Y));
            j.Append(",\"tile\":[").Append(me.Position.Tile.X).Append(',').Append(me.Position.Tile.Y).Append(']');
            j.Append(",\"level\":").Append(me.Level).Append(",\"health\":").Append(me.Health);
            j.Append(",\"stamina\":").Append(F(me.Stamina)).Append(",\"food\":").Append(F(me.Food)).Append(",\"water\":").Append(F(me.Water));
            j.Append(",\"downed\":").Append(me.IsDowned ? "true" : "false").Append(",\"dead\":").Append(me.IsDead ? "true" : "false").Append('}');

            j.Append(",\"hand\":{\"slot\":").Append(inv.SelectedHotbar + 1).Append(",\"item\":")
                .Append(Quote(inv.EquippedDefinition != null ? inv.EquippedDefinition.id : "")).Append('}');
            j.Append(",\"hotbar\":[");
            for (int h = 0; h < inv.HotbarSlots; h++)
            {
                ItemStack st = inv.GetHotbar(h);
                j.Append(h > 0 ? "," : "").Append(Quote(st.IsEmpty ? "" : inv.Catalog[st.Item].id + (st.Count > 1 ? " x" + st.Count : "")));
            }
            j.Append("],\"bag\":[");
            for (int i = 0; i < inv.Bag.Count; i++)
            {
                ItemStack st = inv.Bag[i];
                j.Append(i > 0 ? "," : "").Append(Quote(inv.Catalog[st.Item].id + " x" + st.Count));
            }
            j.Append(']');

            // Building with the hand: what the ghost says, so a script can see why a placement is refused.
            PrototypeBuilder builder = _host.Builder;
            j.Append(",\"build\":{\"active\":").Append(builder != null && builder.Active ? "true" : "false")
                .Append(",\"piece\":").Append(Quote(builder?.Piece != null ? builder.Piece.Value.Name : ""))
                .Append(",\"cost\":").Append(Quote(_host.BuildCost.ToString()))
                .Append(",\"queued\":").Append(builder != null ? builder.Queue.Count : 0)
                .Append(",\"hint\":").Append(Quote(_host.BuildHint ?? ""))
                .Append(",\"ghost\":[");
            if (builder != null)
            {
                for (int i = 0; i < builder.Preview.Count && i < 20; i++)
                {
                    PrototypeBuilder.Placement p = builder.Preview[i];
                    j.Append(i > 0 ? "," : "").Append("{\"x\":").Append(p.Cell.X).Append(",\"y\":").Append(p.Cell.Y)
                        .Append(",\"verdict\":").Append(Quote(p.Verdict.ToString())).Append('}');
                }
            }
            j.Append("]}");

            // What is around, on the player's level: only cells with something built or dug.
            BlockWorld blocks = _host.ViewBlocks;
            j.Append(",\"around\":[");
            bool first = true;
            for (int dy = 3; dy >= -3; dy--)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    int x = me.Position.Tile.X + dx;
                    int y = me.Position.Tile.Y + dy;
                    if (!blocks.InBounds(x, y))
                    {
                        continue;
                    }
                    string floor = blocks.GetBlock(x, y, me.Level, BuildLayer.Floor).Key;
                    string structure = blocks.GetBlock(x, y, me.Level, BuildLayer.Structure).Key;
                    string obj = blocks.GetBlock(x, y, me.Level, BuildLayer.Object).Key;
                    bool solid = blocks.IsSolidAt(x, y, me.Level);
                    if (floor == "air" && structure == "air" && obj == "air" && !solid)
                    {
                        continue;
                    }
                    j.Append(first ? "" : ",").Append("{\"x\":").Append(x).Append(",\"y\":").Append(y);
                    if (floor != "air") j.Append(",\"floor\":").Append(Quote(floor));
                    if (structure != "air") j.Append(",\"structure\":").Append(Quote(structure));
                    if (obj != "air") j.Append(",\"object\":").Append(Quote(obj));
                    j.Append(",\"solid\":").Append(solid ? "true" : "false").Append('}');
                    first = false;
                }
            }
            j.Append(']');

            // Zombies near the player (from the host: the debug view, not what a client knows).
            ZombieSimulation zombies = _host.HostSession.Zombies;
            j.Append(",\"zombies\":[");
            first = true;
            int listed = 0;
            for (int i = 0; i < zombies.Capacity && listed < 12; i++)
            {
                if (!zombies.IsAlive(i))
                {
                    continue;
                }
                Float2 p = zombies.GetPosition(i);
                if ((p - me.Position).LengthSquared > 15f * 15f)
                {
                    continue;
                }
                j.Append(first ? "" : ",").Append("{\"type\":").Append(Quote(_host.HostSession.Catalog[zombies.GetTypeIndex(i)].id))
                    .Append(",\"x\":").Append(F(p.X)).Append(",\"y\":").Append(F(p.Y)).Append(",\"level\":").Append(zombies.GetLevel(i))
                    .Append(",\"state\":").Append(Quote(zombies.GetState(i).ToString())).Append('}');
                first = false;
                listed++;
            }
            j.Append("],\"messages\":[");
            lock (_messages)
            {
                for (int i = 0; i < _messages.Count; i++)
                {
                    j.Append(i > 0 ? "," : "").Append(Quote(_messages[i]));
                }
            }
            j.Append("]}");
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20)
                        {
                            sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        // ---------------------------------------------------------------- http

        private void Serve()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext();
                }
                catch (Exception)
                {
                    return; // stopped
                }
                try
                {
                    Handle(ctx);
                }
                catch (Exception e)
                {
                    Reply(ctx, 500, "{\"ok\":false,\"error\":" + Quote(e.Message) + "}");
                }
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            if (!IPAddress.IsLoopback(ctx.Request.RemoteEndPoint.Address))
            {
                Reply(ctx, 403, "{\"ok\":false,\"error\":\"local connections only\"}");
                return;
            }
            string text = ctx.Request.QueryString["c"];
            if (ctx.Request.HasEntityBody)
            {
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                {
                    text = reader.ReadToEnd();
                }
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "state";
            }
            var commands = new List<DebugCommand>();
            string error = DebugCommand.ParseScript(text, commands);
            if (error != null)
            {
                Reply(ctx, 400, "{\"ok\":false,\"error\":" + Quote(error) + "}");
                return;
            }
            var job = new Job(commands);
            _incoming.Enqueue(job);
            if (!job.Done.Wait(RequestTimeoutMs))
            {
                Reply(ctx, 504, "{\"ok\":false,\"error\":\"timed out; the game may be paused or not in Play mode\"}");
                return;
            }
            Reply(ctx, 200, job.Response);
        }

        private static void Reply(HttpListenerContext ctx, int status, string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = body.Length;
            ctx.Response.OutputStream.Write(body, 0, body.Length);
            ctx.Response.OutputStream.Close();
        }
    }
}
