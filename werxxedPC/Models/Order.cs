using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace werxxedPC.Models
{
    [Table("order")]
    public class Order
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("position")]
        public string Position { get; set; } = null!;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer Customer { get; set; } = null!;

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}