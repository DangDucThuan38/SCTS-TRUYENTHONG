using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using ngay_1.DTOs;
using ngay_1.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Buffers.Text;
using System.Text.Json;
using Image = SixLabors.ImageSharp.Image;

namespace APISCTS.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SCTSController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public SCTSController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("test")]
        public IActionResult GetTest()
        {
            return Ok(new
            {
                status = "success",
                message = "API đang hoạt động!",
                time = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
            });
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveData([FromBody] object data)
        {
            try
            {
                string folderPath = _configuration["FileStorage:OutputPath"];

                string filePath = Path.Combine(folderPath, "quansohangngay.json");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(filePath, jsonString);

                return Ok(new { message = "Lưu thành công!", path = filePath });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetData()
        {
            try
            {
                string folderPath = _configuration["FileStorage:OutputPath"];
                string filePath = Path.Combine(folderPath, "quansohangngay.json");

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("Chưa có dữ liệu.");
                }

                string jsonString = await System.IO.File.ReadAllTextAsync(filePath);

                var data = JsonSerializer.Deserialize<QuanSoHangNgayDto>(jsonString);

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }



        [HttpPost("upload-list")]
        [DisableRequestSizeLimit] // Vẫn giữ cái này để IIS không ngắt kết nối giữa chừng
        public async Task<IActionResult> UploadMediaList([FromForm] List<UploadDTO> items)
        {
            if (items == null || items.Count == 0) return BadRequest("Không nhận được file nào.");

            // Cấu hình giới hạn: 100MB = 100 * 1024 * 1024 bytes
            long maxFileSize = 100 * 1024 * 1024;

            try
            {
                string folderPath = _configuration["FileStorage:UploadPath"];
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string jsonPath = Path.Combine(folderPath, "dataimage.json");
                List<MediaMetadata> metadataList = new List<MediaMetadata>();

                // Xử lý đọc JSON an toàn (tránh lỗi JSON token rỗng đã gặp)
                if (System.IO.File.Exists(jsonPath))
                {
                    var existingJson = await System.IO.File.ReadAllTextAsync(jsonPath);
                    if (!string.IsNullOrWhiteSpace(existingJson))
                    {
                        try {
                            metadataList = JsonSerializer.Deserialize<List<MediaMetadata>>(existingJson) ?? new List<MediaMetadata>();
                        } catch { metadataList = new List<MediaMetadata>(); }
                    }
                }

                int lastPosition = metadataList.Any() ? metadataList.Max(m => m.Position) : 0;

                foreach (var item in items)
                {
                    var file = item.file;
                    if (file == null || file.Length == 0) continue;

                    // --- BẮT ĐẦU CHẶN FILE > 100MB ---
                    if (file.Length > maxFileSize)
                    {
                        return BadRequest($"File '{item.FileName}' vượt quá giới hạn 100MB. Vui lòng nén lại.");
                    }
                    // ---------------------------------

                    // Xác định đuôi file dựa trên ContentType thực tế của file hoặc tên
                    string extension = file.ContentType.Contains("video") ? ".mp4" : ".jpg";
                    string fullFileName = item.FileName.EndsWith(extension) ? item.FileName : item.FileName + extension;
                    string filePath = Path.Combine(folderPath, fullFileName);

                    // Lưu file video
                    if (extension == ".mp4")
                    {
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                    }
                    // Lưu file ảnh (Sử dụng SixLabors.ImageSharp)
                    else
                    {
                        using (var inputStream = file.OpenReadStream())
                        using (var image = await Image.LoadAsync(inputStream))
                        {
                            await image.SaveAsJpegAsync(filePath, new JpegEncoder { Quality = 90 });
                        }
                    }

                    // Gán vị trí trình chiếu
                    int assignedPosition = item.Position;
                    if (assignedPosition <= 0)
                    {
                        lastPosition++;
                        assignedPosition = lastPosition;
                    }

                    metadataList.Add(new MediaMetadata
                    {
                        FileName = fullFileName,
                        FilePath = filePath,
                        Position = assignedPosition,
                        Duration = item.Duration > 0 ? item.Duration : 10,
                        UploadDate = DateTime.Now
                    });
                }

                // Lưu lại file JSON
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(metadataList, jsonOptions);
                await System.IO.File.WriteAllTextAsync(jsonPath, jsonString);

                return Ok(new { message = "Lưu file thành công!", count = items.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        [HttpGet("get-media-list")]
        public async Task<IActionResult> GetMediaList()
        {
            try
            {
                string folderPath = _configuration["FileStorage:UploadPath"];
                string fileUrl = _configuration["FileStorage:FILEURL"];
                string jsonPath = Path.Combine(folderPath, "dataimage.json");

                if (!System.IO.File.Exists(jsonPath))
                {
                    return Ok(new List<object>());
                }

                var jsonContent = await System.IO.File.ReadAllTextAsync(jsonPath);

                var metadataList = JsonSerializer.Deserialize<List<MediaMetadata>>(jsonContent);

                if (metadataList == null) return Ok(new List<object>());
                string baseUrl = $"{Request.Scheme}://{Request.Host}";
                var result = metadataList.OrderBy(m => m.Position).Select(m => new
                {
                    m.FileName,
                    m.Position,
                    m.Duration,
                    Type = m.FileName.ToLower().EndsWith(".mp4") ? "video" : "image",
                    // PHẢI KHỚP VỚI RequestPath TRONG Program.cs
                    Src = $"{baseUrl}/StaticData/DATA/{m.FileName}"
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi đọc metadata: {ex.Message}");
            }
        }


        [HttpDelete("delete-file/{fileName}")]
        public async Task<IActionResult> DeleteFile(string fileName)
        {
            try
            {
                string folderPath = _configuration["FileStorage:UploadPath"];
                string filePath = Path.Combine(folderPath, fileName);
                string jsonPath = Path.Combine(folderPath, "dataimage.json");

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                if (System.IO.File.Exists(jsonPath))
                {
                    var jsonContent = await System.IO.File.ReadAllTextAsync(jsonPath);
                    var metadataList = JsonSerializer.Deserialize<List<MediaMetadata>>(jsonContent) ?? new List<MediaMetadata>();

                    var itemToRemove = metadataList.FirstOrDefault(m => m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

                    if (itemToRemove != null)
                    {
                        metadataList.Remove(itemToRemove);

                        var reorderedList = metadataList
                            .OrderBy(m => m.Position)
                            .Select((m, index) => {
                                m.Position = index + 1; 
                                return m;
                            }).ToList();

                        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                        string updatedJson = JsonSerializer.Serialize(reorderedList, jsonOptions);
                        await System.IO.File.WriteAllTextAsync(jsonPath, updatedJson);
                    }
                }

                return Ok(new { message = $"Đã xóa file {fileName} và cập nhật dữ liệu thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi xóa file: {ex.Message}");
            }
        }

        [HttpPost("update-media-list")]
        public async Task<IActionResult> UpdateMediaList([FromBody] List<UpdateMediaOrderRequest> updatedItems)
        {
            if (updatedItems == null || !updatedItems.Any())
                return BadRequest("Dữ liệu cập nhật không hợp lệ.");

            try
            {
                string folderPath = _configuration["FileStorage:UploadPath"];
                string jsonPath = Path.Combine(folderPath, "dataimage.json");

                if (!System.IO.File.Exists(jsonPath))
                    return NotFound("Không tìm thấy dữ liệu để cập nhật.");

                var jsonContent = await System.IO.File.ReadAllTextAsync(jsonPath);
                var metadataList = JsonSerializer.Deserialize<List<MediaMetadata>>(jsonContent) ?? new List<MediaMetadata>();

                foreach (var item in metadataList)
                {
                    var updatedInfo = updatedItems.FirstOrDefault(u => u.FileName.Equals(item.FileName, StringComparison.OrdinalIgnoreCase));

                    if (updatedInfo != null)
                    {
                        item.Position = updatedInfo.Position;
                        item.Duration = updatedInfo.Duration;
                    }
                }

                var finalSortedList = metadataList.OrderBy(m => m.Position).ToList();

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                string updatedJson = JsonSerializer.Serialize(finalSortedList, jsonOptions);
                await System.IO.File.WriteAllTextAsync(jsonPath, updatedJson);

                return Ok(new { message = "Cập nhật thứ tự và thời gian thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        [HttpPost("save-congtac")]
        public async Task<IActionResult> SaveCongTac([FromBody] CongTacModel item)
        {
            try
            {
                string folderPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string filePath = Path.Combine(folderPath, "congtac.json");

                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                List<CongTacModel> list = new List<CongTacModel>();

                // Kiểm tra file tồn tại và có nội dung (độ dài > 0)
                if (System.IO.File.Exists(filePath))
                {
                    string oldJson = await System.IO.File.ReadAllTextAsync(filePath);

                    // CHỈ giải mã nếu chuỗi JSON không trống
                    if (!string.IsNullOrWhiteSpace(oldJson))
                    {
                        list = JsonSerializer.Deserialize<List<CongTacModel>>(oldJson) ?? new List<CongTacModel>();
                    }
                }

                // Logic tự sinh ID nếu Id gửi lên là rỗng hoặc chuỗi mặc định "string"
                if (string.IsNullOrEmpty(item.Id) || item.Id == "string")
                {
                    item.Id = Guid.NewGuid().ToString(); // Tự sinh ID duy nhất
                    list.Add(item);
                }
                else
                {
                    var index = list.FindIndex(x => x.Id == item.Id);
                    if (index != -1) list[index] = item;
                    else list.Add(item);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, options));

                return Ok(new { success = true, message = "Lưu thành công!", id = item.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }


        [HttpGet("get-congtac")]
        public async Task<IActionResult> GetCongTac()
        {
            try
            {
                // 1. Xác định đường dẫn file từ cấu hình
                string folderPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string filePath = Path.Combine(folderPath, "congtac.json");

                // 2. Nếu file không tồn tại, trả về danh sách rỗng thay vì báo lỗi
                if (!System.IO.File.Exists(filePath))
                {
                    return Ok(new List<CongTacModel>());
                }

                // 3. Đọc nội dung file
                string jsonString = await System.IO.File.ReadAllTextAsync(filePath);

                // 4. Kiểm tra nội dung file có trống hay không
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return Ok(new List<CongTacModel>());
                }

                // 5. Giải mã JSON sang danh sách Object
                var list = JsonSerializer.Deserialize<List<CongTacModel>>(jsonString);

                return Ok(list);
            }
            catch (Exception ex)
            {
                // Trả về lỗi 500 nếu có vấn đề về quyền truy cập file hoặc hệ thống
                return StatusCode(500, new { success = false, message = $"Lỗi đọc dữ liệu: {ex.Message}" });
            }
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                // 1. Lấy đường dẫn file từ cấu hình (giống hàm GetData của bạn)
                string folderPath = _configuration["FileStorage:OutputPath"];
                string filePath = Path.Combine(folderPath, "login.json");

                if (!System.IO.File.Exists(filePath))
                {
                    return StatusCode(500, "Cơ sở dữ liệu người dùng không tồn tại.");
                }

                // 2. Đọc file và Deserialize thành danh sách UserAccount
                string jsonString = await System.IO.File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var users = JsonSerializer.Deserialize<List<UserAccount>>(jsonString, options);

                // 3. Tìm kiếm user khớp với thông tin đăng nhập
                var user = users?.FirstOrDefault(u => u.Ten == request.Ten && u.MatKhau == request.MatKhau);

                if (user != null)
                {
                    return Ok(new
                    {
                        message = "Đăng nhập thành công!",
                        user = new { ten = user.Ten, role = user.Role }
                    });
                }

                return Unauthorized(new { message = "Tài khoản hoặc mật khẩu không đúng!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }


        // Lấy danh sách tất cả tài khoản từ file login.json
        [HttpGet("get-accounts")]
        public async Task<IActionResult> GetAccounts()
        {
            try
            {
                string folderPath = _configuration["FileStorage:OutputPath"];
                string filePath = Path.Combine(folderPath, "login.json");

                if (!System.IO.File.Exists(filePath)) return Ok(new List<UserAccount>());

                string jsonString = await System.IO.File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var users = JsonSerializer.Deserialize<List<UserAccount>>(jsonString, options);

                // Trả về danh sách (ẩn mật khẩu để bảo mật)
                var result = users.Select(u => new {
                    ten = u.Ten,
                    role = u.Role
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        // Xóa tài khoản theo tên đăng nhập
        [HttpDelete("delete-user/{username}")]
        public async Task<IActionResult> DeleteUser(string username)
        {
            try
            {
                string folderPath = _configuration["FileStorage:OutputPath"];
                string filePath = Path.Combine(folderPath, "login.json");

                if (!System.IO.File.Exists(filePath)) return NotFound("Không tìm thấy file dữ liệu.");

                string jsonString = await System.IO.File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var users = JsonSerializer.Deserialize<List<UserAccount>>(jsonString, options);

                // Tìm user cần xóa
                var userToRemove = users.FirstOrDefault(u => u.Ten.Equals(username, StringComparison.OrdinalIgnoreCase));

                if (userToRemove == null) return NotFound("Người dùng không tồn tại.");

                // Xóa và lưu lại file
                users.Remove(userToRemove);
                string newJson = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(filePath, newJson);

                return Ok(new { message = "Đã xóa tài khoản thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }


        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser([FromBody] UserAccount newUser)
        {
            try
            {
                // 1. Kiểm tra dữ liệu đầu vào cơ bản
                if (string.IsNullOrEmpty(newUser.Ten) || string.IsNullOrEmpty(newUser.MatKhau))
                {
                    return BadRequest(new { message = "Vui lòng nhập đầy đủ tên và mật khẩu!" });
                }

                // 2. Đường dẫn file login.json
                string folderPath = _configuration["FileStorage:OutputPath"];
                string filePath = Path.Combine(folderPath, "login.json");

                List<UserAccount> users = new List<UserAccount>();

                // 3. Nếu file đã tồn tại, đọc danh sách hiện tại
                if (System.IO.File.Exists(filePath))
                {
                    string jsonString = await System.IO.File.ReadAllTextAsync(filePath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    users = JsonSerializer.Deserialize<List<UserAccount>>(jsonString, options) ?? new List<UserAccount>();
                }

                // 4. Kiểm tra trùng lặp (không cho phép tạo tên đăng nhập đã có)
                if (users.Any(u => u.Ten.Equals(newUser.Ten, StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(new { message = "Tên đăng nhập này đã tồn tại trên hệ thống!" });
                }

                // 5. Thêm user mới vào danh sách
                users.Add(new UserAccount
                {
                    Ten = newUser.Ten,
                    MatKhau = newUser.MatKhau,
                    Role = newUser.Role ?? "2" // Mặc định là Quản lý quân số nếu không chọn
                });

                // 6. Lưu lại file với định dạng đẹp (Indented)
                string newJson = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(filePath, newJson);

                return Ok(new { message = "Tạo tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }


        [HttpPost("save-nhanvien")]
        public async Task<IActionResult> SaveNhanVien([FromBody] NhanVienModel item)
        {
            try
            {
                string folderPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string filePath = Path.Combine(folderPath, "nhanvien.json");

                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                List<NhanVienModel> list = new List<NhanVienModel>();

                // 2. Đọc dữ liệu cũ từ file JSON nếu có
                if (System.IO.File.Exists(filePath))
                {
                    string oldJson = await System.IO.File.ReadAllTextAsync(filePath);
                    if (!string.IsNullOrWhiteSpace(oldJson))
                    {
                        list = JsonSerializer.Deserialize<List<NhanVienModel>>(oldJson) ?? new List<NhanVienModel>();
                    }
                }

                // 3. Logic xử lý ID (Tạo mới hoặc Cập nhật)
                if (string.IsNullOrEmpty(item.Id) || item.Id == "string")
                {
                    // Trường hợp thêm mới
                    item.Id = Guid.NewGuid().ToString();
                    list.Add(item);
                }
                else
                {
                    // Trường hợp cập nhật dựa trên ID
                    var index = list.FindIndex(x => x.Id == item.Id);
                    if (index != -1)
                    {
                        list[index] = item;
                    }
                    else
                    {
                        list.Add(item); // Nếu gửi ID nhưng không tìm thấy trong file thì vẫn thêm mới
                    }
                }

                // 4. Cấu hình format JSON (để đọc được tiếng Việt và thụt lề đẹp)
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                // 5. Ghi dữ liệu trở lại file
                await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, options));

                return Ok(new { success = true, message = "Lưu thông tin nhân viên thành công!", id = item.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }



        [HttpPost("update-nhanvien")]
        public async Task<IActionResult> UpdateNhanVien([FromBody] NhanVienModel item)
        {
            try
            {
                // 1. Cấu hình đường dẫn (giống API cũ của bạn)
                string folderPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string filePath = Path.Combine(folderPath, "nhanvien.json");

                if (!System.IO.File.Exists(filePath))
                    return NotFound(new { success = false, message = "Không tìm thấy dữ liệu nguồn!" });

                // 2. Đọc toàn bộ danh sách hiện tại
                string json = await System.IO.File.ReadAllTextAsync(filePath);
                var list = JsonSerializer.Deserialize<List<NhanVienModel>>(json) ?? new List<NhanVienModel>();

                // 3. Tìm nhân viên theo ID
                var existingItem = list.FirstOrDefault(x => x.Id == item.Id);

                if (existingItem != null)
                {
                    // Cập nhật các trường thông tin
                    existingItem.HoTen = item.HoTen;
                    existingItem.NgaySinh = item.NgaySinh;
                    existingItem.PhongBan = item.PhongBan;
                    existingItem.GhiChu = item.GhiChu;
                    // Bạn có thể thêm các trường khác nếu cần
                }
                else
                {
                    return BadRequest(new { success = false, message = "Nhân viên không tồn tại để cập nhật!" });
                }

                // 4. Lưu lại vào file JSON (có định dạng Tiếng Việt và thụt lề)
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, options));

                return Ok(new { success = true, message = "Cập nhật thông tin thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }


        [HttpDelete("delete-nhanvien/{id}")]
        public async Task<IActionResult> DeleteNhanVien(string id)
        {
            try
            {
                // 1. Cấu hình đường dẫn file
                string folderPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string filePath = Path.Combine(folderPath, "nhanvien.json");

                if (!System.IO.File.Exists(filePath))
                    return NotFound(new { success = false, message = "Không tìm thấy file dữ liệu!" });

                // 2. Đọc dữ liệu hiện tại
                string json = await System.IO.File.ReadAllTextAsync(filePath);
                var list = JsonSerializer.Deserialize<List<NhanVienModel>>(json) ?? new List<NhanVienModel>();

                // 3. Tìm và xóa
                var itemToRemove = list.FirstOrDefault(x => x.Id == id);
                if (itemToRemove == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy nhân viên cần xóa!" });
                }

                list.Remove(itemToRemove);

                // 4. Ghi lại vào file với format đẹp
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, options));

                return Ok(new { success = true, message = "Đã xóa nhân viên thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }
    }
}

public class MediaMetadata
{
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public int Position { get; set; }
    public int Duration { get; set; }
    public DateTime UploadDate { get; set; }
}

public class NhanVienModel
{
    public string Id { get; set; }
    public string HoTen { get; set; }
    public DateTime NgaySinh { get; set; }
    public string PhongBan { get; set; }
    public string GhiChu { get; set; }
}


public class CongTacModel
{
    public string Id { get; set; }
    public string NhanSu { get; set; }
    public string NoiDung { get; set; }

    public string TuNgay { get; set; }  // Ví dụ: "2026-03-01"
    public string DenNgay { get; set; } // Ví dụ: "2026-03-05"

    public string DiaDiem { get; set; }
    public string TrangThai { get; set; }

    public string? ViTri { get; set; }
}
    // Kiểu int để lưu thứ tự 1, 2, 3...}


    public class UpdateMediaOrderRequest
{

    public string FileName { get; set; }
    public int Position { get; set; }
    public int Duration { get; set; }
}


public class QuanSoHangNgayDto
{
    public string Ngay { get; set; }
    public string Tongquanso { get; set; }
    public string Phep { get; set; }
    public string PhepNote { get; set; }
    public string Online { get; set; }
}

public class LoginRequest
{
    // Sử dụng string.Empty để tránh cảnh báo Nullable nếu bạn dùng .NET 6+
    public string Site { get; set; } = string.Empty;
    public string Ten { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;
}

public class UserAccount
{
    public string Site { get; set; } = string.Empty;
    public string Ten { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
