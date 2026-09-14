using System.Drawing;

namespace PurePad.Formatting;

/// <summary>
/// Maps a <see cref="TokenKind"/> to the colour used to render it. A theme owns no
/// tokenizing logic — it is pure presentation data, which keeps colour choices
/// decoupled from the highlighters (Single Responsibility).
/// </summary>
public sealed class SyntaxTheme
{
    private readonly IReadOnlyDictionary<TokenKind, Color> _colors;

    public SyntaxTheme(Color defaultForeground, IReadOnlyDictionary<TokenKind, Color> colors)
    {
        DefaultForeground = defaultForeground;
        _colors = colors ?? throw new ArgumentNullException(nameof(colors));
    }

    /// <summary>Colour for <see cref="TokenKind.PlainText"/> and any unmapped kind.</summary>
    public Color DefaultForeground { get; }

    public Color ColorFor(TokenKind kind) =>
        _colors.TryGetValue(kind, out var color) ? color : DefaultForeground;

    /// <summary>
    /// A light theme reminiscent of a classic editor on the Vista "Window" background:
    /// blue keywords, green comments, dark-red strings.
    /// </summary>
    public static SyntaxTheme CreateDefault() => new(
        defaultForeground: SystemColors.WindowText,
        colors: new Dictionary<TokenKind, Color>
        {
            [TokenKind.Keyword] = Color.FromArgb(0, 0, 255),
            [TokenKind.String] = Color.FromArgb(163, 21, 21),
            [TokenKind.Number] = Color.FromArgb(9, 134, 88),
            [TokenKind.Comment] = Color.FromArgb(0, 128, 0),
            [TokenKind.Operator] = Color.FromArgb(80, 80, 80),
            [TokenKind.Punctuation] = Color.FromArgb(80, 80, 80),
            [TokenKind.Property] = Color.FromArgb(0, 0, 255),
            [TokenKind.Boolean] = Color.FromArgb(0, 0, 255),
            [TokenKind.Null] = Color.FromArgb(0, 0, 255),
            [TokenKind.Tag] = Color.FromArgb(128, 0, 128),
            [TokenKind.AttributeName] = Color.FromArgb(255, 0, 0),
            [TokenKind.AttributeValue] = Color.FromArgb(0, 0, 255),
            [TokenKind.Heading] = Color.FromArgb(0, 0, 200),
            [TokenKind.Emphasis] = Color.FromArgb(0, 128, 0),
        });

    /// <summary>
    /// A dark-background palette tuned for legibility on a near-black editor surface.
    /// </summary>
    public static SyntaxTheme CreateDark() => new(
        defaultForeground: Color.FromArgb(220, 220, 220),
        colors: new Dictionary<TokenKind, Color>
        {
            [TokenKind.Keyword] = Color.FromArgb(86, 156, 214),
            [TokenKind.String] = Color.FromArgb(206, 145, 120),
            [TokenKind.Number] = Color.FromArgb(181, 206, 168),
            [TokenKind.Comment] = Color.FromArgb(106, 153, 85),
            [TokenKind.Operator] = Color.FromArgb(180, 180, 180),
            [TokenKind.Punctuation] = Color.FromArgb(180, 180, 180),
            [TokenKind.Property] = Color.FromArgb(156, 220, 254),
            [TokenKind.Boolean] = Color.FromArgb(86, 156, 214),
            [TokenKind.Null] = Color.FromArgb(86, 156, 214),
            [TokenKind.Tag] = Color.FromArgb(197, 134, 192),
            [TokenKind.AttributeName] = Color.FromArgb(156, 220, 254),
            [TokenKind.AttributeValue] = Color.FromArgb(206, 145, 120),
            [TokenKind.Heading] = Color.FromArgb(86, 156, 214),
            [TokenKind.Emphasis] = Color.FromArgb(106, 153, 85),
        });
}
