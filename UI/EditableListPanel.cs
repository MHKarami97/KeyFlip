namespace KeyFlip.UI;

/// <summary>Dark list with an input box, an add button and a remove button.</summary>
public sealed class EditableListPanel : Panel
{
    private readonly float _scale;
    private readonly Func<IReadOnlyList<string>> _load;
    private readonly Action<string> _add;
    private readonly Action<string> _remove;
    private readonly ListBox _list = new();
    private readonly ComboBox _input = new();
    private readonly Font _listFont = Theme.Create(10);

    public EditableListPanel(
        string title,
        int width,
        float scale,
        Func<IReadOnlyList<string>> load,
        Action<string> add,
        Action<string> remove,
        IEnumerable<string>? suggestions = null)
    {
        _scale = scale;
        _load = load;
        _add = add;
        _remove = remove;

        BackColor = Theme.Background;
        Size = new Size(S(width), S(170));

        Controls.Add(new Label
        {
            Text = title,
            ForeColor = Theme.Muted,
            Font = Theme.Create(9),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(0, 0, S(width), S(22))
        });

        _list.BorderStyle = BorderStyle.None;
        _list.BackColor = Theme.Surface;
        _list.ForeColor = Theme.Text;
        _list.Font = _listFont;
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.ItemHeight = S(26);
        _list.IntegralHeight = false;
        _list.Bounds = new Rectangle(0, S(26), S(width), S(104));
        _list.DrawItem += OnDrawItem;
        Controls.Add(_list);

        _input.FlatStyle = FlatStyle.Flat;
        _input.BackColor = Theme.Surface;
        _input.ForeColor = Theme.Text;
        _input.Font = _listFont;
        _input.Location = new Point(S(142), S(138));
        _input.Width = S(width) - S(142);
        if (suggestions is not null)
        {
            _input.Items.AddRange(suggestions.Cast<object>().ToArray());
        }

        Controls.Add(_input);

        var remover = new ActionButton("حذف", scale, Theme.Danger) { Bounds = new Rectangle(0, S(137), S(64), S(30)) };
        remover.Click += (_, _) => RemoveSelected();
        Controls.Add(remover);

        var adder = new ActionButton("افزودن", scale, Theme.Accent) { Bounds = new Rectangle(S(70), S(137), S(64), S(30)) };
        adder.Click += (_, _) => AddInput();
        Controls.Add(adder);

        Reload();
    }

    public void Reload()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        _list.Items.AddRange(_load().Cast<object>().ToArray());
        _list.EndUpdate();
    }

    public void SetSuggestions(IEnumerable<string> suggestions)
    {
        _input.BeginUpdate();
        _input.Items.Clear();
        _input.Items.AddRange(suggestions.Cast<object>().ToArray());
        _input.EndUpdate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _listFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AddInput()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        _add(text);
        _input.Text = string.Empty;
        Reload();
    }

    private void RemoveSelected()
    {
        if (_list.SelectedItem is string selected)
        {
            _remove(selected);
            Reload();
        }
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? Color.FromArgb(70, Theme.Accent) : Theme.Surface);
        e.Graphics.FillRectangle(background, e.Bounds);

        var text = _list.Items[e.Index]?.ToString() ?? string.Empty;
        var bounds = Rectangle.Inflate(e.Bounds, -S(8), 0);
        TextRenderer.DrawText(e.Graphics, text, _listFont, bounds, Theme.Text,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
    }

    private int S(int value) => (int)Math.Round(value * _scale);
}
