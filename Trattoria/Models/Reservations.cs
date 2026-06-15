using System.ComponentModel.DataAnnotations;

namespace Trattoria.Models
{
    public class Reservations
    {
        [Key]
        public int ReservationsID { get; set; }
        public int UserID { get; set; }
        public int TablesID { get; set; } 
        public string Name { get; set; }
        public string Surname { get; set; }
        public string ContactPhone { get; set; }
        public string Email { get; set; }
        public DateTime ReservationDate { get; set; }
        public int Capacity { get; set; }
        public string Status { get; set; }
    }
}
