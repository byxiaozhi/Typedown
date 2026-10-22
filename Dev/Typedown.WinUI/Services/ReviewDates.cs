using System;
using System.Collections.Generic;
using System.Linq;

namespace Typedown.WinUI.Services
{
    // New since the fork: live review (docs/live-review-design.md, section 6). The date of a change is the day the comparison first saw
    // it. It is kept with the change while the change stays the same, so it does not move to "today" every morning and it survives
    // closing and reopening Caret: for each change a small fingerprint and its first day are saved (ReviewStore), and after a restart
    // the changes found are matched to them.
    //
    // Plain .NET (no WinUI), so the tests in Caret.ConverterTests compile it as it is.
    internal static class ReviewDates
    {
        // How much of the text before and after a change is part of its fingerprint.
        public const int Context = 40;

        // A change as it was saved: the old and the new text, the unchanged text just before and after it, its occurrence number (the
        // 1st, 2nd... change with that same old and new text, in the order of the document) and the day it was first seen (yyyy-MM-dd).
        public sealed record Seen(string Old, string New, string Before, string After, int Occurrence, string Day);

        // A change just found, without its day.
        public sealed record Probe(string Old, string New, string Before, string After, int Occurrence);

        // The day of each found change: three rounds, each only for what the one before left unmatched, and a change that matches in
        // none gets `today`. Each saved fingerprint is used once.
        //   1. the whole fingerprint;
        //   2. old text, new text and the text around the change on either side (the other side was edited a little);
        //   3. old text, new text and occurrence number, only when that is unambiguous: as many saved as found with that old and new
        //      text, so each pairs with exactly one (two identical changes were saved and one has vanished: the survivor does not take
        //      the day of the one that went).
        public static List<string> Assign(IReadOnlyList<Seen> saved, IReadOnlyList<Probe> found, string today)
        {
            saved ??= Array.Empty<Seen>();
            var days = new string[found.Count];
            var used = new bool[saved.Count];

            void Round(Func<Probe, Seen, bool> same, Func<Probe, bool> allowed = null)
            {
                for (var i = 0; i < found.Count; i++)
                {
                    if (days[i] != null || (allowed != null && !allowed(found[i]))) continue;
                    for (var j = 0; j < saved.Count; j++)
                    {
                        if (used[j] || !same(found[i], saved[j])) continue;
                        days[i] = saved[j].Day;
                        used[j] = true;
                        break;
                    }
                }
            }

            Round((f, s) => f.Old == s.Old && f.New == s.New && f.Before == s.Before && f.After == s.After && f.Occurrence == s.Occurrence);
            Round((f, s) => f.Old == s.Old && f.New == s.New && (f.Before == s.Before || f.After == s.After));
            Round((f, s) => f.Old == s.Old && f.New == s.New && f.Occurrence == s.Occurrence,
                f => saved.Count(s => s.Old == f.Old && s.New == f.New) == found.Count(p => p.Old == f.Old && p.New == f.New));

            for (var i = 0; i < days.Length; i++) days[i] ??= today;
            return days.ToList();
        }

        // What is saved after a comparison: the changes found with their days (the fingerprints of changes that no longer exist are
        // dropped).
        public static List<Seen> Remember(IReadOnlyList<Probe> found, IReadOnlyList<string> days) =>
            found.Select((p, i) => new Seen(p.Old, p.New, p.Before, p.After, p.Occurrence, days[i])).ToList();

        // The probes of changes at these places of `text`: `places[i]` is where change i starts and `lengths[i]` how long its new text is
        // (0 for a deletion).
        public static List<Probe> Probes(string text, IReadOnlyList<string> olds, IReadOnlyList<string> news, IReadOnlyList<int> places, IReadOnlyList<int> lengths)
        {
            var found = new List<Probe>(olds.Count);
            var count = new Dictionary<(string, string), int>();
            for (var i = 0; i < olds.Count; i++)
            {
                var at = Math.Clamp(places[i], 0, text.Length);
                var end = Math.Clamp(at + lengths[i], at, text.Length);
                var before = text.Substring(Math.Max(0, at - Context), Math.Min(Context, at));
                var after = text.Substring(end, Math.Min(Context, text.Length - end));
                var key = (olds[i], news[i]);
                count[key] = count.TryGetValue(key, out var n) ? n + 1 : 1;
                found.Add(new Probe(olds[i], news[i], before, after, count[key]));
            }
            return found;
        }
    }
}
