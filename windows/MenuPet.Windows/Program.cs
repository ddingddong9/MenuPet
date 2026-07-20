using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfSize = System.Windows.Size;

namespace MenuPet.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var options = WindowsAppOptions.Load(args);
        var app = new MenuPetApplication(options);
        app.Run();
    }
}

internal sealed class MenuPetApplication : Application
{
    private readonly WindowsAppOptions _options;
    private PetController? _controller;

    public MenuPetApplication(WindowsAppOptions options)
    {
        _options = options;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _controller = new PetController(this, _options);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}

internal sealed class WindowsAppOptions
{
    public string? AppName { get; init; }
    public string? SpeechText { get; init; }
    public string? PetPath { get; init; }
    public string DataDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MenuPet"
    );

    public static WindowsAppOptions Load(string[] args)
    {
        var options = new WindowsAppOptions();
        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            var next = index + 1 < args.Length ? args[index + 1] : "";
            switch (current)
            {
                case "--name" when !string.IsNullOrWhiteSpace(next):
                    options = options.WithAppName(next);
                    index++;
                    break;
                case "--speech-text" when !string.IsNullOrWhiteSpace(next):
                    options = options.WithSpeechText(next);
                    index++;
                    break;
                case "--pet" when !string.IsNullOrWhiteSpace(next):
                    options = options.WithPetPath(Path.GetFullPath(next));
                    index++;
                    break;
            }
        }

        return options;
    }

    private WindowsAppOptions WithAppName(string value) => new()
    {
        AppName = value,
        SpeechText = SpeechText,
        PetPath = PetPath,
        DataDirectory = DataDirectory
    };

    private WindowsAppOptions WithSpeechText(string value) => new()
    {
        AppName = AppName,
        SpeechText = value,
        PetPath = PetPath,
        DataDirectory = DataDirectory
    };

    private WindowsAppOptions WithPetPath(string value) => new()
    {
        AppName = AppName,
        SpeechText = SpeechText,
        PetPath = value,
        DataDirectory = DataDirectory
    };
}

internal sealed class PetSettings
{
    public double ScalePercent { get; set; } = 100;
    public double MovementSpeedPercent { get; set; } = 100;
    public string SpeechText { get; set; } = "hug me...";
    public bool RandomBenchPressEnabled { get; set; } = true;
    public string AppName { get; set; } = "MenuPet";
    public string? PetImagePath { get; set; }
}

internal enum MotionMode
{
    Wander,
    Bounce,
    Rest
}

internal sealed class PetController : IDisposable
{
    private const double MaxPetHeight = 360;
    private const double PetMargin = 24;
    private static readonly WpfSize ChickenSize = new(82, 62);

    private readonly MenuPetApplication _app;
    private readonly WindowsAppOptions _options;
    private readonly string _settingsPath;
    private readonly string _defaultPetPath;
    private readonly Random _random = new();
    private readonly DispatcherTimer _timer;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ContextMenuStrip _trayMenu = new();
    private readonly Forms.ToolStripMenuItem _randomBenchMenuItem = new("랜덤 벤치프레스");
    private readonly Dictionary<MotionMode, Forms.ToolStripMenuItem> _motionMenuItems = new();
    private readonly Forms.TrackBar _scaleTrackBar = new();
    private readonly Forms.TrackBar _speedTrackBar = new();
    private readonly Forms.Label _scaleLabel = new();
    private readonly Forms.Label _speedLabel = new();

    private PetSettings _settings;
    private PetWindow? _petWindow;
    private SpeechBubbleWindow? _speechWindow;
    private ChickenWindow? _chickenWindow;
    private SettingsWindow? _settingsWindow;
    private BitmapImage? _petImage;

    private DateTime _motionStart = DateTime.Now;
    private DateTime _lastTick = DateTime.Now;
    private WpfPoint _petPosition;
    private WpfPoint _walkTarget;
    private WpfSize _petSize = new(220, 220);
    private DateTime _nextDecisionDate = DateTime.Now;
    private DateTime? _restUntil;
    private DateTime? _speechUntil;
    private DateTime? _benchPressUntil;
    private DateTime? _chickenFeedUntil;
    private DateTime? _jumpStart;
    private DateTime? _dashUntil;
    private DateTime? _cursorThrowStartTime;
    private DateTime _lastCursorThrowDate = DateTime.MinValue;
    private WpfPoint _cursorThrowStartPoint;
    private WpfPoint _cursorThrowControlPoint;
    private WpfPoint _cursorThrowEndPoint;
    private double _walkPhase;
    private double _walkSpeed = 110;
    private bool _facesRight;
    private bool _isDraggingChicken;
    private MotionMode _motionMode = MotionMode.Wander;

    public PetController(MenuPetApplication app, WindowsAppOptions options)
    {
        _app = app;
        _options = options;
        Directory.CreateDirectory(_options.DataDirectory);
        _settingsPath = Path.Combine(_options.DataDirectory, "settings.json");
        _defaultPetPath = Path.Combine(_options.DataDirectory, "pet-character.png");
        _settings = LoadSettings(options);
        SaveSettings();

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = TrayIconFactory.CreateHeartIcon(),
            Text = _settings.AppName,
            Visible = true
        };

        BuildTrayMenu();
        _trayIcon.ContextMenuStrip = _trayMenu;
        _trayIcon.DoubleClick += (_, _) => ShowPetFromMenu();

        LoadPetImage();
        UpdateMenuControls();

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += (_, _) => TickMotion();

        if (_settings.ScalePercent > 0)
        {
            ShowPet(true);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayMenu.Dispose();
        _settingsWindow?.Close();
        _speechWindow?.Close();
        _chickenWindow?.Close();
        _petWindow?.Close();
    }

    private PetSettings LoadSettings(WindowsAppOptions options)
    {
        PetSettings settings;
        try
        {
            settings = File.Exists(_settingsPath)
                ? JsonSerializer.Deserialize<PetSettings>(File.ReadAllText(_settingsPath)) ?? new PetSettings()
                : new PetSettings();
        }
        catch
        {
            settings = new PetSettings();
        }

        settings.AppName = string.IsNullOrWhiteSpace(options.AppName)
            ? (string.IsNullOrWhiteSpace(settings.AppName) ? "MenuPet" : settings.AppName)
            : options.AppName;
        settings.SpeechText = string.IsNullOrWhiteSpace(settings.SpeechText)
            ? (string.IsNullOrWhiteSpace(options.SpeechText) ? "hug me..." : options.SpeechText)
            : settings.SpeechText;

        if (!string.IsNullOrWhiteSpace(options.PetPath))
        {
            CopyPetImage(options.PetPath);
            settings.PetImagePath = _defaultPetPath;
        }
        else if (string.IsNullOrWhiteSpace(settings.PetImagePath))
        {
            var bundledPrivateAsset = Path.Combine(AppContext.BaseDirectory, "private-assets", "pet-character.png");
            if (File.Exists(_defaultPetPath))
            {
                settings.PetImagePath = _defaultPetPath;
            }
            else if (File.Exists(bundledPrivateAsset))
            {
                settings.PetImagePath = bundledPrivateAsset;
            }
        }

        settings.ScalePercent = Math.Clamp(Math.Round(settings.ScalePercent), 0, 100);
        settings.MovementSpeedPercent = Math.Clamp(Math.Round(settings.MovementSpeedPercent), 0, 200);
        return settings;
    }

