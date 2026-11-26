using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.Entities;

namespace PcShop.Tests;

public class DatabaseIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public DatabaseIntegrationTests()
    {
        // Создаем временную SQLite БД в памяти
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated(); // Создаем схему БД
    }

    [Fact]
    public async Task AddProduct_ShouldPersistToDatabase()
    {
        // Arrange
        var category = new Category { Title = "Процессоры" };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var product = new Product
        {
            Name = "Test CPU",
            Brand = "AMD",
            Price = 25000,
            Stock = 10,
            CategoryId = category.Id,
            Specs = """{"socket":"AM5","tdp":105}"""
        };

        // Act
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Assert
        var savedProduct = await _context.Products.FirstOrDefaultAsync(p => p.Name == "Test CPU");
        savedProduct.Should().NotBeNull();
        savedProduct!.Price.Should().Be(25000);
        savedProduct.Specs.Should().Contain("AM5");
    }

    [Fact]
    public async Task CreateOrder_ShouldCopyBuildItemsWithFixedPrice()
    {
        // Arrange
        var customer = new Customer { Email = "test@test.com", Name = "Test", PasswordHash = "hash" };
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var category = new Category { Title = "CPU" };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var product = new Product
        {
            Name = "CPU",
            Brand = "AMD",
            Price = 30000, // Текущая цена
            Stock = 5,
            CategoryId = category.Id,
            Specs = "{}"
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var build = new Build { CustomerId = customer.Id, IsActive = true };
        _context.Builds.Add(build);
        await _context.SaveChangesAsync();

        var buildItem = new BuildItem { BuildId = build.Id, ProductId = product.Id, Quantity = 1 };
        _context.BuildItems.Add(buildItem);
        await _context.SaveChangesAsync();

        // Act: Имитируем оформление заказа
        var order = new Order { CustomerId = customer.Id, Status = "new" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var orderItem = new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = 1,
            PriceAtPurchase = product.Price // Фиксируем цену на момент покупки
        };
        _context.OrderItems.Add(orderItem);
        await _context.SaveChangesAsync();

        // Меняем цену товара
        product.Price = 35000;
        await _context.SaveChangesAsync();

        // Assert: Цена в заказе не изменилась
        var savedOrderItem = await _context.OrderItems.FirstOrDefaultAsync(oi => oi.OrderId == order.Id);
        savedOrderItem.Should().NotBeNull();
        savedOrderItem!.PriceAtPurchase.Should().Be(30000); // Старая цена, не 35000
    }

    [Fact]
    public async Task AddReview_ShouldEnforceUniqueConstraint()
    {
        // Arrange
        var customer = new Customer { Email = "user@test.com", Name = "User", PasswordHash = "hash" };
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var category = new Category { Title = "GPU" };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var product = new Product
        {
            Name = "GPU",
            Brand = "NVIDIA",
            Price = 50000,
            Stock = 3,
            CategoryId = category.Id,
            Specs = "{}"
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var review1 = new Review
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            Rating = 5,
            Text = "Great!"
        };
        _context.Reviews.Add(review1);
        await _context.SaveChangesAsync();

        // Act & Assert: Второй отзыв на тот же товар должен упасть
        var review2 = new Review
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            Rating = 4,
            Text = "Good"
        };
        _context.Reviews.Add(review2);

        var act = () => _context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}