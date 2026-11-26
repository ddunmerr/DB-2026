using PcShop.Models.Entities;

namespace PcShop.Services;

public interface ICompatibilityService
{
    /// <summary>
    /// Возвращает товары указанной категории, совместимые с уже выбранными компонентами.
    /// </summary>
    Task<List<Product>> GetCompatibleProductsAsync(int categoryId, IEnumerable<Product> currentBuild);

    /// <summary>
    /// Финальная валидация сборки. Возвращает список критических ошибок (блокирующих заказ).
    /// </summary>
    Task<List<string>> ValidateBuildAsync(IEnumerable<Product> buildItems);

    /// <summary>
    /// Возвращает список предупреждений о "узких горлышках" и неоптимальных конфигурациях.
    /// Не блокирует заказ, но информирует пользователя.
    /// </summary>
    Task<List<string>> GetBottleneckWarningsAsync(IEnumerable<Product> buildItems);
}