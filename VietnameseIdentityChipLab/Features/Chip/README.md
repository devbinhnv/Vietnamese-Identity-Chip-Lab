# Đọc thông tin và ảnh từ chip

## Vai trò

Nối Android IsoDep với [lõi ICAO](../../../Icao9303.Core/README.md), chọn giao thức phù hợp và chuyển DG1/DG2 thành kết quả trình bày. Không thay thuật toán mật mã trong thư viện `Emrtd`.

## Quy trình

1. `IsoDepTransport` mở kết nối và đặt timeout 10 giây.
2. Đọc EF.CardAccess. Chọn PACE Generic Mapping được công bố và hỗ trợ; không có PACE phù hợp hoặc CardAccess bị từ chối thì thử BAC. Không fallback BAC sau khi PACE đã bắt đầu và thất bại.
3. Xác thực thành công mới đọc DG1. Giải mã rồi tạo `ChipMrzResult` từ ba dòng TD1 hợp lệ trong chip; so sánh đủ ba dòng với bản đã xác nhận.
4. Thử DG2, hiển thị JPEG khi giải mã thành công. Trả thông tin riêng nếu chưa có hoặc chưa hiển thị được ảnh; lỗi đọc ảnh không xóa DG1 đã đọc.
5. Gửi kết quả hoặc lỗi gắn với generation hiện tại. Chỉ UI của đúng phiên được cập nhật.

## Contract

| API | Đầu vào | Đầu ra/quyền sở hữu |
| --- | --- | --- |
| `ReadIdentityChip` | IsoDep, `Td1Mrz` đã xác nhận, generation | Trạng thái/kết quả của đúng phiên; transport đóng sau lần đọc |
| `IsoDepTransport.Transmit` | Command APDU hợp lệ và nằm trong giới hạn | Response nguyên vẹn gồm SW cuối; không retry |
| `ChipMrzResult.FromDg1` | Các dòng MRZ trong chip và bản đã xác nhận | `Mrz` lấy từ chip + `MatchesConfirmed`; sai DG1 thì throw |
| `DecodeDg2` | Byte LDS DG2 | MIME + byte ảnh, hoặc null |
| `SetFaceImage` | Bitmap JPEG giải mã được | Chuyển quyền sở hữu cho Activity; ảnh cũ được giải phóng |

Xem C06–C09 trong [contract chung](../../../docs/quy-trinh-va-contract.md).

## Lỗi và giới hạn

Không dùng dữ liệu camera thay DG1 để báo thành công. Họ tên khác phải báo khác dù checksum đúng. JPEG 2000, SOD/CSCA, DG3/DG4 chưa thuộc phần đọc/hiển thị hiện tại. Lỗi xác thực có thể do kết nối hoặc giao thức, không đủ để kết luận MRZ sai. UI không hiển thị raw/hex/APDU.

## Kiểm chứng

Kiểm thử DG1 thiếu/sai, tên khác, thiếu ảnh và trạng thái lỗi nằm trong `ChipReadResultTests`. Người dùng đã phản hồi NFC đọc được; vẫn cần kịch bản theo thiết bị/thẻ trong [kiểm chứng](../../../docs/kiem-thu.md).
