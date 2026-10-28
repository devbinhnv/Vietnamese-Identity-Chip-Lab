# Các chức năng

## Vai trò

Các module Android xử lý thiết bị và chính sách ứng dụng; [lõi ICAO](../../Icao9303.Core/README.md) xử lý chuẩn TD1 và phiên chip.

## Quy trình

Luồng chính đi qua `Mrz → Nfc → Chip → UI`. NDEF còn là module riêng trong mã và không được tự dùng khi nhận CCCD thiếu IsoDep hoặc xác thực thất bại.

## Contract

| Module | Nhận | Trả |
| --- | --- | --- |
| [Mrz](Mrz/README.md) | Frame camera và thao tác đối chiếu | `Td1Mrz` chỉ sau xác nhận |
| [Nfc](Nfc/README.md) | `Td1Mrz` đã xác nhận và `Tag` | Lần đọc IsoDep gắn với mã phiên |
| [Chip](Chip/README.md) | IsoDep và đầu vào MRZ | Thông tin chip, đối chiếu, ảnh hoặc lỗi |
| [Ndef](Ndef/README.md) | Thẻ/record NDEF được chọn rõ | Nội dung record hoặc lỗi |

Điều kiện chuyển bước và tài nguyên được quy định trong [contract chung](../../docs/quy-trinh-va-contract.md).

## Lỗi và giới hạn

Không đưa heuristic OCR vào thuật toán khóa. Không thử giao thức khác sau một lần xác thực đã bắt đầu trong cùng phiên. Không gán kết quả/ảnh từ phiên cũ sang phiên mới.

## Kiểm chứng

Kiểm thử helper và phiên lõi nằm trong [bộ kiểm thử](../../Icao9303.Core.Tests/README.md); tương tác phần cứng cần thử trên thiết bị.
