using System.ComponentModel.DataAnnotations;

namespace ngay_1.Models
{
    public class ApplicationUser
    {
        public class User
        {
            [Key]
            public int Id { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }
            public Employees Employee { get; set; }
        }

    }
}
