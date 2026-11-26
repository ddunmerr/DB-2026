using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.Entities;
using PcShop.Models.ViewModels;

namespace PcShop.Services;

public class CompatibilityService : ICompatibilityService
{
    private readonly AppDbContext _db;
    private readonly List<CompatibilityRule> _rules;

    private const int CategoryCpu = 2;
    private const int CategoryMb = 3;
    private const int CategoryRam = 4;
    private const int CategoryGpu = 5;
    private const int CategoryPsu = 6;
    private const int CategoryCase = 7;

    private const int BaseSystemTdp = 100;   // плата, накопители, вентиляторы
    private const int PsuHeadroomWatts = 100; // рекомендуемый запас

    public CompatibilityService(AppDbContext db)
    {
        _db = db;
        _rules = BuildRules();
    }

    /// <summary>
    /// Парное правило совместимости. Проверяется в обе стороны,
    /// поэтому порядок выбора комплектующих не влияет на результат.
    /// </summary>
    private sealed record CompatibilityRule(
        int CategoryA,
        int CategoryB,
        Func<Product, Product, string?> Check);

    private List<CompatibilityRule> BuildRules() => new()
    {
        // === CPU ↔ MB: сокет ===
        new(CategoryCpu, CategoryMb, (cpu, mb) =>
        {
            var cpuSocket = GetStr(ParseSpecs(cpu), "socket");
            var mbSocket = GetStr(ParseSpecs(mb), "socket");
            return cpuSocket != mbSocket
                ? $"Сокет процессора ({cpuSocket}) не совпадает с сокетом материнской платы ({mbSocket})"
                : null;
        }),

        // === MB ↔ RAM: тип памяти (физическая несовместимость) ===
        new(CategoryMb, CategoryRam, (mb, ram) =>
        {
            var mbType = GetStr(ParseSpecs(mb), "memory_type");
            var ramType = GetStr(ParseSpecs(ram), "type");
            return mbType != ramType
                ? $"Материнская плата поддерживает {mbType}, а выбранная память — {ramType}"
                : null;
        }),

        // === MB ↔ Case: форм-фактор ===
        new(CategoryMb, CategoryCase, (mb, cs) =>
        {
            var mbForm = GetStr(ParseSpecs(mb), "form_factor");
            var caseForm = GetStr(ParseSpecs(cs), "form_factor");
            return FormFactorRank(mbForm) > FormFactorRank(caseForm)
                ? $"Плата формата {mbForm} не влезет в корпус (макс. поддерживаемый формат — {caseForm})"
                : null;
        }),

        // === GPU ↔ Case: длина видеокарты ===
        new(CategoryGpu, CategoryCase, (gpu, cs) =>
        {
            var len = GetInt(ParseSpecs(gpu), "length");
            var max = GetInt(ParseSpecs(cs), "max_gpu_length");
            return max > 0 && len > max
                ? $"Видеокарта ({len} мм) длиннее, чем позволяет корпус (макс. {max} мм)"
                : null;
        }),

        // === GPU ↔ PSU: коннекторы питания (с учётом количества) ===
        new(CategoryGpu, CategoryPsu, (gpu, psu) =>
        {
            var required = GetStringArray(ParseSpecs(gpu), "power_connectors");
            var available = GetStringArray(ParseSpecs(psu), "power_connectors");

            var missing = required
                .GroupBy(c => c)
                .Where(g => available.Count(a => a == g.Key) < g.Count())
                .Select(g => $"{g.Key} ×{g.Count()} (у БП есть {available.Count(a => a == g.Key)})")
                .ToList();

            return missing.Count > 0
                ? $"Блоку питания не хватает коннекторов для видеокарты: {string.Join(", ", missing)}"
                : null;
        }),
    };

