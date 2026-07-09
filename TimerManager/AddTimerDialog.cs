namespace TimerManager;

public class AddTimerDialog : Form
{
    private readonly TextBox _txtName;
    private readonly NumericUpDown _numHours;
    private readonly NumericUpDown _numMinutes;
    private readonly NumericUpDown _numSeconds;
    private readonly TextBox _txtSoundPath;
    private readonly Button _btnBrowse;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;
    private bool _normalizing;

    private readonly List<Button> _colorSwatches = [];
    private int? _selectedColorArgb;

    private const string DefaultSoundLabel = "(Standard)";

    // Palette: null = keine (zustandsabhängige Standardfarbe), sonst RGB
    private static readonly int?[] Palette =
        [null, 0xE84B4B, 0xE8862B, 0xE8C020, 0x37C341, 0x2BC0B0, 0x3B8CE8, 0x9B5BE8, 0xE85BB0];

    public string TimerName => _txtName.Text.Trim();
    public TimeSpan CountdownDuration =>
        TimeSpan.FromHours((double)_numHours.Value) +
        TimeSpan.FromMinutes((double)_numMinutes.Value) +
        TimeSpan.FromSeconds((double)_numSeconds.Value);
    public string? SoundPath => _txtSoundPath.Text == DefaultSoundLabel ? null : _txtSoundPath.Text;
    public int? AccentColorArgb => _selectedColorArgb;

    public AddTimerDialog(TimerEntry? existing = null)
    {
        Text = "Neuen Timer hinzufügen";
        Size = new Size(370, 344);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(40, 40, 40);
        ForeColor = Color.White;

        // ── NAME ──────────────────────────────────────────
        var lblName = MakeSection("NAME", new Point(16, 14));
        _txtName = new TextBox
        {
            Location = new Point(16, 33),
            Width = 326,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10),
            Text = "Timer " + DateTime.Now.ToString("HH:mm:ss")
        };

        // ── DAUER ─────────────────────────────────────────
        var lblDauer = MakeSection("DAUER", new Point(16, 72));

        _numHours = MakeSpinner(new Point(16, 92), max: 99, val: 0);
        var lblH = MakeUnit("Std", new Point(84, 97));
        _numMinutes = MakeSpinner(new Point(120, 92), max: 5999, val: 5);
        var lblM = MakeUnit("Min", new Point(188, 97));
        _numSeconds = MakeSpinner(new Point(224, 92), max: 5999, val: 0);
        var lblS = MakeUnit("Sek", new Point(292, 97));

        // Auto-Übertrag: sobald Wert > 59 sofort normalisieren
        _numMinutes.ValueChanged += (_, _) => { if (_numMinutes.Value > 59) NormalizeDuration(); };
        _numSeconds.ValueChanged += (_, _) => { if (_numSeconds.Value > 59) NormalizeDuration(); };
        // Auch beim Verlassen des Feldes normalisieren
        _numMinutes.Leave += (_, _) => NormalizeDuration();
        _numSeconds.Leave += (_, _) => NormalizeDuration();

        // ── ALARMTON ──────────────────────────────────────
        var lblAlarm = MakeSection("ALARMTON", new Point(16, 134));
        _txtSoundPath = new TextBox
        {
            Location = new Point(16, 153),
            Width = 204,
            ReadOnly = true,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.LightGray,
            Text = DefaultSoundLabel
        };
        _btnBrowse = new Button
        {
            Text = "Auswahl …",
            Location = new Point(226, 151),
            Width = 116,
            Height = 26,
            BackColor = Color.FromArgb(70, 70, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnBrowse.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Alarmton wählen",
                Filter = "Audiodateien (*.wav;*.mp3)|*.wav;*.mp3|WAV-Dateien (*.wav)|*.wav|MP3-Dateien (*.mp3)|*.mp3",
                RestoreDirectory = true
            };
            if (ofd.ShowDialog(this) is DialogResult.OK)
            {
                _txtSoundPath.Text = ofd.FileName;
                _txtSoundPath.ForeColor = Color.White;
            }
        };
        _txtSoundPath.DoubleClick += (_, _) =>
        {
            _txtSoundPath.Text = DefaultSoundLabel;
            _txtSoundPath.ForeColor = Color.LightGray;
        };

