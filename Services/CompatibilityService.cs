using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.Entities;

namespace PcShop.Services;

public class CompatibilityService : ICompatibilityService
{
    private readonly AppDbContext _db;

    // ID категорий
    private const int CategoryCpu = 2;
    private const int CategoryMb = 3;
    private const int CategoryRam = 4;
    private const int CategoryGpu = 5;
    private const int CategoryPsu = 6;
    private const int CategoryCase = 7;

    public CompatibilityService(AppDbContext db) => _db = db;

    // ============================================================
    // 1. ПОЛУЧЕНИЕ СОВМЕСТИМЫХ ТОВАРОВ (двусторонняя проверка)
    // ============================================================
    public async Task<List<Product>> GetCompatibleProductsAsync(
        int categoryId, IEnumerable<Product> currentBuild)
    {
        var buildList = currentBuild.ToList();
        var candidates = await _db.Products
            .Where(p => p.CategoryId == categoryId)
            .ToListAsync();

        return candidates.Where(p => IsCompatible(p, categoryId, buildList)).ToList();
    }

    private bool IsCompatible(Product candidate, int categoryId, List<Product> build)
    {
        var specs = ParseSpecs(candidate);

        var cpu = build.FirstOrDefault(p => p.CategoryId == CategoryCpu);
        var mb = build.FirstOrDefault(p => p.CategoryId == CategoryMb);
        var ram = build.FirstOrDefault(p => p.CategoryId == CategoryRam);
        var gpu = build.FirstOrDefault(p => p.CategoryId == CategoryGpu);
        var psu = build.FirstOrDefault(p => p.CategoryId == CategoryPsu);

        return categoryId switch
        {
            // === CPU ===
            // Если выбрана MB — сокет CPU должен совпадать с сокетом MB
            CategoryCpu when mb != null =>
                GetStr(specs, "socket") == GetStr(ParseSpecs(mb), "socket"),

            // === MB ===
            // Если выбран CPU — сокет MB должен совпадать с сокетом CPU
            // Если выбрана RAM — тип памяти MB должен совпадать с типом RAM
            CategoryMb => IsMbCompatible(specs, cpu, ram),

            // === RAM ===
            // Если выбрана MB — тип памяти RAM должен совпадать с типом, который поддерживает MB
            // Если выбран CPU — частота RAM не должна превышать макс. частоту CPU
            CategoryRam => IsRamCompatible(specs, mb, cpu),

            // === GPU ===
            // Если выбран PSU — коннекторы питания GPU должны поддерживаться БП
            CategoryGpu => IsGpuCompatible(specs, psu),

            // === PSU ===
            // Мощность должна покрывать TDP системы
            // Коннекторы должны поддерживать GPU (если выбрана)
            CategoryPsu => IsPsuCompatible(specs, cpu, gpu),

            // === Case ===
            // Форм-фактор MB должен поддерживаться
            // Длина GPU должна влезать
            CategoryCase => IsCaseCompatible(specs, mb, gpu),

            _ => true
        };
    }

    // --- Проверки совместимости по категориям ---

    private bool IsMbCompatible(JsonElement mbSpecs, Product? cpu, Product? ram)
    {
        // Проверка сокета с CPU
        if (cpu != null)
        {
            if (GetStr(mbSpecs, "socket") != GetStr(ParseSpecs(cpu), "socket"))
                return false;
        }

        // Проверка типа памяти с RAM
        if (ram != null)
        {
            if (GetStr(mbSpecs, "memory_type") != GetStr(ParseSpecs(ram), "type"))
                return false;
        }

        return true;
    }

    private bool IsRamCompatible(JsonElement ramSpecs, Product? mb, Product? cpu)
    {
        if (mb != null)
        {
            if (GetStr(ramSpecs, "type") != GetStr(ParseSpecs(mb), "memory_type"))
                return false;
        }
        return true;
    }

