using System.Text.RegularExpressions;

namespace SPOLab2a
{
    public sealed class Token(string text, int line, int start, int end)
    {
        public string text = text;
        public int line = line;
        public int start = start;
        public int end = end;
    }

    public sealed class CondInfo(int line, string kind, string text, int level)
    {
        public int line = line;
        public string kind = kind;
        public string text = text;
        public int level = level;
    }

    public sealed class GilbResult
    {
        public int total_operators;
        public List<CondInfo> conditionals = [];

        public int CL => conditionals.Count;

        public double Cl => total_operators == 0 ? 0 : (double)CL / total_operators;

        public int CLI() => conditionals.Max(c => c.level);
    }

    public static class GilbAnalyzer
    {
        public static GilbResult Analyze(string source)
        {
            var tokens = Tokenizer.Tokenize(source);
            var parser = new Parser(source, tokens);
            Stats s = parser.ParseTop();
            return new GilbResult
            {
                total_operators = s.total,
                conditionals = [.. s.items.OrderBy(c => c.line).ThenBy(c => c.level)]
            };
        }
    }

    internal static class Tokenizer
    {
        public static List<Token> Tokenize(string src)
        {
            var res = new List<Token>();
            int len = src.Length, i = 0, line = 1;

            while (i < len)
            {
                char c = src[i];

                if (c == '\n')
                {
                    i++;
                    line++;
                    continue;
                }
                
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // comment
                if (c == '/' && i + 1 < len && src[i + 1] == '/')
                {
                    while (i < len && src[i] != '\n') i++;
                    continue;
                }

                // multi-line comment
                if (c == '/' && i + 1 < len && src[i + 1] == '*')
                {
                    i += 2;
                    while (i < len && !(src[i] == '*' && i + 1 < len && src[i + 1] == '/'))
                    {
                        if (src[i] == '\n') line++;
                        i++;
                    }
                    i = Math.Min(i + 2, len);
                    continue;
                }

                // string literal
                if (c == '"')
                {
                    int start = i, startLine = line;
                    if (string.CompareOrdinal(src, i, "\"\"\"", 0, 3) == 0)
                    {
                        i += 3;
                        while (i < len && string.CompareOrdinal(src, i, "\"\"\"", 0, 3) != 0)
                        {
                            if (src[i] == '\n') line++;
                            if (src[i] == '\\') i++;
                            i++;
                        }
                        i = Math.Min(i + 3, len);
                    }
                    else
                    {
                        i++;
                        while (i < len && src[i] != '"' && src[i] != '\n')
                        {
                            if (src[i] == '\\') i++;
                            i++;
                        }
                        i = Math.Min(i + 1, len);
                    }
                    res.Add(new Token("\"str\"", startLine, start, Math.Min(i, len)));
                    continue;
                }

                // char lineral
                if (c == '\'')
                {
                    int start = i;
                    i++;
                    while (i < len && src[i] != '\'' && src[i] != '\n')
                    {
                        if (src[i] == '\\') i++;
                        i++;
                    }
                    i = Math.Min(i + 1, len);
                    res.Add(new Token("'c'", line, start, Math.Min(i, len)));
                    continue;
                }

                // identifier
                if (char.IsLetter(c) || c == '_' || c == '$')
                {
                    int start = i;
                    while (i < len && (char.IsLetterOrDigit(src[i]) || src[i] == '_' || src[i] == '$')) i++;
                    res.Add(new Token(src[start..i], line, start, i));
                    continue;
                }

                // numeric literal
                if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < len && (char.IsLetterOrDigit(src[i]) || src[i] == '.' || src[i] == '_')) i++;
                    res.Add(new Token(src.Substring(start, i - start), line, start, i));
                    continue;
                }

                // some other constructs
                if (i + 1 < len)
                {
                    string two = src.Substring(i, 2);
                    if (two == "->" || two == "::" || two == "++" || two == "--")
                    {
                        res.Add(new Token(two, line, i, i + 2));
                        i += 2;
                        continue;
                    }
                }
                res.Add(new Token(c.ToString(), line, i, i + 1));
                i++;
            }