    private void BuildTrayMenu()
    {
        AddMenuItem("펫 이미지 선택...", ChoosePetImage);
        AddMenuItem("말풍선 문구 수정...", EditSpeechText);
        AddMenuItem("설정...", OpenSettings);
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        AddMenuItem($"{_settings.AppName} 보이기", ShowPetFromMenu);
        AddMenuItem($"{_settings.AppName} 숨기기", HidePetFromMenu);
        AddMenuItem("닭가슴살 꺼내기", ShowChickenBreast);
        AddMenuItem("벤치프레스 하기", PerformBenchPress);

        _randomBenchMenuItem.CheckOnClick = false;
        _randomBenchMenuItem.Click += (_, _) => SetRandomBenchPressEnabled(!_settings.RandomBenchPressEnabled);
        _trayMenu.Items.Add(_randomBenchMenuItem);

        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add(MakeTrackBarItem(_scaleLabel, _scaleTrackBar, 0, 100, value => SetScalePercent(value)));
        _trayMenu.Items.Add(MakeTrackBarItem(_speedLabel, _speedTrackBar, 0, 200, value => SetMovementSpeedPercent(value)));
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        AddMotionItems();
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        AddMenuItem("종료", () => _app.Shutdown());
    }

    private void AddMenuItem(string title, Action action)
    {
        var item = new Forms.ToolStripMenuItem(title);
        item.Click += (_, _) => _app.Dispatcher.Invoke(action);
        _trayMenu.Items.Add(item);
    }

    private static Forms.ToolStripControlHost MakeTrackBarItem(
        Forms.Label label,
        Forms.TrackBar trackBar,
        int minimum,
        int maximum,
        Action<int> onChanged
    )
    {
        var panel = new Forms.FlowLayoutPanel
        {
            FlowDirection = Forms.FlowDirection.TopDown,
            AutoSize = false,
            Width = 230,
            Height = 58,
            Margin = Forms.Padding.Empty,
            Padding = new Forms.Padding(8, 4, 8, 0)
        };
        label.AutoSize = false;
        label.Width = 210;
        label.Height = 18;
        trackBar.Minimum = minimum;
        trackBar.Maximum = maximum;
        trackBar.TickFrequency = Math.Max(1, (maximum - minimum) / 5);
        trackBar.Width = 206;
        trackBar.Height = 32;
        trackBar.Scroll += (_, _) => onChanged(trackBar.Value);
        panel.Controls.Add(label);
        panel.Controls.Add(trackBar);

        return new Forms.ToolStripControlHost(panel)
        {
            AutoSize = false,
            Width = 240,
            Height = 64
        };
    }

    private void AddMotionItems()
    {
        foreach (MotionMode mode in Enum.GetValues(typeof(MotionMode)))
        {
            var title = mode switch
            {
                MotionMode.Wander => "랜덤 산책",
                MotionMode.Bounce => "제자리 통통",
                _ => "잠깐 쉬기"
            };
            var item = new Forms.ToolStripMenuItem(title);
            item.Click += (_, _) => SetMotionMode(mode);
            _motionMenuItems[mode] = item;
            _trayMenu.Items.Add(item);
        }

        UpdateMotionMenuItems();
    }

    private void UpdateMenuControls()
    {
        _scaleLabel.Text = $"{_settings.AppName} 크기 {(int)_settings.ScalePercent}%";
        _speedLabel.Text = $"이동속도 {(int)_settings.MovementSpeedPercent}%";
        _scaleTrackBar.Value = (int)Math.Clamp(_settings.ScalePercent, 0, 100);
        _speedTrackBar.Value = (int)Math.Clamp(_settings.MovementSpeedPercent, 0, 200);
        _randomBenchMenuItem.Checked = _settings.RandomBenchPressEnabled;
        _settingsWindow?.SetRandomBenchPressEnabled(_settings.RandomBenchPressEnabled);
        UpdateMotionMenuItems();
    }

    private void UpdateMotionMenuItems()
    {
        foreach (var pair in _motionMenuItems)
        {
            pair.Value.Checked = pair.Key == _motionMode;
        }
    }

