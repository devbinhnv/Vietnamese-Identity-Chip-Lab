# Kiểm chứng hành vi

## Vai trò

Phân biệt kiểm thử tự động, build/package và thử trên điện thoại. Tài liệu mô tả kịch bản kiểm chứng của luồng hiện tại, không phải kế hoạch triển khai.

Chuẩn bị .NET/workload, SDK/JDK và cài APK theo mục **Setup môi trường** trong [README tổng quan](../README.md). Các lệnh dưới đây chạy từ thư mục gốc sau khi setup; không yêu cầu máy chủ OCR hay kết nối Internet khi đọc trên điện thoại.

## Quy trình tự động

```powershell
dotnet test .\Icao9303.Core.Tests\Icao9303.Core.Tests.csproj -c Release

dotnet build .\VietnameseIdentityChipLab\VietnameseIdentityChipLab.csproj -c Release `
  -p:AndroidSdkDirectory="$PWD\.android-sdk" `
  -p:JavaSdkDirectory="$PWD\.android-jdk"
```

Chi tiết nhóm kiểm thử tại [README test](../Icao9303.Core.Tests/README.md). Build tạo APK trong `VietnameseIdentityChipLab/bin/Release/net10.0-android/` và kiểm tra binding Android/ML Kit. Có thể kiểm tra gói bằng:

```powershell
.\.android-sdk\build-tools\36.0.0\aapt2.exe dump permissions `
  .\VietnameseIdentityChipLab\bin\Release\net10.0-android\vn.hoctap.nfcreader-Signed.apk

.\.android-jdk\bin\java.exe -jar .\.android-sdk\build-tools\36.0.0\lib\apksigner.jar verify `
  .\VietnameseIdentityChipLab\bin\Release\net10.0-android\vn.hoctap.nfcreader-Signed.apk
```

## Contract cần giữ khi thử thiết bị

| Thao tác | Kết quả mong đợi |
| --- | --- |
| Mở ứng dụng | Chỉ vùng MRZ hiện; không có nút NDEF, raw hoặc bảng kỹ thuật |
| Từ chối camera rồi cấp lại | Báo cần quyền; lần quét được cấp quyền có thể mở camera |
| Quét MRZ rõ và hợp lệ | Một frame hợp lệ dừng camera; hiện chuỗi và thông tin; nút tiếp tục bật |
| Chụp thủ công bản thiếu/sai | Dừng để sửa; nút tiếp tục vẫn khóa cho đến khi hợp lệ |
| Chọn Tiếp tục quét CCCD | Vùng MRZ ẩn; vùng CCCD hiện, có hướng dẫn áp thẻ và vòng chờ |
| NFC tắt hoặc không có adapter | Thông báo đúng điều kiện; không báo đang đọc chip |
| Áp CCCD sau xác nhận | Thông báo chuyển qua kết nối/xác thực/đọc, sau đó hiện dữ liệu lấy từ chip |
| Thẻ không có IsoDep | Báo không kết nối được chip; không xuất hiện lỗi NDEF |
| Nhấc thẻ giữa chừng | Báo dừng ở bước tương ứng; thử lại cần nhấc và áp thẻ |
| DG1 đọc được, DG2 lỗi/không hỗ trợ | Giữ thông tin DG1, thông báo ảnh chưa có/không hiển thị được |
| Quay lại MRZ trong lúc chờ/đọc | Tắt Reader Mode, giữ chuỗi MRZ, xóa kết quả/ảnh chip cũ; callback muộn không hiện lại |
| Sửa MRZ hoặc quét giấy tờ khác | Đầu vào đã xác nhận bị hủy; phải chọn tiếp tục lại |
| Ra nền khi đang quét rồi quay lại | Camera/phiên cũ đã dừng; có thể chủ động quét lại, không tự nhận kết quả cũ |

So sánh thông tin chip với bản MRZ đã xác nhận phải gồm cả họ tên. Kịch bản dữ liệu khác và DG1 sai được kiểm thử helper, không cần cố tình thay đổi chip thật.

## Lỗi và giới hạn

Không lấy thời gian build hoặc số test pass làm độ chính xác OCR. Không suy ra tương thích mọi CCCD từ một lần đọc. Việc so sánh MRZ và đọc chip không thay xác minh SOD/CSCA. Đổi cấu hình thiết bị/thẻ có thể làm thay đổi kết quả cần kiểm chứng.

## Bằng chứng hiện có

Lần kiểm chứng mã gần nhất ngày 28/10/2026, sau khi đổi tên thành **Vietnamese Identity Chip Lab**: 21/21 ca kiểm thử đạt, build toàn solution ở cấu hình Release không lỗi/cảnh báo và chữ ký APK đã được kiểm tra. Nhãn ứng dụng và Activity trong gói Android đều dùng tên mới. Application ID giữ nguyên để hỗ trợ cài cập nhật.

Người dùng đã phản hồi đọc NFC thành công trước lần đổi tên. Các lần hoàn thiện trước đã tối giản giao diện và loại nhánh NDEF khỏi màn hình chính; lần đổi tên không thay quy trình MRZ/PACE/BAC. Các ca thiết bị trong bảng là tiêu chí thử lại, không được ghi nhận là đã chạy tự động trên điện thoại.
