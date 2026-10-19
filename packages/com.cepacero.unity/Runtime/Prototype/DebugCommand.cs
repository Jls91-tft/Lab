using System;
using System.Collections.Generic;
using System.Globalization;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// One line of the debug bridge (D-023): a verb and its arguments, e.g. "walk 120 85" or
    /// "drag 10 5 15 5 room". Blank lines and "#" comments are skipped by <see cref="ParseScript"/>.
    /// </summary>
    public readonly struct DebugCommand
    {
        public static readonly string[] Verbs =
        {
            "state", "select", "aim", "click", "hold", "rclick", "mclick", "drag", "key", "move", "walk", "wait",
            "screenshot", "give", "spawn", "time", "clear", "tp", "hide", "probe",
        };

        public static readonly string[] Keys =
        {
            "up", "down", "shape+", "shape-", "color+", "color-", "rotate", "undo", "copy", "paste", "saveplan", "loadplan",
            "esc", "take", "pickup", "equip", "bandage", "save", "load", "cost", "help", "light", "bag",
        };

        public DebugCommand(string verb, string[] args, string line)
        {
            Verb = verb;
            Args = args;
            Line = line;
        }

        public string Verb { get; }

        public string[] Args { get; }

        /// <summary>The line as written, for the reply.</summary>
        public string Line { get; }

        public int Count => Args.Length;

        /// <summary>Splits a script into commands; returns the first error, or null when every line is understood.</summary>
        public static string ParseScript(string text, List<DebugCommand> into)
        {
            string[] lines = (text ?? "").Replace("\r", "").Split(new[] { '\n', ';' });
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                int hash = line.IndexOf('#');
                if (hash >= 0)
                {
                    line = line.Substring(0, hash).Trim();
                }
                if (line.Length == 0)
                {
                    continue;
                }
                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                string verb = parts[0].ToLowerInvariant();
                var args = new string[parts.Length - 1];
                Array.Copy(parts, 1, args, 0, args.Length);
                var command = new DebugCommand(verb, args, line);
                string error = command.Validate();
                if (error != null)
                {
                    return $"'{line}': {error}";
                }
                into.Add(command);
            }
            return null;
        }

        /// <summary>Null when the verb and its arguments make sense.</summary>
        public string Validate()
        {
            switch (Verb)
            {
                case "state":
                    return Count == 0 ? null : "state takes no arguments";
                case "select":
                    return Count == 1 && Int(0) >= 1 && Int(0) <= 9 ? null : "select N (1-9)";
                case "aim":
                case "click":
                case "rclick":
                case "mclick":
                    return Count == 0 || (Count == 2 && IsInt(0) && IsInt(1)) ? null : $"{Verb} [X Y]";
                case "hold":
                    return Count == 3 && IsInt(0) && IsInt(1) && IsNumber(2) ? null : "hold X Y SECONDS";
                case "drag":
                    return (Count == 4 || Count == 5) && IsInt(0) && IsInt(1) && IsInt(2) && IsInt(3)
                        && (Count == 4 || Array.IndexOf(new[] { "line", "outline", "room", "select" }, Args[4]) >= 0)
                        ? null : "drag X1 Y1 X2 Y2 [line|outline|room|select]";
                case "key":
                    return Count == 1 && Array.IndexOf(Keys, Args[0].ToLowerInvariant()) >= 0 ? null : "key " + string.Join("|", Keys);
                case "move":
                    return (Count == 3 || (Count == 4 && Args[3] == "run")) && IsNumber(0) && IsNumber(1) && IsNumber(2)
                        ? null : "move DX DY SECONDS [run]";
                case "walk":
                    return (Count == 2 || (Count == 3 && Args[2] == "run")) && IsInt(0) && IsInt(1) ? null : "walk X Y [run]";
                case "wait":
                    return Count == 1 && IsNumber(0) ? null : "wait SECONDS";
                case "screenshot":
                    return Count == 1 && IsSafeName(Args[0]) ? null : "screenshot NAME (letters, digits, - and _)";
                case "give":
                    return (Count == 1 || (Count == 2 && IsInt(1))) ? null : "give ITEM_ID [COUNT]";
                case "spawn":
                    return (Count == 3 || (Count == 4 && IsInt(3))) && IsInt(1) && IsInt(2) ? null : "spawn ZOMBIE_ID X Y [LEVEL]";
                case "time":
                    return Count == 1 && (Args[0] == "day" || Args[0] == "night" || Args[0] == "dawn") ? null : "time day|night|dawn";
                case "probe":
                    return Count == 2 && IsInt(0) && IsInt(1) ? null : "probe X Y";
                case "hide":
                    return Count == 1 ? null : "hide NAMES|none (wall, upper, roof, line, detail, paper, deco, floor)";
                case "tp":
                    return Count == 2 && IsInt(0) && IsInt(1) ? null : "tp X Y";
                case "clear":
                    return Count == 0 ? null : "clear takes no arguments";
                default:
                    return "unknown verb; known: " + string.Join(", ", Verbs);
            }
        }

        public int Int(int i) => int.TryParse(Args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : int.MinValue;

        public float Number(int i) => float.TryParse(Args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : float.NaN;

        private bool IsInt(int i) => i < Count && Int(i) != int.MinValue;

        private bool IsNumber(int i) => i < Count && !float.IsNaN(Number(i));

        private static bool IsSafeName(string name)
        {
            if (name.Length == 0 || name.Length > 64)
            {
                return false;
            }
            foreach (char c in name)
            {
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
