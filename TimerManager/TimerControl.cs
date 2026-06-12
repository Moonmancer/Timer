namespace TimerManager;

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

    private static readonly Color ColorStopped = Color.FromArgb(60, 60, 60);
    private static readonly Color ColorRunning = Color.FromArgb(30, 100, 30);
    private static readonly Color ColorPaused = Color.FromArgb(100, 80, 20);
    private static readonly Color ColorFinished = Color.FromArgb(120, 30, 30);

    public TimerControl(TimerEntry entry)
    {
        _entry = entry;

        Height = 80;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 6);
        Padding = new Padding(10, 8, 10, 8);
        BackColor = ColorStopped;

        _lblName = new Label
        {
            Text = entry.Name,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 8)
        };

        _lblTime = new Label
        {
            Text = "00:00:00",
            ForeColor = Color.White,
            Font = new Font("Consolas", 22, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 28)
        };

        _lblState = new Label
        {
            Text = "Gestoppt",
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 8),
            AutoSize = true,
            Location = new Point(14, 62)
        };

        _btnStartPause = CreateButton("", Color.FromArgb(40, 160, 40));
        _btnStartPause.Width = 36;
        _btnStartPause.Click += (_, _) => OnStartPause();

        _btnReset = CreateButton("", Color.FromArgb(80, 80, 80));
        _btnReset.Width = 36;
        _btnReset.Click += (_, _) => { _entry.Reset(); Update(); };

        _btnEdit = CreateButton("", Color.FromArgb(50, 90, 160));
        _btnEdit.Width = 36;
        _btnEdit.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);

        _btnRemove = CreateButton("", Color.FromArgb(160, 40, 40));
        _btnRemove.Width = 36;
        _btnRemove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);

        Controls.AddRange([_lblName, _lblTime, _lblState, _btnStartPause, _btnReset, _btnEdit, _btnRemove]);
        Resize += (_, _) => LayoutButtons();
        LayoutButtons();
    }

    private static Button CreateButton(string text, Color backColor) => new()
    {
        Text = text,
        BackColor = backColor,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe MDL2 Assets", 11),
        Height = 28,
        Width = 90,
        Cursor = Cursors.Hand,
        TextAlign = ContentAlignment.MiddleCenter,
        Padding = new Padding(0),
        UseCompatibleTextRendering = false
    };

    private void LayoutButtons()
    {
        int right = Width - 10;
        _btnRemove.Location = new Point(right - _btnRemove.Width, 8);
        _btnEdit.Location = new Point(right - _btnRemove.Width - _btnEdit.Width - 4, 8);
        _btnReset.Location = new Point(right - _btnRemove.Width - _btnEdit.Width - _btnReset.Width - 10, 8);
        _btnStartPause.Location = new Point(right - _btnRemove.Width - _btnEdit.Width - _btnReset.Width - _btnStartPause.Width - 16, 8);
    }

    private void OnStartPause()
    {
        if (_entry.State is TimerState.Running)
            _entry.Pause();
        else
            _entry.Start();
        Update();
    }

    public new void Update()
    {
        var display = _entry.GetDisplay();

        if (_prevState is TimerState.Running && _entry.State is TimerState.Finished)
            PlayAlarm();
        _prevState = _entry.State;
        _lblTime.Text = display.ToString(@"hh\:mm\:ss");

        switch (_entry.State)
        {
            case TimerState.Running:
                BackColor = ColorRunning;
                _btnStartPause.Text = "";
                _btnStartPause.BackColor = Color.FromArgb(180, 130, 20);
                _lblState.Text = "";
                break;
            case TimerState.Paused:
                BackColor = ColorPaused;
                _btnStartPause.Text = "";
                _btnStartPause.BackColor = Color.FromArgb(40, 160, 40);
                _lblState.Text = "Pausiert";
                break;
            case TimerState.Finished:
                BackColor = ColorFinished;
                _btnStartPause.Text = "";
                _btnStartPause.BackColor = Color.FromArgb(40, 160, 40);
                _lblState.Text = " Fertig!";
                break;
            default:
                BackColor = ColorStopped;
                _btnStartPause.Text = "";
                _btnStartPause.BackColor = Color.FromArgb(40, 160, 40);
                _lblState.Text = "";
                break;
        }
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
