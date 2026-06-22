using System;
using Godot;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Màn hình Đăng nhập / Đăng ký — dựng hoàn toàn bằng code, tông tối + vàng đồng,
/// nền gradient + quầng sáng + cánh hoa anh đào bay. Hai tab trượt mượt, ô nhập có
/// hiện/ẩn mật khẩu, nút có vòng quay "đang tải", báo lỗi rung nhẹ.
///
/// Sau khi đăng nhập/đăng ký/chơi khách → chuyển sang World qua SceneTransition.
/// Tài khoản lưu cục bộ qua <see cref="AccountManager"/> (xem file đó để đổi sang máy chủ).
/// </summary>
public partial class AuthScreen : Control
{
    private enum Mode { Login, Register }

    private const string WorldScene = "res://scenes/world/World.tscn";

    private const float CardWidth        = 436f;
    private const float CardContentWidth = 380f;
    private const float FormSlotHeight   = 268f;

    // ----- Bảng màu riêng của màn này (bổ sung cho UiKit) -----
    private static readonly Color BgTop    = new(0.07f, 0.08f, 0.13f);
    private static readonly Color BgBottom = new(0.02f, 0.02f, 0.05f);
    private static readonly Color CardBg   = new(0.10f, 0.11f, 0.16f, 0.97f);
    private static readonly Color FieldBg  = new(0.055f, 0.065f, 0.10f);
    private static readonly Color FieldEdge = new(0.24f, 0.26f, 0.33f);
    private static readonly Color DarkOnGold = new(0.16f, 0.11f, 0.04f);
    private static readonly Color PetalPink  = new(1f, 0.72f, 0.82f);
    private static readonly Color OkColor    = new(0.55f, 0.92f, 0.60f);
    private static readonly Color ErrColor   = new(1f, 0.50f, 0.45f);

    private Mode _mode = Mode.Login;
    private bool _busy;

    private readonly RandomNumberGenerator _rng = new();
    private SystemFont _jp;

    // Node tham chiếu
    private TextureRect _moon;
    private Node2D      _petals;
    private PanelContainer _card;
    private Panel  _tabHi;
    private Button _btnLogin, _btnReg, _primary;
    private Spinner _spinner;
    private Control _formSlot, _loginForm, _regForm;
    private Label   _msg;
    private CheckButton _remember;
    private LineEdit _userLogin, _passLogin, _userReg, _passReg, _confirmReg;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        _rng.Randomize();

        BuildBackground();
        BuildCard();

        // Phím Enter trong ô → nhảy ô kế / gửi
        _userLogin.TextSubmitted   += _ => _passLogin.GrabFocus();
        _passLogin.TextSubmitted   += _ => Submit();
        _userReg.TextSubmitted     += _ => _passReg.GrabFocus();
        _passReg.TextSubmitted     += _ => _confirmReg.GrabFocus();
        _confirmReg.TextSubmitted  += _ => Submit();

