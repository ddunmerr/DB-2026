using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace werxxedPC.Models
{
    [Table("product")]
    public class Product
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        public string Title { get; set; } = null!;

        [Column("brand")]
        public string? Brand { get; set; }

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("price")]
        public decimal Price { get; set; }

        [Column("stock")]
        public int Stock { get; set; }

        [Column("specs")]
        public string Specs { get; set; } = null!;

        [Column("category_id")]
        public int CategoryId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public Category Category { get; set; } = null!;
    }
}