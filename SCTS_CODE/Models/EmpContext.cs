using Microsoft.EntityFrameworkCore;

namespace ngay_1.Models
{
    public class EmpContext : DbContext
    {
        public EmpContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Employees> Employees
        {
            get;
            set;
        }

        public DbSet<ApplicationUser> ApplicationUsers
        {
            get;
            set;
        }

    }
}