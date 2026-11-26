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
        // Создаем In-Memory БД для тестов (не засоряет реальную)
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        _service = new CompatibilityService(context);
    }

    [Fact]
    public async Task ValidateBuild_WithDifferentSockets_ReturnsError()
    {
        // Arrange
        var cpu = CreateProduct(2, "CPU", """{"socket":"AM5","tdp":105}""");
        var mb = CreateProduct(3, "MB", """{"socket":"LGA1700","memory_type":"DDR5"}""");

        // Act
        var errors = await _service.ValidateBuildAsync(new[] { cpu, mb });

        // Assert
        errors.Should().ContainSingle()
            .Which.Should().Contain("сокет");
    }

    [Fact]
    public async Task ValidateBuild_WithCompatibleComponents_ReturnsNoErrors()
    {
        // Arrange
        var cpu = CreateProduct(2, "CPU", """{"socket":"AM5","tdp":105}""");
        var mb = CreateProduct(3, "MB", """{"socket":"AM5","memory_type":"DDR5"}""");
        var ram = CreateProduct(4, "RAM", """{"type":"DDR5","speed":5600}""");

        // Act
        var errors = await _service.ValidateBuildAsync(new[] { cpu, mb, ram });

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateBuild_WithWrongMemoryType_ReturnsError()
    {
        // Arrange
        var mb = CreateProduct(3, "MB", """{"socket":"AM5","memory_type":"DDR5"}""");
        var ram = CreateProduct(4, "RAM", """{"type":"DDR4","speed":3200}""");

        // Act
        var errors = await _service.ValidateBuildAsync(new[] { mb, ram });

        // Assert
        errors.Should().ContainSingle()
            .Which.Should().Contain("DDR5").And.Contain("DDR4");
    }

    [Fact]
    public async Task ValidateBuild_WithGpuTooLong_ReturnsError()
    {
        // Arrange
        var gpu = CreateProduct(5, "GPU", """{"length":336,"tdp":450}""");
        var @case = CreateProduct(7, "Case", """{"form_factor":"ATX","max_gpu_length":325}""");

        // Act
        var errors = await _service.ValidateBuildAsync(new[] { gpu, @case });

        // Assert
        errors.Should().ContainSingle()
            .Which.Should().Contain("336").And.Contain("325");
    }

    [Fact]
    public async Task ValidateBuild_WithInsufficientPsu_ReturnsError()
    {
        // Arrange
        var cpu = CreateProduct(2, "CPU", """{"socket":"AM5","tdp":105}""");
        var gpu = CreateProduct(5, "GPU", """{"length":242,"tdp":200}""");
        var psu = CreateProduct(6, "PSU", """{"wattage":400}"""); // Нужно минимум 105+200+100=405

        // Act
        var errors = await _service.ValidateBuildAsync(new[] { cpu, gpu, psu });

        // Assert
        errors.Should().ContainSingle()
            .Which.Should().Contain("400").And.Contain("405");
    }

    [Fact]
    public async Task GetBottleneckWarnings_WithWeakCpu_ReturnsWarning()
    {
        // Arrange
        var cpu = CreateProduct(2, "CPU", """{"socket":"AM5","tdp":105,"performance_score":10000}""");
        var gpu = CreateProduct(5, "GPU", """{"length":242,"tdp":200,"performance_score":35000}""");

        // Act
        var warnings = await _service.GetBottleneckWarningsAsync(new[] { cpu, gpu });

        // Assert
        warnings.Should().ContainSingle()
            .Which.Should().Contain("узким горлышком");
    }

    // Вспомогательный метод для создания тестовых товаров
    private Product CreateProduct(int categoryId, string name, string specs)
    {
        return new Product
        {
            Id = categoryId * 100 + name.GetHashCode() % 100, // Уникальный ID
            Name = name,
            Brand = "TestBrand",
            Price = 1000,
            Stock = 10,
            CategoryId = categoryId,
            Specs = specs
        };
    }
}