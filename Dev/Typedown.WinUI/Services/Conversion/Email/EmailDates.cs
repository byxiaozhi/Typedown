using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services.Conversion
{
    // Reading the dates that mail clients write in quoted headers, in any of the languages.
    internal static class EmailDates
    {
        private static readonly Regex Year = new(@"\b((?:19|20)\d{2})\b");
        private static readonly Regex Time = new(@"\b(\d{1,2})[:h.](\d{2})(?::\d{2})?\s*(a\.?\s?m\b\.?|p\.?\s?m\b\.?)?(?![\d])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Numeric = new(@"\b(\d{1,2})[./-](\d{1,2})[./-]((?:19|20)\d{2})\b");
        private static readonly Regex Iso = new(@"\b((?:19|20)\d{2})-(\d{2})-(\d{2})\b");
        private static readonly Regex Meridiem = new(@"\d\s*[ap]\.?\s?m\b\.?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Word = new(@"[^\W\d_]+\.?");
        private static readonly Regex DayNumber = new(@"\b(\d{1,2})(?:st|nd|rd|th|er|º|\.)?\b");

        // RFC 2822, as in a real Date: header: "Tue, 04 Mar 2025 09:30:00 +0000". The time is kept as
        // written, in the sender's own offset.
        private static readonly Regex Rfc2822 = new(
            @"^(?:[A-Za-z]{3,9},?\s+)?(\d{1,2})\s+([A-Za-z]{3,9})\.?,?\s+(\d{2}|\d{4}),?\s+(\d{1,2}):(\d{2})(?::(\d{2}))?(?:\s+(?:[+-]\d{4}|[A-Za-z]{1,5}|\(.*\)))*$");
        private static readonly string[] EnglishMonths =
        {
            "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec",
            "january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december",
        };

        public static string Format(DateTime value) => value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        // "YYYY-MM-DD HH:MM" (or "YYYY-MM-DD"), or null when it can't be read safely. Numeric dates like
        // 03/04/2025 are only accepted when the day is unambiguous, since an English sender and a French
        // one mean different days by them.
        public static string Parse(string text, IReadOnlyDictionary<string, int> months)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();

            if (!Meridiem.IsMatch(text) && ParseRfc2822(text) is string rfc) return rfc;

            int year, month, day;
            var rest = text;

            var iso = Iso.Match(text);
            if (iso.Success)
            {
                year = Int(iso.Groups[1].Value);
                month = Int(iso.Groups[2].Value);
                day = Int(iso.Groups[3].Value);
                rest = Cut(text, iso);
            }
            else
            {
                var numeric = Numeric.Match(text);
                if (numeric.Success)
                {
                    int a = Int(numeric.Groups[1].Value), b = Int(numeric.Groups[2].Value);
                    year = Int(numeric.Groups[3].Value);
                    if (a > 12 && b <= 12) { day = a; month = b; }
                    else if (b > 12 && a <= 12) { month = a; day = b; }
                    else return null;
                    rest = Cut(text, numeric);
                }
                else
                {
                    var y = Year.Match(text);
                    if (!y.Success) return null;
                    year = Int(y.Groups[1].Value);
                    month = 0;
                    foreach (Match word in Word.Matches(text))
                    {
                        var key = EmailRules.Fold(word.Value).TrimEnd('.');
                        if (months.TryGetValue(key, out var found))
                        {
                            month = found;
                            break;
                        }
                    }
                    if (month == 0) return null;
                    var withoutYear = Cut(text, y);
                    var withoutTime = Time.Replace(withoutYear, " ");
                    var days = DayNumber.Matches(withoutTime).Select(m => Int(m.Groups[1].Value)).Where(n => n >= 1 && n <= 31).ToList();
                    if (days.Count == 0) return null;
                    day = days[0];
                    rest = withoutYear;
                }
            }

            if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return null;
            var date = new DateTime(year, month, day);

            var t = Time.Match(rest);
            if (!t.Success) return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            int hour = Int(t.Groups[1].Value), minute = Int(t.Groups[2].Value);
            var meridiem = t.Groups[3].Value.ToLowerInvariant().Replace(".", "").Replace(" ", "");
            if (meridiem == "pm" && hour < 12) hour += 12;
            else if (meridiem == "am" && hour == 12) hour = 0;
            if (hour > 23 || minute > 59) return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return Format(date.AddHours(hour).AddMinutes(minute));
        }

        private static string ParseRfc2822(string text)
        {
            var m = Rfc2822.Match(text);
            if (!m.Success) return null;
            var index = Array.IndexOf(EnglishMonths, m.Groups[2].Value.ToLowerInvariant());
            if (index < 0) return null;
            var month = index % 12 + 1;
            int day = Int(m.Groups[1].Value), year = Int(m.Groups[3].Value);
            if (year < 100) year += year > 68 ? 1900 : 2000;
            int hour = Int(m.Groups[4].Value), minute = Int(m.Groups[5].Value);
            if (year < 1 || day < 1 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59) return null;
            return Format(new DateTime(year, month, day, hour, minute, 0));
        }

        private static string Cut(string text, Match m) => text[..m.Index] + " " + text[(m.Index + m.Length)..];

        // Unicode digits count as digits in the patterns, as in the plugin; char.GetNumericValue reads them.
        private static int Int(string digits)
        {
            var value = 0;
            foreach (var c in digits) value = value * 10 + (int)char.GetNumericValue(c);
            return value;
        }
    }
}
