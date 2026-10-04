using Microsoft.EntityFrameworkCore;
using werxxedPC.Models;

namespace werxxedPC.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        // Таблицы
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<PriceHistory> PriceHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Здесь настраиваются связи, ключи, ограничения
            modelBuilder.Entity<Category>()
                .HasOne(c => c.Parent)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentId);

            modelBuilder.Entity<Cart>()
                .HasIndex(c => c.CustomerId)
                .IsUnique();

            modelBuilder.Entity<CartItem>()
                .HasIndex(ci => new { ci.CartId, ci.ProductId })
                .IsUnique();

            modelBuilder.Entity<Review>()
                .HasIndex(r => new { r.CustomerId, r.ProductId })
                .IsUnique();
        }
    }
}