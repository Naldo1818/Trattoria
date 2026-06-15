using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Trattoria.Models;

namespace Trattoria.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Orders> Orders { get; set; }
        public DbSet<Tables> Tables { get; set; }
        public DbSet<OrderDetails> OrderDetails { get; set; }
        public DbSet<Users> Users { get; set; }
        public DbSet<Reservations> Reservations { get; set; }
        public DbSet<MenuItems> MenuItems { get; set; }
    }
}