# Vosk — model & thư viện native

Thư mục này chứa model nhận dạng giọng nói cho [VoiceRecognizer.cs](../../src/Voice/VoiceRecognizer.cs).
File `.gdignore` khiến Godot **không import** đống file model (tránh chậm/lỗi). Không nên commit model lên git.

## 1. Model tiếng Nhật (~48 MB)
1. Tải `vosk-model-small-ja-0.22` tại https://alphacephei.com/vosk/models
2. Giải nén sao cho có đúng thư mục: `data/vosk/vosk-model-small-ja-0.22/`
   (bên trong gồm các thư mục `am/`, `conf/`, `graph/`, `ivector/`…)

> Phải là **model small** — nó cho phép đổi từ vựng (grammar) lúc chạy. Model lớn (1 GB) là static, không hợp.

## 2. Thư viện native `libvosk.dll` (Windows)
Vosk NuGet **không** kèm sẵn thư viện native. Tải bản Windows:
1. Vào https://github.com/alphacep/vosk-api/releases
2. Tải `vosk-win64-0.3.45.zip` (hoặc bản mới hơn), giải nén lấy **`libvosk.dll`**
3. Đặt `libvosk.dll` vào **thư mục gốc project** (cạnh `project.godot`) để chạy trong editor.

## 3. Chạy thử
1. Build C# (Godot tự restore package Vosk khi build lần đầu).
2. Mở `scenes/test/VoiceTest.tscn` → chạy **scene này** (phím **F6**).
3. Giữ phím **V**, đọc một từ Bài 1 (vd 「せんせい」), rồi thả phím.
4. Xem cửa sổ **Output**: dòng `[Voice] Thô='...'` cho biết model nghe ra gì.

## Lưu ý
- Model + `libvosk.dll` **không** được đóng vào `.pck` khi export — lúc export phải copy thủ công vào thư mục build (cạnh file `.exe`).
- Lần đầu nếu không thu được tiếng: kiểm tra quyền micro của Windows, và thử đặt `MuteMicBus = false` trong VoiceRecognizer.
