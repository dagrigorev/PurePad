using System.Text;
using PurePad.LargeFiles;

namespace PurePad.Tests;

/// <summary>
/// End-to-end coverage of the editable large-file path: a real file is memory-mapped and
/// char-indexed, edited through a <see cref="PieceTable"/>, and streamed back out.
/// </summary>
public sealed class LargeFileEditingTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"purepad-large-{Guid.NewGuid():N}.txt");

    private async Task<LargeFileDocument> OpenAsync(string content)
    {
        File.WriteAllText(_path, content, new UTF8Encoding(false));
        return await LargeFileDocument.OpenAsync(_path, progress: null, CancellationToken.None);
    }

    [Fact]
    public async Task Document_is_char_indexed_and_editable()
    {
        using LargeFileDocument doc = await OpenAsync("alpha\nbeta\ngamma");

        Assert.True(doc.EditingSupported);
        Assert.Equal(3, doc.LineCount);
        Assert.Equal("alpha", doc.GetLine(0));
        Assert.Equal("gamma", doc.GetLine(2));
        Assert.Equal("alpha\nbeta\ngamma".Length, doc.CharLength);
    }

    [Fact]
    public async Task PieceTable_over_mapped_original_reads_lines()
    {
        using LargeFileDocument doc = await OpenAsync("one\ntwo\nthree");
        var table = new PieceTable(new MappedOriginalText(doc), doc.CharLineStarts());

        Assert.Equal(3, table.LineCount);
        Assert.Equal("two", table.GetLine(1));
        Assert.Equal("one\ntwo\nthree", table.GetAllText());
    }

    [Fact]
    public async Task Edit_and_stream_save_round_trips()
    {
        using LargeFileDocument doc = await OpenAsync("Hello world\nsecond line");
        var table = new PieceTable(new MappedOriginalText(doc), doc.CharLineStarts());

        table.Insert(5, ",");                 // "Hello, world"
        table.Delete(0, 0);                   // no-op guard
        table.Insert(table.Length, "\nthird"); // append a line

        var writer = new StringWriter();
        table.WriteTo(writer);

        Assert.Equal("Hello, world\nsecond line\nthird", writer.ToString());
        Assert.Equal(3, table.LineCount);
    }

    [Fact]
    public async Task Reading_across_original_and_edits_by_char_range_is_correct()
    {
        using LargeFileDocument doc = await OpenAsync("abcdefghij");
        var table = new PieceTable(new MappedOriginalText(doc), doc.CharLineStarts());

        table.Insert(5, "-XYZ-"); // abcde-XYZ-fghij

        Assert.Equal("abcde-XYZ-fghij", table.GetAllText());
        Assert.Equal("e-XYZ-f", table.GetText(4, 7));
    }

    public void Dispose()
    {
        try { File.Delete(_path); } catch { /* best effort */ }
    }
}
