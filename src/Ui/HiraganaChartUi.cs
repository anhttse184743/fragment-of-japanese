using Godot;
using System;
using System.Collections.Generic;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Giao diện bảng chữ cái Hiragana 46 chữ, hiển thị 5 cột, có âm thanh.
/// </summary>
public partial class HiraganaChartUi : CanvasLayer
{
    private static HiraganaChartUi _instance;
    public static HiraganaChartUi Instance => _instance;

    [Export] private Control _root;
    [Export] private GridContainer _grid;
    [Export] private Button _btnClose;
    [Export] private AudioStreamPlayer _audioPlayer;

    private Dictionary<string, AudioStream> _audioCache = new();

    public override void _Ready()
    {
        _instance = this;
        if (_root != null) _root.Visible = false;
        
        if (_btnClose != null)
        {
            _btnClose.Pressed += Close;
        }
        
        PopulateGrid();
    }

    public void Open()
    {
        if (_root != null)
        {
            _root.Visible = true;
            SetHudVisible(false); // Ẩn joystick/HUD
            
            // Tạo animation fade-in nhẹ
            _root.Modulate = new Color(1, 1, 1, 0);
            var tween = CreateTween();
            tween.TweenProperty(_root, "modulate", Colors.White, 0.2f);
        }
    }

    public void Close()
    {
        if (_root != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(_root, "modulate", new Color(1, 1, 1, 0), 0.2f);
            tween.TweenCallback(Callable.From(() => {
                _root.Visible = false;
                SetHudVisible(true); // Hiện lại joystick/HUD
            }));
        }
    }

    private void SetHudVisible(bool visible)
    {
        foreach (Node n in GetTree().GetNodesInGroup("hud"))
        {
            if (n is CanvasLayer cl) cl.Visible = visible;
            else if (n is CanvasItem ci) ci.Visible = visible;
        }
    }

    private void PopulateGrid()
    {
        if (_grid == null) return;

        // Xóa các children cũ nếu có
        foreach (Node child in _grid.GetChildren())
        {
            child.QueueFree();
        }

        var hiraList = JapaneseDB.Instance.Hiragana;
        
        // Mảng 2D: [row, col] -> 5 hàng (A, I, U, E, O), 11 cột (A..Wa, N)
        VocabularyEntry[,] gridData = new VocabularyEntry[5, 11];

        void FillCol(int col, int startIdx, bool hasY = false, bool isWa = false, bool isN = false)
        {
            if (isN)
            {
                if (startIdx < hiraList.Count) gridData[0, col] = hiraList[startIdx]; // N
                return;
            }
            if (isWa)
            {
                if (startIdx < hiraList.Count) gridData[0, col] = hiraList[startIdx];     // WA
                if (startIdx + 1 < hiraList.Count) gridData[4, col] = hiraList[startIdx + 1]; // WO
                return;
            }
            if (hasY)
            {
                if (startIdx < hiraList.Count) gridData[0, col] = hiraList[startIdx];     // YA
                if (startIdx + 1 < hiraList.Count) gridData[2, col] = hiraList[startIdx + 1]; // YU
                if (startIdx + 2 < hiraList.Count) gridData[4, col] = hiraList[startIdx + 2]; // YO
                return;
            }
            // Bình thường (5 chữ)
            for (int r = 0; r < 5; r++)
            {
                if (startIdx + r < hiraList.Count)
                    gridData[r, col] = hiraList[startIdx + r];
            }
        }

        FillCol(0, 0);   // A
        FillCol(1, 5);   // KA
        FillCol(2, 10);  // SA
        FillCol(3, 15);  // TA
        FillCol(4, 20);  // NA
        FillCol(5, 25);  // HA
        FillCol(6, 30);  // MA
        FillCol(7, 35, true); // YA
        FillCol(8, 38);  // RA
        FillCol(9, 43, false, true); // WA
        FillCol(10, 45, false, false, true); // N

        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 11; c++)
            {
                var entry = gridData[r, c];
                if (entry != null)
                {
                    _grid.AddChild(CreateKanaButton(entry));
                }
                else
                {
                    _grid.AddChild(CreateSpacer());
                }
            }
        }
    }

    private Control CreateSpacer()
    {
        return new Control { CustomMinimumSize = new Vector2(130, 130) };
    }

    private Button CreateKanaButton(VocabularyEntry entry)
    {
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(130, 130),
            FocusMode = Control.FocusModeEnum.None
        };

        // Trang trí nút
        UiKit.StyleButton(btn, UiKit.BrownSlot, UiKit.BrownBorder, UiKit.BrownDark);
        
        var vbox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        btn.AddChild(vbox);
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var lblKana = new Label
        {
            Text = entry.Kana,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        lblKana.AddThemeFontSizeOverride("font_size", 64);
        lblKana.AddThemeColorOverride("font_color", UiKit.BrownText);

        var lblRomaji = new Label
        {
            Text = entry.Romaji,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        lblRomaji.AddThemeFontSizeOverride("font_size", 28);
        lblRomaji.AddThemeColorOverride("font_color", UiKit.BrownTextDim);

        vbox.AddChild(lblKana);
        vbox.AddChild(lblRomaji);

        btn.Pressed += () => PlayAudio(entry.Id);
        return btn;
    }

    private void PlayAudio(string id)
    {
        if (_audioPlayer == null) return;

        if (_audioCache.TryGetValue(id, out var stream))
        {
            _audioPlayer.Stream = stream;
            _audioPlayer.Play();
            return;
        }

        string path = $"res://assets/audio/hiragana/{id}.mp3";
        if (ResourceLoader.Exists(path))
        {
            var loadedStream = ResourceLoader.Load<AudioStream>(path);
            _audioCache[id] = loadedStream;
            _audioPlayer.Stream = loadedStream;
            _audioPlayer.Play();
        }
        else
        {
            GD.Print($"[HiraganaChart] Không tìm thấy file âm thanh: {path}");
        }
    }
}
