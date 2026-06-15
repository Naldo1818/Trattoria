using System.ComponentModel.DataAnnotations;

namespace Trattoria.Models
{
    public class OrderDetails
    {
        [Key]
        public int OrderDetailsID { get; set; }

        public int OrderID { get; set; }

        public int MenuItemID { get; set; }

        public int Quantity { get; set; }

        public decimal Price { get; set; }
    }
}