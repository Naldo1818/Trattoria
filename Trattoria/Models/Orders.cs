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
    }
}
