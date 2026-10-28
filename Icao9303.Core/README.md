# Lõi ICAO Doc 9303

## Vai trò

Giữ quy tắc TD1, đầu vào khóa và trạng thái phiên chip, độc lập với Android và OCR. Thư viện `Emrtd` 0.1.0 thực hiện mật mã, PACE/BAC, MAC và secure messaging.

## Quy trình

1. `Td1Mrz.Inspect` kiểm tra ba dòng đúng độ dài/bảng ký tự và tính bốn chữ số kiểm tra.
2. `ParseValidated` chỉ tạo `Td1Mrz` khi các kiểm tra đã có đều đạt, trường số giấy tờ không rỗng và hai ngày đủ sáu chữ số. Số giấy tờ kéo dài sang vùng tùy chọn bị từ chối.
3. `IcaoChipSession` đọc CardAccess trước xác thực. Caller Android chọn PACE Generic Mapping được hỗ trợ hoặc BAC.
4. So sánh `MRZ_information` với đầu vào của `Emrtd.MrzKey`; sau đó mới bắt đầu xác thực và bật secure messaging.
5. Chỉ phiên đã xác thực mới đọc DG1/DG2. Các hàm decode diễn giải byte LDS, không thay dữ liệu chip.

## Contract

| API/kiểu | Bảo đảm |
| --- | --- |
| `Td1Mrz.Inspect(first, second, third)` | Trả `Td1Inspection` với bốn cặp ký tự đọc/tính; không tự sửa OCR |
| `Td1Mrz.ParseValidated(...)` | Trả đối tượng chỉ đọc hoặc throw; đúng tập kiểm tra hiện hỗ trợ |
| `Td1Mrz.MrzInformation` | 24 ký tự lấy từ đúng trường số giấy tờ, ngày sinh, ngày hết hạn và checksum |
| `Td1Mrz.MrzHash()` | SHA-1 của ASCII `MrzInformation`, 20 byte |
| `IcaoChipSession` | Không đọc DG trước secure messaging, không xác thực lại sau khi một lần đã bắt đầu |
| `ReadDg1Raw`, `ReadDg2Raw` | Byte LDS bất biến; DG2 có thể null |
| `DecodeDg1`, `DecodeDg2` | Dòng MRZ hoặc MIME/byte ảnh theo bộ giải mã thư viện |

Xem C04/C06–C09 trong [contract chung](../docs/quy-trinh-va-contract.md) và [đầu vào khóa](../docs/quy-trinh-key-va-giai-ma.md).

## Lỗi và giới hạn

`FormatException` cho khuôn dạng/checksum sai; `NotSupportedException` cho trường/biến thể chưa hỗ trợ; `InvalidOperationException` cho sai trạng thái phiên; lỗi thư viện/thẻ được truyền lên caller. PACE-CAM bị chặn trước APDU. Không xác minh SOD/CSCA, không đọc DG3/DG4.

Kiểm tra hiện có không xác minh mã quốc gia, ngày lịch thực tế hoặc thế kỷ của năm hai chữ số. Không nên diễn giải `ParseValidated` như chứng nhận tuân thủ toàn bộ ICAO hoặc xác minh giấy tờ thật. Không thay dãy số vùng tùy chọn vào trường số giấy tờ theo suy đoán.

## Kiểm chứng

[Bộ kiểm thử](../Icao9303.Core.Tests/README.md) dùng mẫu TD1 ICAO và transport giả để kiểm tra đầu vào khóa, ranh giới OCR và trạng thái phiên. Khả năng tương thích thẻ cần kiểm tra riêng trên thiết bị.
