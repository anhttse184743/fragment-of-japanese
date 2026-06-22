# Hệ thống Quảng cáo (AdMob)

Hệ thống QC đã được dựng theo kiến trúc **provider** để game **vẫn chạy & biên dịch bình thường khi chưa cài plugin native**:

| Provider | Khi nào dùng | Hành vi |
|---|---|---|
| `MockAdProvider` | Editor / Desktop (mặc định) | Giả lập overlay phủ màn — test trọn luồng banner / interstitial / rewarded ngay trong editor |
| `AdMobProvider` | Android + đã bật cờ `ADMOB_ENABLED` | Quảng cáo thật qua plugin AdMob |

`AdManager` (autoload) tự chọn provider phù hợp lúc chạy.

---

## ✅ Đang chạy được NGAY (giả lập, không cần cài gì)

Mở game trong Godot và thử:

- **Rewarded** — Mở cửa hàng (phím **P**) → tab **Nạp** → bấm **"▶ Xem QC +100 Vàng"**. Overlay đếm ngược hiện ra; xem hết → **+100 Vàng** vào ví; giới hạn **5 lần/ngày** (`n/5`). Bấm **"Bỏ qua"** giữa chừng = mất thưởng.
- **Gỡ QC** — nút **"Gỡ QC"** ở cùng hàng → tắt banner + interstitial (rewarded vẫn xem được). Trạng thái được lưu.
- **Interstitial** — xảy ra khi **chuyển cảnh** (`SceneTransition.GoTo`), có kiểm soát tần suất (mỗi 2 lần chuyển + cách nhau ≥90s), tự bỏ qua nếu đã gỡ QC.
- **Banner** — hiện ở **Main Menu** (ẩn khi vào chơi).

Trạng thái lưu ở `user://ads.save.json` (số lần xem/ngày + đã gỡ QC), tự reset theo ngày.

---

## 🔧 Bật QUẢNG CÁO THẬT trên Android (các bước thủ công trong Godot Editor)

> Các bước này cần **Godot Editor + Android SDK + JDK** nên phải làm tay (không tự động hoá được từ code).

1. **Cài plugin**: trong Godot → `AssetLib` → tìm **"AdMob"** (Poing Studios — `godot-admob-plugin`, hỗ trợ C#) → tải về & cài. Hoặc tải bản release từ <https://github.com/poingstudios/godot-admob-plugin> rồi giải nén vào thư mục `addons/`.
2. **Cài Android Build Template**: menu `Project → Install Android Build Template…` (tạo thư mục `android/build/`).
3. **Bật plugin**: `Project → Project Settings → Plugins` → bật plugin AdMob.
4. **Đặt App ID** (test): trong phần cấu hình của plugin (hoặc `AndroidManifest`), đặt:
   ```
   ca-app-pub-3940256099942544~3347511713
   ```
   ⚠️ App ID dùng dấu `~`, khác với Ad Unit ID dùng dấu `/`.
5. **Bật cờ biên dịch**: mở `FRAGMENT OF JAPANESE.csproj`, **bỏ comment** khối `ADMOB_ENABLED` đã chuẩn bị sẵn ở cuối file. Lúc này `AdMobProvider.cs` mới được biên dịch (chỉ cho bản Android).
6. **Export Android** (cần bật **Use Gradle Build / custom build template**). Cài trên thiết bị thật → quảng cáo **test** sẽ luôn hiển thị.

### Khi phát hành thật
- Thay 3 Ad Unit ID + App ID test trong [`src/Ads/AdConfig.cs`](../src/Ads/AdConfig.cs) bằng ID thật từ AdMob console.
- Thay App ID thật trong Manifest (bước 4).
- **Không** bấm vào quảng cáo thật của chính mình (vi phạm chính sách AdMob) — đó là lý do nên giữ ID test khi dev.

---

## 🗂️ Bản đồ mã nguồn

| File | Vai trò |
|---|---|
| [`src/Ads/AdConfig.cs`](../src/Ads/AdConfig.cs) | ID quảng cáo + App ID + hằng tinh chỉnh (cap/ngày, vàng/lần, cooldown) |
| [`src/Ads/IAdProvider.cs`](../src/Ads/IAdProvider.cs) | Giao diện trừu tượng banner/interstitial/rewarded |
| [`src/Ads/MockAdProvider.cs`](../src/Ads/MockAdProvider.cs) | Giả lập (editor/desktop) |
| [`src/Ads/AdMobProvider.cs`](../src/Ads/AdMobProvider.cs) | Quảng cáo thật (`#if ADMOB_ENABLED`) |
| [`src/Ads/AdManager.cs`](../src/Ads/AdManager.cs) | Autoload trung tâm: chọn provider, lưu trạng thái, gating, cộng thưởng |

Đã nối: `project.godot` (autoload `AdManager`), `ShopUi` (tab Nạp), `SceneTransition` (interstitial), `MainMenu` (banner).

---

## API gọi từ code

```csharp
using FragmentOfJapanese.Ads;

AdManager.Instance.ShowBanner();
AdManager.Instance.HideBanner();

await AdManager.Instance.MaybeShowInterstitialAsync();   // tự giới hạn tần suất
AdManager.Instance.ShowInterstitial(() => GD.Print("đã đóng"));   // hiện ngay

AdManager.Instance.WatchRewardedForGold();               // xem → +Vàng (có cap/ngày)
AdManager.Instance.ShowRewarded(earned => { if (earned) /* thưởng tuỳ biến */ });

AdManager.Instance.SetAdsRemoved(true);                  // "gỡ QC" (nên gắn vào IAP thật sau)

// Sự kiện cho UI:
AdManager.Instance.RewardGranted   += gold => { /* +gold */ };
AdManager.Instance.AdsRemovedChanged += () => { /* refresh UI */ };
```

Tinh chỉnh số liệu (cap 5/ngày, 100 Vàng/lần, cooldown 90s, mỗi 2 lần chuyển cảnh) ở `AdConfig.cs`.
