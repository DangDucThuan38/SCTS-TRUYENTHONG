using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using ngay_1.DTOs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Text.Json;

namespace ngay_1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TruyenThongController : ControllerBase
    {

        private readonly IConfiguration _configuration;

        public TruyenThongController(IConfiguration configuration)
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

        // Quản lý tài khoản và đăng nhập
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Site)) return BadRequest("Thiếu SiteId");

                string rootPath = _configuration["FileStorage:OutputPath"];
                // Tạo đường dẫn động: DataStorage/SiteA/login.json
                string folderPath = Path.Combine(rootPath, request.Site);

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
                        user = new {site = user.Site ,ten = user.Ten, role = user.Role }
                    });
                }

                return Unauthorized(new { message = "Tài khoản hoặc mật khẩu không đúng!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        [HttpGet("get-accounts")]
        public async Task<IActionResult> GetAccounts(string Site)
        {
            try
            {
                if (string.IsNullOrEmpty(Site)) return BadRequest("Thiếu Site");
                string rootPath = _configuration["FileStorage:OutputPath"];
                // Tạo đường dẫn động: DataStorage/SiteA/login.json
                string folderPath = Path.Combine(rootPath, Site);
                string filePath = Path.Combine(folderPath, "login.json");

                if (!System.IO.File.Exists(filePath)) return Ok(new List<UserAccount>());

                string jsonString = await System.IO.File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var users = JsonSerializer.Deserialize<List<UserAccount>>(jsonString, options);

                // Trả về danh sách (ẩn mật khẩu để bảo mật)
                var result = users.Select(u => new {
                    site = u.Site,
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


        // 1. TẠO NGƯỜI DÙNG
        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser([FromBody] UserAccount newUser, [FromQuery] string site)
        {
            try
            {
                if (string.IsNullOrEmpty(site)) return BadRequest("Thiếu thông tin Site.");
                if (string.IsNullOrEmpty(newUser.Ten) || string.IsNullOrEmpty(newUser.MatKhau))
                    return BadRequest(new { message = "Vui lòng nhập đầy đủ tên và mật khẩu!" });

                string rootPath = _configuration["FileStorage:OutputPath"];
                // Logic Folder động: root/SCTS/login.json
                string folderPath = Path.Combine(rootPath, site);
                string filePath = Path.Combine(folderPath, "login.json");

                // Đảm bảo thư mục của Site tồn tại
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                List<UserAccount> users = new List<UserAccount>();

                if (System.IO.File.Exists(filePath))
                {
                    string jsonString = await System.IO.File.ReadAllTextAsync(filePath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    users = JsonSerializer.Deserialize<List<UserAccount>>(jsonString, options) ?? new List<UserAccount>();
                }

                // Kiểm tra trùng tên trong cùng Site
                if (users.Any(u => u.Ten.Equals(newUser.Ten, StringComparison.OrdinalIgnoreCase)))
                    return BadRequest(new { message = $"Tên đăng nhập này đã tồn tại trong site {site}!" });

                // Thêm user mới
                users.Add(new UserAccount
                {
                    Ten = newUser.Ten,
                    MatKhau = newUser.MatKhau,
                    Role = newUser.Role ?? "2",
                    Site = site // Lưu site vào object để khi login trả về đúng thông tin
                });

                string newJson = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(filePath, newJson);

                return Ok(new { message = $"Tạo tài khoản thành công cho site {site}!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        // 2. XÓA NGƯỜI DÙNG
        [HttpDelete("delete-user/{username}")]
        public async Task<IActionResult> DeleteUser(string username, [FromQuery] string site)
        {
            try
            {
                if (string.IsNullOrEmpty(site)) return BadRequest("Thiếu thông tin Site.");

                string rootPath = _configuration["FileStorage:OutputPath"];
                string folderPath = Path.Combine(rootPath, site);
                string filePath = Path.Combine(folderPath, "login.json");

                if (!System.IO.File.Exists(filePath)) return NotFound("Cơ sở dữ liệu người dùng không tồn tại.");

                string jsonString = await System.IO.File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var users = JsonSerializer.Deserialize<List<UserAccount>>(jsonString, options) ?? new List<UserAccount>();

                var userToRemove = users.FirstOrDefault(u => u.Ten.Equals(username, StringComparison.OrdinalIgnoreCase));
                if (userToRemove == null) return NotFound("Người dùng không tồn tại.");

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

        // Quản lý quân số 
        [HttpPost("save")]
        public async Task<IActionResult> SaveData([FromBody] object data, [FromQuery] string Site)
        {
            try
            {
                // 1. Kiểm tra nếu không truyền Site qua URL
                if (string.IsNullOrEmpty(Site))
                {
                    return BadRequest(new { message = "Vui lòng cung cấp tham số Site (ví dụ: ?Site=HN)" });
                }

                // 2. Lấy đường dẫn gốc từ cấu hình
                string rootPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";

                // 3. Tạo đường dẫn thư mục riêng cho từng Site
                string folderPath = Path.Combine(rootPath, Site);
                string filePath = Path.Combine(folderPath, "quansohangngay.json");

                // 4. Đảm bảo thư mục tồn tại
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // 5. Serialize dữ liệu với hỗ trợ Tiếng Việt
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    // Giúp hiển thị tiếng Việt rõ ràng trong file thay vì mã hóa Unicode
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string jsonString = JsonSerializer.Serialize(data, options);

                // 6. Ghi file (sử dụng Async để không chặn luồng)
                await System.IO.File.WriteAllTextAsync(filePath, jsonString);

                return Ok(new { message = "Lưu thành công!", site = Site, path = filePath });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetData([FromQuery] string Site)
        {
            try
            {
                if (string.IsNullOrEmpty(Site))
                {
                    return BadRequest("Vui lòng cung cấp tham số Site.");
                }

                // 2. Kết hợp Site vào đường dẫn thư mục
                string rootPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string folderPath = Path.Combine(rootPath, Site); // Ví dụ: C:\Data\Site01\
                string filePath = Path.Combine(folderPath, "quansohangngay.json");

                // 3. Kiểm tra file có tồn tại không
                if (!System.IO.File.Exists(filePath))
                {
                    return Ok(new QuanSoHangNgayDto()); // Trả về object rỗng để client không bị lỗi
                }

                // 4. Đọc file
                string jsonString = await System.IO.File.ReadAllTextAsync(filePath);

                // 5. Kiểm tra nội dung file có rỗng không (tránh lỗi Deserialize)
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return Ok(new QuanSoHangNgayDto());
                }

                // 6. Giải mã JSON
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = JsonSerializer.Deserialize<QuanSoHangNgayDto>(jsonString, options);

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống tại Site {Site}: {ex.Message}");
            }
        }


        // Media management (quản lý media)

        [HttpPost("upload-list")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadMediaList([FromForm] List<UploadDTO> items, [FromQuery] string Site)
        {
            // 1. Kiểm tra đầu vào
            if (string.IsNullOrEmpty(Site)) return BadRequest("Vui lòng cung cấp tham số Site.");
            if (items == null || items.Count == 0) return BadRequest("Không nhận được file nào.");

            long maxFileSize = 100 * 1024 * 1024; // 100MB

            try
            {
                // 2. Tạo đường dẫn thư mục theo Site: Uploads/SiteA, Uploads/SiteB...
                string rootUploadPath = _configuration["FileStorage:UploadPath"] ?? "Uploads";
                string folderPath = Path.Combine(rootUploadPath, Site);

                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                // 3. Đường dẫn file JSON riêng của Site đó
                string jsonPath = Path.Combine(folderPath, "dataimage.json");
                List<MediaMetadata> metadataList = new List<MediaMetadata>();

                // 4. Đọc dữ liệu cũ của riêng Site này
                if (System.IO.File.Exists(jsonPath))
                {
                    var existingJson = await System.IO.File.ReadAllTextAsync(jsonPath);
                    if (!string.IsNullOrWhiteSpace(existingJson))
                    {
                        try
                        {
                            metadataList = JsonSerializer.Deserialize<List<MediaMetadata>>(existingJson) ?? new List<MediaMetadata>();
                        }
                        catch { metadataList = new List<MediaMetadata>(); }
                    }
                }

                int lastPosition = metadataList.Any() ? metadataList.Max(m => m.Position) : 0;

                foreach (var item in items)
                {
                    var file = item.file;
                    if (file == null || file.Length == 0) continue;

                    if (file.Length > maxFileSize)
                    {
                        return BadRequest($"File '{item.FileName}' vượt quá giới hạn 100MB.");
                    }

                    // Xác định định dạng
                    string extension = file.ContentType.Contains("video") ? ".mp4" : ".jpg";
                    string fullFileName = item.FileName.EndsWith(extension) ? item.FileName : item.FileName + extension;
                    string filePath = Path.Combine(folderPath, fullFileName);

                    // Lưu file vật lý
                    if (extension == ".mp4")
                    {
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                    }
                    else
                    {
                        using (var inputStream = file.OpenReadStream())
                        using (var image = await Image.LoadAsync(inputStream))
                        {
                            await image.SaveAsJpegAsync(filePath, new JpegEncoder { Quality = 90 });
                        }
                    }

                    // Cập nhật Metadata
                    int assignedPosition = item.Position > 0 ? item.Position : ++lastPosition;

                    metadataList.Add(new MediaMetadata
                    {
                        FileName = fullFileName,
                        // Lưu đường dẫn tương đối để dễ truy cập từ web (Site/tenfile.jpg)
                        FilePath = Path.Combine(Site, fullFileName),
                        Position = assignedPosition,
                        Duration = item.Duration > 0 ? item.Duration : 10,
                        UploadDate = DateTime.Now
                    });
                }

                // 5. Lưu lại JSON vào đúng folder của Site
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string jsonString = JsonSerializer.Serialize(metadataList, jsonOptions);
                await System.IO.File.WriteAllTextAsync(jsonPath, jsonString);

                return Ok(new { message = $"Lưu thành công cho site {Site}!", count = items.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        [HttpGet("get-media-list")]
        public async Task<IActionResult> GetMediaList([FromQuery] string Site)
        {
            try
            {
                // 1. Kiểm tra tham số Site
                if (string.IsNullOrEmpty(Site))
                {
                    return BadRequest("Vui lòng cung cấp tham số Site.");
                }

                // 2. Lấy đường dẫn thư mục theo Site
                string rootUploadPath = _configuration["FileStorage:UploadPath"] ?? "Uploads";
                string folderPath = Path.Combine(rootUploadPath, Site);
                string jsonPath = Path.Combine(folderPath, "dataimage.json");

                // 3. Nếu chưa có file JSON (chưa có dữ liệu), trả về mảng rỗng
                if (!System.IO.File.Exists(jsonPath))
                {
                    return Ok(new List<object>());
                }

                // 4. Đọc và giải mã JSON
                var jsonContent = await System.IO.File.ReadAllTextAsync(jsonPath);
                if (string.IsNullOrWhiteSpace(jsonContent)) return Ok(new List<object>());

                var metadataList = JsonSerializer.Deserialize<List<MediaMetadata>>(jsonContent);
                if (metadataList == null) return Ok(new List<object>());

                // 5. Tạo đường dẫn URL tĩnh
                // Lưu ý: Src phải khớp với cấu hình StaticFiles trong Program.cs
                string baseUrl = $"{Request.Scheme}://{Request.Host}";

                var result = metadataList.OrderBy(m => m.Position).Select(m => new
                {
                    m.FileName,
                    m.Position,
                    m.Duration,
                    Type = m.FileName.ToLower().EndsWith(".mp4") ? "video" : "image",
                    // Thêm Site vào URL để trỏ đúng thư mục vật lý
                    Src = $"{baseUrl}/StaticData/{Site}/{m.FileName}"
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi đọc metadata của Site {Site}: {ex.Message}");
            }
        }

        [HttpPost("delete-file/{fileName}")]
        public async Task<IActionResult> DeleteFile(string fileName, [FromQuery] string Site)
        {
            try
            {
                // 1. Kiểm tra Site
                if (string.IsNullOrEmpty(Site))
                {
                    return BadRequest("Vui lòng cung cấp tham số Site để xác định vị trí file.");
                }

                // 2. Xác định đường dẫn thư mục của riêng Site đó
                string rootUploadPath = _configuration["FileStorage:UploadPath"] ?? "Uploads";
                string folderPath = Path.Combine(rootUploadPath, Site);

                string filePath = Path.Combine(folderPath, fileName);
                string jsonPath = Path.Combine(folderPath, "dataimage.json");

                // 3. Xóa file vật lý (Ảnh/Video) trong folder của Site
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // 4. Cập nhật lại file JSON metadata của Site đó
                if (System.IO.File.Exists(jsonPath))
                {
                    var jsonContent = await System.IO.File.ReadAllTextAsync(jsonPath);
                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        var metadataList = JsonSerializer.Deserialize<List<MediaMetadata>>(jsonContent) ?? new List<MediaMetadata>();

                        // Tìm và xóa item trong list
                        var itemToRemove = metadataList.FirstOrDefault(m => m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

                        if (itemToRemove != null)
                        {
                            metadataList.Remove(itemToRemove);

                            // Sắp xếp lại thứ tự (Position) sau khi xóa để không bị nhảy số
                            var reorderedList = metadataList
                                .OrderBy(m => m.Position)
                                .Select((m, index) => {
                                    m.Position = index + 1;
                                    return m;
                                }).ToList();

                            var jsonOptions = new JsonSerializerOptions
                            {
                                WriteIndented = true,
                                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            };
                            string updatedJson = JsonSerializer.Serialize(reorderedList, jsonOptions);
                            await System.IO.File.WriteAllTextAsync(jsonPath, updatedJson);
                        }
                    }
                }

                return Ok(new { message = $"Đã xóa file {fileName} thuộc site {Site} và cập nhật dữ liệu thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi xóa file tại site {Site}: {ex.Message}");
            }
        }


        [HttpPost("update-media-list")]
        public async Task<IActionResult> UpdateMediaList([FromBody] List<UpdateMediaOrderRequest> updatedItems, [FromQuery] string Site)
        {
            if (string.IsNullOrEmpty(Site))
                return BadRequest("Vui lòng cung cấp tham số Site.");

            if (updatedItems == null || !updatedItems.Any())
                return BadRequest("Dữ liệu cập nhật không hợp lệ.");
            try
            {
                // 2. Xác định đường dẫn thư mục và file JSON theo Site
                string rootUploadPath = _configuration["FileStorage:UploadPath"] ?? "Uploads";
                string folderPath = Path.Combine(rootUploadPath, Site);
                string jsonPath = Path.Combine(folderPath, "dataimage.json");

                // 3. Kiểm tra file tồn tại
                if (!System.IO.File.Exists(jsonPath))
                    return NotFound($"Không tìm thấy dữ liệu để cập nhật cho site {Site}.");

                // 4. Đọc dữ liệu hiện tại
                var jsonContent = await System.IO.File.ReadAllTextAsync(jsonPath);
                var metadataList = JsonSerializer.Deserialize<List<MediaMetadata>>(jsonContent) ?? new List<MediaMetadata>();

                // 5. Cập nhật thông tin dựa trên dữ liệu gửi lên
                foreach (var item in metadataList)
                {
                    var updatedInfo = updatedItems.FirstOrDefault(u => u.FileName.Equals(item.FileName, StringComparison.OrdinalIgnoreCase));

                    if (updatedInfo != null)
                    {
                        item.Position = updatedInfo.Position;
                        item.Duration = updatedInfo.Duration;
                    }
                }

                // 6. Sắp xếp lại danh sách theo vị trí mới
                var finalSortedList = metadataList.OrderBy(m => m.Position).ToList();

                // 7. Lưu lại vào file JSON của đúng Site
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string updatedJson = JsonSerializer.Serialize(finalSortedList, jsonOptions);
                await System.IO.File.WriteAllTextAsync(jsonPath, updatedJson);

                return Ok(new { message = $"Cập nhật thứ tự và thời gian cho site {Site} thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        [HttpPost("save-congtac")]
        public async Task<IActionResult> SaveCongTac([FromBody] CongTacModel item, [FromQuery] string Site)
        {
            try
            {
                if (string.IsNullOrEmpty(Site)) return BadRequest("Vui lòng cung cấp tham số Site.");

                string rootPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string folderPath = Path.Combine(rootPath, Site);
                string filePath = Path.Combine(folderPath, "congtac.json");

                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                List<CongTacModel> list = new List<CongTacModel>();

                if (System.IO.File.Exists(filePath))
                {
                    string oldJson = await System.IO.File.ReadAllTextAsync(filePath);
                    if (!string.IsNullOrWhiteSpace(oldJson))
                    {
                        list = JsonSerializer.Deserialize<List<CongTacModel>>(oldJson) ?? new List<CongTacModel>();
                    }
                }

                if (string.IsNullOrEmpty(item.Id) || item.Id == "string")
                {
                    item.Id = Guid.NewGuid().ToString();

                    // --- Logic tự tăng ViTri (string) ---
                    int maxViTri = 0;
                    if (list.Any())
                    {
                        maxViTri = list.Select(x => int.TryParse(x.ViTri, out int v) ? v : 0).Max();
                    }
                    item.ViTri = (maxViTri + 1).ToString();
                    // ------------------------------------

                    list.Add(item);
                }
                else
                {
                    var index = list.FindIndex(x => x.Id == item.Id);
                    if (index != -1)
                    {
                        // Khi sửa, giữ nguyên ViTri cũ
                        item.ViTri = list[index].ViTri;
                        list[index] = item;
                    }
                    else
                    {
                        list.Add(item);
                    }
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                // Luôn lưu file đã được sắp xếp theo ViTri
                var sortedList = list.OrderBy(x => int.TryParse(x.ViTri, out int v) ? v : 0).ToList();
                await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(sortedList, options));

                return Ok(new { success = true, message = $"Lưu công tác cho {Site} thành công!", id = item.Id, viTri = item.ViTri });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }
        // API Xóa công tác theo Site
        [HttpPost("delete-congtac/{id}")]
        public async Task<IActionResult> DeleteCongTac(string id, [FromQuery] string Site)
        {
            try
            {
                // 1. Kiểm tra tham số đầu vào
                if (string.IsNullOrEmpty(Site)) return BadRequest("Vui lòng cung cấp tham số Site.");
                if (string.IsNullOrEmpty(id)) return BadRequest("Vui lòng cung cấp Id cần xóa.");

                // 2. Xác định đường dẫn file theo Site
                string rootPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string filePath = Path.Combine(rootPath, Site, "congtac.json");

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new { success = false, message = $"Không tìm thấy dữ liệu công tác cho Site: {Site}" });
                }

                // 3. Đọc dữ liệu hiện có
                string jsonContent = await System.IO.File.ReadAllTextAsync(filePath);
                var list = JsonSerializer.Deserialize<List<CongTacModel>>(jsonContent) ?? new List<CongTacModel>();

                // 4. Tìm và xóa phần tử
                var itemToRemove = list.FirstOrDefault(x => x.Id == id);
                if (itemToRemove == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy công tác với Id đã cung cấp." });
                }

                list.Remove(itemToRemove);

                // 5. Lưu lại danh sách mới sau khi xóa
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, options));

                return Ok(new { success = true, message = $"Đã xóa công tác tại {Site} thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpPost("change-position")]
        public async Task<IActionResult> ChangePosition(string id, string direction, [FromQuery] string Site)
        {
            try
            {
                if (string.IsNullOrEmpty(Site)) return BadRequest("Thiếu Site");

                string rootPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string filePath = Path.Combine(rootPath, Site, "congtac.json");

                if (!System.IO.File.Exists(filePath)) return NotFound("Không tìm thấy file dữ liệu.");

                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var list = JsonSerializer.Deserialize<List<CongTacModel>>(json) ?? new List<CongTacModel>();

                // Sắp xếp theo thứ tự số của chuỗi ViTri
                list = list.OrderBy(x => int.TryParse(x.ViTri, out int v) ? v : 0).ToList();

                var index = list.FindIndex(x => x.Id == id);
                if (index == -1) return NotFound("Không tìm thấy mục cần chuyển.");

                bool isChanged = false;

                if (direction == "up" && index > 0)
                {
                    // Hoán đổi ViTri với mục phía trên
                    string temp = list[index].ViTri;
                    list[index].ViTri = list[index - 1].ViTri;
                    list[index - 1].ViTri = temp;
                    isChanged = true;
                }
                else if (direction == "down" && index < list.Count - 1)
                {
                    // Hoán đổi ViTri với mục phía dưới
                    string temp = list[index].ViTri;
                    list[index].ViTri = list[index + 1].ViTri;
                    list[index + 1].ViTri = temp;
                    isChanged = true;
                }

                if (isChanged)
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    // Lưu lại danh sách sau khi đã sắp xếp lại theo ViTri mới
                    await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list.OrderBy(x => int.TryParse(x.ViTri, out int v) ? v : 0), options));
                    return Ok(new { success = true, message = "Đã thay đổi thứ tự hiển thị" });
                }

                return BadRequest("Không thể di chuyển theo hướng này");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }


        [HttpGet("get-congtac")]
        public async Task<IActionResult> GetCongTac([FromQuery] string Site)
        {
            try
            {
                if (string.IsNullOrEmpty(Site)) return BadRequest("Vui lòng cung cấp tham số Site.");

                // 1. Tìm đúng thư mục của Site
                string rootPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string filePath = Path.Combine(rootPath, Site, "congtac.json");

                // 2. Nếu file không tồn tại (Site chưa có dữ liệu công tác)
                if (!System.IO.File.Exists(filePath))
                {
                    return Ok(new List<CongTacModel>());
                }

                // 3. Đọc nội dung file
                string jsonString = await System.IO.File.ReadAllTextAsync(filePath);

                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return Ok(new List<CongTacModel>());
                }

                // 4. Giải mã
                var list = JsonSerializer.Deserialize<List<CongTacModel>>(jsonString);

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi đọc dữ liệu của Site {Site}: {ex.Message}" });
            }
        }



        [HttpGet("get-tiendocongviec")]
        public async Task<IActionResult> GetTienDoCongViec([FromQuery] string Site)
        {
            try
            {
                if (string.IsNullOrEmpty(Site)) return BadRequest("Vui lòng cung cấp tham số Site.");

                // 1. Tìm đúng thư mục của Site
                string rootPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
                string filePath = Path.Combine(rootPath, Site, "tiendocongviec.json");

                // 2. Nếu file không tồn tại (Site chưa có dữ liệu công tác)
                if (!System.IO.File.Exists(filePath))
                {
                    return Ok(new List<CongTacModel>());
                }

                // 3. Đọc nội dung file
                string jsonString = await System.IO.File.ReadAllTextAsync(filePath);

                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return Ok(new List<CongTacModel>());
                }

                // 4. Giải mã
                var list = JsonSerializer.Deserialize<List<CongTacModel>>(jsonString);

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi đọc dữ liệu của Site {Site}: {ex.Message}" });
            }
        }




    }

}
