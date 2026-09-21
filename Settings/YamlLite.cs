using System;
using System.Collections.Generic;

namespace KeyboardLayoutIndicator.Settings
{
    /// <summary>
    /// Упрощённый YAML-парсер, рассчитанный именно на формат настроек этой программы:
    ///   ключ_верхнего_уровня:
    ///     подключ: значение
    ///     подключ2: значение2
    /// Поддерживает комментарии (#), пустые строки и значения в кавычках.
    /// Не является полноценным YAML-парсером общего назначения.
    /// </summary>
    public static class YamlLite
    {
        public static Dictionary<string, Dictionary<string, string>> Parse(string text)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string? currentKey = null;

            foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = StripComment(rawLine);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int indent = line.Length - line.TrimStart(' ', '\t').Length;
                string trimmed = line.Trim();

                if (indent == 0)
                {
                    string key = trimmed.TrimEnd(':').Trim();
                    if (key.Length == 0) continue;

                    currentKey = key;
                    if (!result.ContainsKey(key))
                        result[key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    if (currentKey == null) continue;

                    int colonIdx = trimmed.IndexOf(':');
                    if (colonIdx < 0) continue;

                    string k = trimmed.Substring(0, colonIdx).Trim();
                    string v = trimmed.Substring(colonIdx + 1).Trim();
                    v = Unquote(v);

                    result[currentKey][k] = v;
                }
            }

            return result;
        }

        private static string StripComment(string line)
        {
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') inQuotes = !inQuotes;
                else if (c == '#' && !inQuotes)
                    return line.Substring(0, i);
            }
            return line;
        }

        private static string Unquote(string v)
        {
            if (v.Length >= 2 &&
                ((v[0] == '"' && v[^1] == '"') || (v[0] == '\'' && v[^1] == '\'')))
            {
                return v.Substring(1, v.Length - 2);
            }
            return v;
        }
    }
}
