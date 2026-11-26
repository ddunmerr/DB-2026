using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PcShop.Models.ViewModels;

public class ProductEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Название обязательно")]
    [StringLength(255, ErrorMessage = "Максимум 255 символов")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Бренд обязателен")]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = "Цена обязательна")]
    [Range(1, 1000000, ErrorMessage = "Цена должна быть от 1 до 1 000 000")]
    [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:F2}")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Остаток обязателен")]
    [Range(0, 10000, ErrorMessage = "Остаток не может быть отрицательным")]
    public int Stock { get; set; }

    public int CategoryId { get; set; }
    public string? CurrentImageUrl { get; set; }

    // Поле для загрузки файла (не сохраняется в БД напрямую)
    [Display(Name = "Изображение товара")]
    public IFormFile? ImageFile { get; set; }

    [Required(ErrorMessage = "Характеристики (JSON) обязательны")]
    [Display(Name = "Характеристики (JSON)")]
    public string Specs { get; set; } = "{}";

    [Display(Name = "Описание")]
    public string? Description { get; set; }
}