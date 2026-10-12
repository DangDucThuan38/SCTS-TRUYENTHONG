using Microsoft.AspNetCore.Mvc;

namespace ngay_1.DTOs
{
    public class UploadDTO
    {
        public IFormFile file { get; set; }
        public string FileName { get; set; }
        public int Position { get; set; }      // Vị trí hiển thị (VD: 1, 2, 3...)
        public int Duration { get; set; }      // Thời gian trình chiếu (giây)
    }
}