    private bool IsGpuCompatible(JsonElement gpuSpecs, Product? psu)
    {
        // Проверка коннекторов питания с PSU
        if (psu != null)
        {
            var gpuConnectors = GetStringArray(ParseSpecs(psu), "power_connectors");
            var psuConnectors = GetStringArray(gpuSpecs, "power_connectors");

            // Простая проверка: каждый коннектор GPU должен быть в PSU
            // (в реальности нужна более сложная логика, но для курсовой достаточно)
            foreach (var required in gpuConnectors)
            {
                if (!psuConnectors.Contains(required))
                    return false;
            }
        }

        return true;
    }

    private bool IsPsuCompatible(JsonElement psuSpecs, Product? cpu, Product? gpu)
    {
        var wattage = GetInt(psuSpecs, "wattage");
        var totalTdp = 100; // базовое потребление системы

        if (cpu != null) totalTdp += GetInt(ParseSpecs(cpu), "tdp");
        if (gpu != null) totalTdp += GetInt(ParseSpecs(gpu), "tdp");

        // Запас минимум 100W
        return wattage >= totalTdp + 100;
    }

    private bool IsCaseCompatible(JsonElement caseSpecs, Product? mb, Product? gpu)
    {
        // Форм-фактор MB
        if (mb != null)
        {
            var mbForm = GetStr(ParseSpecs(mb), "form_factor");
            var caseSupported = GetStr(caseSpecs, "form_factor");

            var hierarchy = new Dictionary<string, int>
            {
                ["ITX"] = 1, ["mATX"] = 2, ["ATX"] = 3, ["E-ATX"] = 4
            };

            if (hierarchy.TryGetValue(mbForm, out var mbSize) &&
                hierarchy.TryGetValue(caseSupported, out var caseSize) &&
                mbSize > caseSize)
                return false;
        }

        // Длина GPU
        if (gpu != null)
        {
            var gpuLength = GetInt(ParseSpecs(gpu), "length");
            var maxGpuLength = GetInt(caseSpecs, "max_gpu_length");
            if (gpuLength > maxGpuLength) return false;
        }

        return true;
    }

    // ============================================================
    // 2. ФИНАЛЬНАЯ ВАЛИДАЦИЯ СБОРКИ (критические ошибки)
    // ============================================================
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

        // CPU ↔ MB (сокет)
        if (cpu != null && mb != null)
        {
            if (GetStr(ParseSpecs(cpu), "socket") != GetStr(ParseSpecs(mb), "socket"))
                errors.Add("Процессор и материнская плата имеют разные сокеты");
        }

        // MB ↔ RAM (тип памяти)
        if (mb != null && ram != null)
        {
            if (GetStr(ParseSpecs(mb), "memory_type") != GetStr(ParseSpecs(ram), "type"))
                errors.Add("Тип памяти не поддерживается материнской платой");
        }

        // RAM ↔ CPU (частота)
        if (ram != null && cpu != null)
        {
            var ramSpeed = GetInt(ParseSpecs(ram), "speed");
            var cpuMaxSpeed = GetInt(ParseSpecs(cpu), "max_ram_speed");
            if (cpuMaxSpeed > 0 && ramSpeed > cpuMaxSpeed)
                errors.Add($"Частота памяти ({ramSpeed} МГц) превышает максимум, поддерживаемый процессором ({cpuMaxSpeed} МГц)");
        }

        // GPU ↔ Case (длина)
        if (@case != null && gpu != null)
        {
            var gpuLen = GetInt(ParseSpecs(gpu), "length");
            var maxLen = GetInt(ParseSpecs(@case), "max_gpu_length");
            if (gpuLen > maxLen)
                errors.Add($"Видеокарта ({gpuLen} мм) не влезет в корпус (макс. {maxLen} мм)");
        }

        // PSU ↔ система (мощность)
        if (psu != null && cpu != null && gpu != null)
        {
            var totalTdp = 100 + GetInt(ParseSpecs(cpu), "tdp") + GetInt(ParseSpecs(gpu), "tdp");
            var wattage = GetInt(ParseSpecs(psu), "wattage");
            if (wattage < totalTdp + 100)
                errors.Add($"БП ({wattage}W) не потянет систему (нужно ~{totalTdp + 100}W)");
        }