    private void SaveSettings()
    {
        Directory.CreateDirectory(_options.DataDirectory);
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    private void LoadPetImage()
    {
        _petImage = null;
        if (string.IsNullOrWhiteSpace(_settings.PetImagePath) || !File.Exists(_settings.PetImagePath))
        {
            return;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(_settings.PetImagePath);
        image.EndInit();
        image.Freeze();
        _petImage = image;
    }

    private void CopyPetImage(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Pet PNG not found.", sourcePath);
        }

        Directory.CreateDirectory(_options.DataDirectory);
        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(_defaultPetPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.Copy(sourcePath, _defaultPetPath, true);
        if (!HashesMatch(sourcePath, _defaultPetPath))
        {
            throw new IOException("Copied pet PNG hash did not match the source PNG.");
        }
    }

    private static bool HashesMatch(string left, string right)
    {
        using var sha = SHA256.Create();
        var leftHash = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(left)));
        var rightHash = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(right)));
        return leftHash.Equals(rightHash, StringComparison.OrdinalIgnoreCase);
    }

    private void SetScalePercent(double value)
    {
        _settings.ScalePercent = Math.Clamp(Math.Round(value), 0, 100);
        SaveSettings();
        UpdateMenuControls();

        if (_settings.ScalePercent <= 0)
        {
            HidePetOnly();
        }
        else if (_petWindow?.IsVisible == true)
        {
            ResizePet();
        }
        else
        {
            ShowPet(true);
        }
    }

    private void SetMovementSpeedPercent(double value)
    {
        _settings.MovementSpeedPercent = Math.Clamp(Math.Round(value), 0, 200);
        SaveSettings();
        UpdateMenuControls();
    }

    private void SetRandomBenchPressEnabled(bool isEnabled)
    {
        _settings.RandomBenchPressEnabled = isEnabled;
        SaveSettings();
        UpdateMenuControls();
    }

    private void SetMotionMode(MotionMode mode)
    {
        _motionMode = mode;
        _restUntil = null;
        _speechUntil = null;
        StopBenchPress();
        _jumpStart = null;
        _dashUntil = null;
        UpdateMotionMenuItems();

        if (_motionMode == MotionMode.Wander)
        {
            ChooseNextWalkTarget(true);
        }
    }

    private PetWindow MakePetWindowIfNeeded()
    {
        if (_petWindow != null)
        {
            return _petWindow;
        }

        var window = new PetWindow();
        window.PetClicked += HidePetOnly;
        window.SetPetImage(_petImage);
        _petWindow = window;
        return window;
    }

    private SpeechBubbleWindow MakeSpeechWindowIfNeeded()
    {
        if (_speechWindow != null)
        {
            return _speechWindow;
        }

        _speechWindow = new SpeechBubbleWindow();
        return _speechWindow;
    }

    private ChickenWindow MakeChickenWindowIfNeeded()
    {
        if (_chickenWindow != null)
        {
            return _chickenWindow;
        }

        var window = new ChickenWindow();
        window.DragStarted += () =>
        {
            _isDraggingChicken = true;
            HideSpeechBubble();
            StopBenchPress();
            StopChickenFeeding();
        };
        window.Moved += () =>
        {
            _restUntil = null;
            _dashUntil = null;
        };
        window.DragEnded += () => _isDraggingChicken = false;
        _chickenWindow = window;
        return window;
    }

    private void ResizePet()
    {
        var frame = TargetFrame();
        _petPosition = ClampedPosition(new WpfPoint(frame.X, frame.Y), VisibleFrame(), frame.Size);
        _petSize = frame.Size;
        _petWindow?.SetFrame(new WpfRect(_petPosition, _petSize));
        _petWindow?.SetPetImage(_petImage);
        StartMotion();
    }

    private void ShowPet(bool animateFromBottom)
    {
        if (_settings.ScalePercent <= 0)
        {
            return;
        }

        var window = MakePetWindowIfNeeded();
        window.SetPetImage(_petImage);
        var frame = TargetFrame();
        _petPosition = new WpfPoint(frame.X, frame.Y);
        _petSize = frame.Size;
        _walkTarget = _petPosition;

        if (animateFromBottom)
        {
            var screen = VisibleFrame();
            var startFrame = new WpfRect(frame.X, screen.Bottom + frame.Height + 12, frame.Width, frame.Height);
            window.SetFrame(startFrame);
        }
        else
        {
            window.SetFrame(frame);
        }

        window.Show();
        window.Activate();
        _petPosition = new WpfPoint(frame.X, frame.Y);
        window.SetFrame(frame);
        ChooseNextWalkTarget(true);
        StartMotion();
    }

    private void StartMotion()
    {
        if (_petWindow?.IsVisible != true || _settings.ScalePercent <= 0)
        {
            return;
        }

        _motionStart = DateTime.Now;
        _lastTick = _motionStart;
        _timer.Start();
    }

    private void TickMotion()
    {
        if (_petWindow?.IsVisible != true)
        {
            _timer.Stop();
            return;
        }

        var now = DateTime.Now;
        var delta = Math.Clamp((now - _lastTick).TotalSeconds, 0, 1.0 / 20.0);
        _lastTick = now;
        UpdateBenchPress(now);
        UpdateChickenFeeding(now);

        double bobOffset;
        var isChasingChicken = UpdateChickenChase(now, delta);
        if (isChasingChicken)
        {
            bobOffset = CurrentWalkBob(now);
        }
        else
        {
            switch (_motionMode)
            {
                case MotionMode.Wander:
                    UpdateWander(now, delta);
                    bobOffset = CurrentWalkBob(now);
                    break;
                case MotionMode.Bounce:
                    bobOffset = Math.Sin((now - _motionStart).TotalSeconds * 5.2) * 9.0;
                    break;
                default:
                    bobOffset = Math.Sin((now - _motionStart).TotalSeconds * 2.1) * 3.0;
                    break;
            }
        }

        bobOffset += CurrentJumpOffset(now);
        var frame = new WpfRect(_petPosition.X, _petPosition.Y - bobOffset, _petSize.Width, _petSize.Height);
        _petWindow.SetFrame(frame);
        _petWindow.SetRenderState(
            _facesRight,
            _benchPressUntil.HasValue,
            _benchPressUntil.HasValue ? _petWindow.BenchPressStart : DateTime.Now,
            _chickenFeedUntil.HasValue,
            _chickenFeedUntil.HasValue ? _petWindow.ChickenFeedStart : DateTime.Now,
            _cursorThrowStartTime.HasValue || _petWindow.IsThrowingCursor,
            _petWindow.CursorThrowStart
        );

        UpdateSpeechBubble(now, frame);
        UpdateCursorThrow(now, frame);
    }

    private bool UpdateChickenChase(DateTime now, double delta)
    {
        if (_chickenWindow?.IsVisible != true || _petWindow?.IsVisible != true)
        {
            return false;
        }

        HideSpeechBubble();
        StopBenchPress();
        _restUntil = null;
        _dashUntil = null;

        var chickenFrame = _chickenWindow.ScreenFrame;
        var chickenCenter = Center(chickenFrame);
        _walkTarget = ClampedPosition(
            new WpfPoint(chickenCenter.X - _petSize.Width * 0.50, chickenCenter.Y - _petSize.Height * 0.34),
            VisibleFrame(),
            _petSize
        );

        MoveTowardWalkTarget(delta, Math.Max(_walkSpeed, 178.0));

        var mouth = MouthPoint(_petPosition);
        var eatRadius = Math.Max(38, Math.Min(_petSize.Width, _petSize.Height) * 0.14);
        var petCatchRect = new WpfRect(
            _petPosition.X + _petSize.Width * 0.22,
            _petPosition.Y + _petSize.Height * 0.20,
            _petSize.Width * 0.56,
            _petSize.Height * 0.44
        );

        if (Distance(mouth, chickenCenter) <= eatRadius || petCatchRect.Contains(chickenCenter))
        {
            EatChicken(now);
        }

        return true;
    }

    private void UpdateChickenFeeding(DateTime now)
    {
        if (!_chickenFeedUntil.HasValue)
        {
            return;
        }

        if (now >= _chickenFeedUntil.Value || _petWindow?.IsVisible != true)
        {
            StopChickenFeeding();
            ChooseNextWalkTarget(true);
        }
    }

    private void UpdateWander(DateTime now, double delta)
    {
        if (_restUntil.HasValue && now < _restUntil.Value)
        {
            return;
        }

        _restUntil = null;

        if (now >= _nextDecisionDate || Distance(_petPosition, _walkTarget) < 14)
        {
            ChooseNextWalkTarget(false);
        }

        MoveTowardWalkTarget(delta, _walkSpeed);
    }

    private void MoveTowardWalkTarget(double delta, double baseSpeed)
    {
        var dx = _walkTarget.X - _petPosition.X;
        var dy = _walkTarget.Y - _petPosition.Y;
        var remaining = Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy));
        var userSpeed = _settings.MovementSpeedPercent / 100.0;
        var dashSpeed = _dashUntil.HasValue && DateTime.Now < _dashUntil.Value ? 2.35 : 1.0;
        var step = Math.Min(remaining, baseSpeed * Math.Max(0.12, userSpeed) * dashSpeed * delta);

        _petPosition = new WpfPoint(
            _petPosition.X + dx / remaining * step,
            _petPosition.Y + dy / remaining * step
        );
        _petPosition = ClampedPosition(_petPosition, VisibleFrame(), _petSize);

        if (Math.Abs(dx) > 0.5)
        {
            _facesRight = dx > 0;
        }

        _walkPhase += delta * 7.0 * Math.Max(0.25, userSpeed) * dashSpeed;
    }

    private void ChooseNextWalkTarget(bool force)
    {
        var now = DateTime.Now;

        if (!force && _chickenWindow?.IsVisible != true)
        {
            var roll = _random.NextDouble();
            if (_settings.RandomBenchPressEnabled && roll < 0.11)
            {
                StartBenchPress(now.AddSeconds(RandomDouble(3.4, 5.2)));
                return;
            }

            if (roll < 0.23)
            {
                var until = now.AddSeconds(RandomDouble(1.8, 3.4));
                _restUntil = until;
                _nextDecisionDate = until;
                ShowSpeechBubble(until);
                return;
            }

            if (roll < 0.33)
            {
                _restUntil = now.AddSeconds(RandomDouble(0.8, 1.9));
                _nextDecisionDate = _restUntil.Value;
                return;
            }

            if (roll < 0.41)
            {
                _jumpStart = now;
            }
            else if (roll < 0.54)
            {
                _dashUntil = now.AddSeconds(RandomDouble(0.45, 0.85));
            }
        }

        var screen = VisibleFrame();
        var minX = screen.Left + PetMargin;
        var minY = screen.Top + PetMargin;
        var maxX = Math.Max(minX, screen.Right - _petSize.Width - PetMargin);
        var maxY = Math.Max(minY, screen.Bottom - _petSize.Height - PetMargin);
        var lowYBandMax = minY + (maxY - minY) * 0.42;
        var targetY = _random.NextDouble() < 0.68
            ? RandomDouble(minY, Math.Max(minY, lowYBandMax))
            : RandomDouble(minY, maxY);

        _walkTarget = new WpfPoint(RandomDouble(minX, maxX), targetY);
        _walkSpeed = RandomDouble(72, 138);
        _nextDecisionDate = now.AddSeconds(RandomDouble(2.0, 5.0));
    }

    private double CurrentWalkBob(DateTime now)
    {
        if (_restUntil.HasValue && now < _restUntil.Value)
        {
            return Math.Sin((now - _motionStart).TotalSeconds * 2.1) * 3.0;
        }

        var userSpeed = _settings.MovementSpeedPercent / 100.0;
        var dashSpeed = _dashUntil.HasValue && now < _dashUntil.Value ? 1.35 : 1.0;
        return Math.Abs(Math.Sin(_walkPhase)) * 7.5 * Math.Max(0.4, userSpeed) * dashSpeed;
    }

    private double CurrentJumpOffset(DateTime now)
    {
        if (!_jumpStart.HasValue)
        {
            return 0;
        }

        const double duration = 0.7;
        var elapsed = (now - _jumpStart.Value).TotalSeconds;
        if (elapsed >= duration)
        {
            _jumpStart = null;
            return 0;
        }

        return Math.Sin(elapsed / duration * Math.PI) * 46.0;
    }

    private void ShowSpeechBubble(DateTime until)
    {
        _speechUntil = until;
        StopBenchPress();
        var window = MakeSpeechWindowIfNeeded();
        window.Message = _settings.SpeechText;
        PositionSpeechBubble(_petWindow?.ScreenFrame ?? new WpfRect(_petPosition, _petSize));
        window.Show();
    }

    private void UpdateSpeechBubble(DateTime now, WpfRect petFrame)
    {
        if (!_speechUntil.HasValue)
        {
            return;
        }

        if (now >= _speechUntil.Value || _petWindow?.IsVisible != true)
        {
            HideSpeechBubble();
            return;
        }

        PositionSpeechBubble(petFrame);
        _speechWindow?.InvalidateVisual();
        if (_speechWindow?.IsVisible != true)
        {
            _speechWindow?.Show();
        }
    }

    private void PositionSpeechBubble(WpfRect petFrame)
    {
        if (_speechWindow == null)
        {
            return;
        }

        var screen = VisibleFrame();
        const double width = 220;
        const double height = 70;
        var x = Math.Clamp(petFrame.Left + petFrame.Width / 2 - width / 2, screen.Left + 8, screen.Right - width - 8);
        var y = Math.Clamp(petFrame.Top - height - 4, screen.Top + 8, screen.Bottom - height - 8);
        _speechWindow.SetFrame(new WpfRect(x, y, width, height));
    }

    private void HideSpeechBubble()
    {
        _speechUntil = null;
        _speechWindow?.Hide();
    }

    private void StartBenchPress(DateTime until)
    {
        HideSpeechBubble();
        StopChickenFeeding();
        HideChickenBreast();
        StopCursorThrow();
        _restUntil = until;
        _nextDecisionDate = until;
        _jumpStart = null;
        _dashUntil = null;
        _benchPressUntil = until;
        if (_petWindow != null)
        {
            _petWindow.BenchPressStart = DateTime.Now;
            _petWindow.IsBenchPressing = true;
        }
    }

    private void UpdateBenchPress(DateTime now)
    {
        if (!_benchPressUntil.HasValue)
        {
            return;
        }

        if (now >= _benchPressUntil.Value || _petWindow?.IsVisible != true)
        {
            StopBenchPress();
        }
    }

    private void StopBenchPress()
    {
        _benchPressUntil = null;
        if (_petWindow != null)
        {
            _petWindow.IsBenchPressing = false;
        }
    }

    private void EatChicken(DateTime now)
    {
        _chickenWindow?.Hide();
        _isDraggingChicken = false;
        _chickenFeedUntil = now.AddSeconds(2.15);
        if (_petWindow != null)
        {
            _petWindow.ChickenFeedStart = now;
            _petWindow.IsFeedingChicken = true;
        }
        _restUntil = _chickenFeedUntil;
        _nextDecisionDate = _chickenFeedUntil.Value;
        _jumpStart = null;
        _dashUntil = null;
    }

    private void StopChickenFeeding()
    {
        _chickenFeedUntil = null;
        if (_petWindow != null)
        {
            _petWindow.IsFeedingChicken = false;
        }
    }

    private void HideChickenBreast()
    {
        _isDraggingChicken = false;
        _chickenWindow?.Hide();
    }

    private WpfRect InitialChickenFrame()
    {
        var screen = VisibleFrame();
        var petFrame = _petWindow?.ScreenFrame ?? new WpfRect(_petPosition, _petSize);
        var x = petFrame.Left - ChickenSize.Width - 18;
        if (x < screen.Left + 14)
        {
            x = petFrame.Right + 18;
        }

        x = Math.Clamp(x, screen.Left + 14, screen.Right - ChickenSize.Width - 14);
        var y = Math.Clamp(
            petFrame.Top + petFrame.Height / 2 - ChickenSize.Height / 2,
            screen.Top + 14,
            screen.Bottom - ChickenSize.Height - 14
        );
        return new WpfRect(x, y, ChickenSize.Width, ChickenSize.Height);
    }

    private void UpdateCursorThrow(DateTime now, WpfRect petFrame)
    {
        if (_cursorThrowStartTime.HasValue)
        {
            const double duration = 0.32;
            var progress = Math.Clamp((now - _cursorThrowStartTime.Value).TotalSeconds / duration, 0, 1);
            var point = QuadraticPoint(_cursorThrowStartPoint, _cursorThrowControlPoint, _cursorThrowEndPoint, progress);
            Forms.Cursor.Position = new Drawing.Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
            if (progress >= 1)
            {
                _cursorThrowStartTime = null;
                _lastCursorThrowDate = now;
            }
            return;
        }

        if (_petWindow?.IsThrowingCursor == true && (now - _petWindow.CursorThrowStart).TotalSeconds > 0.72)
        {
            _petWindow.IsThrowingCursor = false;
        }

        if ((now - _lastCursorThrowDate).TotalSeconds <= 2.35)
        {
            return;
        }
        if (_isDraggingChicken || _chickenWindow?.IsVisible == true || _chickenFeedUntil.HasValue)
        {
            return;
        }
        if (_petWindow?.IsBenchPressing == true)
        {
            return;
        }

        var mouse = Forms.Cursor.Position;
        var cursor = new WpfPoint(mouse.X, mouse.Y);
        var catchRect = new WpfRect(
            petFrame.Left + petFrame.Width * 0.22,
            petFrame.Top + petFrame.Height * 0.16,
            petFrame.Width * 0.56,
            petFrame.Height * 0.68
        );
        if (!catchRect.Contains(cursor))
        {
            return;
        }

        StartCursorThrow(cursor, petFrame, now);
    }

    private void StartCursorThrow(WpfPoint mouse, WpfRect petFrame, DateTime now)
    {
        var screen = VisibleFrame();
        screen.Inflate(-18, -18);
        var leftRoom = mouse.X - screen.Left;
        var rightRoom = screen.Right - mouse.X;
        var direction = rightRoom >= leftRoom ? 1.0 : -1.0;
        var distance = Math.Max(240.0, petFrame.Width * 0.78);
        var lift = RandomDouble(90, 170);
        var rawEnd = new WpfPoint(mouse.X + direction * distance, mouse.Y - lift);
        var end = new WpfPoint(
            Math.Clamp(rawEnd.X, screen.Left, screen.Right),
            Math.Clamp(rawEnd.Y, screen.Top, screen.Bottom)
        );
        var control = new WpfPoint((mouse.X + end.X) / 2, Math.Max(screen.Top, Math.Min(mouse.Y, end.Y) - 130));

        _cursorThrowStartTime = now;
        _cursorThrowStartPoint = mouse;
        _cursorThrowControlPoint = control;
        _cursorThrowEndPoint = end;
        _facesRight = direction > 0;
        if (_petWindow != null)
        {
            _petWindow.CursorThrowStart = now;
            _petWindow.IsThrowingCursor = true;
        }
    }

    private void StopCursorThrow()
    {
        _cursorThrowStartTime = null;
        if (_petWindow != null)
        {
            _petWindow.IsThrowingCursor = false;
        }
    }

    private void HidePetOnly()
    {
        _timer.Stop();
        HideSpeechBubble();
        StopBenchPress();
        StopChickenFeeding();
        HideChickenBreast();
        StopCursorThrow();
        _petWindow?.Hide();
    }

    private void ShowPetFromMenu()
    {
        if (_settings.ScalePercent <= 0)
        {
            SetScalePercent(100);
        }
        else
        {
            ShowPet(true);
        }
    }

    private void HidePetFromMenu()
    {
        SetScalePercent(0);
    }

    private void ShowChickenBreast()
    {
        if (_settings.ScalePercent <= 0)
        {
            SetScalePercent(100);
        }
        else if (_petWindow?.IsVisible != true)
        {
            ShowPet(true);
        }

        StopChickenFeeding();
        HideSpeechBubble();
        StopBenchPress();
        _restUntil = null;
        _dashUntil = null;

        var window = MakeChickenWindowIfNeeded();
        window.SetFrame(InitialChickenFrame());
        window.Show();
        StartMotion();
    }

    private void PerformBenchPress()
    {
        if (_settings.ScalePercent <= 0)
        {
            SetScalePercent(100);
        }
        else if (_petWindow?.IsVisible != true)
        {
            ShowPet(true);
        }

        StartBenchPress(DateTime.Now.AddSeconds(4.8));
        StartMotion();
    }

    private void ChoosePetImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "펫 이미지 선택",
            Filter = "PNG images (*.png)|*.png",
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            CopyPetImage(dialog.FileName);
            _settings.PetImagePath = _defaultPetPath;
            SaveSettings();
            LoadPetImage();
            _petWindow?.SetPetImage(_petImage);
            if (_settings.ScalePercent <= 0)
            {
                SetScalePercent(100);
            }
            else
            {
                ShowPet(true);
            }
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, _settings.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditSpeechText()
    {
        var text = PromptWindow.Show(_settings.AppName, "말풍선 문구 수정", _settings.SpeechText);
        if (text == null)
        {
            return;
        }

        _settings.SpeechText = string.IsNullOrWhiteSpace(text) ? (_options.SpeechText ?? "hug me...") : text.Trim();
        SaveSettings();
    }

    private void OpenSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_settings.AppName, PerformBenchPress, () => _settings.RandomBenchPressEnabled, SetRandomBenchPressEnabled);
        }

        _settingsWindow.SetRandomBenchPressEnabled(_settings.RandomBenchPressEnabled);
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private WpfRect TargetFrame()
    {
        var screen = VisibleFrame();
        var aspect = _petImage == null
            ? 1.0
            : _petImage.PixelWidth / Math.Max(1.0, (double)_petImage.PixelHeight);
        var height = Math.Max(1.0, MaxPetHeight * _settings.ScalePercent / 100.0);
        var width = Math.Max(1.0, height * aspect);
        return new WpfRect(screen.Right - width - PetMargin, screen.Bottom - height - PetMargin, width, height);
    }

    private static WpfRect VisibleFrame()
    {
        var area = Forms.Screen.PrimaryScreen?.WorkingArea ?? new Drawing.Rectangle(0, 0, 1440, 900);
        return new WpfRect(area.Left, area.Top, area.Width, area.Height);
    }

    private static WpfPoint ClampedPosition(WpfPoint position, WpfRect screen, WpfSize petSize)
    {
        var minX = screen.Left + PetMargin;
        var minY = screen.Top + PetMargin;
        var maxX = Math.Max(minX, screen.Right - petSize.Width - PetMargin);
        var maxY = Math.Max(minY, screen.Bottom - petSize.Height - PetMargin);
        return new WpfPoint(Math.Clamp(position.X, minX, maxX), Math.Clamp(position.Y, minY, maxY));
    }

    private WpfPoint MouthPoint(WpfPoint origin)
    {
        var xOffset = _facesRight ? _petSize.Width * 0.45 : _petSize.Width * 0.55;
        return new WpfPoint(origin.X + xOffset, origin.Y + _petSize.Height * 0.34);
    }

    private static WpfPoint Center(WpfRect rect) => new(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

    private static double Distance(WpfPoint point, WpfPoint other)
    {
        var dx = point.X - other.X;
        var dy = point.Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private double RandomDouble(double min, double max) => min + _random.NextDouble() * (max - min);

    private static WpfPoint QuadraticPoint(WpfPoint start, WpfPoint control, WpfPoint end, double progress)
    {
        var inverse = 1.0 - progress;
        return new WpfPoint(
            inverse * inverse * start.X + 2.0 * inverse * progress * control.X + progress * progress * end.X,
            inverse * inverse * start.Y + 2.0 * inverse * progress * control.Y + progress * progress * end.Y
        );
    }
}

internal sealed class PetWindow : Window
{
    private readonly PetSurface _surface = new();

    public event Action? PetClicked;
    public DateTime BenchPressStart { get; set; } = DateTime.Now;
    public DateTime ChickenFeedStart { get; set; } = DateTime.Now;
    public DateTime CursorThrowStart { get; set; } = DateTime.Now;
    public bool IsBenchPressing { get; set; }
    public bool IsFeedingChicken { get; set; }
    public bool IsThrowingCursor { get; set; }
    public WpfRect ScreenFrame => new(Left, Top, Width, Height);

    public PetWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Content = _surface;
        MouseLeftButtonDown += (_, _) => PetClicked?.Invoke();
    }

    public void SetPetImage(BitmapSource? image)
    {
        _surface.PetImage = image;
        _surface.InvalidateVisual();
    }

    public void SetFrame(WpfRect frame)
    {
        Left = frame.Left;
        Top = frame.Top;
        Width = frame.Width;
        Height = frame.Height;
        _surface.Width = frame.Width;
        _surface.Height = frame.Height;
    }

    public void SetRenderState(
        bool facesRight,
        bool isBenchPressing,
        DateTime benchPressStart,
        bool isFeedingChicken,
        DateTime chickenFeedStart,
        bool isThrowingCursor,
        DateTime cursorThrowStart
    )
    {
        IsBenchPressing = isBenchPressing;
        IsFeedingChicken = isFeedingChicken;
        IsThrowingCursor = isThrowingCursor;
        _surface.FacesRight = facesRight;
        _surface.IsBenchPressing = isBenchPressing;
        _surface.BenchPressStart = benchPressStart;
        _surface.IsFeedingChicken = isFeedingChicken;
        _surface.ChickenFeedStart = chickenFeedStart;
        _surface.IsThrowingCursor = isThrowingCursor;
        _surface.CursorThrowStart = cursorThrowStart;
        _surface.InvalidateVisual();
    }
}

