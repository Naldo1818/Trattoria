using Trattoria.Models;

namespace Trattoria.ViewModels
{
    /// <summary>
    /// ViewModel for the Admin dashboard.
    /// Covers Staff management, Menu management, Reservations overview, and Sales reports.
    /// </summary>
    public class AdminHomeViewModel
    {
        // ── Stats ────────────────────────────────────────────────
        public int TotalStaff { get; set; }
        public int ActiveStaff { get; set; }
        public int TotalMenuItems { get; set; }
        public int TodayReservations { get; set; }
        public decimal TodayRevenue { get; set; }
        public int TodayOrderCount { get; set; }

        // ── Staff ────────────────────────────────────────────────
        public List<Users> Staff { get; set; } = new();
        public List<string> Roles { get; set; } = new() { "Admin", "Waiter", "Chefs", "Bartender", "Receptionist" };

        // ── Menu ─────────────────────────────────────────────────
        public List<MenuItems> MenuItems { get; set; } = new();
        public IEnumerable<IGrouping<string, MenuItems>> MenuGroups =>
            MenuItems.GroupBy(m => m.Type).OrderBy(g => g.Key);

        // ── Today's reservations ─────────────────────────────────
        public List<ReservationDisplayItem> TodaysReservations { get; set; } = new();

        // ── Sales report ─────────────────────────────────────────
        public List<DailySalesItem> DailySales { get; set; } = new();   // last 7 days
        public List<MonthlySalesItem> MonthlySales { get; set; } = new();   // last 6 months

        public decimal WeekRevenue => DailySales.Sum(d => d.Revenue);
        public decimal MonthRevenue => MonthlySales.FirstOrDefault()?.Revenue ?? 0;

        // ── Admin info ───────────────────────────────────────────
        public string AdminName { get; set; } = string.Empty;

        // ── Active tab for UI state ──────────────────────────────
        public string ActiveTab { get; set; } = "overview"; // overview | staff | menu | reports
    }

    public class DailySalesItem
    {
        public DateTime Date { get; set; }
        public string Label => Date.Date == DateTime.Today ? "Today"
                                    : Date.Date == DateTime.Today.AddDays(-1) ? "Yesterday"
                                    : Date.ToString("ddd d MMM");
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public decimal AvgOrder => OrderCount > 0 ? Math.Round(Revenue / OrderCount, 2) : 0;
    }

    public class MonthlySalesItem
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Label => new DateTime(Year, Month, 1).ToString("MMM yyyy");
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public decimal AvgOrder => OrderCount > 0 ? Math.Round(Revenue / OrderCount, 2) : 0;
    }
}