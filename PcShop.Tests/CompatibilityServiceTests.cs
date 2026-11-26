using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.Entities;
using PcShop.Services;

namespace PcShop.Tests;

public class CompatibilityServiceTests
{
    private readonly CompatibilityService _service;

    public CompatibilityServiceTests()
    {
        // Инициализация: создаем изолированную In-Memory базу данных для каждого теста, 
        // чтобы они не влияли друг на друга и не засоряли основную БД.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        _service = new CompatibilityService(context);
    }

    /// <summary>
    /// Проверяет, что система блокирует сборку с несовместимыми сокетами процессора и материнской платы.
    /// </summary>
    [Fact]
    public async Task ValidateBuild_WithDifferentSockets_ReturnsError()
    {
        // Arrange (Подготовка): Создаем процессор AM5 и материнскую плату LGA1700
        var cpu = CreateProduct(2, "CPU", """{"socket":"AM5","tdp":105}""");
        var mb = CreateProduct(3, "MB", """{"socket":"LGA1700","memory_type":"DDR5"}""");

        // Act (Действие): Запускаем валидацию сборки
        var errors = await _service.ValidateBuildAsync(new[] { cpu, mb });

        // Assert (Проверка): Ожидаем ровно одну ошибку, содержащую слово "сокет"
        errors.Should().ContainSingle()
            .Which.Should().Contain("сокет");
    }

    /// <summary>
    /// Проверяет, что корректно подобранные совместимые компоненты проходят валидацию без ошибок.
    /// </summary>
    [Fact]
    public async Task ValidateBuild_WithCompatibleComponents_ReturnsNoErrors()
    {
        // Arrange: Создаем совместимые компоненты (AM5 + AM5 + DDR5)
        var cpu = CreateProduct(2, "CPU", """{"socket":"AM5","tdp":105}""");
        var mb = CreateProduct(3, "MB", """{"socket":"AM5","memory_type":"DDR5"}""");
        var ram = CreateProduct(4, "RAM", """{"type":"DDR5","speed":5600}""");

        // Act
        var errors = await _service.ValidateBuildAsync(new[] { cpu, mb, ram });

        // Assert: Список ошибок должен быть пустым
        errors.Should().BeEmpty();
    }

    /// <summary>
    /// Проверяет блокировку при попытке установить память неподдерживаемого типа (например, DDR4 в плату для DDR5).
    /// </summary>
    [Fact]
    public async Task ValidateBuild_WithWrongMemoryType_ReturnsError()
    {
        // Arrange: Плата поддерживает DDR5, а выбрана память DDR4
        var mb = CreateProduct(3, "MB", """{"socket":"AM5","memory_type":"DDR5"}""");
        var ram = CreateProduct(4, "RAM", """{"type":"DDR4","speed":3200}""");

        // Act
        var errors = await _service.ValidateBuildAsync(new[] { mb, ram });

        // Assert: Ошибка должна указывать на несовпадение типов памяти
        errors.Should().ContainSingle()
            .Which.Should().Contain("DDR5").And.Contain("DDR4");
    }

    /// <summary>
    /// Проверяет, что видеокарта, физически не вмещающаяся в корпус по длине, вызывает ошибку.
    /// </summary>
    [Fact]
    public async Task ValidateBuild_WithGpuTooLong_ReturnsError()
    {
        // Arrange: Видеокарта 336 мм, корпус вмещает максимум 325 мм
        var gpu = CreateProduct(5, "GPU", """{"length":336,"tdp":450}""");
        var @case = CreateProduct(7, "Case", """{"form_factor":"ATX","max_gpu_length":325}""");

        // Act
        var errors = await _service.ValidateBuildAsync(new[] { gpu, @case });

        // Assert: Ошибка должна содержать упоминание конкретных размеров
        errors.Should().ContainSingle()
            .Which.Should().Contain("336").And.Contain("325");
    }

    /// <summary>
    /// Проверяет, что блок питания с недостаточной мощностью для системы (CPU + GPU + запас) блокируется.
    /// </summary>
    [Fact]
    public async Task ValidateBuild_WithInsufficientPsu_ReturnsError()
    {
        // Arrange: CPU (105W) + GPU (200W) + База (100W) = 405W минимум. БП выдает только 400W.
        var cpu = CreateProduct(2, "CPU", """{"socket":"AM5","tdp":105}""");
        var gpu = CreateProduct(5, "GPU", """{"length":242,"tdp":200}""");
        var psu = CreateProduct(6, "PSU", """{"wattage":400}""");

        // Act
        var errors = await _service.ValidateBuildAsync(new[] { cpu, gpu, psu });

        // Assert: Ошибка должна указывать на нехватку мощности
        errors.Should().ContainSingle()
            .Which.Should().Contain("400").And.Contain("405");
    }

    /// <summary>
    /// Проверяет работу системы предупреждений: слабый процессор с мощной видеокартой должен генерировать warning о bottleneck, но не блокировать сборку.
    /// </summary>
    [Fact]
    public async Task GetBottleneckWarnings_WithWeakCpu_ReturnsWarning()
    {
        // Arrange: CPU с низким performance_score (10000) и GPU с высоким (35000)
        var cpu = CreateProduct(2, "CPU", """{"socket":"AM5","tdp":105,"performance_score":10000}""");
        var gpu = CreateProduct(5, "GPU", """{"length":242,"tdp":200,"performance_score":35000}""");

        // Act
        var warnings = await _service.GetBottleneckWarningsAsync(new[] { cpu, gpu });

        // Assert: Должно быть предупреждение о "узком горлышке"
        warnings.Should().ContainSingle()
            .Which.Should().Contain("узким горлышком");
    }

    // Вспомогательный метод для быстрого создания тестовых объектов Product
    private Product CreateProduct(int categoryId, string name, string specs)
    {
        return new Product
        {
            Id = categoryId * 100 + name.GetHashCode() % 100,
            Name = name,
            Brand = "TestBrand",
            Price = 1000,
            Stock = 10,
            CategoryId = categoryId,
            Specs = specs
        };
    }
}