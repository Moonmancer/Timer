namespace TimerManager;

public class MainForm : Form
{
    private readonly Panel _timerListPanel;
    private readonly Button _btnAdd;
    private readonly System.Windows.Forms.Timer _globalTick;
    private readonly List<TimerControl> _timerControls = [];
    private readonly Label _lblEmpty;

    public MainForm()
    {
        Text = "Timer Manager";
        Size = new Size(480, 600);
        MinimumSize = new Size(200, 150);
        BackColor = Color.FromArgb(28, 28, 28);
        ForeColor = Color.White;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        // Toolbar
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Color.FromArgb(36, 36, 36),
            Padding = new Padding(10, 10, 10, 0)
        };

        var _btnPin = new Button
        {
            Text = "",
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe MDL2 Assets", 11),
            Height = 30,
            Width = 36,
            Location = new Point(10, 10),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0),
            UseCompatibleTextRendering = false
        };
        _btnPin.Click += (_, _) =>
        {
            TopMost = !TopMost;
            _btnPin.BackColor = TopMost ? Color.FromArgb(0, 100, 180) : Color.FromArgb(60, 60, 60);
        };

        _btnAdd = new Button
        {
            Text = "",
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe MDL2 Assets", 11),
            Height = 30,
            Width = 36,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0),
            UseCompatibleTextRendering = false
        };
        _btnAdd.Click += (_, _) => AddTimer();
        toolbar.Controls.AddRange([_btnPin, _btnAdd]);
        toolbar.Resize += (_, _) => _btnAdd.Location = new Point(toolbar.Width - _btnAdd.Width - 10, 10);

        // Scrollable timer list
        _timerListPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(10, 10, 10, 0)
        };

        _lblEmpty = new Label
        {
            Text = "Noch keine Timer.\nKlicke auf \"+ Timer hinzufügen\".",
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 11),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _timerListPanel.Controls.Add(_lblEmpty);

        Controls.Add(_timerListPanel);
        Controls.Add(toolbar);

        // Global tick – aktualisiert alle Timer gleichzeitig
        _globalTick = new System.Windows.Forms.Timer { Interval = 250 };
        _globalTick.Tick += (_, _) => RefreshAll();
        _globalTick.Start();

        // Gespeicherte Timer wiederherstellen
        foreach (var entry in TimerPersistence.Load())
            AddTimerControl(entry);

        // Fenstereinstellungen wiederherstellen
        var win = TimerPersistence.LoadWindowSettings();
        if (win is not null)
        {
            Size = new Size(win.Width, win.Height);
            if (Screen.AllScreens.Any(s => s.WorkingArea.Contains(win.X, win.Y)))
                Location = new Point(win.X, win.Y);
            if (win.Maximized)
                WindowState = FormWindowState.Maximized;
        }
    }

    private void AddTimer()
    {
        using var dlg = new AddTimerDialog();
        if (dlg.ShowDialog(this) is not DialogResult.OK) return;
        AddTimerControl(new TimerEntry(dlg.TimerName, dlg.CountdownDuration) { SoundPath = dlg.SoundPath });
        Save();
    }

    private void AddTimerControl(TimerEntry entry)
    {
        var ctrl = new TimerControl(entry);
        ctrl.RemoveRequested += (s, _) => RemoveTimer((TimerControl)s!);
        ctrl.EditRequested += (s, _) => EditTimer((TimerControl)s!);

        _timerControls.Insert(0, ctrl);
        _timerListPanel.Controls.Add(ctrl);
        ctrl.BringToFront();
        _lblEmpty.Visible = false;
    }

    private void EditTimer(TimerControl ctrl)
    {
        using var dlg = new AddTimerDialog(ctrl.Entry);
        if (dlg.ShowDialog(this) is not DialogResult.OK) return;
        ctrl.Entry.Name = dlg.TimerName;
        ctrl.Entry.CountdownDuration = dlg.CountdownDuration;
        ctrl.Entry.SoundPath = dlg.SoundPath;
        ctrl.Entry.Reset();
        ctrl.RefreshAfterEdit();
        Save();
    }

    private void RemoveTimer(TimerControl ctrl)
    {
        _timerControls.Remove(ctrl);
        _timerListPanel.Controls.Remove(ctrl);
        ctrl.Dispose();
        _lblEmpty.Visible = _timerControls.Count == 0;
        Save();
    }

    private void Save() => TimerPersistence.Save(_timerControls.Select(c => c.Entry));

    private void RefreshAll()
    {
        foreach (var ctrl in _timerControls)
            ctrl.Update();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _globalTick.Stop();
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        TimerPersistence.SaveWindowSettings(new(
            bounds.Width, bounds.Height, bounds.X, bounds.Y,
            WindowState == FormWindowState.Maximized));
        base.OnFormClosed(e);
    }
}