internal sealed class PetSurface : FrameworkElement
{
    public BitmapSource? PetImage { get; set; }
    public bool FacesRight { get; set; }
    public bool IsBenchPressing { get; set; }
    public bool IsFeedingChicken { get; set; }
    public bool IsThrowingCursor { get; set; }
    public DateTime BenchPressStart { get; set; } = DateTime.Now;
    public DateTime ChickenFeedStart { get; set; } = DateTime.Now;
    public DateTime CursorThrowStart { get; set; } = DateTime.Now;

    protected override void OnRender(DrawingContext dc)
    {
        var rect = new WpfRect(0, 0, ActualWidth, ActualHeight);
        if (rect.Width <= 1 || rect.Height <= 1)
        {
            return;
        }

        if (IsBenchPressing)
        {
            DrawBenchPress(dc, rect);
            return;
        }

        if (PetImage != null)
        {
            DrawPetImage(dc, rect);
        }
        else
        {
            DrawPlaceholder(dc, rect);
        }

        if (IsFeedingChicken)
        {
            DrawChickenFeeding(dc, rect);
        }
        if (IsThrowingCursor)
        {
            DrawCursorThrow(dc, rect);
        }
    }

    private void DrawPetImage(DrawingContext dc, WpfRect bounds)
    {
        if (PetImage == null)
        {
            return;
        }

        var ratio = Math.Min(bounds.Width / PetImage.PixelWidth, bounds.Height / PetImage.PixelHeight);
        var size = new WpfSize(PetImage.PixelWidth * ratio, PetImage.PixelHeight * ratio);
        var drawRect = new WpfRect((bounds.Width - size.Width) / 2, (bounds.Height - size.Height) / 2, size.Width, size.Height);

        if (FacesRight)
        {
            dc.PushTransform(new ScaleTransform(-1, 1, bounds.Width / 2, bounds.Height / 2));
            dc.DrawImage(PetImage, drawRect);
            dc.Pop();
        }
        else
        {
            dc.DrawImage(PetImage, drawRect);
        }
    }

