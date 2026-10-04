using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace werxxedPC.Models
{
    [Table("cart_item")]
    public class CartItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("cart_id")]
        public int CartId { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [ForeignKey(nameof(CartId))]
        public Cart Cart { get; set; } = null!;

        [ForeignKey(nameof(ProductId))]
        public Product Product { get; set; } = null!;
    }
}