        // ── FARBE ─────────────────────────────────────────
        var lblColor = MakeSection("FARBE", new Point(16, 190));
        int sx = 16;
        foreach (var argb in Palette)
        {
            var swatch = new Button
            {
                Location = new Point(sx, 209),
                Size = new Size(30, 26),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Text = argb is null ? "–" : "",
                ForeColor = Color.White,
                BackColor = argb is null
                    ? Color.FromArgb(70, 70, 70)
                    : Color.FromArgb(unchecked((int)(0xFF000000u | (uint)argb.Value))),
                Tag = argb
            };
            swatch.FlatAppearance.BorderSize = 0;
            swatch.Click += (s, _) => SelectColor((Button)s!);
            _colorSwatches.Add(swatch);
            sx += 34;
        }

        // ── FOOTER ────────────────────────────────────────
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            BackColor = Color.FromArgb(30, 30, 30)
        };
        _btnOk = new Button
        {
            Text = "Hinzufügen",
            DialogResult = DialogResult.OK,
            Size = new Size(96, 28),
            BackColor = Color.FromArgb(40, 140, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };
        _btnCancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Size = new Size(86, 28),
            BackColor = Color.FromArgb(70, 70, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };
        footer.Controls.AddRange([_btnOk, _btnCancel]);
        footer.Resize += (_, _) =>
        {
            _btnCancel.Location = new Point(footer.ClientSize.Width - _btnCancel.Width - 10, 12);
            _btnOk.Location = new Point(_btnCancel.Left - _btnOk.Width - 6, 12);
        };

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
        Controls.AddRange([lblName, _txtName, lblDauer,
                           _numHours, lblH, _numMinutes, lblM, _numSeconds, lblS,
                           lblAlarm, _txtSoundPath, _btnBrowse, lblColor, footer]);
        Controls.AddRange(_colorSwatches.ToArray());

        SelectColor(_colorSwatches[0]);  // Standard: keine Farbe

        if (existing is not null)
        {
            Text = "Timer bearbeiten";
            _btnOk.Text = "Speichern";
            _txtName.Text = existing.Name;
            _numHours.Value = (int)existing.CountdownDuration.TotalHours;
            _numMinutes.Value = existing.CountdownDuration.Minutes;
            _numSeconds.Value = existing.CountdownDuration.Seconds;
            if (existing.SoundPath is not null)
            {
                _txtSoundPath.Text = existing.SoundPath;
                _txtSoundPath.ForeColor = Color.White;
            }
            var match = _colorSwatches.FirstOrDefault(b => (int?)b.Tag == existing.AccentColorArgb);
            SelectColor(match ?? _colorSwatches[0]);
        }
    }

    private void SelectColor(Button chosen)
    {
        _selectedColorArgb = (int?)chosen.Tag;
        foreach (var b in _colorSwatches)
        {
            bool sel = ReferenceEquals(b, chosen);
            b.FlatAppearance.BorderColor = Color.White;
            b.FlatAppearance.BorderSize = sel ? 3 : 0;
        }
    }

    private void NormalizeDuration()
    {
        if (_normalizing) return;
        _normalizing = true;
        try
        {
            long total = (long)_numHours.Value * 3600
                       + (long)_numMinutes.Value * 60
                       + (long)_numSeconds.Value;
            _numHours.Value = Math.Min(total / 3600, 99);
            _numMinutes.Value = (total % 3600) / 60;
            _numSeconds.Value = total % 60;
        }
        finally { _normalizing = false; }
    }

    private static NumericUpDown MakeSpinner(Point loc, int max, int val)
    {
        var spinner = new NumericUpDown
        {
            Location = loc,
            Width = 62,
            Minimum = 0,
            Maximum = max,
            Value = val,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White
        };
        spinner.Enter += (_, _) => spinner.Select(0, spinner.Text.Length);
        spinner.Click += (_, _) => spinner.Select(0, spinner.Text.Length);
        return spinner;
    }

    private static Label MakeSection(string text, Point loc) => new()
    {
        Text = text,
        Location = loc,
        AutoSize = true,
        Font = new Font("Segoe UI", 8, FontStyle.Bold),
        ForeColor = Color.FromArgb(100, 160, 230)
    };

    private static Label MakeUnit(string text, Point loc) => new()
    {
        Text = text,
        Location = loc,
        AutoSize = true,
        ForeColor = Color.LightGray
    };

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (DialogResult is DialogResult.OK && string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Bitte einen Namen eingeben.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
            e.Cancel = true;
        }
        if (DialogResult is DialogResult.OK && CountdownDuration == TimeSpan.Zero)
        {
            MessageBox.Show("Bitte eine Countdown-Dauer > 0 angeben.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
            e.Cancel = true;
        }
    }
}
