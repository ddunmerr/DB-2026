using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace werxxedPC.Models
{
    [Table("category")]
    public class Category
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        public string Title { get; set; } = null!;

        [Column("slug")]
        public string Slug { get; set; } = null!;

        [Column("parent_id")]
        public int? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        public Category? Parent { get; set; }

        public ICollection<Category> Children { get; set; } = new List<Category>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}