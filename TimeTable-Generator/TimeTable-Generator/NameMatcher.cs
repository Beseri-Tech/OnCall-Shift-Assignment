using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TimeTable_Generator
{
    public enum NameMatchType
    {
        None,
        Fuzzy,
        Exact
    }

    public class NameMatch
    {
        public string RawName { get; set; }
        public Person Person { get; set; }
        public NameMatchType Type { get; set; }
        public double Score { get; set; }
    }

    /// <summary>
    /// Matches names typed differently across sheets
    /// ("DR. SAW JU WEN" / "Dr Saw Ju Wen", "BT" / "BINTI", small typos).
    /// </summary>
    public static class NameMatcher
    {
        private const double FUZZY_THRESHOLD = 0.85;

        private static readonly Dictionary<string, string> TokenAliases = new Dictionary<string, string>
        {
            { "BT", "BINTI" },
            { "BTE", "BINTI" },
            { "BNT", "BINTI" },
            { "B", "BIN" },
            { "AP", "A/P" },
            { "AL", "A/L" },
        };

        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            string s = name.ToUpperInvariant();
            s = Regex.Replace(s, @"[^A-Z0-9/ ]", " ");        // drop . , ' - etc. (keep A/P, A/L)
            s = Regex.Replace(s, @"\s*/\s*", "/");

            var tokens = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            while (tokens.Count > 0 && (tokens[0] == "DR" || tokens[0] == "DR/"))
                tokens.RemoveAt(0);

            for (int i = 0; i < tokens.Count; i++)
                if (TokenAliases.TryGetValue(tokens[i], out var alias))
                    tokens[i] = alias;

            return string.Join(" ", tokens);
        }

        /// <summary>
        /// Matches each raw name to at most one person; each person is used at most once (best score wins).
        /// </summary>
        public static List<NameMatch> MatchAll(IList<string> rawNames, IList<Person> people)
        {
            var normPeople = people.Select(p => Normalize(p.Name)).ToList();
            var candidates = new List<Tuple<int, int, double>>();   // (raw index, person index, score)

            for (int r = 0; r < rawNames.Count; r++)
            {
                string n = Normalize(rawNames[r]);
                if (n.Length == 0) continue;

                for (int p = 0; p < people.Count; p++)
                {
                    double score = Similarity(n, normPeople[p]);
                    if (score >= FUZZY_THRESHOLD)
                        candidates.Add(Tuple.Create(r, p, score));
                }
            }

            var result = rawNames.Select(r => new NameMatch { RawName = r, Type = NameMatchType.None }).ToList();
            var usedPeople = new HashSet<int>();

            foreach (var c in candidates.OrderByDescending(c => c.Item3))
            {
                if (result[c.Item1].Person != null || usedPeople.Contains(c.Item2)) continue;

                result[c.Item1].Person = people[c.Item2];
                result[c.Item1].Score = c.Item3;
                result[c.Item1].Type = c.Item3 >= 1.0 ? NameMatchType.Exact : NameMatchType.Fuzzy;
                usedPeople.Add(c.Item2);
            }

            return result;
        }

        /// <summary>1.0 = identical after normalisation; compares with and without spaces.</summary>
        public static double Similarity(string a, string b)
        {
            if (a == b) return 1.0;

            string ca = a.Replace(" ", ""), cb = b.Replace(" ", "");
            if (ca == cb) return 0.99;

            return Math.Max(Ratio(a, b), Ratio(ca, cb));
        }

        private static double Ratio(string a, string b)
        {
            int max = Math.Max(a.Length, b.Length);
            if (max == 0) return 1.0;
            return 1.0 - (double)Levenshtein(a, b) / max;
        }

        private static int Levenshtein(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                var tmp = prev; prev = cur; cur = tmp;
            }
            return prev[b.Length];
        }
    }
}
