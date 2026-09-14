using System.Text;
using PurePad.Domain;

namespace PurePad.Tests;

public sealed class TextDocumentTests
{
    [Fact]
    public void New_document_is_untitled_and_clean()
    {
        var document = new TextDocument();

        Assert.Equal("Untitled", document.DisplayName);
        Assert.False(document.HasPath);
        Assert.False(document.IsModified);
        Assert.Equal(string.Empty, document.Extension);
    }

    [Fact]
    public void MarkSaved_records_path_encoding_and_clears_modified()
    {
        var document = new TextDocument { IsModified = true };
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        document.MarkSaved(@"C:\work\notes.json", encoding);

        Assert.True(document.HasPath);
        Assert.Equal("notes.json", document.DisplayName);
        Assert.Equal(".json", document.Extension);
        Assert.Same(encoding, document.Encoding);
        Assert.False(document.IsModified);
    }

    [Fact]
    public void Extension_is_lower_cased()
    {
        var document = new TextDocument();
        document.MarkSaved(@"C:\work\DATA.JSON", Encoding.UTF8);

        Assert.Equal(".json", document.Extension);
    }

    [Fact]
    public void Changed_fires_when_modified_flag_flips()
    {
        var document = new TextDocument();
        int raised = 0;
        document.Changed += (s, e) => raised++;

        document.IsModified = true;
        document.IsModified = true; // no change, should not re-raise

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Reset_returns_to_untitled_clean_state()
    {
        var document = new TextDocument();
        document.MarkSaved(@"C:\work\a.txt", Encoding.UTF8);
        document.IsModified = true;

        document.Reset();

        Assert.False(document.HasPath);
        Assert.Equal("Untitled", document.DisplayName);
        Assert.False(document.IsModified);
    }
}
