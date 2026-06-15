using System.ComponentModel.DataAnnotations;

namespace Trattoria.Models
{
    public class Orders
    {
        [Key]
        public int OrderID { get; set; }

        public int UserID { get; set; }

        public int TablesID { get; set; }

        public DateTime OrderTime { get; set; }

        public string Status { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal TipAmount { get; set; }

        public string PaymentMethod { get; set; } // Cash, Card, EFT

        public bool IsPaid { get; set; }
    }
}

