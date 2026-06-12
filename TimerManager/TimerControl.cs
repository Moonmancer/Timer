namespace TimerManager;

using System.Drawing.Drawing2D;

public class TimerControl : Panel
{
    private readonly TimerEntry _entry;
    private readonly Label _lblName;
    private readonly Label _lblTime;
    private readonly Label _lblState;
    private readonly Button _btnStartPause;
    private readonly Button _btnReset;
    private readonly Button _btnEdit;
    private readonly Button _btnRemove;

    public event EventHandler? RemoveRequested;
    public event EventHandler? EditRequested;

    public TimerEntry Entry => _entry;

    private TimerState _prevState = TimerState.Stopped;

    // Set to true while the user is dragging this card
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsDragging { get; set; }

    private Color _cardColor = BgStopped;
    private Color _accentColor = AccentStopped;

    // Card background colours per state
    private static readonly Color BgStopped = Color.FromArgb(38, 38, 46);
    private static readonly Color BgRunning = Color.FromArgb(16, 40, 18);
    private static readonly Color BgPaused = Color.FromArgb(44, 34, 12);
    private static readonly Color BgFinished = Color.FromArgb(50, 16, 16);

    // Left accent strip colours per state
    private static readonly Color AccentStopped = Color.FromArgb(80, 80, 105);
    private static readonly Color AccentRunning = Color.FromArgb(40, 195, 65);
    private static readonly Color AccentPaused = Color.FromArgb(215, 155, 20);
    private static readonly Color AccentFinished = Color.FromArgb(215, 50, 50);

    public TimerControl(TimerEntry entry)
    {
        _entry = entry;
        DoubleBuffered = true;

        Height = 92;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(14, 8, 10, 8);
        BackColor = BgStopped;

        _lblName = new Label
        {
            Text = entry.Name,
            ForeColor = Color.FromArgb(200, 200, 215),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoSize = true,
            BackColor = BgStopped,
            Location = new Point(18, 8)
        };

        _lblTime = new Label
        {
            Text = "00:00:00",
            ForeColor = Color.White,
            Font = new Font("Consolas", 22, FontStyle.Bold),
            AutoSize = true,
            BackColor = BgStopped,
            Location = new Point(14, 27)
        };

        _lblState = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(145, 145, 165),
            Font = new Font("Segoe UI", 8),
            AutoSize = true,
            BackColor = BgStopped,
            Location = new Point(18, 70)
        };

        _btnStartPause = CreateButton("", Color.FromArgb(35, 160, 50));
        _btnStartPause.Click += (_, _) => OnStartPause();

        _btnReset = CreateButton("", Color.FromArgb(58, 58, 72));
        _btnReset.Click += (_, _) => { _entry.Reset(); Update(); };
        AddHoverEffect(_btnReset, Color.FromArgb(58, 58, 72), Color.FromArgb(82, 82, 100));

