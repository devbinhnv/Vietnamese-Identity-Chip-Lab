# Lớp ứng dụng Android

## Vai trò

`VietnameseIdentityChipLab` chứa giao diện, quyền camera/NFC, OCR và cầu nối IsoDep. Kiểm tra TD1 và trạng thái secure messaging được giữ trong [lõi ICAO](../Icao9303.Core/README.md).

## Quy trình

```text
Vùng MRZ → camera/OCR → chuỗi và thông tin → người dùng xác nhận
                                                     ↓
Vùng CCCD ← kết quả thông tin/ảnh ← DG1/DG2 ← PACE/BAC ← IsoDep
```

Hai vùng hiển thị lần lượt. Không bật Reader Mode trước khi chọn **Tiếp tục quét CCCD**. **Quay lại MRZ** hủy phiên NFC và yêu cầu xác nhận lại trước lần đọc tiếp.

## Contract

| Folder | Trách nhiệm | Contract chính |
| --- | --- | --- |
| [App](App/README.md) | Vùng hiển thị và tài nguyên ảnh | C10: kết quả/lỗi → UI |
| [Features/Mrz](Features/Mrz/README.md) | Camera, OCR và bản MRZ đã đối chiếu | C01–C04 |
| [Features/Nfc](Features/Nfc/README.md) | Phiên quét, callback và hủy | C05, C10 |
| [Features/Chip](Features/Chip/README.md) | Kết nối, giao thức, DG1 và DG2 | C06–C09 |
| [Features/Ndef](Features/Ndef/README.md) | Module NDEF độc lập, chưa nối vào màn hình chính | Không thuộc luồng CCCD |
| [Properties](Properties/README.md) | Quyền và khả năng phần cứng | Điều kiện camera/NFC |

Định nghĩa các contract: [quy trình chung](../docs/quy-trinh-va-contract.md). Chúng là API nội bộ C#/Android, không truyền qua mạng.

## Lỗi và giới hạn

Không có nhánh NDEF trong luồng đọc CCCD. Bắt được Tag chưa có nghĩa đã kết nối/xác thực/đọc thông tin thành công. Callback cũ phải bị chặn sau hủy phiên. Dữ liệu phiên và MRZ chưa được lưu/khôi phục khi Activity bị tạo lại.

## Kiểm chứng

Build Android kiểm tra binding và tạo APK. Camera, vị trí anten, việc nhấc thẻ và vòng đời Activity cần thử theo [kịch bản thiết bị](../docs/kiem-thu.md).