        PlayIntro();
    }

    // ───────────────────────── Nền ─────────────────────────

    private void BuildBackground()
    {
        var baseRect = new ColorRect { Color = BgBottom, MouseFilter = MouseFilterEnum.Ignore };
        baseRect.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(baseRect);

        // Gradient dọc
        var grad = new Gradient();
        grad.SetColor(0, BgTop);
        grad.SetColor(1, BgBottom);
        var gradTex = new GradientTexture2D
        {
            Gradient = grad, Width = 8, Height = 256,
            FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1),
        };
        var bg = new TextureRect { Texture = gradTex, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // Quầng sáng vàng (radial) phía sau thẻ
        var glowGrad = new Gradient();
        glowGrad.SetColor(0, UiKit.Fade(UiKit.Accent, 0.50f));
        glowGrad.SetColor(1, UiKit.Fade(UiKit.Accent, 0f));
        var glowTex = new GradientTexture2D
        {
            Gradient = glowGrad, Width = 256, Height = 256,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
        };
        _moon = new TextureRect
        {
            Texture = glowTex, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Size = new Vector2(880, 880), Position = new Vector2(960 - 440, 360 - 440),
            Modulate = new Color(1, 1, 1, 0.7f),
        };
        AddChild(_moon);

        // Cánh hoa anh đào bay
        _petals = new Node2D();
        AddChild(_petals);
        for (int i = 0; i < 16; i++)
            SpawnPetal(onScreen: true);
    }

    private void SpawnPetal(bool onScreen)
    {
        var petal = new Polygon2D
        {
            Polygon = new Vector2[]
            {
                new(0, -13), new(7, -4), new(5, 8), new(0, 13), new(-5, 8), new(-7, -4),
            },
            Color = UiKit.Fade(PetalPink, _rng.RandfRange(0.22f, 0.5f)),
        };
        _petals.AddChild(petal);
        AnimatePetal(petal, onScreen);
    }

    private void AnimatePetal(Polygon2D petal, bool onScreen)
    {
        if (!IsInstanceValid(petal)) return;

        float x      = _rng.RandfRange(-40, 1960);
        float startY = onScreen ? _rng.RandfRange(-80, 1080) : _rng.RandfRange(-260, -40);
        float drift  = _rng.RandfRange(-160, 160);
        float dur    = _rng.RandfRange(8f, 15f);
        float s      = _rng.RandfRange(0.5f, 1.15f);

        petal.Position = new Vector2(x, startY);
        petal.Scale    = new Vector2(s, s);
        petal.Rotation = _rng.RandfRange(0f, Mathf.Tau);

        var tw = CreateTween();
        tw.TweenProperty(petal, "position:y", 1140f, dur).SetTrans(Tween.TransitionType.Linear);
        tw.Parallel().TweenProperty(petal, "position:x", x + drift, dur)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.Parallel().TweenProperty(petal, "rotation", petal.Rotation + _rng.RandfRange(-5f, 5f), dur);
        tw.TweenCallback(Callable.From(() => AnimatePetal(petal, onScreen: false)));
    }

    // ───────────────────────── Thẻ ─────────────────────────

    private void BuildCard()
    {
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _card = new PanelContainer { Modulate = new Color(1, 1, 1, 0) };
        _card.AddThemeStyleboxOverride("panel", UiKit.Box(CardBg, 18, UiKit.Fade(UiKit.Accent, 0.5f), 1, 28, 26));
        center.AddChild(_card);

        var col = new VBoxContainer { CustomMinimumSize = new Vector2(CardContentWidth, 0) };
        col.AddThemeConstantOverride("separation", 18);
        _card.AddChild(col);

        col.AddChild(BuildHeader());
        col.AddChild(BuildTabs());
        col.AddChild(BuildFormSlot());
        col.AddChild(BuildMessage());
        col.AddChild(BuildPrimary());
        col.AddChild(BuildFooter());

        UpdateTabVisual();
    }

    private Control BuildHeader()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);

        var ringHolder = new CenterContainer();
        var ring = new Panel { CustomMinimumSize = new Vector2(66, 66) };
        ring.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Fade(UiKit.Accent, 0.14f), 33, UiKit.Accent, 2));
        var rc = new CenterContainer();
        rc.SetAnchorsPreset(LayoutPreset.FullRect);
        ring.AddChild(rc);
        var kana = new Label { Text = "あ" };
        kana.AddThemeFontOverride("font", JpFont());
        kana.AddThemeFontSizeOverride("font_size", 30);
        kana.AddThemeColorOverride("font_color", UiKit.Gold);
        rc.AddChild(kana);
        ringHolder.AddChild(ring);
        v.AddChild(ringHolder);

        var title = new Label { Text = "FRAGMENT OF JAPANESE", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", Colors.White);
        v.AddChild(title);

        var sub = new Label { Text = "Hành trình chinh phục tiếng Nhật", HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 13);
        sub.AddThemeColorOverride("font_color", UiKit.Fade(UiKit.Accent, 0.85f));
        v.AddChild(sub);

        return v;
    }

    private Control BuildTabs()
    {
        var tabs = new Control { CustomMinimumSize = new Vector2(CardContentWidth, 48) };

        var pill = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        pill.SetAnchorsPreset(LayoutPreset.FullRect);
        pill.AddThemeStyleboxOverride("panel", UiKit.Box(FieldBg, 12, UiKit.Fade(UiKit.Accent, 0.18f), 1));
        tabs.AddChild(pill);

        float hiW = CardContentWidth / 2f - 4f;
        _tabHi = new Panel { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(hiW, 40), Position = new Vector2(4, 4) };
        _tabHi.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Accent, 9));
        tabs.AddChild(_tabHi);

        _btnLogin = MakeTabButton("ĐĂNG NHẬP");
        SetHalf(_btnLogin, left: true);
        _btnLogin.Pressed += () => SwitchMode(Mode.Login);
        tabs.AddChild(_btnLogin);

        _btnReg = MakeTabButton("ĐĂNG KÝ");
        SetHalf(_btnReg, left: false);
        _btnReg.Pressed += () => SwitchMode(Mode.Register);
        tabs.AddChild(_btnReg);

        return tabs;
    }

    private static void SetHalf(Control c, bool left)
    {
        c.AnchorLeft   = left ? 0f : 0.5f;
        c.AnchorRight  = left ? 0.5f : 1f;
        c.AnchorTop    = 0f;
        c.AnchorBottom = 1f;
        c.OffsetLeft = 0; c.OffsetTop = 0; c.OffsetRight = 0; c.OffsetBottom = 0;
    }

    private Button MakeTabButton(string text)
    {
        var b = new Button { Text = text, Flat = true, FocusMode = FocusModeEnum.None };
        var clear = UiKit.Box(new Color(0, 0, 0, 0), 9);
        b.AddThemeStyleboxOverride("normal", clear);
        b.AddThemeStyleboxOverride("hover", clear);
        b.AddThemeStyleboxOverride("pressed", clear);
        b.AddThemeStyleboxOverride("focus", clear);
        b.AddThemeFontSizeOverride("font_size", 15);
        return b;
    }

    private Control BuildFormSlot()
    {
        _formSlot = new Control { CustomMinimumSize = new Vector2(CardContentWidth, FormSlotHeight) };

        _loginForm = BuildLoginForm();
        _loginForm.SetAnchorsPreset(LayoutPreset.FullRect);
        _formSlot.AddChild(_loginForm);

        _regForm = BuildRegisterForm();
        _regForm.SetAnchorsPreset(LayoutPreset.FullRect);
        _regForm.Visible  = false;
        _regForm.Modulate = new Color(1, 1, 1, 0);
        _formSlot.AddChild(_regForm);

        return _formSlot;
    }

    private Control BuildLoginForm()
    {
        var form = new VBoxContainer();
        form.AddThemeConstantOverride("separation", 14);

        _userLogin = AddField(form, "Tên đăng nhập", "Nhập tên đăng nhập", secret: false);
        _passLogin = AddField(form, "Mật khẩu", "Nhập mật khẩu", secret: true);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        _remember = new CheckButton { Text = "Ghi nhớ đăng nhập" };
        _remember.AddThemeFontSizeOverride("font_size", 13);
        _remember.AddThemeColorOverride("font_color", UiKit.TextDim);
        _remember.AddThemeColorOverride("font_hover_color", Colors.White);
        _remember.AddThemeColorOverride("font_pressed_color", Colors.White);
        row.AddChild(_remember);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var forgot = MakeLinkButton("Quên mật khẩu?", 13);
        forgot.Pressed += () => UiKit.Toast(this, "Tính năng khôi phục mật khẩu sắp ra mắt.");
        row.AddChild(forgot);

        form.AddChild(row);
        return form;
    }

    private Control BuildRegisterForm()
    {
        var form = new VBoxContainer();
        form.AddThemeConstantOverride("separation", 14);

        _userReg    = AddField(form, "Tên đăng nhập", "Tạo tên đăng nhập", secret: false);
        _passReg    = AddField(form, "Mật khẩu", "Tạo mật khẩu", secret: true);
        _confirmReg = AddField(form, "Nhập lại mật khẩu", "Nhập lại mật khẩu", secret: true);

        var hint = new Label { Text = "Tên 3–20 ký tự · Mật khẩu từ 6 ký tự.", HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", UiKit.TextDim);
        form.AddChild(hint);

        return form;
    }

    private LineEdit AddField(VBoxContainer form, string label, string placeholder, bool secret)
    {
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 5);

        var l = new Label { Text = label };
        l.AddThemeFontSizeOverride("font_size", 13);
        l.AddThemeColorOverride("font_color", new Color(0.78f, 0.80f, 0.86f));
        block.AddChild(l);

        var le = new LineEdit
        {
            PlaceholderText   = placeholder,
            Secret            = secret,
            CustomMinimumSize = new Vector2(0, 46),
            CaretBlink        = true,
        };
        StyleField(le, secret);
        block.AddChild(le);
        if (secret) AddEyeToggle(le);

        form.AddChild(block);
        return le;
    }

    private void StyleField(LineEdit le, bool secret)
    {
        var normal = UiKit.Box(FieldBg, 11, FieldEdge, 1);
        normal.ContentMarginLeft   = 14;
        normal.ContentMarginRight  = secret ? 56 : 14;
        normal.ContentMarginTop    = 10;
        normal.ContentMarginBottom = 10;
        le.AddThemeStyleboxOverride("normal", normal);
        le.AddThemeStyleboxOverride("read_only", normal);

        le.AddThemeStyleboxOverride("focus", UiKit.Box(new Color(0, 0, 0, 0), 11, UiKit.Accent, 2));
        le.AddThemeColorOverride("font_color", new Color(0.96f, 0.96f, 0.99f));
        le.AddThemeColorOverride("font_placeholder_color", new Color(0.50f, 0.52f, 0.60f));
        le.AddThemeColorOverride("caret_color", UiKit.Accent);
        le.AddThemeColorOverride("selection_color", UiKit.Fade(UiKit.Accent, 0.30f));
        le.AddThemeFontSizeOverride("font_size", 16);
    }

    private void AddEyeToggle(LineEdit le)
    {
        var btn = new Button { Text = "HIỆN", Flat = true, FocusMode = FocusModeEnum.None };
        var clear = UiKit.Box(new Color(0, 0, 0, 0), 6);
        btn.AddThemeStyleboxOverride("normal", clear);
        btn.AddThemeStyleboxOverride("hover", clear);
        btn.AddThemeStyleboxOverride("pressed", clear);
        btn.AddThemeStyleboxOverride("focus", clear);
        btn.AddThemeFontSizeOverride("font_size", 11);
        btn.AddThemeColorOverride("font_color", UiKit.Fade(UiKit.Accent, 0.85f));
        btn.AddThemeColorOverride("font_hover_color", UiKit.Gold);

        btn.AnchorLeft = 1; btn.AnchorRight = 1; btn.AnchorTop = 0.5f; btn.AnchorBottom = 0.5f;
        btn.OffsetLeft = -50; btn.OffsetRight = -8; btn.OffsetTop = -15; btn.OffsetBottom = 15;
        btn.Pressed += () =>
        {
            le.Secret = !le.Secret;
            btn.Text  = le.Secret ? "HIỆN" : "ẨN";
        };
        le.AddChild(btn);
    }

    private Label BuildMessage()
    {
        _msg = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize   = new Vector2(0, 20),
        };
        _msg.AddThemeFontSizeOverride("font_size", 13);
        return _msg;
    }

    private Control BuildPrimary()
    {
        _primary = new Button { Text = PrimaryText(), CustomMinimumSize = new Vector2(0, 52), FocusMode = FocusModeEnum.None };
        _primary.AddThemeStyleboxOverride("normal",  UiKit.Box(UiKit.Accent, 12));
        _primary.AddThemeStyleboxOverride("hover",   UiKit.Box(UiKit.Gold, 12));
        _primary.AddThemeStyleboxOverride("pressed", UiKit.Box(new Color(0.72f, 0.56f, 0.26f), 12));
        _primary.AddThemeStyleboxOverride("focus",   UiKit.Box(new Color(0, 0, 0, 0), 12));
        _primary.AddThemeColorOverride("font_color", DarkOnGold);
        _primary.AddThemeColorOverride("font_hover_color", new Color(0.12f, 0.08f, 0.02f));
        _primary.AddThemeColorOverride("font_pressed_color", new Color(0.12f, 0.08f, 0.02f));
        _primary.AddThemeFontSizeOverride("font_size", 18);
        _primary.Pressed      += Submit;
        _primary.MouseEntered += () => HoverScale(_primary, 1.03f);
        _primary.MouseExited  += () => HoverScale(_primary, 1.0f);

        _spinner = new Spinner
        {
            CustomMinimumSize = new Vector2(26, 26),
            Visible           = false,
            MouseFilter       = MouseFilterEnum.Ignore,
            Color             = DarkOnGold,
        };
        _spinner.AnchorLeft = 0.5f; _spinner.AnchorRight = 0.5f; _spinner.AnchorTop = 0.5f; _spinner.AnchorBottom = 0.5f;
        _spinner.OffsetLeft = -13; _spinner.OffsetTop = -13; _spinner.OffsetRight = 13; _spinner.OffsetBottom = 13;
        _primary.AddChild(_spinner);

        return _primary;
    }

    private Control BuildFooter()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);

        var divider = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        divider.AddThemeConstantOverride("separation", 10);
        divider.AddChild(MakeHLine());
        var or = new Label { Text = "hoặc" };
        or.AddThemeFontSizeOverride("font_size", 12);
        or.AddThemeColorOverride("font_color", UiKit.TextDim);
        divider.AddChild(or);
        divider.AddChild(MakeHLine());
        v.AddChild(divider);

        var guest = MakeLinkButton("Chơi với tư cách khách", 14);
        guest.Pressed += OnGuest;
        var gc = new CenterContainer();
        gc.AddChild(guest);
        v.AddChild(gc);

        return v;
    }

    private static Control MakeHLine() => new ColorRect
    {
        Color               = UiKit.Fade(Colors.White, 0.12f),
        CustomMinimumSize   = new Vector2(0, 1),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical   = SizeFlags.ShrinkCenter,
        MouseFilter         = MouseFilterEnum.Ignore,
    };

    private Button MakeLinkButton(string text, int fontSize)
    {
        var b = new Button { Text = text, Flat = true, FocusMode = FocusModeEnum.None };
        var clear = UiKit.Box(new Color(0, 0, 0, 0), 6);
        b.AddThemeStyleboxOverride("normal", clear);
        b.AddThemeStyleboxOverride("hover", clear);
        b.AddThemeStyleboxOverride("pressed", clear);
        b.AddThemeStyleboxOverride("focus", clear);
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.AddThemeColorOverride("font_color", UiKit.Fade(UiKit.Accent, 0.85f));
        b.AddThemeColorOverride("font_hover_color", UiKit.Gold);
        return b;
    }

    // ───────────────────────── Hành vi ─────────────────────────

    private string PrimaryText() => _mode == Mode.Login ? "ĐĂNG NHẬP" : "TẠO TÀI KHOẢN";

    private void SwitchMode(Mode mode)
    {
        if (_busy || mode == _mode) return;
        _mode = mode;

        float tx = mode == Mode.Login ? 4f : CardContentWidth / 2f;
        CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out)
            .TweenProperty(_tabHi, "position:x", tx, 0.25);

        UpdateTabVisual();

        Control show = mode == Mode.Login ? _loginForm : _regForm;
        Control hide = mode == Mode.Login ? _regForm : _loginForm;

        var outTw = CreateTween();
        outTw.TweenProperty(hide, "modulate:a", 0f, 0.12);
        outTw.TweenCallback(Callable.From(() => hide.Visible = false));

        show.Visible  = true;
        show.Modulate = new Color(1, 1, 1, 0);
        var inTw = CreateTween();
        inTw.TweenInterval(0.10);
        inTw.TweenProperty(show, "modulate:a", 1f, 0.16);

        SetMessage("", UiKit.TextDim);
        _primary.Text = PrimaryText();

        var first = mode == Mode.Login ? _userLogin : _userReg;
        first.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void UpdateTabVisual()
    {
        _btnLogin.AddThemeColorOverride("font_color", _mode == Mode.Login ? DarkOnGold : UiKit.TextDim);
        _btnLogin.AddThemeColorOverride("font_hover_color", _mode == Mode.Login ? DarkOnGold : Colors.White);
        _btnReg.AddThemeColorOverride("font_color", _mode == Mode.Register ? DarkOnGold : UiKit.TextDim);
        _btnReg.AddThemeColorOverride("font_hover_color", _mode == Mode.Register ? DarkOnGold : Colors.White);
    }

    private void Submit()
    {
        if (_busy) return;
        if (_mode == Mode.Login) DoLogin();
        else                     DoRegister();
    }

    private async void DoLogin()
    {
        string u = _userLogin.Text.Trim();
        string p = _passLogin.Text;
        if (u.Length == 0 || p.Length == 0) { Fail("Vui lòng nhập đầy đủ thông tin."); return; }

        SetBusy(true);
        SetMessage("Đang đăng nhập...", UiKit.TextDim);
        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this)) return;

        var mgr = AccountManager.Instance;
        var res = mgr != null ? mgr.Login(u, p, _remember.ButtonPressed) : AccountManager.AuthResult.Error;

        if (res == AccountManager.AuthResult.Ok)
        {
            SetMessage("Đăng nhập thành công!", OkColor);
            await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);
            if (!IsInstanceValid(this)) return;
            Proceed();
        }
        else
        {
            SetBusy(false);
            Fail(MsgFor(res));
        }
    }

    private async void DoRegister()
    {
        string u = _userReg.Text.Trim();
        string p = _passReg.Text;
        string c = _confirmReg.Text;
        if (u.Length == 0 || p.Length == 0 || c.Length == 0) { Fail("Vui lòng nhập đầy đủ thông tin."); return; }

        SetBusy(true);
        SetMessage("Đang tạo tài khoản...", UiKit.TextDim);
        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this)) return;

        var mgr = AccountManager.Instance;
        var res = mgr != null ? mgr.Register(u, p, c) : AccountManager.AuthResult.Error;

        if (res == AccountManager.AuthResult.Ok)
        {
            mgr.Login(u, p, remember: true);   // tạo xong đăng nhập luôn
            SetMessage("Tạo tài khoản thành công!", OkColor);
            await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);
            if (!IsInstanceValid(this)) return;
            Proceed();
        }
        else
        {
            SetBusy(false);
            Fail(MsgFor(res));
        }
    }

    private void OnGuest()
    {
        if (_busy) return;
        AccountManager.Instance?.PlayAsGuest();
        SetMessage("Đang vào game...", UiKit.TextDim);
        Proceed();
    }

    private void Proceed()
    {
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.GoTo(WorldScene, allowAd: false);
        else
            GetTree().ChangeSceneToFile(WorldScene);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        if (_spinner != null) _spinner.Visible = busy;
        if (_primary != null) _primary.Text = busy ? "" : PrimaryText();
    }

    private void Fail(string message)
    {
        SetMessage(message, ErrColor);
        Shake();
    }

    private void SetMessage(string text, Color color)
    {
        if (_msg == null) return;
        _msg.Text = text;
        _msg.AddThemeColorOverride("font_color", color);
    }

    private static string MsgFor(AccountManager.AuthResult res) => res switch
    {
        AccountManager.AuthResult.EmptyFields      => "Vui lòng nhập đầy đủ thông tin.",
        AccountManager.AuthResult.InvalidUsername  => "Tên đăng nhập cần 3–20 ký tự (chữ, số, _).",
        AccountManager.AuthResult.InvalidPassword  => "Mật khẩu cần ít nhất 6 ký tự.",
        AccountManager.AuthResult.PasswordMismatch => "Mật khẩu nhập lại không khớp.",
        AccountManager.AuthResult.UserExists       => "Tên đăng nhập đã tồn tại.",
        AccountManager.AuthResult.UserNotFound      => "Tài khoản không tồn tại.",
        AccountManager.AuthResult.WrongPassword    => "Sai mật khẩu.",
        _                                           => "Đã có lỗi xảy ra. Thử lại nhé.",
    };

    // ───────────────────────── Hiệu ứng ─────────────────────────

    private async void PlayIntro()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInstanceValid(this) || _card == null) return;

        _card.PivotOffset = _card.Size / 2f;
        _card.Scale       = new Vector2(0.93f, 0.93f);

        var tw = CreateTween().SetParallel(true);
        tw.TweenProperty(_card, "modulate:a", 1f, 0.35).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(_card, "scale", Vector2.One, 0.42).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        // Điền sẵn tên đã ghi nhớ
        var mgr = AccountManager.Instance;
        if (mgr != null && !string.IsNullOrEmpty(mgr.LastUsername))
        {
            _userLogin.Text          = mgr.LastUsername;
            _remember.ButtonPressed  = mgr.RememberPref;
            _passLogin.CallDeferred(Control.MethodName.GrabFocus);
        }
        else
        {
            _userLogin.CallDeferred(Control.MethodName.GrabFocus);
        }

        if (_moon != null)
        {
            var m = CreateTween().SetLoops();
            m.TweenProperty(_moon, "modulate:a", 0.9f, 3.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            m.TweenProperty(_moon, "modulate:a", 0.55f, 3.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
    }

    private void HoverScale(Control c, float scale)
    {
        c.PivotOffset = c.Size / 2f;
        CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out)
            .TweenProperty(c, "scale", new Vector2(scale, scale), 0.12);
    }

    private void Shake()
    {
        if (_card == null) return;
        _card.PivotOffset = _card.Size / 2f;
        var t = CreateTween();
        t.TweenProperty(_card, "rotation", 0.03f, 0.05);
        t.TweenProperty(_card, "rotation", -0.025f, 0.06);
        t.TweenProperty(_card, "rotation", 0.015f, 0.06);
        t.TweenProperty(_card, "rotation", 0f, 0.05);
    }

    private SystemFont JpFont() => _jp ??= new SystemFont
    {
        FontNames = new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo", "Noto Sans CJK JP", "Noto Sans JP", "MS Gothic", "Hiragino Sans" },
        AllowSystemFallback = true,
    };
}
