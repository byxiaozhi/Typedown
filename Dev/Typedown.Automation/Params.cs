using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Typedown.Automation
{
    /// <summary>
    /// Reads a method's params object. Unknown fields are ignored (section 1.2); a missing required field, a wrong
    /// type or a value outside its range is <c>invalid_params</c> naming the field and the reason.
    /// </summary>
    public sealed class Params
    {
        /// <summary>The largest integer a JavaScript client can hold exactly (revisions, counts).</summary>
        public const long MaxSafeInteger = 9007199254740991;

        private readonly JObject obj;

        public Params(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null) obj = new JObject();
            else if (token is JObject o) obj = o;
            else throw Invalid("params", "notAnObject");
        }

        public static AutomationException Invalid(string field, string reason, string? message = null) =>
            new(AutomationErrorKind.invalid_params, message ?? $"Invalid parameter '{field}': {reason}.",
                new Dictionary<string, object?> { ["field"] = field, ["reason"] = reason });

        public bool Has(string name) => obj.TryGetValue(name, out var v) && v.Type != JTokenType.Null;

        private JToken? Get(string name, bool required)
        {
            if (obj.TryGetValue(name, out var v) && v.Type != JTokenType.Null) return v;
            if (required) throw Invalid(name, "required");
            return null;
        }

        public string RequiredString(string name, bool allowEmpty = true) => String(name, true, allowEmpty)!;

        public string? OptionalString(string name, bool allowEmpty = true) => String(name, false, allowEmpty);

        private string? String(string name, bool required, bool allowEmpty)
        {
            var v = Get(name, required);
            if (v == null) return null;
            if (v.Type != JTokenType.String) throw Invalid(name, "notAString");
            var s = (string)v!;
            if (!allowEmpty && s.Length == 0) throw Invalid(name, "empty");
            return s;
        }

        public long RequiredInteger(string name, long min = 0, long max = MaxSafeInteger) => Integer(name, true, min, max)!.Value;

        public long? OptionalInteger(string name, long min = 0, long max = MaxSafeInteger) => Integer(name, false, min, max);

        private long? Integer(string name, bool required, long min, long max)
        {
            var v = Get(name, required);
            if (v == null) return null;
            long value;
            if (v.Type == JTokenType.Integer)
            {
                try { value = v.Value<long>(); } catch (OverflowException) { throw Invalid(name, "outOfRange"); }
            }
            else if (v.Type == JTokenType.Float && Math.Floor(v.Value<double>()) == v.Value<double>() && Math.Abs(v.Value<double>()) <= MaxSafeInteger)
                value = (long)v.Value<double>();
            else throw Invalid(name, "notAnInteger");
            if (value < min || value > max) throw Invalid(name, "outOfRange");
            return value;
        }

        public bool OptionalBoolean(string name, bool fallback)
        {
            var v = Get(name, false);
            if (v == null) return fallback;
            if (v.Type != JTokenType.Boolean) throw Invalid(name, "notABoolean");
            return (bool)v!;
        }

        /// <summary>A string that must be one of <paramref name="allowed"/>; an unknown value is invalid (section 1.2).</summary>
        public string OptionalEnum(string name, string fallback, params string[] allowed)
        {
            var s = OptionalString(name);
            if (s == null) return fallback;
            if (Array.IndexOf(allowed, s) < 0) throw Invalid(name, "unknownValue");
            return s;
        }

        public JObject? OptionalObject(string name)
        {
            var v = Get(name, false);
            if (v == null) return null;
            return v as JObject ?? throw Invalid(name, "notAnObject");
        }

        public IReadOnlyList<string>? OptionalStringArray(string name)
        {
            var v = Get(name, false);
            if (v == null) return null;
            if (!(v is JArray array)) throw Invalid(name, "notAnArray");
            var list = new List<string>();
            foreach (var item in array)
            {
                if (item.Type != JTokenType.String) throw Invalid(name, "notAStringArray");
                list.Add((string)item!);
            }
            return list;
        }
    }
}
