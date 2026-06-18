# Player sprite 2.5D — 4 hướng + animation

Scaffolding đã sẵn. Khi có art pixel, làm 3 bước dưới là chạy.

## 1. Quy ước tên animation  `{state}_{hướng}`

4 trạng thái × 4 hướng = 16 animation (đã tạo sẵn trong `player_frames.tres`):

| state  | loop  | tốc độ gợi ý | hướng (suffix)            |
|--------|-------|--------------|---------------------------|
| idle   | có    | 8 fps        | `down` `up` `left` `right`|
| run    | có    | 10 fps       | (như trên)                |
| jump   | không | 10 fps       | (như trên)                |
| attack | không | 12 fps       | (như trên)                |

→ Ví dụ: `idle_down`, `run_left`, `jump_up`, `attack_right`.

- **"down"** = nhân vật quay MẶT về phía camera (thấy mặt trước).
- **"up"** = quay LƯNG về camera. `left` / `right` = nghiêng hai bên.
- Dùng 8 hướng: đổi `Directions = 8` trên node Animator; thêm suffix
  `down_right, up_right, up_left, down_left`.

## 2. Nhét frame pixel vào

Mở `player_frames.tres` → panel **SpriteFrames** dưới đáy → chọn từng animation →
xoá frame icon tạm → kéo các frame pixel vào đúng thứ tự. (Hoặc kéo cả sprite sheet,
Godot cắt theo lưới.)

## 3. Ráp vào Player.tscn (khi rảnh chỉnh camera)

Trong `scenes/entities/Player.tscn`:

1. Thay node `Visual` (Sprite3D) bằng **`AnimatedSprite3D`** — hoặc thêm mới rồi xoá cái cũ.
   - `Sprite Frames` = `res://assets/sprites/player/player_frames.tres`
   - `Billboard` = **Y-Billboard** (đứng thẳng, xoay mặt về camera)
   - **`Texture Filter` = Nearest** ← BẮT BUỘC cho pixel khỏi bị mờ
   - `Pixel Size` ~ 0.01 (tùy độ lớn art)
2. Thêm node con của `Player`: **`PlayerAnimator`** (gắn `src/Entities/Player/PlayerAnimator.cs`), gán:
   - `_sprite`     → AnimatedSprite3D vừa tạo
   - `_player`     → node `Player`
   - `_camera`     → `MainCamera3D`
   - `_controller` → node `Controller`
3. Xong. Chạy F6.

## Điều khiển (test bàn phím)
`WASD` đi · `Shift` bật/tắt chạy nhanh · `Space` nhảy · `J` tấn công.
Chạy nhanh → animation `run` tự phát nhanh hơn (`RunFastSpeedScale`).

## Lệch hướng?
Nếu nhân vật quay sai hướng so với art, chỉnh `AngleOffsetDeg` trên node `PlayerAnimator`
theo nấc 90° (0 / 90 / 180 / 270) tới khi khớp.

## Import pixel (toàn project, nếu muốn)
Chọn file ảnh pixel trong FileSystem → tab **Import** → Preset **2D Pixel** → Reimport.
Hoặc đặt `Texture Filter = Nearest` ngay trên node là đủ.