    private void DrawPlaceholder(DrawingContext dc, WpfRect rect)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(255, 96, 145)), 3);
        var brush = new SolidColorBrush(Color.FromArgb(28, 255, 96, 145));
        dc.DrawRoundedRectangle(brush, pen, rect.Inset(rect.Width * 0.10, rect.Height * 0.10), 26, 26);
        DrawCenteredText(dc, "PNG 선택", rect, rect.Height * 0.08, Colors.Black, 0.70, FontWeights.Black);
    }

    private void DrawBenchPress(DrawingContext dc, WpfRect rect)
    {
        var elapsed = (DateTime.Now - BenchPressStart).TotalSeconds;
        var rep = (Math.Sin(elapsed * 5.6) + 1.0) / 2.0;
        var lift = rep * rect.Height * 0.20;
        var centerX = rect.Width / 2;
        var benchY = rect.Top + rect.Height * 0.70;
        var bodyY = benchY - rect.Height * 0.12;
        var shoulderY = bodyY - rect.Height * 0.08;
        var handY = shoulderY - rect.Height * 0.12 - lift;
        var barY = handY - rect.Height * 0.025;
        var headRect = new WpfRect(centerX - rect.Width * 0.22, shoulderY - rect.Height * 0.24, rect.Width * 0.30, rect.Height * 0.26);

        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), null, new WpfPoint(rect.Width / 2, rect.Height - 16), rect.Width * 0.32, 8);

        var darkPen = new Pen(new SolidColorBrush(Color.FromRgb(22, 26, 33)), Math.Max(5, rect.Height * 0.020))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawLine(darkPen, new WpfPoint(rect.Width * 0.20, benchY), new WpfPoint(rect.Width * 0.80, benchY));
        dc.DrawLine(new Pen(darkPen.Brush, Math.Max(3, rect.Height * 0.014)), new WpfPoint(rect.Width * 0.30, benchY), new WpfPoint(rect.Width * 0.25, rect.Height * 0.83));
        dc.DrawLine(new Pen(darkPen.Brush, Math.Max(3, rect.Height * 0.014)), new WpfPoint(rect.Width * 0.70, benchY), new WpfPoint(rect.Width * 0.75, rect.Height * 0.83));

        var barPen = new Pen(new SolidColorBrush(Color.FromRgb(11, 11, 13)), Math.Max(5, rect.Height * 0.018))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawLine(barPen, new WpfPoint(rect.Width * 0.13, barY), new WpfPoint(rect.Width * 0.87, barY));
        var platePen = new Pen(new SolidColorBrush(Color.FromRgb(8, 8, 10)), Math.Max(6, rect.Height * 0.024))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        foreach (var x in new[] { rect.Width * 0.11, rect.Width * 0.16, rect.Width * 0.84, rect.Width * 0.89 })
        {
            dc.DrawLine(platePen, new WpfPoint(x, barY - rect.Width * 0.020), new WpfPoint(x, barY + rect.Width * 0.020));
        }

        var pinkPen = new Pen(new SolidColorBrush(Color.FromRgb(250, 46, 107)), Math.Max(4, rect.Height * 0.016))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        var hip = new WpfPoint(centerX - rect.Width * 0.04, bodyY);
        var chest = new WpfPoint(centerX + rect.Width * 0.03, shoulderY);
        dc.DrawLine(pinkPen, hip, chest);
        dc.DrawLine(pinkPen, hip, new WpfPoint(rect.Width * 0.28, rect.Height * 0.82));
        dc.DrawLine(pinkPen, hip, new WpfPoint(rect.Width * 0.72, rect.Height * 0.82));
        dc.DrawLine(pinkPen, new WpfPoint(chest.X - rect.Width * 0.06, chest.Y), new WpfPoint(centerX - rect.Width * 0.16, (chest.Y + handY) / 2 - rect.Height * 0.03));
        dc.DrawLine(pinkPen, new WpfPoint(centerX - rect.Width * 0.16, (chest.Y + handY) / 2 - rect.Height * 0.03), new WpfPoint(centerX - rect.Width * 0.19, handY));
        dc.DrawLine(pinkPen, new WpfPoint(chest.X + rect.Width * 0.06, chest.Y), new WpfPoint(centerX + rect.Width * 0.16, (chest.Y + handY) / 2 - rect.Height * 0.03));
        dc.DrawLine(pinkPen, new WpfPoint(centerX + rect.Width * 0.16, (chest.Y + handY) / 2 - rect.Height * 0.03), new WpfPoint(centerX + rect.Width * 0.19, handY));

        DrawPhotoHead(dc, headRect);
        DrawCenteredText(dc, "BENCH!", new WpfRect(0, rect.Height * 0.04, rect.Width, rect.Height * 0.12), Math.Max(12, rect.Height * 0.06), Colors.Black, 0.70, FontWeights.Black);
    }

    private void DrawPhotoHead(DrawingContext dc, WpfRect rect)
    {
        var pink = new SolidColorBrush(Color.FromRgb(250, 46, 107));
        if (PetImage == null)
        {
            dc.DrawEllipse(null, new Pen(pink, Math.Max(3, rect.Height * 0.08)), new WpfPoint(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2), rect.Width / 2, rect.Height / 2);
            return;
        }

        var source = new Int32Rect(
            (int)(PetImage.PixelWidth * 0.30),
            (int)(PetImage.PixelHeight * 0.02),
            Math.Max(1, (int)(PetImage.PixelWidth * 0.48)),
            Math.Max(1, (int)(PetImage.PixelHeight * 0.54))
        );
        var crop = new CroppedBitmap(PetImage, source);
        dc.PushClip(new RectangleGeometry(rect, rect.Width * 0.28, rect.Height * 0.28));
        dc.DrawImage(crop, rect);
        dc.Pop();
        dc.DrawRoundedRectangle(null, new Pen(pink, Math.Max(2, rect.Height * 0.045)), rect, rect.Width * 0.28, rect.Height * 0.28);
    }

    private void DrawChickenFeeding(DrawingContext dc, WpfRect rect)
    {
        var progress = Math.Clamp((DateTime.Now - ChickenFeedStart).TotalSeconds / 2.15, 0, 1);
        var textAlpha = progress < 0.78 ? 1.0 : Math.Max(0.0, 1.0 - (progress - 0.78) / 0.22);
        var pop = Math.Sin(Math.Min(1.0, progress * 2.0) * Math.PI) * 8.0;
        var hearts = new[]
        {
            (-0.28, 0.30, 0.00, 0.045),
            (-0.14, 0.15, 0.20, 0.055),
            (0.15, 0.19, 0.36, 0.050),
            (0.29, 0.33, 0.55, 0.043),
            (0.02, 0.09, 0.72, 0.040)
        };

        foreach (var (xOffset, yOffset, phase, sizeRatio) in hearts)
        {
            var local = (progress + phase) % 1.0;
            var alpha = Math.Max(0, Math.Min(1, Math.Sin(local * Math.PI))) * textAlpha;
            var x = rect.Width / 2 + rect.Width * xOffset + Math.Sin(local * Math.PI * 2.0) * 6.0;
            var y = rect.Height * yOffset - local * rect.Height * 0.12;
            DrawText(dc, "♥", new WpfPoint(x, y), Math.Max(13, rect.Height * sizeRatio), Color.FromRgb(255, 51, 110), alpha, FontWeights.Black);
        }

        DrawCenteredText(
            dc,
            "냠냠",
            new WpfRect(rect.Width * 0.30, rect.Height * 0.18 - pop, rect.Width * 0.40, rect.Height * 0.13),
            Math.Max(15, rect.Height * 0.065),
            Color.FromRgb(250, 46, 100),
            textAlpha,
            FontWeights.Black
        );
    }

    private void DrawCursorThrow(DrawingContext dc, WpfRect rect)
    {
        var progress = Math.Clamp((DateTime.Now - CursorThrowStart).TotalSeconds / 0.70, 0, 1);
        var alpha = Math.Max(0.0, 1.0 - progress);
        var direction = FacesRight ? 1.0 : -1.0;
        var centerY = rect.Height * 0.37;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha * 184), 250, 46, 107)), Math.Max(3, rect.Height * 0.012))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        for (var index = 0; index < 3; index++)
        {
            var y = centerY + (index - 1) * rect.Height * 0.055;
            var x1 = rect.Width / 2 + direction * rect.Width * 0.02;
            var x2 = rect.Width / 2 + direction * rect.Width * (0.28 + index * 0.05);
            var curve = new StreamGeometry();
            using (var context = curve.Open())
            {
                context.BeginFigure(new WpfPoint(x1, y), false, false);
                context.BezierTo(
                    new WpfPoint(x1 + direction * rect.Width * 0.12, y - rect.Height * 0.10),
                    new WpfPoint(x2 - direction * rect.Width * 0.10, y + rect.Height * 0.07),
                    new WpfPoint(x2, y - rect.Height * 0.05),
                    true,
                    false
                );
            }
            curve.Freeze();
            dc.DrawGeometry(null, pen, curve);
        }

        DrawCenteredText(
            dc,
            "휙!",
            new WpfRect(rect.Width / 2 + direction * rect.Width * 0.08 - rect.Width * 0.15, centerY - rect.Height * 0.14, rect.Width * 0.30, rect.Height * 0.12),
            Math.Max(14, rect.Height * 0.060),
            Color.FromRgb(18, 21, 26),
            alpha,
            FontWeights.Black
        );
    }

    private void DrawCenteredText(DrawingContext dc, string text, WpfRect rect, double size, Color color, double alpha, FontWeight weight)
    {
        var formatted = MakeText(text, size, color, alpha, weight);
        dc.DrawText(formatted, new WpfPoint(rect.Left + (rect.Width - formatted.Width) / 2, rect.Top + (rect.Height - formatted.Height) / 2));
    }

    private void DrawText(DrawingContext dc, string text, WpfPoint center, double size, Color color, double alpha, FontWeight weight)
    {
        var formatted = MakeText(text, size, color, alpha, weight);
        dc.DrawText(formatted, new WpfPoint(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private FormattedText MakeText(string text, double size, Color color, double alpha, FontWeight weight)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B)),
            dpi
        );
    }
}

