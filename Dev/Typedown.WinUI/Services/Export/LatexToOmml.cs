using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    // The formulas of a document ($x^2$ and $$...$$) as Word equations (OMML), from the LaTeX the editor shows with KaTeX. It covers
    // what is written in practice: fractions, roots, powers and indices, sums and integrals with their limits, brackets, matrices,
    // Greek letters, accents, the usual symbols and the names of functions. What it does not know it writes as the text it is, so a
    // formula is never lost. Plain .NET (no WinUI).
    internal sealed partial class LatexToOmml
    {
        private readonly string s;
        private int i;
        private readonly HashSet<OpenXmlElement> limitBases = new();

        private LatexToOmml(string latex) => s = latex ?? "";

        // The content of an equation. A formula that cannot be read at all is its own text.
        public static List<OpenXmlElement> Convert(string latex)
        {
            try
            {
                var parser = new LatexToOmml(latex);
                var result = parser.Sequence(() => false);
                if (result.Count > 0) return result;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IndexOutOfRangeException)
            {
            }
            return new List<OpenXmlElement> { MathRun(latex ?? "") };
        }

        // --- the pieces of an equation
        private static M.Run MathRun(string text, bool upright = false, bool bold = false, bool asText = false)
        {
            var run = new M.Run();
            if (asText) run.Append(new M.RunProperties(new M.NormalText()));
            else if (upright || bold) run.Append(new M.RunProperties(new M.Style { Val = bold ? M.StyleValues.Bold : M.StyleValues.Plain }));
            run.Append(new W.RunProperties(new W.RunFonts { Ascii = "Cambria Math", HighAnsi = "Cambria Math" }));
            run.Append(new M.Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }

        private static T Holding<T>(T holder, IEnumerable<OpenXmlElement> content) where T : OpenXmlCompositeElement
        {
            var any = false;
            foreach (var element in content) { holder.Append(element); any = true; }
            if (!any) holder.Append(MathRun(""));
            return holder;
        }

        private static M.Base BaseOf(IEnumerable<OpenXmlElement> content) => Holding(new M.Base(), content);

        private static M.Delimiter Delimited(string open, string close, IEnumerable<OpenXmlElement> content)
        {
            var properties = new M.DelimiterProperties();
            properties.Append(new M.BeginChar { Val = open });
            properties.Append(new M.EndChar { Val = close });
            return new M.Delimiter(properties, BaseOf(content));
        }

        // --- reading
        private bool End => i >= s.Length;

        private bool LookingAt(string text) => string.CompareOrdinal(s, i, text, 0, text.Length) == 0;

        private void SkipSpaces()
        {
            while (!End && char.IsWhiteSpace(s[i])) i++;
        }

        // Elements until stop() says so (or the end). Characters are gathered into runs; scripts attach to what is in front of them.
        private List<OpenXmlElement> Sequence(Func<bool> stop)
        {
            var list = new List<OpenXmlElement>();
            var buffer = new StringBuilder();
            void Flush()
            {
                if (buffer.Length == 0) return;
                list.Add(MathRun(buffer.ToString()));
                buffer.Clear();
            }
            while (!End && !stop())
            {
                var c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '^' || c == '_' || c == '\'')
                {
                    OpenXmlElement baseElement;
                    if (buffer.Length > 0)
                    {
                        var last = buffer[buffer.Length - 1];
                        buffer.Length--;
                        Flush();
                        baseElement = MathRun(last.ToString());
                    }
                    else if (list.Count > 0)
                    {
                        baseElement = list[list.Count - 1];
                        list.RemoveAt(list.Count - 1);
                    }
                    else baseElement = MathRun("");
                    list.Add(Scripts(baseElement));
                    continue;
                }
                if (c == '{')
                {
                    Flush();
                    i++;
                    list.AddRange(Sequence(() => !End && s[i] == '}'));
                    if (!End) i++;
                    continue;
                }
                if (c == '}') { i++; continue; } // a closing brace with nothing open
                if (c == '\\')
                {
                    Flush();
                    list.AddRange(Command());
                    continue;
                }
                if (c == '&') { i++; buffer.Append(' '); continue; }
                buffer.Append(Symbol(c));
                i++;
            }
            Flush();
            return list;
        }

        private static string Symbol(char c) => c switch
        {
            '-' => "−",
            '*' => "∗",
            '<' or '>' => c.ToString(),
            _ => c.ToString(),
        };

        // The base with the powers and indices that follow it, in either order.
        private OpenXmlElement Scripts(OpenXmlElement baseElement)
        {
            List<OpenXmlElement> sub = null, sup = null;
            while (!End)
            {
                SkipSpaces();
                if (End) break;
                if (s[i] == '_' && sub == null) { i++; sub = Argument(); }
                else if (s[i] == '^' && sup == null) { i++; sup = Argument(); }
                else if (s[i] == '\'' && sup == null) { i++; sup = new List<OpenXmlElement> { MathRun("′") }; }
                else break;
            }
            var content = new[] { baseElement };
            if (sub != null && sup != null)
                return new M.SubSuperscript(BaseOf(content), Holding(new M.SubArgument(), sub), Holding(new M.SuperArgument(), sup));
            if (sup != null) return new M.Superscript(BaseOf(content), Holding(new M.SuperArgument(), sup));
            if (limitBases.Contains(baseElement))
                return new M.LimitLower(BaseOf(content), Holding(new M.Limit(), sub ?? new List<OpenXmlElement>()));
            return new M.Subscript(BaseOf(content), Holding(new M.SubArgument(), sub ?? new List<OpenXmlElement>()));
        }

        // One argument: a group in braces, a command, or a single character.
        private List<OpenXmlElement> Argument()
        {
            SkipSpaces();
            if (End) return new List<OpenXmlElement>();
            if (s[i] == '{')
            {
                i++;
                var inner = Sequence(() => !End && s[i] == '}');
                if (!End) i++;
                return inner;
            }
            if (s[i] == '\\') return Command();
            var one = s[i].ToString();
            i++;
            return new List<OpenXmlElement> { MathRun(Symbol(one[0])) };
        }

        // --- commands
        private List<OpenXmlElement> One(OpenXmlElement element) => new() { element };

        private List<OpenXmlElement> Command()
        {
            i++; // the backslash
            if (End) return new List<OpenXmlElement>();
            string name;
            if (char.IsLetter(s[i]))
            {
                var start = i;
                while (!End && char.IsLetter(s[i])) i++;
                name = s.Substring(start, i - start);
            }
            else name = s[i++].ToString();

            if (Symbols.TryGetValue(name, out var symbol)) return One(MathRun(symbol));
            if (FunctionNames.Contains(name))
            {
                // a thin space between the name and what it is applied to ("sin x"), none before a bracket or a script
                SkipSpaces();
                var applied = !End && (char.IsLetterOrDigit(s[i]) || s[i] == (char)92);
                return One(MathRun(applied ? name + " " : name, upright: true));
            }
            if (name is "lim" or "max" or "min" or "sup" or "inf" or "limsup" or "liminf")
            {
                var function = MathRun(name == "limsup" ? "lim sup" : name == "liminf" ? "lim inf" : name, upright: true);
                limitBases.Add(function);
                return One(function);
            }
            switch (name)
            {
                case "frac" or "dfrac" or "tfrac":
                    return One(new M.Fraction(Holding(new M.Numerator(), Argument()), Holding(new M.Denominator(), Argument())));
                case "binom":
                {
                    var properties = new M.FractionProperties(new M.FractionType { Val = M.FractionTypeValues.NoBar });
                    var fraction = new M.Fraction(properties, Holding(new M.Numerator(), Argument()), Holding(new M.Denominator(), Argument()));
                    return One(Delimited("(", ")", new[] { fraction }));
                }
                case "sqrt": return One(Root());
                case "left":
                {
                    var open = DelimiterChar();
                    var inner = Sequence(() => LookingAt(@"\right"));
                    var close = "";
                    if (LookingAt(@"\right")) { i += 6; close = DelimiterChar(); }
                    return One(Delimited(open, close, inner));
                }
                case "right": DelimiterChar(); return new List<OpenXmlElement>();
                case "text" or "textrm" or "mbox" or "hbox": return One(MathRun(RawArgument(), asText: true));
                case "mathrm" or "operatorname" or "mathsf" or "mathtt": return One(MathRun(RawArgument(), upright: true));
                case "mathbf" or "boldsymbol" or "bm" or "textbf": return One(MathRun(RawArgument(), bold: true));
                case "mathbb": return One(MathRun(Blackboard(RawArgument()), upright: true));
                case "mathcal" or "mathfrak" or "mathscr" or "mathit" or "textit" or "mathnormal": return One(MathRun(RawArgument()));
                case "hat" or "widehat": return One(Accent("̂"));
                case "bar" or "overline": return One(Accent("̅"));
                case "vec" or "overrightarrow": return One(Accent("⃗"));
                case "dot": return One(Accent("̇"));
                case "ddot": return One(Accent("̈"));
                case "tilde" or "widetilde": return One(Accent("̃"));
                case "sum" or "prod" or "coprod" or "int" or "iint" or "iiint" or "oint" or "bigcup" or "bigcap" or "bigoplus" or "bigotimes" or "bigvee" or "bigwedge":
                    return One(Nary(name));
                case "begin": return One(Environment());
                case "," or ";" or ":" or " " or "!" or "quad" or "qquad" or "\\" or "hspace" or "mspace":
                    if (name is "hspace" or "mspace") RawArgument();
                    return One(MathRun(name == "!" ? "" : name is "quad" or "qquad" ? " " : name == "," ? " " : " "));
                case "{" or "}" or "%" or "$" or "&" or "#" or "_": return One(MathRun(name));
                case "displaystyle" or "textstyle" or "scriptstyle" or "limits" or "nolimits" or "big" or "Big" or "bigg" or "Bigg" or "nonumber" or "notag" or "label":
                    return new List<OpenXmlElement>();
                default:
                    return One(MathRun(name, upright: true));
            }
        }

        private OpenXmlElement Root()
        {
            SkipSpaces();
            List<OpenXmlElement> degree = null;
            if (!End && s[i] == '[')
            {
                var close = s.IndexOf(']', i);
                if (close > i)
                {
                    degree = new LatexToOmml(s.Substring(i + 1, close - i - 1)).Sequence(() => false);
                    i = close + 1;
                }
            }
            var radicand = Argument();
            var properties = new M.RadicalProperties();
            if (degree == null) properties.Append(new M.HideDegree { Val = M.BooleanValues.One });
            return new M.Radical(properties, Holding(new M.Degree(), degree ?? new List<OpenXmlElement>()), BaseOf(radicand));
        }

        private OpenXmlElement Accent(string mark)
        {
            var properties = new M.AccentProperties(new M.AccentChar { Val = mark });
            return new M.Accent(properties, BaseOf(Argument()));
        }

        // The text of an argument as it is written, braces counted: for \text{...} and \mathrm{...}.
        private string RawArgument()
        {
            SkipSpaces();
            if (End) return "";
            if (s[i] != '{') return s[i++].ToString();
            var depth = 0;
            var start = ++i;
            for (; !End; i++)
            {
                if (s[i] == '{') depth++;
                else if (s[i] == '}' && depth-- == 0) break;
            }
            var raw = s.Substring(start, Math.Min(i, s.Length) - start);
            if (!End) i++;
            return raw.Replace(@"\ ", " ").Replace(@"\_", "_").Replace(@"\%", "%").Replace(@"\&", "&").Replace(@"\$", "$").Replace(@"\#", "#");
        }

        private static string Blackboard(string letters) => string.Concat(letters.Select(c => c switch
        {
            'R' => "ℝ", 'N' => "ℕ", 'Z' => "ℤ", 'Q' => "ℚ", 'C' => "ℂ", 'P' => "ℙ", 'H' => "ℍ", _ => c.ToString(),
        }));

        // The character after \left or \right; "" for none (a dot, or a command that is not a bracket).
        private string DelimiterChar()
        {
            SkipSpaces();
            if (End) return "";
            var c = s[i];
            if (c == '.') { i++; return ""; }
            if (c != '\\') { i++; return c.ToString(); }
            i++;
            if (End) return "";
            if (!char.IsLetter(s[i]))
            {
                var symbol = s[i++];
                return symbol switch { '{' => "{", '}' => "}", '|' => "‖", _ => "" };
            }
            var start = i;
            while (!End && char.IsLetter(s[i])) i++;
            return s.Substring(start, i - start) switch
            {
                "langle" => "⟨", "rangle" => "⟩", "lbrace" => "{", "rbrace" => "}", "lvert" or "rvert" or "vert" => "|",
                "lVert" or "rVert" or "Vert" => "‖", "lceil" => "⌈", "rceil" => "⌉", "lfloor" => "⌊", "rfloor" => "⌋", _ => "",
            };
        }

        // --- big operators: the limits, and the first thing after them as the body
        private OpenXmlElement Nary(string name)
        {
            string character = name switch
            {
                "sum" => "∑", "prod" => "∏", "coprod" => "∐", "int" => "∫", "iint" => "∬", "iiint" => "∭", "oint" => "∮",
                "bigcup" => "⋃", "bigcap" => "⋂", "bigoplus" => "⨁", "bigotimes" => "⨂", "bigvee" => "⋁", _ => "⋀",
            };
            List<OpenXmlElement> sub = null, sup = null;
            while (!End)
            {
                SkipSpaces();
                if (!End && s[i] == '_' && sub == null) { i++; sub = Argument(); }
                else if (!End && s[i] == '^' && sup == null) { i++; sup = Argument(); }
                else break;
            }
            var properties = new M.NaryProperties(new M.AccentChar { Val = character });
            properties.Append(new M.LimitLocation { Val = name.Contains("int") ? M.LimitLocationValues.SubscriptSuperscript : M.LimitLocationValues.UnderOver });
            if (sub == null) properties.Append(new M.HideSubArgument { Val = M.BooleanValues.One });
            if (sup == null) properties.Append(new M.HideSuperArgument { Val = M.BooleanValues.One });
            var body = new List<OpenXmlElement>();
            SkipSpaces();
            if (!End && s[i] != '}' && s[i] != '&' && !LookingAt(@"\\") && !LookingAt(@"\end") && !LookingAt(@"\right"))
            {
                body = Argument();
                SkipSpaces();
                if (!End && (s[i] == '^' || s[i] == '_' || s[i] == '\'') && body.Count > 0)
                {
                    var last = body[body.Count - 1];
                    body.RemoveAt(body.Count - 1);
                    body.Add(Scripts(last));
                }
            }
            return new M.Nary(properties, Holding(new M.SubArgument(), sub ?? new List<OpenXmlElement>()), Holding(new M.SuperArgument(), sup ?? new List<OpenXmlElement>()), BaseOf(body));
        }

        // \begin{pmatrix} 1 & 2 \ 3 & 4 \end{pmatrix} and the like: matrices, cases, aligned lines.
        private OpenXmlElement Environment()
        {
            var kind = RawArgument();
            var close = @"\end{" + kind + "}";
            var depth = 0;
            var start = i;
            var end = -1;
            for (var at = i; at < s.Length; at++)
            {
                if (string.CompareOrdinal(s, at, @"\begin{" + kind + "}", 0, kind.Length + 8) == 0) depth++;
                else if (string.CompareOrdinal(s, at, close, 0, close.Length) == 0 && depth-- == 0) { end = at; break; }
            }
            var inner = end < 0 ? s.Substring(start) : s.Substring(start, end - start);
            i = end < 0 ? s.Length : end + close.Length;

            var rows = SplitRows(inner).Select(row => row.Select(cell => new LatexToOmml(cell).Sequence(() => false)).ToList()).ToList();
            var plain = kind.TrimEnd('*');
            if (plain is "aligned" or "align" or "gather" or "gathered" or "split" or "eqnarray" or "alignedat")
            {
                var array = new M.EquationArray();
                foreach (var row in rows) array.Append(BaseOf(row.SelectMany(cell => cell)));
                return array;
            }
            var matrix = new M.Matrix();
            foreach (var row in rows)
            {
                var matrixRow = new M.MatrixRow();
                foreach (var cell in row) matrixRow.Append(BaseOf(cell));
                matrix.Append(matrixRow);
            }
            return plain switch
            {
                "pmatrix" => Delimited("(", ")", new[] { matrix }),
                "bmatrix" => Delimited("[", "]", new[] { matrix }),
                "Bmatrix" => Delimited("{", "}", new[] { matrix }),
                "vmatrix" => Delimited("|", "|", new[] { matrix }),
                "Vmatrix" => Delimited("‖", "‖", new[] { matrix }),
                "cases" => Delimited("{", "", new[] { matrix }),
                _ => matrix,
            };
        }

        // The rows (split at \) and cells (split at &) of an environment, braces respected.
        private static List<List<string>> SplitRows(string text)
        {
            var rows = new List<List<string>> { new() };
            var cell = new StringBuilder();
            var depth = 0;
            for (var at = 0; at < text.Length; at++)
            {
                var c = text[at];
                if (c == '{') depth++;
                else if (c == '}') depth--;
                if (depth == 0 && c == '&') { rows[^1].Add(cell.ToString()); cell.Clear(); continue; }
                if (depth == 0 && c == '\\' && at + 1 < text.Length && text[at + 1] == '\\')
                {
                    rows[^1].Add(cell.ToString());
                    cell.Clear();
                    rows.Add(new List<string>());
                    at++;
                    continue;
                }
                cell.Append(c);
            }
            rows[^1].Add(cell.ToString());
            return rows.Where(row => row.Any(c => c.Trim().Length > 0)).ToList();
        }

        private static readonly HashSet<string> FunctionNames = new()
        {
            "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "coth", "log", "ln", "lg", "exp",
            "det", "gcd", "deg", "dim", "ker", "arg", "hom", "Pr", "mod", "bmod",
        };

        private static readonly Dictionary<string, string> Symbols = new()
        {
            ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ϵ", ["varepsilon"] = "ε",
            ["zeta"] = "ζ", ["eta"] = "η", ["theta"] = "θ", ["vartheta"] = "ϑ", ["iota"] = "ι", ["kappa"] = "κ",
            ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["xi"] = "ξ", ["pi"] = "π", ["varpi"] = "ϖ", ["rho"] = "ρ",
            ["varrho"] = "ϱ", ["sigma"] = "σ", ["varsigma"] = "ς", ["tau"] = "τ", ["upsilon"] = "υ", ["phi"] = "ϕ",
            ["varphi"] = "φ", ["chi"] = "χ", ["psi"] = "ψ", ["omega"] = "ω",
            ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Xi"] = "Ξ", ["Pi"] = "Π",
            ["Sigma"] = "Σ", ["Upsilon"] = "Υ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω",
            ["cdot"] = "⋅", ["times"] = "×", ["div"] = "÷", ["pm"] = "±", ["mp"] = "∓", ["ast"] = "∗", ["star"] = "⋆",
            ["circ"] = "∘", ["bullet"] = "∙", ["oplus"] = "⊕", ["otimes"] = "⊗", ["odot"] = "⊙",
            ["le"] = "≤", ["leq"] = "≤", ["ge"] = "≥", ["geq"] = "≥", ["ne"] = "≠", ["neq"] = "≠", ["approx"] = "≈",
            ["equiv"] = "≡", ["sim"] = "∼", ["simeq"] = "≃", ["cong"] = "≅", ["propto"] = "∝", ["ll"] = "≪", ["gg"] = "≫",
            ["infty"] = "∞", ["partial"] = "∂", ["nabla"] = "∇", ["hbar"] = "ℏ", ["ell"] = "ℓ", ["Re"] = "ℜ", ["Im"] = "ℑ",
            ["aleph"] = "ℵ", ["emptyset"] = "∅", ["varnothing"] = "∅", ["prime"] = "′", ["angle"] = "∠", ["perp"] = "⊥",
            ["parallel"] = "∥", ["mid"] = "∣", ["degree"] = "°",
            ["to"] = "→", ["rightarrow"] = "→", ["leftarrow"] = "←", ["leftrightarrow"] = "↔", ["Rightarrow"] = "⇒",
            ["Leftarrow"] = "⇐", ["Leftrightarrow"] = "⇔", ["iff"] = "⇔", ["implies"] = "⇒", ["mapsto"] = "↦", ["uparrow"] = "↑",
            ["downarrow"] = "↓", ["longrightarrow"] = "⟶", ["longleftarrow"] = "⟵",
            ["in"] = "∈", ["notin"] = "∉", ["ni"] = "∋", ["subset"] = "⊂", ["supset"] = "⊃", ["subseteq"] = "⊆",
            ["supseteq"] = "⊇", ["cup"] = "∪", ["cap"] = "∩", ["setminus"] = "∖", ["forall"] = "∀", ["exists"] = "∃",
            ["nexists"] = "∄", ["neg"] = "¬", ["lnot"] = "¬", ["land"] = "∧", ["wedge"] = "∧", ["lor"] = "∨", ["vee"] = "∨",
            ["ldots"] = "…", ["dots"] = "…", ["cdots"] = "⋯", ["vdots"] = "⋮", ["ddots"] = "⋱", ["langle"] = "⟨", ["rangle"] = "⟩",
            ["lbrace"] = "{", ["rbrace"] = "}", ["lvert"] = "|", ["rvert"] = "|", ["vert"] = "|", ["Vert"] = "‖", ["|"] = "‖", ["lceil"] = "⌈",
            ["rceil"] = "⌉", ["lfloor"] = "⌊", ["rfloor"] = "⌋", ["triangle"] = "△", ["square"] = "□", ["checkmark"] = "✓",
        };
    }
}
