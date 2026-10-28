# Module NDEF độc lập

## Vai trò

Giữ khả năng đọc/giải mã record NFC Forum NDEF trong mã. Module không xuất hiện trên màn hình chính và không được gọi bởi `OnTagDiscovered` của luồng CCCD hiện tại.

## Quy trình

Khi được một caller riêng gọi, `ReadNdefTag(Tag)` lấy công nghệ Ndef, kết nối, đọc `NdefMessage`, giải mã từng record rồi đóng kết nối. `NdefDecoder` hỗ trợ Text UTF-8/UTF-16, URI và một số MIME text. Đây không phải cách đọc DG1/DG2.

## Contract

`ReadNdefTag(Tag) → string` trả nội dung record đã giải mã; thẻ không có NDEF, thẻ trống hoặc lỗi kết nối được trả qua exception. `NdefDecoder.Decode(short tnf, byte[]? type, byte[]? payload) → string` trả diễn giải hoặc mô tả lỗi cấu trúc/mã hóa. Hai API này không nhận `Td1Mrz`, không tạo khóa và không bắt tay PACE/BAC.

## Lỗi và giới hạn

CCCD không cần chứa NDEF. Không được đưa người dùng sang NDEF khi IsoDep hoặc xác thực thất bại. Một số thông báo decoder còn tham chiếu payload hex cho caller chẩn đoán; màn hình chính không dùng chúng và không có cam kết hiển thị raw từ module này.

## Kiểm chứng

Build kiểm tra module còn biên dịch. Bộ kiểm thử hiện tại chưa kiểm tra NDEF trên thẻ thật; module không thuộc [luồng kiểm chứng CCCD](../../../docs/kiem-thu.md).