    // ============================================================
    // 1. СПИСОК ТОВАРОВ С ПРИЧИНАМИ НЕСОВМЕСТИМОСТИ
    // ============================================================
    public async Task<List<ProductWithCompatibilityInfo>> GetProductsWithCompatibilityInfoAsync(
        int categoryId, IEnumerable<Product> currentBuild)
    {
        // Компонент той же категории будет заменён — исключаем его из контекста
        var context = currentBuild.Where(p => p.CategoryId != categoryId).ToList();

        var candidates = await _db.Products
            .Where(p => p.CategoryId == categoryId)
            .ToListAsync();

        var result = candidates.Select(p =>
        {
            var reason = GetIncompatibilityReason(p, context);
            return new ProductWithCompatibilityInfo
            {
                Product = p,
                IsCompatible = reason == null,
                IncompatibilityReason = reason
            };
        });

        return result
            .OrderByDescending(p => p.IsCompatible)
            .ThenBy(p => p.Product.Name)
            .ToList();
    }

    public Task<string?> GetIncompatibilityReasonAsync(Product candidate, IEnumerable<Product> currentBuild)
    {
        var context = currentBuild.Where(p => p.CategoryId != candidate.CategoryId).ToList();
        return Task.FromResult(GetIncompatibilityReason(candidate, context));
    }

    private string? GetIncompatibilityReason(Product candidate, List<Product> context)
    {
        // Парные правила
        foreach (var other in context)
        {
            var error = CheckPair(candidate, other);
            if (error != null) return error;
        }

        // Системное правило: мощность БП зависит от CPU + GPU одновременно
        if (candidate.CategoryId is CategoryPsu or CategoryCpu or CategoryGpu)
        {
            var psu = Pick(candidate, context, CategoryPsu);
            if (psu != null)
            {
                var error = CheckPsuWattage(
                    psu,
                    Pick(candidate, context, CategoryCpu),
                    Pick(candidate, context, CategoryGpu));
                if (error != null) return error;
            }
        }

        return null;
    }

    /// <summary>
    /// Проверяет пару товаров, подбирая правило в любом порядке категорий.
    /// </summary>
    private string? CheckPair(Product a, Product b)
    {
        foreach (var rule in _rules)
        {
            if (a.CategoryId == rule.CategoryA && b.CategoryId == rule.CategoryB)
                return rule.Check(a, b);

            if (a.CategoryId == rule.CategoryB && b.CategoryId == rule.CategoryA)
                return rule.Check(b, a);
        }
        return null;
    }

    private string? CheckPsuWattage(Product psu, Product? cpu, Product? gpu)
    {
        var wattage = GetInt(ParseSpecs(psu), "wattage");
        var consumption = EstimateConsumption(cpu, gpu);
        var required = consumption + PsuHeadroomWatts;

        return wattage < required
            ? $"Мощности БП ({wattage} Вт) недостаточно: система потребляет ~{consumption} Вт, нужен БП от {required} Вт"
            : null;
    }

    private int EstimateConsumption(Product? cpu, Product? gpu)
    {
        var tdp = BaseSystemTdp;
        if (cpu != null) tdp += GetInt(ParseSpecs(cpu), "tdp");
        if (gpu != null) tdp += GetInt(ParseSpecs(gpu), "tdp");
        return tdp;
    }

    /// <summary>
    /// Если проверяемый товар относится к нужной категории — берём его,
    /// иначе ищем уже выбранный компонент этой категории.
    /// </summary>
    private Product? Pick(Product candidate, List<Product> context, int categoryId) =>
        candidate.CategoryId == categoryId
            ? candidate
            : context.FirstOrDefault(p => p.CategoryId == categoryId);

    private static int FormFactorRank(string form) => form switch
    {
        "ITX" => 1,
        "mATX" => 2,
        "ATX" => 3,
        "E-ATX" => 4,
        _ => 0
    };

    // ============================================================
    // 2. ФИНАЛЬНАЯ ВАЛИДАЦИЯ СБОРКИ (те же правила)
    // ============================================================
    public Task<List<string>> ValidateBuildAsync(IEnumerable<Product> buildItems)
    {
        var build = buildItems.ToList();
        var errors = new List<string>();

        // Все пары компонентов
        for (int i = 0; i < build.Count; i++)
            for (int j = i + 1; j < build.Count; j++)
            {
                var error = CheckPair(build[i], build[j]);
                if (error != null) errors.Add(error);
            }

        // Системное правило по мощности
        var psu = build.FirstOrDefault(p => p.CategoryId == CategoryPsu);
        if (psu != null)
        {
            var error = CheckPsuWattage(
                psu,
                build.FirstOrDefault(p => p.CategoryId == CategoryCpu),
                build.FirstOrDefault(p => p.CategoryId == CategoryGpu));
            if (error != null) errors.Add(error);
        }

        return Task.FromResult(errors.Distinct().ToList());
    }

