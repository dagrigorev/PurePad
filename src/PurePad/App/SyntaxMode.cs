namespace PurePad.App;

/// <summary>How the editor decides which language to colourise with.</summary>
public enum SyntaxMode
{
    /// <summary>Pick the language from the file extension.</summary>
    Auto,

    /// <summary>Colourising is switched off (plain text).</summary>
    Off,

    /// <summary>A specific language chosen by the user, overriding the extension.</summary>
    Forced,
}
