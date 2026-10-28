# Giao diện chính

## Vai trò

[`MainActivity.cs`](MainActivity.cs) tạo màn hình cuộn gồm tiêu đề, vùng MRZ và vùng CCCD. Các partial class xử lý nghiệp vụ nằm trong folder chức năng. UI không chứa thuật toán mật mã hoặc tự sửa ký tự MRZ.

## Quy trình

Ban đầu chỉ vùng MRZ hiện. Khi người dùng xác nhận bằng **Tiếp tục quét CCCD**, ẩn vùng MRZ và hiện vùng chờ/đọc CCCD. Kết quả gồm thông tin lấy từ chip và ảnh giải mã được; lỗi có thông báo ngắn và nút thử lại. **Quay lại MRZ** đóng phiên NFC, giữ bản MRZ để đối chiếu, rồi đưa người dùng về đầu màn hình.

## Contract

`AddMrzUi` tạo vùng nhập/kết quả MRZ. `ShowMrzStep` hủy phiên và đầu vào NFC đã xác nhận trước khi đổi vùng hiển thị. `ShowStatus` chỉ trình bày thông báo. `SetFaceImage(Bitmap?)` nhận quyền sở hữu bitmap hiển thị, giải phóng ảnh trước đó khi thay; `OnDestroy` hủy phiên và giải phóng ảnh còn giữ. C10 trong [contract chung](../../docs/quy-trinh-va-contract.md) quy định chi tiết.

## Lỗi và giới hạn

Không hiện OCR thô liên tục, APDU, hex, khóa hoặc bảng bước kỹ thuật. Thông báo thành công phải phản ánh phần đã đọc thực sự; thiếu ảnh không được trình bày như đã có ảnh. Lỗi khởi tạo màn hình được bắt để Activity vẫn có nội dung báo lỗi.

## Kiểm chứng

Kiểm tra hai vùng không hiện đồng thời; nút tiếp tục bị khóa với MRZ sai; quay lại không còn ảnh/kết quả chip cũ. Xem [kiểm thử giao diện](../../docs/kiem-thu.md).
