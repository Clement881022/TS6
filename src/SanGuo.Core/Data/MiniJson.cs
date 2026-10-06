using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SanGuo.Core.Data
{
    /// <summary>
    /// 極小的 JSON 讀寫（無外部依賴，Unity / 伺服器共用）。
    /// 物件模型：null、bool、long、double、string、List&lt;object?&gt;、Dictionary&lt;string, object?&gt;。
    /// 輸出的物件鍵依序排序，結果可重現（方便比對與測試）。
    /// </summary>
    public static class MiniJson
    {
        public static object? Parse(string json)
        {
            var p = new Parser(json);
            p.SkipWs();
            var v = p.ReadValue();
            p.SkipWs();
            if (!p.AtEnd) throw p.Error("多餘的內容");
            return v;
        }

        public static string Write(object? value, bool indent = false)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, indent, 0);
            return sb.ToString();
        }

        private static void NewLine(StringBuilder sb, bool indent, int depth)
        {
            if (!indent) return;
            sb.Append('\n').Append(' ', depth * 2);
        }

        private static void WriteValue(StringBuilder sb, object? v, bool indent, int depth)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s: WriteString(sb, s); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d)) throw new ArgumentException("JSON 不支援 NaN / 無限大");
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case IDictionary<string, object?> dict:
                {
                    var keys = new List<string>(dict.Keys);
                    keys.Sort(StringComparer.Ordinal);
                    if (keys.Count == 0) { sb.Append("{}"); break; }
                    sb.Append('{');
                    for (int k = 0; k < keys.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        NewLine(sb, indent, depth + 1);
                        WriteString(sb, keys[k]);
                        sb.Append(indent ? ": " : ":");
                        WriteValue(sb, dict[keys[k]], indent, depth + 1);
                    }
                    NewLine(sb, indent, depth);
                    sb.Append('}');
                    break;
                }
                case IList<object?> list:
                {
                    if (list.Count == 0) { sb.Append("[]"); break; }
                    sb.Append('[');
                    for (int k = 0; k < list.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        NewLine(sb, indent, depth + 1);
                        WriteValue(sb, list[k], indent, depth + 1);
                    }
                    NewLine(sb, indent, depth);
                    sb.Append(']');
                    break;
                }
                default: throw new ArgumentException("不支援的型別：" + v.GetType().Name);
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s; }

            public bool AtEnd => _i >= _s.Length;

            public Exception Error(string msg) => new FormatException($"JSON 解析錯誤（位置 {_i}）：{msg}");

            public void SkipWs()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\n' || _s[_i] == '\r' || _s[_i] == '\t')) _i++;
            }

            public object? ReadValue()
            {
                if (AtEnd) throw Error("意外結尾");
                char c = _s[_i];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == 't') { Expect("true"); return true; }
                if (c == 'f') { Expect("false"); return false; }
                if (c == 'n') { Expect("null"); return null; }
                return ReadNumber();
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error("非預期的字元");
                _i += word.Length;
            }

            private Dictionary<string, object?> ReadObject()
            {
                var dict = new Dictionary<string, object?>();
                _i++;
                SkipWs();
                if (!AtEnd && _s[_i] == '}') { _i++; return dict; }
                while (true)
                {
                    SkipWs();
                    if (AtEnd || _s[_i] != '"') throw Error("物件鍵必須是字串");
                    string key = ReadString();
                    SkipWs();
                    if (AtEnd || _s[_i] != ':') throw Error("缺少冒號");
                    _i++;
                    SkipWs();
                    dict[key] = ReadValue();
                    SkipWs();
                    if (AtEnd) throw Error("意外結尾");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return dict; }
                    throw Error("物件格式錯誤");
                }
            }

            private List<object?> ReadArray()
            {
                var list = new List<object?>();
                _i++;
                SkipWs();
                if (!AtEnd && _s[_i] == ']') { _i++; return list; }
                while (true)
                {
                    SkipWs();
                    list.Add(ReadValue());
                    SkipWs();
                    if (AtEnd) throw Error("意外結尾");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return list; }
                    throw Error("陣列格式錯誤");
                }
            }

            private string ReadString()
            {
                var sb = new StringBuilder();
                _i++;
                while (true)
                {
                    if (AtEnd) throw Error("字串未結束");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("字串未結束");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("\\u 不完整");
                            sb.Append((char)Convert.ToInt32(_s.Substring(_i, 4), 16));
                            _i += 4;
                            break;
                        default: throw Error("未知的跳脫字元");
                    }
                }
            }

            private object ReadNumber()
            {
                int start = _i;
                bool isFloat = false;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == '.' || c == 'e' || c == 'E') isFloat = true;
                    else if (!(c == '-' || c == '+' || (c >= '0' && c <= '9'))) break;
                    _i++;
                }
                if (_i == start) throw Error("非預期的字元");
                string text = _s.Substring(start, _i - start);
                if (!isFloat && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                throw Error("數字格式錯誤");
            }
        }
    }
}
