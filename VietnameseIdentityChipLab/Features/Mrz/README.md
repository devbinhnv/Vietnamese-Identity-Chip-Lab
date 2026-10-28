# Quét và xác nhận MRZ

## Vai trò

Nhận ảnh camera, OCR MRZ TD1, dừng để người dùng đối chiếu và tạo đầu vào cho NFC sau xác nhận. Phần hiển thị chỉ gồm chuỗi MRZ, các trường giấy tờ và trạng thái ngắn.

## Quy trình

1. **Quét MRZ** xin quyền camera khi cần, mở camera sau và chọn kích thước preview/YUV chung gần 1080p. Mỗi thời điểm chỉ xử lý một frame, khoảng cách bắt đầu tối thiểu 200 ms.
2. OCR toàn khung. Chưa có TD1 hợp lệ thì thử vùng ảnh tìm từ nhóm hai/ba dòng OCR; không tìm được thì giữ toàn khung. Lấy kênh Y, xoay hướng, crop và chỉnh nghiêng nhỏ; OCR ảnh xám rồi ảnh threshold. Tối đa ba lượt mỗi frame.
3. Một frame có ba dòng TD1 liên tiếp và đủ bốn chữ số kiểm tra đúng làm dừng tự động. **Chụp thủ công** dừng sau frame tiếp theo dù chưa hợp lệ; **Dừng quét** giữ bản có chữ mới nhất đã hoàn tất.
4. Ô sửa chỉ giữ các dòng có dạng MRZ. Khi hợp lệ, hiển thị họ tên, số giấy tờ, ngày sinh, giới tính, quốc tịch, ngày hết hạn, loại giấy tờ và nơi cấp. Ngày giữ năm hai chữ số, không đoán thế kỷ.
5. Người dùng chọn **Tiếp tục quét CCCD**. Ứng dụng kiểm tra lại, gán đầu vào đã xác nhận rồi chuyển vùng CCCD và bật Reader Mode.

## Contract

| Thành phần | Giao tiếp |
| --- | --- |
| `LiveMrzScanner` | `Action<string, bool>` cho OCR và cờ chụp thủ công; callback lỗi riêng |
| `MrzRegionDetector` | `IEnumerable<MrzOcrLine>` + kích thước → `MrzImageRegion` hoặc không tìm được |
| `ImagePreprocessor` | Image/bitmap → bitmap mới; caller sở hữu và giải phóng sau OCR |
| `LiveMrzCandidate` | Chuỗi OCR → ba dòng đã kiểm tra hoặc false |
| `MrzScanDecision` | Văn bản + thao tác thủ công → quyết định dừng và bản nháp |
| `MrzAccess` | Bản sửa → `Td1Mrz` hoặc lỗi, không tự sửa ký tự |
| `MrzResultFormatter` | `Td1Mrz` → các trường để hiển thị, không có dữ liệu khóa |

C01–C04 trong [contract chung](../../../docs/quy-trinh-va-contract.md) là định nghĩa đầy đủ. `UpdateMrzDetails` chỉ kiểm tra và trình bày; không tự xác nhận đầu vào NFC.

## Lỗi và giới hạn

Sửa MRZ hoặc quét mới hủy phiên NFC và đầu vào cũ. Không tự thay `O/0`, bù ký tự hoặc giảm số checksum. Chỉ hỗ trợ TD1 3 × 30 trong tập của lõi. Không dùng ML Kit Document Scanner, Canny, Hough hoặc sửa phối cảnh. Xử lý ảnh không khôi phục được ký tự bị che/mất nét.

## Kiểm chứng

Các kiểm thử bao phủ mẫu TD1, OCR thiếu/sai, chấp nhận một frame hợp lệ, thủ công, vùng crop và threshold. Camera, ánh sáng và xoay màn hình được thử theo [kịch bản thiết bị](../../../docs/kiem-thu.md).
