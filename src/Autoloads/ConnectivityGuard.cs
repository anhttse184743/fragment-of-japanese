using System;
using System.Threading.Tasks;
using Godot;

namespace FragmentOfJapanese.Autoloads;

/// <summary>
/// Bảo đảm game chạy ONLINE: khi mất kết nối tới backend thì TẠM DỪNG toàn bộ gameplay
/// (SceneTree.Paused = true) và phủ overlay "Mất kết nối"; tự động mở lại khi có mạng.
///
/// Chỉ kích hoạt khi ĐÃ đăng nhập (có access token) — màn đăng nhập tự xử lý lỗi mạng riêng.
/// Node này + overlay đặt ProcessMode=Always để vẫn chạy khi cây bị pause.
/// </summary>
public partial class ConnectivityGuard : Node
{
    public static ConnectivityGuard Instance { get; private set; }

    // Kiểm tra định kỳ; offline thì thử lại nhanh hơn.
    private const float OnlineInterval  = 6f;
    private const float OfflineInterval = 2.5f;
    private const int   FailThreshold   = 2;   // số lần fail liên tiếp mới coi là offline (tránh báo nhầm)

    private float _timer;
    private int   _failStreak;
    private bool  _offline;
    private bool  _checking;

    private CanvasLayer _layer;
    private Control      _root;
    private Label        _statusLabel;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;   // vẫn chạy khi tree paused
        BuildOverlay();
        ApiClient.NetworkErrorDetected += OnNetworkError;
    }

    public override void _ExitTree()
    {
        ApiClient.NetworkErrorDetected -= OnNetworkError;
    }

    public override void _Process(double delta)
    {
        // Chỉ giám sát khi đã đăng nhập (đang trong game).
        if (string.IsNullOrEmpty(ApiClient.Instance?.AccessToken))
        {
            if (_offline) SetOnline();   // vừa logout khi đang offline → gỡ khóa
            return;
        }

        _timer += (float)delta;
        float interval = _offline ? OfflineInterval : OnlineInterval;
        if (_timer >= interval)
        {
            _timer = 0f;
            _ = CheckAsync();
        }
    }

    private void OnNetworkError()
    {
        // Có request vừa lỗi mạng (có thể từ thread nền) → chỉ ép _Process kiểm tra ở tick kế trên
        // MAIN THREAD (an toàn cho việc đụng UI / SceneTree.Paused). Không gọi CheckAsync trực tiếp ở đây.
        _timer = 99999f;
    }

    private async Task CheckAsync()
    {
        if (_checking) return;
        _checking = true;
        try
        {
            var res = await ApiClient.Instance.GetAsync("/health");
            bool ok = res.IsSuccessStatusCode;
            if (ok)
            {
                _failStreak = 0;
                if (_offline) SetOnline();
            }
            else
            {
                _failStreak++;
                if (_failStreak >= FailThreshold && !_offline) SetOffline();
            }
        }
        catch
        {
            _failStreak++;
            if (_failStreak >= FailThreshold && !_offline) SetOffline();
        }
        finally { _checking = false; }
    }

    private void SetOffline()
    {
        _offline = true;
        _timer = 0f;
        if (_layer != null) _layer.Visible = true;
        GetTree().Paused = true;
        GD.Print("[Connectivity] OFFLINE → tạm dừng gameplay.");
    }

    private void SetOnline()
    {
        _offline = false;
        _failStreak = 0;
        _timer = 0f;
        if (_layer != null) _layer.Visible = false;
        GetTree().Paused = false;
        GD.Print("[Connectivity] ONLINE → tiếp tục.");
    }

    // ── Overlay ───────────────────────────────────────────────────────────────
    private void BuildOverlay()
    {
        _layer = new CanvasLayer { Layer = 250, Visible = false, ProcessMode = ProcessModeEnum.Always };
        AddChild(_layer);

        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop, ProcessMode = ProcessModeEnum.Always };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.85f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 20);
        center.AddChild(box);

        var title = new Label { Text = "⚠  MẤT KẾT NỐI MẠNG", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 40);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.75f, 0.4f));
        box.AddChild(title);

        _statusLabel = new Label
        {
            Text = "Game cần kết nối Internet để chơi.\nĐang thử kết nối lại...",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 22);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.88f, 0.85f));
        box.AddChild(_statusLabel);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        box.AddChild(row);

        var retry = new Button { Text = "Thử lại ngay", CustomMinimumSize = new Vector2(200, 60), ProcessMode = ProcessModeEnum.Always };
        retry.Pressed += () => { _statusLabel.Text = "Đang kiểm tra..."; _ = CheckAsync(); };
        row.AddChild(retry);

        var quit = new Button { Text = "Thoát game", CustomMinimumSize = new Vector2(200, 60), ProcessMode = ProcessModeEnum.Always };
        quit.Pressed += () => GetTree().Quit();
        row.AddChild(quit);
    }
}