internal sealed class SpeechBubbleWindow : Window
{
    private readonly SpeechBubbleSurface _surface = new();

    public string Message
    {
        get => _surface.Message;
        set
        {
            _surface.Message = value;
            _surface.InvalidateVisual();
        }
    }

    public SpeechBubbleWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        IsHitTestVisible = false;
        Content = _surface;
    }

    public void SetFrame(WpfRect frame)
    {
        Left = frame.Left;
        Top = frame.Top;
        Width = frame.Width;
        Height = frame.Height;
        _surface.Width = frame.Width;
        _surface.Height = frame.Height;
    }
}

internal sealed class SpeechBubbleSurface : FrameworkElement
{
    public string Message { get; set; } = "hug me...";

    protected override void OnRender(DrawingContext dc)
    {
        var rect = new WpfRect(7, 8, Math.Max(1, ActualWidth - 14), Math.Max(1, ActualHeight - 17));
        var bubble = new RectangleGeometry(rect, 22, 22);
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 251, 253)), new Pen(new SolidColorBrush(Color.FromRgb(255, 148, 194)), 1.6), bubble);

        var tail = new StreamGeometry();
        using (var context = tail.Open())
        {
            context.BeginFigure(new WpfPoint(rect.Left + rect.Width * 0.48, rect.Bottom - 1), true, true);
            context.LineTo(new WpfPoint(rect.Left + rect.Width * 0.56, rect.Bottom + 10), true, false);
            context.LineTo(new WpfPoint(rect.Left + rect.Width * 0.62, rect.Bottom - 1), true, false);
        }
        tail.Freeze();
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 251, 253)), new Pen(new SolidColorBrush(Color.FromRgb(255, 148, 194)), 1.6), tail);

        var textRect = new WpfRect(rect.Left + 14, rect.Top + 5, rect.Width - 28, rect.Height - 10);
        DrawFittedText(dc, Message, textRect);
    }

    private void DrawFittedText(DrawingContext dc, string text, WpfRect rect)
    {
        for (var size = 21.0; size >= 11.0; size -= 1.0)
        {
            var formatted = MakeText(text, size);
            formatted.MaxTextWidth = rect.Width;
            if (formatted.Height <= rect.Height)
            {
                dc.DrawText(formatted, new WpfPoint(rect.Left + (rect.Width - formatted.Width) / 2, rect.Top + (rect.Height - formatted.Height) / 2));
                return;
            }
        }
    }

    private FormattedText MakeText(string text, double size)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size,
            Brushes.Black,
            dpi
        );
    }
}

