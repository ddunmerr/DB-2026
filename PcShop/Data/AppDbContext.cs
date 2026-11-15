using Microsoft.EntityFrameworkCore;
using PcShop.Models.Entities;

namespace PcShop.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Build> Builds => Set<Build>();
    public DbSet<BuildItem> BuildItems => Set<BuildItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Уникальный отзыв на товар от пользователя
        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.CustomerId, r.ProductId })
            .IsUnique();

        // Один отзыв = рейтинг 1..5
        modelBuilder.Entity<Review>()
            .Property(r => r.Rating);
    }
}