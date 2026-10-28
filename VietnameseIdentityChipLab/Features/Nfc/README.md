# Điều phối đọc CCCD qua NFC

## Vai trò

Bật Reader Mode sau xác nhận MRZ, nhận thẻ, điều phối IsoDep và quản lý phiên bất đồng bộ. Luồng chính chỉ đọc CCCD; không có nhánh fallback NDEF.

## Quy trình

1. `BeginCccdScan` yêu cầu `_preparedMrz` khác null, hiện vùng CCCD và bắt đầu chờ. Không khởi động callback mới khi callback trước chưa kết thúc.
2. Kiểm tra adapter, NFC bật và Activity resumed. Bật `NfcA | NfcB | SkipNdefCheck`; hiện hướng dẫn áp thẻ cùng vòng chờ.
3. `OnTagDiscovered` nhận Tag, dùng `_reading` để chặn đọc đồng thời, kiểm tra đầu vào MRZ và lấy IsoDep. Thiếu IsoDep thì trả lỗi kết nối; không gọi NDEF.
4. Gọi `ReadIdentityChip` với MRZ đã xác nhận và mã phiên. UI chỉ nhận thông báo ngắn, kết quả và ảnh; bảng bước kỹ thuật không hiển thị.
5. Kết quả/lỗi tắt Reader Mode và cho phép **Quét lại CCCD**. **Quay lại MRZ**, sửa MRZ hoặc ra nền sẽ hủy phiên.

## Contract

C05/C10 trong [contract chung](../../../docs/quy-trinh-va-contract.md) quy định tiền điều kiện và kết quả. `_nfcGeneration` tăng khi hủy; `_nfcRequested` phải còn true và generation phải khớp trước mọi cập nhật UI/ảnh. `_activeChip` được đóng khi hủy để kết thúc kết nối đang giữ.

`UpdateNfcProgress` hiển thị trạng thái đang chạy. `FinishNfcScan` kết thúc, dừng vòng chờ và hiện kết quả/thử lại. `NfcReadFeedback` chỉ phục vụ mô tả bước cho chẩn đoán nội bộ, không tạo bảng trên màn hình chính.

## Lỗi và giới hạn

Nhận Tag chỉ chứng minh điện thoại phát hiện thẻ; không đồng nghĩa xác thực hoặc đọc thành công. NFC tắt/không có phần cứng, mất kết nối và lỗi xác thực phải được phân biệt bằng thông báo. Ra nền dừng phiên; quay lại ứng dụng phải chủ động nhấn quét lại. Hủy phiên không có nghĩa reset chip; vẫn cần nhấc thẻ trước lần thử mới.

## Kiểm chứng

Kiểm tra NFC tắt, thẻ thiếu IsoDep, nhấc thẻ giữa chừng, quay lại MRZ và hủy khi đang đọc. Cách diễn giải bước lỗi và kết quả có kiểm thử tự động; tương tác phần cứng xem [kiểm chứng](../../../docs/kiem-thu.md).
