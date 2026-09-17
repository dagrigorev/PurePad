using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;
using PurePad.Theming;
using PurePad.View.Dialogs;

namespace PurePad.View.Controls;

/// <summary>
/// A sidebar file tree rooted at a folder. Directories are populated lazily on expand (so a
/// deep tree costs nothing until opened), and activating a file raises <see cref="FileActivated"/>
/// for the host to open it. It is a passive view: it lists the filesystem and reports clicks.
/// </summary>
public sealed class FolderTreeView : UserControl
{
    private readonly TreeView _tree = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        BorderStyle = BorderStyle.None,
        ShowLines = true,
        PathSeparator = "\\",
    };

    private static readonly Color FolderAccent = Color.FromArgb(0xE8, 0xB5, 0x4D);

    private readonly ImageList _images = new() { ColorDepth = ColorDepth.Depth32Bit };
    private readonly Dictionary<string, int> _iconIndex = new(StringComparer.Ordinal);
    private readonly int _iconSize;

    private readonly ContextMenuStrip _menu = new();
    private ToolStripMenuItem _openItem = null!;
    private ToolStripMenuItem _newFileItem = null!;
    private ToolStripMenuItem _newFolderItem = null!;
    private ToolStripMenuItem _renameItem = null!;
    private ToolStripMenuItem _deleteItem = null!;
    private ToolStripMenuItem _refreshItem = null!;

    public FolderTreeView()
    {
        _iconSize = (int)Math.Round(16 * (DeviceDpi / 96.0));
        _images.ImageSize = new Size(_iconSize, _iconSize);
        _tree.ImageList = _images;

        _tree.BeforeExpand += OnBeforeExpand;
        _tree.AfterExpand += (s, e) => { if (e.Node is { } n) SetFolderIcon(n, open: true); };
        _tree.AfterCollapse += (s, e) => { if (e.Node is { } n) SetFolderIcon(n, open: false); };
        _tree.NodeMouseDoubleClick += (s, e) => Activate(e.Node);
        _tree.KeyDown += OnKeyDown;
        _tree.MouseDown += OnMouseDown;
        BuildContextMenu();
        _tree.ContextMenuStrip = _menu;
        Controls.Add(_tree);
    }

    private void BuildContextMenu()
    {
        _openItem = new ToolStripMenuItem("&Open", null, (s, e) => ActivateSelected());
        _newFileItem = new ToolStripMenuItem("New &File...", null, (s, e) => NewFile());
        _newFolderItem = new ToolStripMenuItem("New Fol&der...", null, (s, e) => NewFolder());
        _renameItem = new ToolStripMenuItem("&Rename...", null, (s, e) => RenameSelected());
        _deleteItem = new ToolStripMenuItem("&Delete", null, (s, e) => DeleteSelected());
        _refreshItem = new ToolStripMenuItem("Re&fresh", null, (s, e) => RefreshSelected());

        _menu.Items.AddRange(new ToolStripItem[]
        {
            _openItem, new ToolStripSeparator(),
            _newFileItem, _newFolderItem, new ToolStripSeparator(),
            _renameItem, _deleteItem, _refreshItem, new ToolStripSeparator(),
            new ToolStripMenuItem("&Copy Path", null, (s, e) => CopyPath()),
            new ToolStripMenuItem("Reveal in &Explorer", null, (s, e) => Reveal()),
        });

        _menu.Opening += (s, e) => ConfigureMenu();
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            _tree.SelectedNode = _tree.GetNodeAt(e.Location); // right-click selects the node under the cursor
        }
    }

    private void ConfigureMenu()
    {
        TreeNode? node = _tree.SelectedNode;
        bool isFile = node?.Tag is string f && File.Exists(f);
        bool isDir = node?.Tag is string d && Directory.Exists(d);

        _openItem.Visible = isFile;
        _newFileItem.Visible = _newFolderItem.Visible = _refreshItem.Visible = isDir;
        _renameItem.Enabled = _deleteItem.Enabled = (isFile || isDir) && node?.Parent is not null; // don't rename/delete the root
    }

    /// <summary>Swap a directory node's folder icon on expand/collapse (raw name kept in Node.Name).</summary>
    private void SetFolderIcon(TreeNode node, bool open)
    {
        if (node.Tag is string dir && Directory.Exists(dir))
        {
            string glyph = open ? MaterialIcons.FolderOpen : MaterialIcons.Folder;
            node.ImageIndex = node.SelectedImageIndex = IconIndex(open ? "do" : "dc", glyph, FolderAccent);
        }
    }

    /// <summary>Give a node its name and its unified icon (folder or category-coloured file glyph).</summary>
    private void SetFolderNode(TreeNode node, string name)
    {
        node.Name = name;
        node.Text = name;
        node.ImageIndex = node.SelectedImageIndex = IconIndex("dc", MaterialIcons.Folder, FolderAccent);
    }

    private void SetFileNode(TreeNode node, string name)
    {
        node.Name = name;
        node.Text = name;
        (string glyph, Color color) = FileGlyph(name);
        node.ImageIndex = node.SelectedImageIndex = IconIndex($"f{color.ToArgb():X8}", glyph, color);
    }

    /// <summary>Image-list index for a tinted MDI glyph, rendered once per key and cached.</summary>
    private int IconIndex(string key, string glyph, Color color)
    {
        if (_iconIndex.TryGetValue(key, out int index))
        {
            return index;
        }

        _images.Images.Add(key, FileIconRenderer.Render(glyph, color, _iconSize));
        return _iconIndex[key] = _images.Images.Count - 1;
    }

    /// <summary>The Material glyph and accent colour for a file, grouped by extension category.</summary>
    private static (string Glyph, Color Color) FileGlyph(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".cs" or ".js" or ".mjs" or ".cjs" or ".ts" or ".tsx" or ".jsx" or ".c" or ".cpp" or ".cc"
            or ".h" or ".hpp" or ".java" or ".py" or ".go" or ".rs" or ".php" or ".rb" or ".css"
            or ".sh" or ".bash" or ".ps1" or ".cmd" or ".bat" => (MaterialIcons.Code, Color.FromArgb(0x4C, 0x8D, 0xFF)),  // blue
        ".json" or ".yaml" or ".yml" or ".toml" or ".ini" or ".conf" or ".csv" => (MaterialIcons.Json, Color.FromArgb(0x3F, 0xB9, 0x50)),      // green
        ".xml" or ".html" or ".htm" or ".xaml" or ".csproj" or ".config" or ".xsd" or ".resx" or ".svg" => (MaterialIcons.Xml, Color.FromArgb(0xE3, 0x74, 0x2E)), // orange
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp" => (MaterialIcons.Image, Color.FromArgb(0xA3, 0x71, 0xF7)),       // purple
        ".md" or ".markdown" or ".txt" or ".rst" => (MaterialIcons.Markdown, Color.FromArgb(0x2A, 0xA9, 0xB5)),                                // teal
        ".sql" or ".ddl" or ".dml" or ".pgsql" or ".mysql" => (MaterialIcons.Database, Color.FromArgb(0x7C, 0x6C, 0xE0)),                      // indigo
        ".zip" or ".7z" or ".rar" or ".gz" or ".tar" => (MaterialIcons.Archive, Color.FromArgb(0xB0, 0x89, 0x68)),                             // brown
        ".exe" or ".dll" or ".sln" => (MaterialIcons.Cog, Color.FromArgb(0x8A, 0x8F, 0x98)),                                                   // gray
        _ => (MaterialIcons.FileDefault, Color.FromArgb(0x9A, 0xA0, 0xA8)),                                                                    // gray
    };

    /// <summary>Raised with the full path when a file node is activated (double-click / Enter).</summary>
    public event EventHandler<string>? FileActivated;

    /// <summary>The folder currently shown as the tree root, or null.</summary>
    public string? RootPath { get; private set; }

    /// <summary>Show <paramref name="path"/> as the root of the tree.</summary>
    public void SetRoot(string path)
    {
        RootPath = path;
        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        string label = new DirectoryInfo(path).Name;
        if (string.IsNullOrEmpty(label))
        {
            label = path; // a drive root like "C:\"
        }

        var root = new TreeNode { Tag = path };
        SetFolderNode(root, label);
        AddPlaceholder(root);
        _tree.Nodes.Add(root);
        _tree.EndUpdate();
        root.Expand();
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.ChromeBackground;
        _tree.BackColor = palette.IsDark ? ControlPaint.Light(palette.ChromeBackground, 0.03f) : Color.White;
        _tree.ForeColor = palette.ChromeForeground;
        _tree.LineColor = palette.ChromeForeground;
        if (_tree.IsHandleCreated)
        {
            NativeDarkMode.UseExplorerTheme(_tree.Handle, palette.IsDark);
        }

        if (palette.IsDark)
        {
            _menu.RenderMode = ToolStripRenderMode.Professional;
            _menu.Renderer = new ThemedToolStripRenderer(palette);
        }
        else
        {
            _menu.RenderMode = ToolStripRenderMode.System;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && _tree.SelectedNode is { } node)
        {
            Activate(node);
            e.Handled = true;
        }
    }

    private void Activate(TreeNode node)
    {
        if (node.Tag is string path && File.Exists(path))
        {
            FileActivated?.Invoke(this, path);
        }
        else if (node.Tag is string dir && Directory.Exists(dir))
        {
            node.Toggle();
        }
    }

    private void OnBeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node is { } node && IsPlaceholder(node))
        {
            Populate(node);
        }
    }

    private static bool IsPlaceholder(TreeNode node) =>
        node.Nodes.Count == 1 && node.Nodes[0].Tag is null;

    private static void AddPlaceholder(TreeNode node) => node.Nodes.Add(new TreeNode());

    /// <summary>Fill a directory node with its immediate subfolders (first) and files.</summary>
    private void Populate(TreeNode node)
    {
        node.Nodes.Clear();
        if (node.Tag is not string path)
        {
            return;
        }

        try
        {
            foreach (string dir in Directory.EnumerateDirectories(path).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                string dirName = Path.GetFileName(dir);
                var child = new TreeNode { Tag = dir };
                SetFolderNode(child, dirName);
                AddPlaceholder(child);
                node.Nodes.Add(child);
            }

            foreach (string file in Directory.EnumerateFiles(path).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileName(file);
                var fileNode = new TreeNode { Tag = file };
                SetFileNode(fileNode, fileName);
                node.Nodes.Add(fileNode);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            node.Nodes.Add(new TreeNode("(access denied)") { Tag = null, ForeColor = Color.Gray, ImageIndex = -1, SelectedImageIndex = -1 });
        }
    }

    // ----- context-menu actions ------------------------------------------

    private void ActivateSelected()
    {
        if (_tree.SelectedNode is { } node)
        {
            Activate(node);
        }
    }

    /// <summary>The tree node that represents the folder an action should target.</summary>
    private TreeNode? TargetFolderNode()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node?.Tag is string p)
        {
            return Directory.Exists(p) ? node : node.Parent;
        }

        return null;
    }

    private void NewFile()
    {
        if (TargetFolderNode() is not { Tag: string dir } folderNode)
        {
            return;
        }

        string? name = InputDialog.Ask(this, "New File", "File name:");
        if (name is null)
        {
            return;
        }

        string path = Path.Combine(dir, name);
        try
        {
            if (File.Exists(path))
            {
                MessageBox.Show(this, "A file with that name already exists.", "PurePad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            File.Create(path).Dispose();
            RefreshNode(folderNode);
            FileActivated?.Invoke(this, path); // open the newly created file
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(ex);
        }
    }

    private void NewFolder()
    {
        if (TargetFolderNode() is not { Tag: string dir } folderNode)
        {
            return;
        }

        string? name = InputDialog.Ask(this, "New Folder", "Folder name:");
        if (name is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.Combine(dir, name));
            RefreshNode(folderNode);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(ex);
        }
    }

    private void RenameSelected()
    {
        if (_tree.SelectedNode is not { Tag: string path } node || node.Parent is null)
        {
            return;
        }

        string? name = InputDialog.Ask(this, "Rename", "New name:", Path.GetFileName(path));
        if (name is null || name == Path.GetFileName(path))
        {
            return;
        }

        string target = Path.Combine(Path.GetDirectoryName(path)!, name);
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Move(path, target);
            }
            else
            {
                File.Move(path, target);
            }

            RefreshNode(node.Parent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(ex);
        }
    }

    private void DeleteSelected()
    {
        if (_tree.SelectedNode is not { Tag: string path } node || node.Parent is null)
        {
            return;
        }

        if (MessageBox.Show(this, $"Send '{Path.GetFileName(path)}' to the Recycle Bin?", "PurePad",
            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            else
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }

            RefreshNode(node.Parent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            ShowError(ex);
        }
    }

    private void RefreshSelected()
    {
        if (TargetFolderNode() is { } node)
        {
            RefreshNode(node);
        }
    }

    private void CopyPath()
    {
        if (_tree.SelectedNode?.Tag is string path)
        {
            try { Clipboard.SetText(path); } catch { /* clipboard busy */ }
        }
    }

    private void Reveal()
    {
        if (_tree.SelectedNode?.Tag is not string path)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            ShowError(ex);
        }
    }

    /// <summary>Repopulate a folder node from disk and keep it expanded.</summary>
    private void RefreshNode(TreeNode folderNode)
    {
        Populate(folderNode);
        folderNode.Expand();
    }

    private void ShowError(Exception ex) =>
        MessageBox.Show(this, ex.Message, "PurePad", MessageBoxButtons.OK, MessageBoxIcon.Error);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _menu.Dispose();
            _images.Dispose();
        }

        base.Dispose(disposing);
    }
}
