using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[Route("SCTS/[controller]")]
[ApiController]
public class CongTacController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _filePath;

    public CongTacController(IConfiguration configuration)
    {
        _configuration = configuration;
        string folderPath = _configuration["FileStorage:OutputPath"] ?? "DataStorage";
        _filePath = Path.Combine(folderPath, "congtac.json");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
    }

    // GET: SCTS/CongTac/get-congtac
    [HttpGet("get-congtac")]
    public async Task<IActionResult> GetCongTac()
    {
        if (!System.IO.File.Exists(_filePath)) return Ok(new List<CongTacModel>());
        string json = await System.IO.File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return Ok(new List<CongTacModel>());
        var list = JsonSerializer.Deserialize<List<CongTacModel>>(json);
        return Ok(list.OrderBy(x => x.ViTri).ToList());
    }

    // POST: SCTS/CongTac/save-congtac
    [HttpPost("save-congtac")]
    public async Task<IActionResult> SaveCongTac([FromBody] CongTacModel item)
    {
        try
        {
            List<CongTacModel> list = new List<CongTacModel>();
            if (System.IO.File.Exists(_filePath))
            {
                string json = await System.IO.File.ReadAllTextAsync(_filePath);
                if (!string.IsNullOrWhiteSpace(json))
                    list = JsonSerializer.Deserialize<List<CongTacModel>>(json) ?? new List<CongTacModel>();
            }

            if (string.IsNullOrEmpty(item.Id) || item.Id == "string")
            {
                item.Id = Guid.NewGuid().ToString();

                // --- Logic tự động tăng ViTri ---
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
                    // Khi cập nhật (Edit), giữ nguyên ViTri cũ trừ khi bạn muốn ghi đè
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

            // Lưu danh sách đã sắp xếp luôn để file JSON dễ theo dõi
            var sortedList = list.OrderBy(x => int.TryParse(x.ViTri, out int v) ? v : 0).ToList();
            await System.IO.File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(sortedList, options));

            return Ok(new { success = true, message = "Lưu thành công!", id = item.Id, viTri = item.ViTri });
        }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }


    [HttpPost("change-position")]
    public async Task<IActionResult> ChangePosition(string id, string direction)
    {
        if (!System.IO.File.Exists(_filePath)) return BadRequest("File không tồn tại");

        var json = await System.IO.File.ReadAllTextAsync(_filePath);
        var list = JsonSerializer.Deserialize<List<CongTacModel>>(json) ?? new List<CongTacModel>();

        // Chuyển đổi và sắp xếp dựa trên giá trị số của chuỗi ViTri
        // Sử dụng int.TryParse để tránh lỗi nếu chuỗi không phải là số
        list = list.OrderBy(x => int.TryParse(x.ViTri, out int v) ? v : 0).ToList();

        var index = list.FindIndex(x => x.Id == id);
        if (index == -1) return NotFound("Không tìm thấy mục");

        bool isChanged = false;

        if (direction == "up" && index > 0)
        {
            // Tráo đổi giá trị string ViTri
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

            // Lưu lại danh sách sau khi đã hoán đổi
            await System.IO.File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(list, options));

            return Ok(new { success = true, message = "Đã thay đổi vị trí hiển thị" });
        }

        return Ok(new { success = false, message = "Không thể di chuyển thêm" });
    }

    // DELETE: SCTS/CongTac/delete-congtac/{id}
    [HttpDelete("delete-congtac/{id}")]
    public async Task<IActionResult> DeleteCongTac(string id)
    {
        if (!System.IO.File.Exists(_filePath)) return NotFound();
        string json = await System.IO.File.ReadAllTextAsync(_filePath);
        var list = JsonSerializer.Deserialize<List<CongTacModel>>(json) ?? new List<CongTacModel>();
        var item = list.FirstOrDefault(x => x.Id == id);
        if (item == null) return NotFound();
        list.Remove(item);
        await System.IO.File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        return Ok(new { success = true, message = "Xóa thành công!" });
    }
}