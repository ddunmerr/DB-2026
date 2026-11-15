using PcShop.Models.Entities;

namespace PcShop.Services;

public interface ICompatibilityService
{
    Task<List<Product>> GetCompatibleProductsAsync(int categoryId, IEnumerable<Product> currentBuild);
    Task<List<string>> ValidateBuildAsync(IEnumerable<Product> buildItems);
}