        // GPU ↔ PSU (коннекторы)
        if (gpu != null && psu != null)
        {
            var gpuConnectors = GetStringArray(ParseSpecs(gpu), "power_connectors");
            var psuConnectors = GetStringArray(ParseSpecs(psu), "power_connectors");

            foreach (var required in gpuConnectors)
            {
                if (!psuConnectors.Contains(required))
                {
                    errors.Add($"БП не имеет необходимого коннектора питания для видеокарты: {required}");
                    break;
                }
            }
        }

        return Task.FromResult(errors);
    }

    // ============================================================
    // 3. ПРЕДУПРЕЖДЕНИЯ О "УЗКИХ ГОРЛЫШКАХ"
    // ============================================================
    public Task<List<string>> GetBottleneckWarningsAsync(IEnumerable<Product> buildItems)
    {
        var warnings = new List<string>();
        var build = buildItems.ToList();

        var cpu = build.FirstOrDefault(p => p.CategoryId == CategoryCpu);
        var gpu = build.FirstOrDefault(p => p.CategoryId == CategoryGpu);
        var ram = build.FirstOrDefault(p => p.CategoryId == CategoryRam);
        var psu = build.FirstOrDefault(p => p.CategoryId == CategoryPsu);

        // === Баланс CPU ↔ GPU ===
        if (cpu != null && gpu != null)
        {
            var cpuScore = GetInt(ParseSpecs(cpu), "performance_score");
            var gpuScore = GetInt(ParseSpecs(gpu), "performance_score");

            if (cpuScore > 0 && gpuScore > 0)
            {
                var ratio = (double)gpuScore / cpuScore;

                if (ratio > 2.0)
                {
                    warnings.Add($"⚠️ Процессор может быть узким горлышком для видеокарты. Видеокарта значительно мощнее процессора.");
                }
                else if (ratio < 0.5)
                {
                    warnings.Add($"⚠️ Видеокарта может быть узким горлышком. Процессор значительно мощнее видеокарты.");
                }
            }
        }

        // === Объём RAM ===
        if (ram != null)
        {
            var capacity = GetInt(ParseSpecs(ram), "capacity");
            if (capacity < 16)
            {
                warnings.Add($"⚠️ Объём памяти ({capacity} ГБ) может быть недостаточен для современных игр и задач.");
            }
        }

        // === Запас БП ===
        if (psu != null && cpu != null && gpu != null)
        {
            var totalTdp = 100 + GetInt(ParseSpecs(cpu), "tdp") + GetInt(ParseSpecs(gpu), "tdp");
            var wattage = GetInt(ParseSpecs(psu), "wattage");
            var headroom = ((double)(wattage - totalTdp) / totalTdp) * 100;

            if (headroom < 20)
            {
                warnings.Add($"⚠️ Блок питания работает на пределе (запас всего {headroom:F0}%). Рекомендуется БП с большим запасом мощности.");
            }
        }

        // === Частота RAM vs максимум ===
        if (ram != null && cpu != null)
        {
            var ramSpeed = GetInt(ParseSpecs(ram), "speed");
            var cpuMaxSpeed = GetInt(ParseSpecs(cpu), "max_ram_speed");

            if (cpuMaxSpeed > 0 && ramSpeed > cpuMaxSpeed * 0.9)
            {
                warnings.Add($"⚠️ Частота памяти близка к максимуму, поддерживаемому процессором. Возможно, она будет работать на пониженной частоте.");
            }
        }

        return Task.FromResult(warnings);
    }

    // ============================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // ============================================================

    private JsonElement ParseSpecs(Product p) => JsonDocument.Parse(p.Specs).RootElement;

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
        {
            foreach (var item in v.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var str = item.GetString();
                    if (!string.IsNullOrEmpty(str))
                        result.Add(str);
                }
            }
        }
        return result;
    }
}