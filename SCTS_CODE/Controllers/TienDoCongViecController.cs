using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace APISCTS.Controllers
{
    [Route("SCTS/[controller]")]
    [ApiController]
    public class TienDoCongViecController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _rootFolder;

        public TienDoCongViecController(IConfiguration configuration)
        {
            _configuration = configuration;
            // Lưu lại đường dẫn root, không cố định file json tại đây nữa
            _rootFolder = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
            if (!Directory.Exists(_rootFolder)) Directory.CreateDirectory(_rootFolder);
        }

        // Hàm helper để lấy đường dẫn file tiendocongviec.json và tự động tạo thư mục theo từng Site
        private string GetFilePathForSite(string site)
        {
            string siteFolder = Path.Combine(_rootFolder, site);
            if (!Directory.Exists(siteFolder))
            {
                Directory.CreateDirectory(siteFolder);
            }
            return Path.Combine(siteFolder, "tiendocongviec.json");
        }

        // GET: SCTS/TienDoCongViec/get-tiendo?site=SCTS
        [HttpGet("get-tiendo")]
        public async Task<IActionResult> GetTienDo([FromQuery] string site)
        {
            if (string.IsNullOrEmpty(site)) return BadRequest("Vui lòng cung cấp tham số site.");

            string filePath = GetFilePathForSite(site);
            if (!System.IO.File.Exists(filePath)) return Ok(new List<TienDoCongViecModel>());

            string json = await System.IO.File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(json)) return Ok(new List<TienDoCongViecModel>());

            var list = JsonSerializer.Deserialize<List<TienDoCongViecModel>>(json) ?? new List<TienDoCongViecModel>();
            return Ok(list.OrderBy(x => int.TryParse(x.ViTri, out int v) ? v : 0).ToList());
        }

        // POST: SCTS/TienDoCongViec/save-tiendo?site=SCTS
        [HttpPost("save-tiendo")]
        public async Task<IActionResult> SaveTienDo([FromQuery] string site, [FromBody] TienDoCongViecModel item)
        {
            if (string.IsNullOrEmpty(site)) return BadRequest("Vui lòng cung cấp tham số site.");

            try
            {
                string filePath = GetFilePathForSite(site);
                List<TienDoCongViecModel> list = new List<TienDoCongViecModel>();

                if (System.IO.File.Exists(filePath))
                {
                    string json = await System.IO.File.ReadAllTextAsync(filePath);
                    if (!string.IsNullOrWhiteSpace(json))
                        list = JsonSerializer.Deserialize<List<TienDoCongViecModel>>(json) ?? new List<TienDoCongViecModel>();
                }

                if (string.IsNullOrEmpty(item.Id) || item.Id == "string")
                {
                    item.Id = Guid.NewGuid().ToString();

                    // Tự động tăng ViTri cho đầu việc mới trong Site này
                    int maxViTri = 0;
                    if (list.Count > 0)
                    {
                        maxViTri = list.Select(x => int.TryParse(x.ViTri, out int v) ? v : 0).Max();
                    }
                    item.ViTri = (maxViTri + 1).ToString();

                    list.Add(item);
                }
                else
                {
                    var index = list.FindIndex(x => x.Id == item.Id);
                    if (index != -1)
                    {
                        // Giữ nguyên vị trí cũ khi chỉnh sửa thông tin tiến độ
                        item.ViTri = list[index].ViTri;
                        list[index] = item;
                    }
                    else
                    {
                        int maxViTri = list.Count > 0 ? list.Select(x => int.TryParse(x.ViTri, out int v) ? v : 0).Max() : 0;
                        item.ViTri = (maxViTri + 1).ToString();
                        list.Add(item);
                    }
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var sortedList = list.OrderBy(x => int.TryParse(x.ViTri, out int v) ? v : 0).ToList();
                await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(sortedList, options));

                return Ok(new { success = true, message = "Lưu tiến độ thành công!", id = item.Id, viTri = item.ViTri });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // POST: SCTS/TienDoCongViec/change-position?site=SCTS&id=xxx&direction=up
        [HttpPost("change-position")]
        public async Task<IActionResult> ChangePosition([FromQuery] string site, [FromQuery] string id, [FromQuery] string direction)
        {
            if (string.IsNullOrEmpty(site)) return BadRequest("Vui lòng cung cấp tham số site.");

            string filePath = GetFilePathForSite(site);
            if (!System.IO.File.Exists(filePath)) return BadRequest("File không tồn tại");

            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var list = JsonSerializer.Deserialize<List<TienDoCongViecModel>>(json) ?? new List<TienDoCongViecModel>();

            list = list.OrderBy(x => int.TryParse(x.ViTri, out int v) ? v : 0).ToList();

            var index = list.FindIndex(x => x.Id == id);
            if (index == -1) return NotFound("Không tìm thấy đầu việc");

            bool isChanged = false;

            if (direction == "up" && index > 0)
            {
                string temp = list[index].ViTri;
                list[index].ViTri = list[index - 1].ViTri;
                list[index - 1].ViTri = temp;
                isChanged = true;
            }
            else if (direction == "down" && index < list.Count - 1)
            {
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

                // Lưu lại thứ tự sắp xếp mới sau khi đổi vị trí của riêng Site này
                var sortedList = list.OrderBy(x => int.TryParse(x.ViTri, out int v) ? v : 0).ToList();
                await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(sortedList, options));

                return Ok(new { success = true, message = "Đã thay đổi thứ tự công việc" });
            }

            return Ok(new { success = false, message = "Không thể di chuyển thêm" });
        }

        // DELETE: SCTS/TienDoCongViec/delete-tiendo?site=SCTS&id=xxx
        [HttpPost("delete-tiendo")]
        public async Task<IActionResult> DeleteTienDo([FromQuery] string site, [FromQuery] string id)
        {
            if (string.IsNullOrEmpty(site)) return BadRequest("Vui lòng cung cấp tham số site.");

            string filePath = GetFilePathForSite(site);
            if (!System.IO.File.Exists(filePath)) return NotFound();

            string json = await System.IO.File.ReadAllTextAsync(filePath);
            var list = JsonSerializer.Deserialize<List<TienDoCongViecModel>>(json) ?? new List<TienDoCongViecModel>();

            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) return NotFound();

            list.Remove(item);

            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, options));

            return Ok(new { success = true, message = "Xóa đầu việc thành công!" });
        }
    }

    public class TienDoCongViecModel
    {
        public string Id { get; set; } = string.Empty;
        public string TenDauViec { get; set; } = string.Empty;
        public string NguoiPhuTrach { get; set; } = string.Empty;
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
        public int TienDoHienTai { get; set; }
        public string GhiChu { get; set; } = string.Empty;
        public string PhongBan { get; set; } = string.Empty;
        public string ViTri { get; set; } = "0";
    }
}