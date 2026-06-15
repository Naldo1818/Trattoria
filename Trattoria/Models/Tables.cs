using System.ComponentModel.DataAnnotations;

namespace Trattoria.Models
{
    public class Tables
    {
        [Key]
        public int TableID { get; set; }
        public string Type { get; set; } 
        public int Capacity { get; set; }
        public bool IsAvailable { get; set; }
    }
}
