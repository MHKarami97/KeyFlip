using KeyFlip.Core;

namespace KeyFlip.UI;

/// <summary>Editor for the key-to-character table: one row per key, English and Persian columns.</summary>
public sealed class KeyMapForm : Form
{
    private const int FormWidth = 360;
    private new const int Margin = 16;
    private const int RowHeight = 34;

    private readonly AppSettings _settings;
    private readonly Action _onChanged;
    private readonly KeyMapBuilder _builder = new();
    private readonly float _scale;
    private readonly List<KeyRow> _rows = new();
    private readonly Panel _rowsPanel = new();
    private ToggleSwitch? _autoDetect;

    public KeyMapForm(AppSettings settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;
        _scale = GetScale();

        Text = "KeyFlip - نگاشت کلیدها";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Create(10);
        ClientSize = new Size(S(FormWidth), S(600));
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        BuildAutoDetectRow();
        BuildHint();
        BuildHeader();
        BuildRowsPanel();
        BuildButtons();
        LoadRows();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Dwm.UseDarkTitleBar(Handle);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (Visible)
        {
            _autoDetect!.Checked = _settings.AutoDetectKeyMap;
            LoadRows();
        }

        base.OnVisibleChanged(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }

    private void BuildAutoDetectRow()
    {
        Controls.Add(new Label
        {
            Text = "تشخیص خودکار از چیدمان فارسی ویندوز",
            ForeColor = Theme.Text,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(S(Margin) + S(52), S(14), S(FormWidth - Margin * 2 - 52), S(30))
        });

        _autoDetect = new ToggleSwitch
        {
            Checked = _settings.AutoDetectKeyMap,
            BackColor = Theme.Background,
            Size = new Size(S(40), S(22)),
            Location = new Point(S(Margin), S(18))
        };
        _autoDetect.CheckedChanged += (_, _) =>
        {
            _settings.AutoDetectKeyMap = _autoDetect.Checked;
            _onChanged();
            LoadRows();
        };
        Controls.Add(_autoDetect);
    }

    private void BuildHint()
    {
        Controls.Add(new Label
        {
            Text = "هر ردیف یک کلید است. ستون چپ حرف انگلیسی و ستون راست حرف فارسی همان کلید را نشان می‌دهد. ستون فارسی را خالی بگذارید تا کلید نادیده گرفته شود.",
            ForeColor = Theme.Muted,
            Font = Theme.Create(9),
            AutoSize = false,
            TextAlign = ContentAlignment.TopRight,
            Bounds = new Rectangle(S(Margin), S(50), S(FormWidth - Margin * 2), S(52))
        });
    }

    private void BuildHeader()
    {
        AddHeaderLabel("کلید", 0);
        AddHeaderLabel("English", 1);
        AddHeaderLabel("فارسی", 2);
    }

    private void AddHeaderLabel(string text, int column) =>
        Controls.Add(new Label
        {
            Text = text,
            ForeColor = Theme.Muted,
            Font = Theme.Create(9),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(S(Margin) + ColumnX(column), S(106), ColumnWidth(column), S(22))
        });

    private void BuildRowsPanel()
    {
        _rowsPanel.BackColor = Theme.Background;
        _rowsPanel.AutoScroll = true;
        _rowsPanel.Bounds = new Rectangle(S(Margin), S(130), S(FormWidth - Margin * 2) + S(8), S(400));
        Controls.Add(_rowsPanel);

        var top = 0;
        foreach (var key in KeyMapBuilder.CandidateKeys)
        {
            var label = new Label
            {
                Text = KeyMapBuilder.DescribeKey(key),
                ForeColor = Theme.Text,
                Font = Theme.Create(10, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(ColumnX(0), top, ColumnWidth(0), S(RowHeight) - S(4))
            };
            var english = CreateBox(1, top);
            var persian = CreateBox(2, top);
            _rowsPanel.Controls.Add(label);
            _rowsPanel.Controls.Add(english);
            _rowsPanel.Controls.Add(persian);
            _rows.Add(new KeyRow(key, english, persian));
            top += S(RowHeight);
        }
    }

    private TextBox CreateBox(int column, int top) => new()
    {
        MaxLength = 1,
        TextAlign = HorizontalAlignment.Center,
        BackColor = Theme.Surface,
        ForeColor = Theme.Text,
        BorderStyle = BorderStyle.FixedSingle,
        Font = Theme.Create(11),
        Bounds = new Rectangle(ColumnX(column) + S(20), top, ColumnWidth(column) - S(40), S(RowHeight) - S(4))
    };

    private void BuildButtons()
    {
        var width = (S(FormWidth - Margin * 2) - S(16)) / 3;
        AddButton("ذخیره", Theme.Accent, 2, width, Save);
        AddButton("بازنشانی", Theme.Danger, 1, width, Reset);
        AddButton("بستن", Theme.Muted, 0, width, Hide);
    }

    private void AddButton(string text, Color accent, int index, int width, Action action)
    {
        var button = new ActionButton(text, _scale, accent)
        {
            BackColor = Theme.Background,
            Bounds = new Rectangle(S(Margin) + index * (width + S(8)), S(544), width, S(40))
        };
        button.Click += (_, _) => action();
        Controls.Add(button);
    }

    private void LoadRows()
    {
        var table = _builder.Build(_settings);
        foreach (var row in _rows)
        {
            if (table.TryGetValue(row.Key, out var entry))
            {
                row.English.Text = entry.English.ToString();
                row.Persian.Text = entry.Persian.ToString();
            }
            else
            {
                row.English.Text = KeyMapBuilder.FallbackEnglish(row.Key).ToString();
                row.Persian.Text = string.Empty;
            }
        }
    }

    private void Save()
    {
        var baseTable = _builder.BuildBase(_settings.AutoDetectKeyMap);
        var persianOverrides = new Dictionary<int, string>();
        var englishOverrides = new Dictionary<int, string>();

        foreach (var row in _rows)
        {
            var hasBase = baseTable.TryGetValue(row.Key, out var baseEntry);
            var basePersian = hasBase ? baseEntry.Persian.ToString() : string.Empty;
            var baseEnglish = hasBase ? baseEntry.English.ToString() : string.Empty;
            var persian = row.Persian.Text.Trim();
            var english = row.English.Text.Trim();

            if (persian != basePersian)
            {
                persianOverrides[row.Key] = persian;
            }

            if (english.Length > 0 && english != baseEnglish && (hasBase || persian.Length > 0))
            {
                englishOverrides[row.Key] = english;
            }
        }

        _settings.PersianKeyOverrides = persianOverrides;
        _settings.EnglishKeyOverrides = englishOverrides;
        _onChanged();
        LoadRows();
    }

    private void Reset()
    {
        _settings.PersianKeyOverrides = new Dictionary<int, string>();
        _settings.EnglishKeyOverrides = new Dictionary<int, string>();
        _onChanged();
        LoadRows();
    }

    private int ColumnX(int column) => column switch
    {
        0 => 0,
        1 => S(100),
        _ => S(210)
    };

    private int ColumnWidth(int column) => column == 0 ? S(90) : S(100);

    private static float GetScale()
    {
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        return graphics.DpiX / 96f;
    }

    private int S(int value) => (int)Math.Round(value * _scale);

    private sealed record KeyRow(int Key, TextBox English, TextBox Persian);
}
