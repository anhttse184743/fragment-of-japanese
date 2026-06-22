using Godot;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Learning;

/// <summary>
/// Harness test hệ thống học/ôn + khóa bài (log-based). Luôn theo "bài hiện tại".
///   C = ĐÚNG (suy nghĩ ~2s)   G = ĐÚNG nhưng NHANH (nghi chọn bừa)   X = SAI
///   B = tạo phiên ôn 12 mục   S = thống kê + tình trạng mở khóa
/// </summary>
public partial class LearningTest : Node
{
    private ReviewItem _cur;

    public override void _Ready()
    {
        var lt = LearningTracker.Instance;
        lt.LessonUnlocked += OnUnlock;
        GD.Print("[LearnTest] C=đúng(chắc)  G=đúng nhưng nhanh(nghi bừa)  X=sai  B=phiên ôn  S=thống kê");
        PrintStats();
        Advance();
    }

    private void OnUnlock(int lesson) => GD.Print($"[LearnTest] 🔓🔓 MỞ KHÓA Bài {lesson}! Giờ mới được học từ mới của bài này.");

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
        var lt = LearningTracker.Instance;
        switch (k.Keycode)
        {
            case Key.C: if (_cur != null) { lt.Record(_cur.Kind, _cur.Id, true, 2000);  GD.Print($"  → ĐÚNG (chắc): {Label(_cur)}"); }                  Advance(); break;
            case Key.G: if (_cur != null) { lt.Record(_cur.Kind, _cur.Id, true, 400);   GD.Print($"  → ĐÚNG nhưng nhanh, nghi chọn bừa: {Label(_cur)}"); } Advance(); break;
            case Key.X: if (_cur != null) { lt.Record(_cur.Kind, _cur.Id, false, 1500); GD.Print($"  → SAI: {Label(_cur)}"); }                           Advance(); break;
            case Key.B: BuildSession(); break;
            case Key.S: PrintStats();   break;
        }
    }

    private void Advance()
    {
        var lt = LearningTracker.Instance;
        _cur = lt.NextItem();   // tự lấy theo bài hiện tại + ôn bài cũ đến hạn
        if (_cur == null) GD.Print("[LearnTest] Hết mục để học/ôn lúc này.");
        else              GD.Print($"[LearnTest] (Bài {lt.CurrentLesson}) Tiếp theo {(_cur.IsNew ? "[MỚI]" : "[ÔN ]")}: {Label(_cur)}");
    }

    private void BuildSession()
    {
        var s = LearningTracker.Instance.BuildSession(12);
        GD.Print($"── PHIÊN ÔN ({s.Count} mục), trộn cũ + mới ──");
        foreach (var it in s) GD.Print($"   {(it.IsNew ? "[MỚI]" : "[ÔN ]")} {Label(it)}");
    }

    private void PrintStats()
    {
        var lt = LearningTracker.Instance;
        int l  = lt.CurrentLesson;
        var st = lt.GetLessonStats(l);
        GD.Print($"── Bài hiện tại {l} (đã mở tới Bài {lt.UnlockedLesson}): tổng {st.Total} · thuộc {st.Mastered} · đang học {st.Learning} · hay sai {st.Struggling} · chưa học {st.New} ──");
    }

    private string Label(ReviewItem it)
    {
        var db = JapaneseDB.Instance;
        string extra = "";
        if (it.Kind == ItemKind.Vocab && db != null)
            foreach (var v in db.VocabN5)
                if (v.Id == it.Id) { extra = " " + v.Kana; break; }
        return $"{it.Kind}:{it.Id}{extra}";
    }
}
