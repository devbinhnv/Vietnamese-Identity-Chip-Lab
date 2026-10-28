# Kiểm thử tự động

## Vai trò

Kiểm tra bất biến TD1, ranh giới OCR, trạng thái phiên và cách phân loại kết quả chip mà không cần Android. Project liên kết các helper thuần C# từ app; không đưa heuristic OCR vào lõi ICAO.

## Quy trình

Chạy từ thư mục gốc:

```powershell
dotnet test .\Icao9303.Core.Tests\Icao9303.Core.Tests.csproj -c Release
```

Các test dùng mẫu giả lập, không dùng MRZ/ảnh từ giấy tờ người dùng.

## Contract được kiểm tra

| File | Bất biến |
| --- | --- |
| `Td1MrzTests.cs` | Đầu vào khóa/hash mẫu ICAO; ký tự đệm; không tự sửa O; chỉ chuẩn hóa newline; chọn ba dòng liền nhau; không đọc trước xác thực/không thử lại; PACE-CAM bị chặn trước APDU |
| `MrzScanTests.cs` | Dừng một frame hợp lệ; thủ công giữ bản sai; không để retry làm mất bản tốt; crop theo tọa độ/lề ảnh; threshold trên nền không đều |
| `ChipReadResultTests.cs` | Chỉ DG1 hợp lệ mới là kết quả đọc; tên khác phải báo khác; thiếu ảnh không báo đủ ảnh; lỗi xác thực không đánh dấu đã đọc thông tin |

Các contract C01–C10 được định nghĩa trong [tài liệu chung](../docs/quy-trinh-va-contract.md). Bài kiểm tra feedback kiểm tra mô tả trạng thái nội bộ; bảng kỹ thuật không còn được hiển thị trên UI chính.

## Lỗi và giới hạn

Test không điều khiển camera, Reader Mode, anten NFC, chip thật hoặc vòng đời Android. Kết quả pass không chứng minh đọc được mọi CCCD, chất lượng OCR hay xác minh SOD.

## Kiểm chứng

Bộ hiện tại có 21 ca. Kết quả chạy và kịch bản kiểm tra thiết bị được mô tả tại [kiểm chứng](../docs/kiem-thu.md).
