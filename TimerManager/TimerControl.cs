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
    private bool _blinkOn = false;
    private int _blinkTick = 0;          // zählt Ticks für 500ms-Blinkintervall
    private DateTime _lastAlarmAt = DateTime.MinValue;  // für 5s-Alarm-Wiederholung

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsDragging { get; set; }

    private Color _cardColor = BgStopped;
    private Color _accentColor = AccentStopped;

    private static readonly Color BgStopped = Color.FromArgb(38, 38, 46);
    private static readonly Color BgRunning = Color.FromArgb(16, 40, 18);
    private static readonly Color BgPaused = Color.FromArgb(44, 34, 12);
    private static readonly Color BgFinished = Color.FromArgb(50, 16, 16);

    private static readonly Color AccentStopped = Color.FromArgb(80, 80, 105);
    private static readonly Color AccentRunning = Color.FromArgb(40, 195, 65);
    private static readonly Color AccentPaused = Color.FromArgb(215, 155, 20);
    private static readonly Color AccentFinished = Color.FromArgb(215, 50, 50);

    // Button block total width (4 buttons x 36px + 3 gaps x 4px)
    private const int BtnBlockWidth = 4 * 36 + 3 * 4;

    public TimerControl(TimerEntry entry)
    {
        _entry = entry;
        DoubleBuffered = true;

        Height = 100;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 8);
        BackColor = BgStopped;

        // Row 1 – Name (left, truncated) + State (right)
        _lblName = new Label
        {
            Text = entry.Name,
            ForeColor = Color.FromArgb(200, 200, 215),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoSize = false,
            AutoEllipsis = true,
            BackColor = BgStopped,
            Location = new Point(14, 8),
            Height = 18
        };

        _lblState = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(145, 145, 165),
            Font = new Font("Segoe UI", 8),
            AutoSize = false,
            BackColor = BgStopped,
            Height = 18,
            TextAlign = ContentAlignment.MiddleRight
        };

        // Row 2 – large time display
        _lblTime = new Label
        {
            Text = "00:00:00",
            ForeColor = Color.White,
            Font = new Font("Consolas", 22, FontStyle.Bold),
            AutoSize = true,
            BackColor = BgStopped,
            Location = new Point(14, 28)
        };

        // Row 3 – buttons (bottom-left)
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
        Resize += (_, _) => DoLayout();
        DoLayout();
    }

    public void RegisterDragHandlers(MouseEventHandler down, MouseEventHandler move, MouseEventHandler up)
    {
        MouseDown += down;
        MouseMove += move;
        MouseUp += up;
        foreach (Control c in Controls)
        {
            if (c is Button) continue;
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
            Height = 28,
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

    private void DoLayout()
    {
        const int left = 14;
        const int right = 10;
        const int row1Y = 8;
        const int row3Y = 64;

        int stateW = 70;  // fixed width for state label
        int nameW = Width - left - stateW - right - 4;

        // Row 1: Name left, State right
        _lblName.Location = new Point(left, row1Y);
        _lblName.Width = Math.Max(20, nameW);

        _lblState.Width = stateW;
        _lblState.Location = new Point(Width - right - stateW, row1Y);

        // Row 3: buttons bottom-left
        int x = left;
        foreach (var btn in new[] { _btnStartPause, _btnReset, _btnEdit, _btnRemove })
        {
            btn.Location = new Point(x, row3Y);
            x += btn.Width + 4;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
        => e.Graphics.Clear(Parent?.BackColor ?? Color.FromArgb(28, 28, 28));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var rect = new RectangleF(0, 0, Width - 1f, Height - 1f);
        using var path = BuildRoundedPath(rect, 10f);

        using (var bg = new SolidBrush(_cardColor))
            g.FillPath(bg, path);

        g.SetClip(path);
        using (var ab = new SolidBrush(_accentColor))
            g.FillRectangle(ab, 0, 0, 5, Height);
        g.ResetClip();

        if (IsDragging)
        {
            using (var overlay = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                g.FillPath(overlay, path);
            using var glowPen = new Pen(Color.FromArgb(60, 255, 255, 255), 6f);
            using var borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 2f);
            g.DrawPath(glowPen, path);
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
        // GetDisplay() zuerst aufrufen – setzt ggf. State auf Finished
        var displayTime = _entry.GetDisplay();
        // Zeitanzeige: bei Finished wird sie im switch-case mit Overtime überschrieben
        if (_entry.State is not TimerState.Finished)
            _lblTime.Text = displayTime.ToString(@"hh\:mm\:ss");

        if (_prevState is TimerState.Running && _entry.State is TimerState.Finished)
        {
            PlayAlarm();
            _lastAlarmAt = DateTime.Now;
        }
        else if (_entry.State is TimerState.Finished
                 && (DateTime.Now - _lastAlarmAt).TotalMilliseconds >= 5000)
        {
            PlayAlarm();
            _lastAlarmAt = DateTime.Now;
        }
        _prevState = _entry.State;

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
                // Blinken alle 500ms (jeden 2. Tick bei 250ms-Takt)
                _blinkTick++;
                if (_blinkTick >= 2) { _blinkOn = !_blinkOn; _blinkTick = 0; }
                _cardColor = _blinkOn ? BgFinished : BgStopped;
                _accentColor = AccentFinished;
                _btnStartPause.Text = ""; _btnStartPause.BackColor = Color.FromArgb(35, 160, 50);
                _lblState.Text = "Fertig!";
                // Überschreitungszeit anzeigen
                var overtime = _entry.GetOvertime();
                _lblTime.Text = "+" + overtime.ToString(@"hh\:mm\:ss");
                break;
            default:
                _blinkOn = false;
                _blinkTick = 0;
                _cardColor = BgStopped; _accentColor = AccentStopped;
                _btnStartPause.Text = ""; _btnStartPause.BackColor = Color.FromArgb(35, 160, 50);
                _lblState.Text = "";
                break;
        }

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
        // Audio auf separatem STA-Thread starten (nicht blockierend für UI)
        var t = new System.Threading.Thread(() =>
        {
            try
            {
                if (soundPath is not null && File.Exists(soundPath))
                {
                    if (soundPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                        NativeMethods.MciPlay(soundPath);
                    else
                    {
                        using var p = new System.Media.SoundPlayer(soundPath);
                        p.PlaySync();
                    }
                    return;
                }

                // Eingebettete timer.mp3 extrahieren und per MCI abspielen
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var resName = asm.GetManifestResourceNames()
                                 .FirstOrDefault(n => n.EndsWith("timer.mp3", StringComparison.OrdinalIgnoreCase));
                if (resName is not null)
                {
                    var tmp = Path.Combine(Path.GetTempPath(), "TimerManager_alarm.mp3");
                    using (var src = asm.GetManifestResourceStream(resName)!)
                    using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                        src.CopyTo(fs);

                    if (!NativeMethods.MciPlay(tmp))
                        goto fallback; // MCI konnte Datei nicht öffnen
                    return;
                }

            fallback:
                // Letzter Fallback: synthetisch erzeugter WAV-Beep
                using (var ms = new System.IO.MemoryStream(SoundGenerator.CreateTimerBeep()))
                using (var p = new System.Media.SoundPlayer(ms))
                    p.PlaySync();
            }
            catch { /* Audiofehler sollen die App nicht zum Absturz bringen */ }
        });
        t.SetApartmentState(System.Threading.ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    private void PlayAlarm() => PlayAlarm(_entry.SoundPath);
}