using System.ComponentModel.DataAnnotations;

namespace Trattoria.Models
{
    public class MenuItems
    {
        [Key]
        public int MenuItemID { get; set; }
        public string Type { get; set; } 
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
    }
}
