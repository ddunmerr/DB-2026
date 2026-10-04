using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace werxxedPC.Models
{
    [Table("customer")]
    public class Customer
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("email")]
        public string Email { get; set; } = null!;

        [Column("username")]
        public string? Username { get; set; }

        [Column("pass")]
        public string Pass { get; set; } = null!;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public Cart? Cart { get; set; }
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}