        _btnEdit = CreateButton("", Color.FromArgb(40, 82, 152));
        _btnEdit.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);
        AddHoverEffect(_btnEdit, Color.FromArgb(40, 82, 152), Color.FromArgb(58, 112, 195));

        _btnRemove = CreateButton("", Color.FromArgb(152, 36, 36));
        _btnRemove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
        AddHoverEffect(_btnRemove, Color.FromArgb(152, 36, 36), Color.FromArgb(195, 52, 52));

        Controls.AddRange([_lblName, _lblTime, _lblState, _btnStartPause, _btnReset, _btnEdit, _btnRemove]);
        Resize += (_, _) => LayoutButtons();
        LayoutButtons();
    }

    /// <summary>Registers drag-related mouse handlers on this card and its non-button children.</summary>
    public void RegisterDragHandlers(MouseEventHandler down, MouseEventHandler move, MouseEventHandler up)
    {
        MouseDown += down;
        MouseMove += move;
        MouseUp += up;
        foreach (Control c in Controls)
        {
            if (c is Button) continue; // buttons handle their own clicks
            c.MouseDown += down;
            c.MouseMove += move;
            c.MouseUp += up;
        }
    }

    private static Button CreateButton(string text, Color backColor)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe MDL2 Assets", 11),
            Height = 30,
            Width = 36,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0),
            UseCompatibleTextRendering = false
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private static void AddHoverEffect(Button btn, Color normal, Color hover)
    {
        btn.MouseEnter += (_, _) => btn.BackColor = hover;
        btn.MouseLeave += (_, _) => btn.BackColor = normal;
    }

    private void LayoutButtons()
    {
        int right = Width - 10;
        const int btnY = 8;
        _btnRemove.Location = new Point(right - _btnRemove.Width, btnY);
        _btnEdit.Location = new Point(right - _btnRemove.Width - _btnEdit.Width - 4, btnY);
        _btnReset.Location = new Point(right - _btnRemove.Width - _btnEdit.Width - _btnReset.Width - 10, btnY);
        _btnStartPause.Location = new Point(right - _btnRemove.Width - _btnEdit.Width - _btnReset.Width - _btnStartPause.Width - 16, btnY);
    }

    // Paint parent background colour in corners to fake rounded transparency
    protected override void OnPaintBackground(PaintEventArgs e)
        => e.Graphics.Clear(Parent?.BackColor ?? Color.FromArgb(28, 28, 28));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var rect = new RectangleF(0, 0, Width - 1f, Height - 1f);
        using var path = BuildRoundedPath(rect, 10f);

        // Card background
        using (var bg = new SolidBrush(_cardColor))
            g.FillPath(bg, path);

        // Left accent strip (clipped to rounded rect)
        g.SetClip(path);
        using (var ab = new SolidBrush(_accentColor))
            g.FillRectangle(ab, 0, 0, 5, Height);

        g.ResetClip();

        // Drag highlight: bright overlay + glow border
        if (IsDragging)
        {
            // Semi-transparent white overlay to lighten the card
            using (var overlay = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                g.FillPath(overlay, path);

            // Outer glow (wider, dimmer)
            using var glowPen = new Pen(Color.FromArgb(60, 255, 255, 255), 6f);
            g.DrawPath(glowPen, path);

            // Inner bright border
            using var borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 2f);
            g.DrawPath(borderPen, path);
        }
        base.OnPaint(e);
    }

    private static GraphicsPath BuildRoundedPath(RectangleF r, float radius)
    {
        float d = radius * 2f;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private void OnStartPause()
    {
        if (_entry.State is TimerState.Running)
            _entry.Pause();
        else
        {
            if (_entry.State is TimerState.Finished)
                _entry.Reset();
            _entry.Start();
        }
        Update();
    }

    public new void Update()
    {
        if (_prevState is TimerState.Running && _entry.State is TimerState.Finished)
            PlayAlarm();
        _prevState = _entry.State;

        _lblTime.Text = _entry.GetDisplay().ToString(@"hh\:mm\:ss");

        switch (_entry.State)
        {
            case TimerState.Running:
                _cardColor = BgRunning; _accentColor = AccentRunning;
                _btnStartPause.Text = ""; _btnStartPause.BackColor = Color.FromArgb(180, 130, 20);
                _lblState.Text = "Läuft";
                break;
            case TimerState.Paused:
                _cardColor = BgPaused; _accentColor = AccentPaused;
                _btnStartPause.Text = ""; _btnStartPause.BackColor = Color.FromArgb(35, 160, 50);
                _lblState.Text = "Pausiert";
                break;
            case TimerState.Finished:
                _cardColor = BgFinished; _accentColor = AccentFinished;
                _btnStartPause.Text = ""; _btnStartPause.BackColor = Color.FromArgb(35, 160, 50);
                _lblState.Text = " Fertig!";
                break;
            default:
                _cardColor = BgStopped; _accentColor = AccentStopped;
                _btnStartPause.Text = ""; _btnStartPause.BackColor = Color.FromArgb(35, 160, 50);
                _lblState.Text = "";
                break;
        }

        // Sync label backgrounds to card colour (prevents artefacts near rounded corners)
        _lblName.BackColor = _cardColor;
        _lblTime.BackColor = _cardColor;
        _lblState.BackColor = _cardColor;

        Invalidate();
    }

    public void RefreshAfterEdit()
    {
        _lblName.Text = _entry.Name;
        Update();
    }

    private static void PlayAlarm(string? soundPath)
    {
        if (soundPath is not null && File.Exists(soundPath))
            Task.Run(() => { using var p = new System.Media.SoundPlayer(soundPath); p.PlaySync(); });
        else
            System.Media.SystemSounds.Exclamation.Play();
    }

    private void PlayAlarm() => PlayAlarm(_entry.SoundPath);
}