internal sealed class ChickenWindow : Window
{
    private bool _dragging;
    private WpfPoint _dragOffset;
    private readonly ChickenSurface _surface = new();

    public event Action? DragStarted;
    public event Action? Moved;
    public event Action? DragEnded;
    public WpfRect ScreenFrame => new(Left, Top, Width, Height);

    public ChickenWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Content = _surface;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    public void SetFrame(WpfRect frame)
    {
        Left = frame.Left;
        Top = frame.Top;
        Width = frame.Width;
        Height = frame.Height;
        _surface.Width = frame.Width;
        _surface.Height = frame.Height;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragOffset = e.GetPosition(this);
        CaptureMouse();
        DragStarted?.Invoke();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var point = PointToScreen(e.GetPosition(this));
        Left = point.X - _dragOffset.X;
        Top = point.Y - _dragOffset.Y;
        Moved?.Invoke();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();
        DragEnded?.Invoke();
    }
}

internal sealed class ChickenSurface : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        var rect = new WpfRect(9, 9, Math.Max(1, ActualWidth - 18), Math.Max(1, ActualHeight - 18));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new WpfPoint(rect.Left + rect.Width * 0.19, rect.Top + rect.Height * 0.87), true, true);
            context.BezierTo(
                new WpfPoint(rect.Left + rect.Width * 0.38, rect.Bottom + rect.Height * 0.03),
                new WpfPoint(rect.Right + rect.Width * 0.03, rect.Top + rect.Height * 0.90),
                new WpfPoint(rect.Right - rect.Width * 0.06, rect.Top + rect.Height * 0.64),
                true,
                false
            );
            context.BezierTo(
                new WpfPoint(rect.Right + rect.Width * 0.05, rect.Top + rect.Height * 0.30),
                new WpfPoint(rect.Right - rect.Width * 0.04, rect.Top - rect.Height * 0.03),
                new WpfPoint(rect.Right - rect.Width * 0.24, rect.Top + rect.Height * 0.06),
                true,
                false
            );
            context.BezierTo(
                new WpfPoint(rect.Left + rect.Width * 0.45, rect.Top - rect.Height * 0.08),
                new WpfPoint(rect.Left - rect.Width * 0.04, rect.Top + rect.Height * 0.05),
                new WpfPoint(rect.Left + rect.Width * 0.08, rect.Top + rect.Height * 0.66),
                true,
                false
            );
            context.BezierTo(
                new WpfPoint(rect.Left - rect.Width * 0.03, rect.Top + rect.Height * 0.80),
                new WpfPoint(rect.Left + rect.Width * 0.07, rect.Top + rect.Height * 0.88),
                new WpfPoint(rect.Left + rect.Width * 0.19, rect.Top + rect.Height * 0.87),
                true,
                false
            );
        }
        geometry.Freeze();
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 199, 143)), new Pen(new SolidColorBrush(Color.FromRgb(199, 107, 56)), 2), geometry);

        var markPen = new Pen(new SolidColorBrush(Color.FromArgb(225, 240, 133, 69)), 1.6)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        foreach (var offset in new[] { 0.32, 0.49, 0.66 })
        {
            var x = rect.Left + rect.Width * offset;
            dc.DrawLine(markPen, new WpfPoint(x - rect.Width * 0.07, rect.Top + rect.Height * 0.75), new WpfPoint(x + rect.Width * 0.09, rect.Top + rect.Height * 0.24));
        }
    }
}

