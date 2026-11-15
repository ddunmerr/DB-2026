using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.Entities;
using System.Text.Json;

namespace PcShop.Services;

public class CompatibilityService : ICompatibilityService
{
    private readonly AppDbContext _db;
    private const int CategoryCpu = 2;
    private const int CategoryMb = 3;
    private const int CategoryRam = 4;
    private const int CategoryGpu = 5;
    private const int CategoryPsu = 6;
    private const int CategoryCase = 7;

    public CompatibilityService(AppDbContext db) => _db = db;

    /// <summary>
    /// Возвращает товары указанной категории, совместимые с уже выбранными компонентами.
    /// </summary>
    public async Task<List<Product>> GetCompatibleProductsAsync(
        int categoryId, IEnumerable<Product> currentBuild)
    {
        var buildList = currentBuild.ToList();
        var candidates = await _db.Products
            .Where(p => p.CategoryId == categoryId)
            .ToListAsync();

        return candidates.Where(p => IsCompatible(p, categoryId, buildList)).ToList();
    }

    /// <summary>
    /// Проверяет, совместим ли кандидат с текущей сборкой.
    /// </summary>
    private bool IsCompatible(Product candidate, int categoryId, List<Product> build)
    {
        var specs = JsonDocument.Parse(candidate.Specs).RootElement;

        var cpu = build.FirstOrDefault(p => p.CategoryId == CategoryCpu);
        var mb = build.FirstOrDefault(p => p.CategoryId == CategoryMb);
        var ram = build.FirstOrDefault(p => p.CategoryId == CategoryRam);
        var gpu = build.FirstOrDefault(p => p.CategoryId == CategoryGpu);

        return categoryId switch
        {
            // Материнская плата: сокет = сокету процессора
            CategoryMb when cpu != null =>
                GetStr(specs, "socket") == GetStr(ParseSpecs(cpu), "socket"),

            // ОЗУ: тип памяти (DDR4/DDR5) должен совпадать с типом, который поддерживает плата
            CategoryRam when mb != null =>
                GetStr(specs, "type") == GetStr(ParseSpecs(mb), "memory_type"),

            // Корпус: форм-фактор платы должен поддерживаться, видеокарта должна влезать по длине
            CategoryCase => IsCaseCompatible(specs, mb, gpu),

            // БП: мощность должна покрывать TDP процессора + видеокарты + запас 100W
            CategoryPsu => IsPsuCompatible(specs, cpu, gpu),

            // По умолчанию — совместимо
            _ => true
        };
    }

    private bool IsCaseCompatible(JsonElement caseSpecs, Product? mb, Product? gpu)
    {
        // Проверяем форм-фактор платы
        if (mb != null)
        {
            var mbForm = GetStr(ParseSpecs(mb), "form_factor"); // ATX, mATX, ITX
            var caseSupported = GetStr(caseSpecs, "form_factor"); // ATX (поддерживает ATX/mATX/ITX)

            var hierarchy = new Dictionary<string, int>
            {
                ["ITX"] = 1,
                ["mATX"] = 2,
                ["ATX"] = 3,
                ["E-ATX"] = 4
            };

            if (hierarchy.TryGetValue(mbForm, out var mbSize) &&
                hierarchy.TryGetValue(caseSupported, out var caseSize) &&
                mbSize > caseSize)
                return false;
        }

        // Проверяем длину видеокарты
        if (gpu != null)
        {
            var gpuLength = GetInt(ParseSpecs(gpu), "length");
            var maxGpuLength = GetInt(caseSpecs, "max_gpu_length");
            if (gpuLength > maxGpuLength)
                return false;
        }

        return true;
    }

    private bool IsPsuCompatible(JsonElement psuSpecs, Product? cpu, Product? gpu)
    {
        var wattage = GetInt(psuSpecs, "wattage");
        var totalTdp = 100; // базовое потребление системы (мат.плата, диски, вентиляторы)

        if (cpu != null)
            totalTdp += GetInt(ParseSpecs(cpu), "tdp");
        if (gpu != null)
            totalTdp += GetInt(ParseSpecs(gpu), "tdp");

        return wattage >= totalTdp + 100; // запас 100W
    }

    /// <summary>
    /// Финальная валидация всей сборки перед заказом.
    /// </summary>
    public Task<List<string>> ValidateBuildAsync(IEnumerable<Product> buildItems)
    {
        var errors = new List<string>();
        var build = buildItems.ToList();

        var cpu = build.FirstOrDefault(p => p.CategoryId == CategoryCpu);
        var mb = build.FirstOrDefault(p => p.CategoryId == CategoryMb);
        var ram = build.FirstOrDefault(p => p.CategoryId == CategoryRam);
        var gpu = build.FirstOrDefault(p => p.CategoryId == CategoryGpu);
        var psu = build.FirstOrDefault(p => p.CategoryId == CategoryPsu);
        var @case = build.FirstOrDefault(p => p.CategoryId == CategoryCase);

        if (cpu != null && mb != null)
        {
            if (GetStr(ParseSpecs(cpu), "socket") != GetStr(ParseSpecs(mb), "socket"))
                errors.Add("Процессор и материнская плата имеют разные сокеты");
        }

        if (mb != null && ram != null)
        {
            if (GetStr(ParseSpecs(mb), "memory_type") != GetStr(ParseSpecs(ram), "type"))
                errors.Add("Тип памяти не поддерживается материнской платой");
        }

        if (@case != null && gpu != null)
        {
            var gpuLen = GetInt(ParseSpecs(gpu), "length");
            var maxLen = GetInt(ParseSpecs(@case), "max_gpu_length");
            if (gpuLen > maxLen)
                errors.Add($"Видеокарта ({gpuLen} мм) не влезет в корпус (макс. {maxLen} мм)");
        }

        if (psu != null && cpu != null && gpu != null)
        {
            var totalTdp = 100 + GetInt(ParseSpecs(cpu), "tdp") + GetInt(ParseSpecs(gpu), "tdp");
            var wattage = GetInt(ParseSpecs(psu), "wattage");
            if (wattage < totalTdp + 100)
                errors.Add($"БП ({wattage}W) не потянет систему (нужно ~{totalTdp + 100}W)");
        }

        return Task.FromResult(errors);
    }

    // ---------- Вспомогательные методы ----------

    private JsonElement ParseSpecs(Product p) => JsonDocument.Parse(p.Specs).RootElement;

    private string GetStr(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private int GetInt(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32() : 0;
}