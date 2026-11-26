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
        // Инициализация: создаем временную базу данных SQLite
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated(); // Генерируем схему таблиц на основе моделей EF Core
    }

    /// <summary>
    /// Проверяет базовую операцию CRUD: успешное добавление товара в БД и его корректное чтение, включая парсинг JSON-характеристик.
    /// </summary>
    [Fact]
    public async Task AddProduct_ShouldPersistToDatabase()
    {
        // Arrange: Создаем и сохраняем категорию
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

        // Act: Добавляем товар и сохраняем изменения
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Assert: Извлекаем товар и проверяем, что все поля, включая JSON, сохранились корректно
        var savedProduct = await _context.Products.FirstOrDefaultAsync(p => p.Name == "Test CPU");
        savedProduct.Should().NotBeNull();
        savedProduct!.Price.Should().Be(25000);
        savedProduct.Specs.Should().Contain("AM5");
    }

    /// <summary>
    /// Проверяет критически важное бизнес-правило: цена товара в заказе фиксируется на момент покупки. 
    /// Последующее изменение цены в каталоге не должно влиять на историю уже оформленных заказов.
    /// </summary>
    [Fact]
    public async Task CreateOrder_ShouldCopyBuildItemsWithFixedPrice()
    {
        // Arrange: Создаем пользователя, категорию и товар с начальной ценой 30000
        var customer = new Customer { Email = "test@test.com", Name = "Test", PasswordHash = "hash" };
        _context.Customers.Add(customer);
        
        var category = new Category { Title = "CPU" };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var product = new Product { Name = "CPU", Brand = "AMD", Price = 30000, Stock = 5, CategoryId = category.Id, Specs = "{}" };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Создаем сборку с этим товаром
        var build = new Build { CustomerId = customer.Id, IsActive = true };
        _context.Builds.Add(build);
        _context.BuildItems.Add(new BuildItem { BuildId = build.Id, ProductId = product.Id, Quantity = 1 });
        await _context.SaveChangesAsync();

        // Act: Имитируем оформление заказа (копирование в OrderItem с фиксацией цены)
        var order = new Order { CustomerId = customer.Id, Status = "new" };
        _context.Orders.Add(order);
        _context.OrderItems.Add(new OrderItem { OrderId = order.Id, ProductId = product.Id, Quantity = 1, PriceAtPurchase = product.Price });
        await _context.SaveChangesAsync();

        // Меняем цену товара в каталоге на 35000
        product.Price = 35000;
        await _context.SaveChangesAsync();

        // Assert: Проверяем, что в заказе сохранилась старая цена (30000), а не новая (35000)
        var savedOrderItem = await _context.OrderItems.FirstOrDefaultAsync(oi => oi.OrderId == order.Id);
        savedOrderItem.Should().NotBeNull();
        savedOrderItem!.PriceAtPurchase.Should().Be(30000); 
    }

    /// <summary>
    /// Проверяет работу ограничений целостности (Constraints) на уровне БД: 
    /// один пользователь не может оставить более одного отзыва на один и тот же товар.
    /// </summary>
    [Fact]
    public async Task AddReview_ShouldEnforceUniqueConstraint()
    {
        // Arrange: Создаем пользователя и товар
        var customer = new Customer { Email = "user@test.com", Name = "User", PasswordHash = "hash" };
        _context.Customers.Add(customer);
        
        var category = new Category { Title = "GPU" };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var product = new Product { Name = "GPU", Brand = "NVIDIA", Price = 50000, Stock = 3, CategoryId = category.Id, Specs = "{}" };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Добавляем первый отзыв (должен пройти успешно)
        var review1 = new Review { CustomerId = customer.Id, ProductId = product.Id, Rating = 5, Text = "Great!" };
        _context.Reviews.Add(review1);
        await _context.SaveChangesAsync();

        // Act & Assert: Попытка добавить второй отзыв от того же пользователя на тот же товар 
        // должна вызвать исключение нарушения уникальности (DbUpdateException)
        var review2 = new Review { CustomerId = customer.Id, ProductId = product.Id, Rating = 4, Text = "Good" };
        _context.Reviews.Add(review2);

        var act = () => _context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // Очистка ресурсов после каждого теста
    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}