internal sealed class SettingsWindow : Window
{
    private readonly CheckBox _randomBenchCheckbox;
    private readonly Action<bool> _setRandomBench;

    public SettingsWindow(string appName, Action performBenchPress, Func<bool> randomBenchEnabled, Action<bool> setRandomBench)
    {
        _setRandomBench = setRandomBench;
        Title = $"{appName} 설정";
        Width = 320;
        Height = 190;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        var panel = new StackPanel
        {
            Margin = new Thickness(22, 18, 22, 18)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "운동",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var benchButton = new Button
        {
            Content = "벤치프레스 하기",
            Height = 32,
            Margin = new Thickness(0, 0, 0, 12)
        };
        benchButton.Click += (_, _) => performBenchPress();
        panel.Children.Add(benchButton);

        _randomBenchCheckbox = new CheckBox
        {
            Content = "랜덤 벤치프레스",
            IsChecked = randomBenchEnabled()
        };
        _randomBenchCheckbox.Checked += (_, _) => _setRandomBench(true);
        _randomBenchCheckbox.Unchecked += (_, _) => _setRandomBench(false);
        panel.Children.Add(_randomBenchCheckbox);

        Content = panel;
    }

    public void SetRandomBenchPressEnabled(bool enabled)
    {
        _randomBenchCheckbox.IsChecked = enabled;
    }
}

internal sealed class PromptWindow : Window
{
    private readonly TextBox _input = new();
    private string? _result;

    private PromptWindow(string appName, string title, string currentValue)
    {
        Title = title;
        Width = 360;
        Height = 150;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        var panel = new DockPanel
        {
            Margin = new Thickness(16)
        };
        _input.Text = currentValue;
        _input.Height = 26;
        _input.Margin = new Thickness(0, 0, 0, 14);
        DockPanel.SetDock(_input, Dock.Top);
        panel.Children.Add(_input);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var save = new Button { Content = "저장", Width = 76, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "취소", Width = 76, Height = 28 };
        save.Click += (_, _) =>
        {
            _result = _input.Text;
            DialogResult = true;
        };
        cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);

        Content = panel;
        Loaded += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };
    }

    public static string? Show(string appName, string title, string currentValue)
    {
        var window = new PromptWindow(appName, title, currentValue);
        return window.ShowDialog() == true ? window._result : null;
    }
}

internal static class TrayIconFactory
{
    public static Drawing.Icon CreateHeartIcon()
    {
        var bitmap = new Drawing.Bitmap(64, 64);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Drawing.Color.Transparent);
        using var circleBrush = new Drawing2D.LinearGradientBrush(
            new Drawing.Rectangle(0, 0, 64, 64),
            Drawing.Color.FromArgb(255, 255, 96, 151),
            Drawing.Color.FromArgb(255, 230, 38, 115),
            45
        );
        graphics.FillEllipse(circleBrush, 5, 5, 54, 54);
        using var font = new Drawing.Font("Segoe UI Symbol", 31, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
        using var textBrush = new Drawing.SolidBrush(Drawing.Color.White);
        using var format = new Drawing.StringFormat
        {
            Alignment = Drawing.StringAlignment.Center,
            LineAlignment = Drawing.StringAlignment.Center
        };
        graphics.DrawString("♥", font, textBrush, new Drawing.RectangleF(4, 0, 56, 58), format);
        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }
}

internal static class RectExtensions
{
    public static WpfRect Inset(this WpfRect rect, double dx, double dy)
    {
        return new WpfRect(rect.Left + dx, rect.Top + dy, Math.Max(1, rect.Width - dx * 2), Math.Max(1, rect.Height - dy * 2));
    }
}