            return res;
        }
    }

    internal sealed class Stats
    {
        public int total;
        public List<CondInfo> items = [];

        public void Add(Stats o)
        {
            total += o.total;
            items.AddRange(o.items);
        }

        public void AddShifted(Stats o, int delta)
        {
            total += o.total;

            foreach (var c in o.items)
                items.Add(new CondInfo(c.line, c.kind, c.text, c.level + delta));
        }
    }

    internal sealed class Parser(string source, List<Token> tokens)
    {
        private readonly string src = source;
        private readonly List<Token> t = tokens;
        private int p = 0;

        private bool Is(string s) => p < t.Count && t[p].text == s;

        public Stats ParseTop()
        {
            var s = new Stats();
            while (p < t.Count)
            {
                if (Is("}"))
                {
                    p++;
                    continue;
                }
                
                s.Add(ParseStatement(false, false));
            }
            return s;
        }

        /// <param name="code">true - method/function contents, false - class body.</param>
        private Stats ParseStatements(bool code, bool inSwitch, bool skipBreak)
        {
            var s = new Stats();
            while (p < t.Count && !Is("}"))
            {
                if (inSwitch && (Is("case") || Is("default"))) break;
                s.Add(ParseStatement(code, skipBreak));
            }
            return s;
        }

        private Stats ParseStatement(bool code, bool skipBreak)
        {
            if (p >= t.Count) return new Stats();

            if (code)
            {
                switch (t[p].text)
                {
                    case "{":
                    {
                        p++;
                        var b = ParseStatements(true, false, skipBreak);
                        if (Is("}")) p++;
                        return b;
                    }
                    case ";": p++; return new Stats();
                    case "if": return ParseIf();
                    case "while": return ParseLoop();
                    case "for": return ParseLoop();
                    case "do": return ParseDo();
                    case "switch": return ParseSwitch();
                    case "try":
                        p++;
                        if (Is("(")) SkipParens(); // try-with-resources
                        return ParseStatement(true, false);
                    case "catch":
                        p++;
                        if (Is("(")) SkipParens();
                        return ParseStatement(true, false);
                    case "finally":
                        p++;
                        return ParseStatement(true, false);
                    case "synchronized":
                        if (p + 1 < t.Count && t[p + 1].text == "(")
                        {
                            p++;
                            SkipParens();
                            return ParseStatement(true, false);
                        }
                        break;
                    case "else": p++; return new Stats();
                }
            }
            return ParseSimple(code, skipBreak);
        }

        private Stats ParseIf()
        {
            Token kw = t[p]; p++;
            string cond = SkipParens();
            var s = new Stats();
            s.total++;
            s.items.Add(new CondInfo(kw.line, "if", "if (" + cond + ")", 0));
            s.AddShifted(ParseStatement(true, false), 1);
            if (Is("else"))
            {
                p++;
                s.AddShifted(ParseStatement(true, false), 1);
            }
            return s;
        }

        // for / for-each / while
        private Stats ParseLoop()
        {
            Token kw = t[p]; p++;
            string cond = SkipParens();
            string kind = kw.text == "while" ? "while" : (cond.Contains(';') ? "for" : "for-each");
            var s = new Stats();
            s.total++;
            s.items.Add(new CondInfo(kw.line, kind, kw.text + " (" + cond + ")", 0));
            s.AddShifted(ParseStatement(true, false), 1);
            return s;
        }

        // do S while (...);
        private Stats ParseDo()
        {
            Token kw = t[p]; p++;
            Stats body = ParseStatement(true, false);
            string cond = "";
            if (Is("while"))
            {
                p++;
                cond = SkipParens();
                if (Is(";")) p++;
            }
            var s = new Stats();
            s.total++;
            s.items.Add(new CondInfo(kw.line, "do-while", "do ... while (" + cond + ")", 0));
            s.AddShifted(body, 1);
            return s;
        }

        private Stats ParseSwitch()
        {
            Token kw = t[p]; p++;
            string expr = SkipParens();
            var s = new Stats();
            if (!Is("{")) return s;
            p++;

            var bodies = new List<Stats>();
            var is_default = new List<bool>();
            while (p < t.Count && !Is("}"))
            {
                if (Is("case") || Is("default"))
                {
                    bool arrow;
                    bool def = false;
                    do
                    {
                        if (Is("default")) def = true;
                        p++;
                        int depth = 0;
                        while (p < t.Count)
                        {
                            string x = t[p].text;
                            if (x == "(") depth++;
                            else if (x == ")") depth--;
                            else if (depth == 0 && (x == ":" || x == "->")) break;
                            p++;
                        }
                        arrow = Is("->");
                        p++;
                    }

                    while (!arrow && (Is("case") || Is("default")));

                    bodies.Add(arrow ? ParseStatement(true, false)
                                     : ParseStatements(true, true, true));
                    is_default.Add(def);
                }
                else p++;
            }
            if (Is("}")) p++;

            int c = is_default.Count(d => !d);
            s.total += c;
            for (int j = 0; j < c; j++)
                s.items.Add(new CondInfo(kw.line, "switch",
                    "switch (" + expr + ") → if " + (j + 1) + " из " + c, j));

            int k = 0;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (!is_default[i]) k++;
                s.AddShifted(bodies[i], is_default[i] ? c : k);
            }
            return s;
        }

        private Stats ParseSimple(bool code, bool skip_break)
        {
            var s = new Stats();
            int start = p;
            bool has_new = false, has_class_kw = false;
            string prev = "";

            while (p < t.Count)
            {
                string x = t[p].text;

                if (x == ";")
                {
                    bool plain_break = skip_break && t[start].text == "break" && p == start + 1;
                    p++;
                    if (code && !plain_break) s.total++;
                    return s;
                }
                if (x == "}") return s;

                if (x == "switch")
                {
                    s.Add(ParseSwitch());
                    prev = "}";
                    continue;
                }

                if (x == "{")
                {
                    if (prev == "=" || prev == "," || prev == "(" || prev == "]")
                    {
                        SkipBraces();
                        prev = "}";
                        continue;
                    }

                    if (prev == "->" || (has_new && prev == ")"))
                    {
                        bool lambda = prev == "->";
                        p++;
                        s.Add(ParseStatements(lambda, false, false));
                        if (Is("}")) p++;
                        prev = "}";
                        continue;
                    }

                    p++;
                    s.Add(ParseStatements(!has_class_kw, false, false));
                    if (Is("}")) p++;
                    return s;
                }

                if (x == "new") has_new = true;
                else if (x == "class" || x == "interface" || x == "enum" || x == "record") has_class_kw = true;

                prev = x;
                p++;
            }
            return s;
        }

        private void SkipBraces()
        {
            int depth = 0;
            while (p < t.Count)
            {
                if (t[p].text == "{") depth++;
                else if (t[p].text == "}") depth--;
                p++;
                if (depth == 0) break;
            }
        }

        private string SkipParens()
        {
            if (!Is("(")) return "";
            int open = p, depth = 0, close = p;
            while (p < t.Count)
            {
                string x = t[p].text;
                if (x == "(") depth++;
                else if (x == ")") depth--;
                close = p;
                p++;
                if (depth == 0) break;
            }
            if (close <= open) return "";
            string inner = src.Substring(t[open].end, Math.Max(0, t[close].start - t[open].end));
            return Regex.Replace(inner, @"\s+", " ").Trim();
        }
    }
}