    // ============================================================
    // 3. ПРЕДУПРЕЖДЕНИЯ О "УЗКИХ ГОРЛЫШКАХ" (не блокируют заказ)
    // ============================================================
    public Task<List<string>> GetBottleneckWarningsAsync(IEnumerable<Product> buildItems)
    {
        var warnings = new List<string>();
        var build = buildItems.ToList();

        var cpu = build.FirstOrDefault(p => p.CategoryId == CategoryCpu);
        var gpu = build.FirstOrDefault(p => p.CategoryId == CategoryGpu);
        var ram = build.FirstOrDefault(p => p.CategoryId == CategoryRam);
        var psu = build.FirstOrDefault(p => p.CategoryId == CategoryPsu);
        var mb = build.FirstOrDefault(p => p.CategoryId == CategoryMb);

        // Баланс CPU ↔ GPU
        if (cpu != null && gpu != null)
        {
            var cpuScore = GetInt(ParseSpecs(cpu), "performance_score");
            var gpuScore = GetInt(ParseSpecs(gpu), "performance_score");

            if (cpuScore > 0 && gpuScore > 0)
            {
                var ratio = (double)gpuScore / cpuScore;
                if (ratio > 2.0)
                    warnings.Add("Процессор может стать узким горлышком: видеокарта значительно мощнее.");
                else if (ratio < 0.5)
                    warnings.Add("Видеокарта может стать узким горлышком: процессор значительно мощнее.");
            }
        }

        // Объём памяти
        if (ram != null)
        {
            var capacity = GetInt(ParseSpecs(ram), "capacity");
            if (capacity < 16)
                warnings.Add($"Объём памяти {capacity} ГБ может быть недостаточен для современных игр.");
        }

        // Запас БП
        if (psu != null && (cpu != null || gpu != null))
        {
            var consumption = EstimateConsumption(cpu, gpu);
            var wattage = GetInt(ParseSpecs(psu), "wattage");
            var headroom = (double)(wattage - consumption) / consumption * 100;

            if (headroom < 20)
                warnings.Add($"Блок питания работает почти на пределе: запас всего {headroom:F0}%.");
        }

        // Частота памяти выше официальной поддержки CPU
        if (ram != null && cpu != null)
        {
            var speed = GetInt(ParseSpecs(ram), "speed");
            var maxSpeed = GetInt(ParseSpecs(cpu), "max_ram_speed");
            if (maxSpeed > 0 && speed > maxSpeed)
                warnings.Add($"Память {speed} МГц будет работать на пониженной частоте (контроллер CPU — до {maxSpeed} МГц).");
        }

        // Частота памяти выше поддержки платы
        if (ram != null && mb != null)
        {
            var speed = GetInt(ParseSpecs(ram), "speed");
            var maxSpeed = GetInt(ParseSpecs(mb), "max_ram_speed");
            if (maxSpeed > 0 && speed > maxSpeed)
                warnings.Add($"Плата официально поддерживает память до {maxSpeed} МГц, модуль на {speed} МГц потребует разгона (XMP/EXPO).");
        }

        // Память занимает не все слоты (потенциал апгрейда)
        if (ram != null && mb != null)
        {
            var modules = GetInt(ParseSpecs(ram), "modules");
            var slots = GetInt(ParseSpecs(mb), "ram_slots");
            if (slots > 0 && modules > slots)
                warnings.Add($"Комплект из {modules} модулей не поместится в {slots} слота памяти на плате.");
        }

        return Task.FromResult(warnings);
    }

    // ============================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ РАБОТЫ С JSON
    // ============================================================
    private JsonElement ParseSpecs(Product p) =>
        JsonDocument.Parse(string.IsNullOrWhiteSpace(p.Specs) ? "{}" : p.Specs).RootElement;

    private string GetStr(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private int GetInt(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32() : 0;

    private List<string> GetStringArray(JsonElement el, string key)
    {
        var result = new List<string>();
        if (el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array)
            foreach (var item in v.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
                    result.Add(s);
        return result;
    }
}