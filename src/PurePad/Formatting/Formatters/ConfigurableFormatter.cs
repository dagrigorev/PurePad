using System.Text;
using PurePad.Formatting.Profiles;

namespace PurePad.Formatting.Formatters;

/// <summary>
/// A generic, config-driven beautifier: it re-indents by bracket depth, trims trailing whitespace
/// and normalizes the final newline, all according to a <see cref="FormatProfile"/>. It is not a
/// language parser — it counts <c>{ } [ ] ( )</c> outside strings/comments — which reformats
/// C-family, JSON and similarly braced text well without a grammar per language.
/// </summary>
public sealed class ConfigurableFormatter : ITextFormatter
{
    private readonly FormatProfile _profile;

    public ConfigurableFormatter(FormatProfile profile) =>
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));

    public bool CanFormat =>
        _profile.ReindentByBrackets || _profile.TrimTrailingWhitespace || _profile.EnsureFinalNewline;

    public string Format(string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string unit = _profile.UseTabs ? "\t" : new string(' ', Math.Max(1, _profile.IndentSize));
        var sb = new StringBuilder(text.Length);
        int depth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();

            if (_profile.ReindentByBrackets)
            {
                if (trimmed.Length == 0)
                {
                    // leave blank lines empty
                }
                else
                {
                    int lead = LeadingClosers(trimmed);
                    int lineDepth = Math.Max(0, depth - lead);
                    sb.Append(Repeat(unit, lineDepth)).Append(trimmed);
                    depth = Math.Max(0, depth + NetDelta(trimmed));
                }
            }
            else
            {
                sb.Append(_profile.TrimTrailingWhitespace ? lines[i].TrimEnd() : lines[i]);
            }

            if (i < lines.Length - 1)
            {
                sb.Append('\n');
            }
        }

        string result = sb.ToString();
        if (_profile.EnsureFinalNewline && result.Length > 0 && !result.EndsWith('\n'))
        {
            result += "\n";
        }

        return result;
    }

    /// <summary>Number of leading closing brackets, so <c>}</c> lines dedent before printing.</summary>
    private static int LeadingClosers(string trimmed)
    {
        int n = 0;
        foreach (char c in trimmed)
        {
            if (c is '}' or ']' or ')')
            {
                n++;
            }
            else if (!char.IsWhiteSpace(c))
            {
                break;
            }
        }

        return n;
    }

    /// <summary>Net bracket change on a line (openers minus closers), ignoring strings and comments.</summary>
    private static int NetDelta(string line)
    {
        int delta = 0;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                break; // line comment
            }

            if (c is '"' or '\'' or '`')
            {
                i++;
                while (i < line.Length && line[i] != c)
                {
                    if (line[i] == '\\') i++;
                    i++;
                }

                continue;
            }

            if (c is '{' or '[' or '(') delta++;
            else if (c is '}' or ']' or ')') delta--;
        }

        return delta;
    }

    private static string Repeat(string unit, int count)
    {
        if (count <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(unit.Length * count);
        for (int i = 0; i < count; i++)
        {
            sb.Append(unit);
        }

        return sb.ToString();
    }
}
