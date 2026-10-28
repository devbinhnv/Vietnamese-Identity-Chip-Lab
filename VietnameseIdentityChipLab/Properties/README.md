# Quyền và khả năng Android

## Vai trò

[`AndroidManifest.xml`](AndroidManifest.xml) khai báo quyền NFC/camera và giới hạn mạng của ứng dụng.

## Quy trình

Quyền camera được xin khi người dùng nhấn quét MRZ. NFC được kiểm tra khi bắt đầu đọc CCCD; máy không có NFC hoặc đang tắt được báo rõ. Camera/NFC khai báo `required="false"` để ứng dụng vẫn cài được khi thiếu phần cứng.

## Contract

| Khai báo | Điều kiện thực thi |
| --- | --- |
| `CAMERA` | Người dùng cấp quyền runtime trước khi mở Camera2 |
| `NFC` | Có adapter và NFC bật trước Reader Mode |
| Loại `INTERNET`, `ACCESS_NETWORK_STATE` | Luồng xử lý không gọi máy chủ hoặc tải model lúc quét |
| `net10.0-android`, min API 23 | Công cụ build và thiết bị phải đáp ứng cấu hình project |

Model OCR Latin được đóng gói bởi dependency ML Kit. Các contract ảnh/NFC ở [quy trình chung](../../docs/quy-trinh-va-contract.md) vẫn phải kiểm tra trạng thái runtime, không dựa riêng vào Manifest.

## Lỗi và giới hạn

Người dùng có thể từ chối quyền hoặc tắt NFC sau khi cài. Có khai báo quyền không bảo đảm camera/NFC đang khả dụng. Không thêm quyền mạng để che lỗi quyền thiết bị.

## Kiểm chứng

Build và kiểm tra quyền trong APK; thử từ chối quyền camera, thiết bị thiếu NFC và bật/tắt NFC theo [kịch bản](../../docs/kiem-thu.md).
