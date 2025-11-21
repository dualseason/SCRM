using System.ComponentModel.DataAnnotations;

namespace SCRM.Entities
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ProductName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Category { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public int Quantity { get; set; } = 1;

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [MaxLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public DateTime? ShippingDate { get; set; }

        public DateTime? DeliveryDate { get; set; }

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;

        public bool IsDeleted { get; set; } = false;
    }
}