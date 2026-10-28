# Tài liệu hệ thống

## Vai trò

Bộ tài liệu mô tả dự án học tập MRZ/NFC theo ba mức: tổng quan hệ thống, quy trình/contract và chi tiết module. [README dự án](../README.md) kết hợp mục đích học tập, vai trò của Codex, chuẩn áp dụng, công nghệ chính/phụ, setup/test và đường dữ liệu toàn hệ thống. Các tài liệu contract, khóa và kiểm thử giữ phần chi tiết để người đọc đi từ bức tranh chung xuống API thực tế.

## Thứ tự đọc

| Thứ tự | Tài liệu | Câu hỏi được trả lời |
| --- | --- | --- |
| 1 | [README dự án](../README.md) | Học gì, dùng chuẩn/công nghệ nào, thành phần chính/phụ là gì, setup/test và toàn luồng ra sao? |
| 2 | [Quy trình và contract](quy-trinh-va-contract.md) | Mỗi bước nhận gì, trả gì, khi nào chuyển bước? |
| 3 | [Khóa và phiên chip](quy-trinh-key-va-giai-ma.md) | Dữ liệu MRZ đi vào PACE/BAC và DG ra sao? |
| 4 | [Kiểm chứng](kiem-thu.md) | Đã kiểm tra bằng gì, còn cần thử thiết bị thế nào? |

## Tra cứu tài liệu module

Phần chi tiết được hệ thống hóa từ tài liệu hiện có, theo đúng chặng xử lý và trách nhiệm trong mã:

| Chặng/thành phần | Tài liệu | Nội dung chính |
| --- | --- | --- |
| Ứng dụng Android | [VietnameseIdentityChipLab](../VietnameseIdentityChipLab/README.md) | Cấu trúc ứng dụng, các module và ranh giới phụ thuộc Android |
| Màn hình chính | [App](../VietnameseIdentityChipLab/App/README.md) | Thao tác người dùng, vùng MRZ/CCCD và trình bày kết quả |
| Điều phối chức năng | [Features](../VietnameseIdentityChipLab/Features/README.md) | Liên kết MRZ → NFC → Chip và trách nhiệm từng module |
| Camera → MRZ | [Mrz](../VietnameseIdentityChipLab/Features/Mrz/README.md) | Camera/OCR, xử lý ảnh dự phòng, kiểm tra và xác nhận MRZ |
| MRZ → phát hiện thẻ | [Nfc](../VietnameseIdentityChipLab/Features/Nfc/README.md) | Reader Mode, callback thẻ, hủy phiên và vòng đời |
| Thẻ → dữ liệu chip | [Chip](../VietnameseIdentityChipLab/Features/Chip/README.md) | Transport IsoDep, xác thực, DG1/DG2 và đối chiếu |
| Quy tắc độc lập thiết bị | [Icao9303.Core](../Icao9303.Core/README.md) | Kiểm tra TD1, đầu vào khóa và điều kiện phiên ICAO |
| Quyền/cấu hình | [Properties](../VietnameseIdentityChipLab/Properties/README.md) | Manifest, quyền camera/NFC và cấu hình nền tảng |
| Kiểm thử logic | [Icao9303.Core.Tests](../Icao9303.Core.Tests/README.md) | Nhóm ca kiểm thử, helper dùng chung và giới hạn bằng chứng |
| Chức năng độc lập | [Ndef](../VietnameseIdentityChipLab/Features/Ndef/README.md) | Giải mã record NDEF; không thuộc luồng đọc CCCD |

## Thuật ngữ dùng chung

| Thuật ngữ | Ý nghĩa trong dự án |
| --- | --- |
| MRZ / TD1 | Vùng ký tự máy đọc được; dạng TD1 có ba dòng, mỗi dòng 30 ký tự |
| OCR | Nhận dạng chữ từ ảnh; kết quả cần qua kiểm tra TD1 trước khi dùng cho NFC |
| APDU / SW | Lệnh và phản hồi trao đổi với chip; SW là mã trạng thái phản hồi |
| PACE / BAC | Hai cơ chế kiểm soát truy cập chip, dùng đầu vào MRZ trong luồng hiện tại |
| Secure messaging | Bảo vệ tính bí mật và toàn vẹn của trao đổi APDU sau xác thực truy cập |
| LDS / DG1 / DG2 | Cấu trúc dữ liệu chip; DG1 chứa MRZ, DG2 chứa dữ liệu ảnh khuôn mặt |
| Contract | Quy ước nội bộ về đầu vào, đầu ra, điều kiện chuyển bước, lỗi và tài nguyên |
| Generation | Mã của lần quét để loại bỏ cập nhật từ callback phiên đã hủy |

## Quy ước

README module dùng cùng cấu trúc: **Vai trò → Quy trình → Contract → Lỗi và giới hạn → Kiểm chứng**. Contract ghi API thực tế trong mã, kiểu dữ liệu, tiền điều kiện, đầu ra và trách nhiệm tài nguyên. Contract trong dự án là giao tiếp nội bộ C#/Android, không phải API HTTP hay dịch vụ mạng.

Quy tắc phân loại: thành phần chính chịu trách nhiệm một chặng nghiệp vụ trong luồng MRZ → CCCD; thành phần phụ hỗ trợ ảnh, định dạng, vòng đời, quyền hoặc kiểm thử. NDEF là module độc lập còn trong mã, không được mô tả như một bước bắt buộc hoặc fallback của CCCD. Chuẩn giao tiếp và thư viện hiện thực chuẩn phải được ghi riêng, không coi việc tích hợp thư viện là đã chứng nhận toàn bộ chuẩn.

Sửa UI hoặc điều phối cần cập nhật [quy trình và contract](quy-trinh-va-contract.md); sửa TD1/phiên chip cần cập nhật thêm [tài liệu khóa](quy-trinh-key-va-giai-ma.md). Chỉ ghi kết quả kiểm thử đã chạy; phân biệt kiểm thử tự động, phản hồi người dùng và kiểm tra trực tiếp trên thiết bị.
