using PcShop.Models.Entities;
using PcShop.Models.ViewModels;

namespace PcShop.Services;

public interface ICompatibilityService
{
    /// <summary>
    /// Возвращает ВСЕ товары категории с информацией о совместимости с текущей сборкой.
    /// </summary>
    Task<List<ProductWithCompatibilityInfo>> GetProductsWithCompatibilityInfoAsync(
        int categoryId, IEnumerable<Product> currentBuild);

    /// <summary>
    /// Причина несовместимости товара с текущей сборкой или null, если совместим.
    /// </summary>
    Task<string?> GetIncompatibilityReasonAsync(Product candidate, IEnumerable<Product> currentBuild);
    
    /// <summary>
    /// Финальная валидация сборки. Возвращает список критических ошибок.
    /// </summary>
    Task<List<string>> ValidateBuildAsync(IEnumerable<Product> buildItems);

    /// <summary>
    /// Возвращает список предупреждений о "узких горлышках".
    /// </summary>
    Task<List<string>> GetBottleneckWarningsAsync(IEnumerable<Product> buildItems);
}