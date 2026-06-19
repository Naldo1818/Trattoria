using Trattoria.Models;

namespace Trattoria.ViewModels
{
    /// <summary>
    /// ViewModel for the Receptionist dashboard.
    /// Aggregates data from Tables, Reservations, and Users models.
    /// </summary>
    public class ReceptionistHomeViewModel
    {
        // ── Stats ────────────────────────────────────────────────
        public int TotalReservations { get; set; }
        public int SeatedCount { get; set; }
        public int WaitingOrConfirmedCount { get; set; }
        public int AvailableTablesCount { get; set; }
        public int OccupiedTablesCount { get; set; }

        // ── Collections ──────────────────────────────────────────
        /// <summary>All reservations, sorted by ReservationDate ascending.</summary>
        public List<ReservationDisplayItem> Reservations { get; set; } = new();

        /// <summary>All tables with their current availability.</summary>
        public List<Tables> Tables { get; set; } = new();

        // ── Receptionist info ────────────────────────────────────
        public string ReceptionistName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Flattened display row: joins Reservations + Tables + Users data.
    /// </summary>
    public class ReservationDisplayItem
    {
        // From Reservations model
        public int ReservationID { get; set; }
        public string GuestName { get; set; } = string.Empty;      // Name + Surname
        public string ContactPhone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime ReservationDate { get; set; }
        public int GuestCapacity { get; set; }
        public string Status { get; set; } = string.Empty;         // "Confirmed" | "Seated" | "Waiting" | "Completed" | "Cancelled"

        // From Tables model (via TablesID FK)
        public int TableID { get; set; }
        public string TableType { get; set; } = string.Empty;
        public int TableCapacity { get; set; }
        public bool TableIsAvailable { get; set; }

        // ── Helpers used in the View ─────────────────────────────
        public string StatusCssClass => Status.ToLower() switch
        {
            "confirmed" => "confirmed",
            "seated" => "seated",
            "waiting" => "waiting",
            "completed" => "completed",
            "cancelled" => "completed",   // re-use muted style
            _ => "confirmed"
        };

        public string StatusLabel => Status.ToLower() switch
        {
            "confirmed" => "✅ Confirmed",
            "seated" => "🪑 Seated",
            "waiting" => "⏳ Waiting",
            "completed" => "✓ Done",
            "cancelled" => "✕ Cancelled",
            _ => Status
        };

        public bool CanSeat => Status is "Confirmed" or "Waiting";
        public bool CanComplete => Status == "Seated";
        public bool CanCancel => Status is not ("Completed" or "Cancelled");
    }
}