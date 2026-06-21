using Trattoria.Models;

namespace Trattoria.ViewModels
{
    /// <summary>
    /// ViewModel for the Chef kitchen dashboard.
    /// Shows active Orders with their items — no Mis en Place panel.
    /// </summary>
    public class ChefsHomeViewModel
    {
        // ── Stats ────────────────────────────────────────────────
        public int ActiveOrdersCount { get; set; }
        public int PendingCount { get; set; }
        public int CookingCount { get; set; }
        public int ReadyCount { get; set; }
        public int TotalItemsInPrep { get; set; }

        // ── Orders ───────────────────────────────────────────────
        public List<KitchenOrderItem> Orders { get; set; } = new();

        // ── Chef info ────────────────────────────────────────────
        public string ChefName { get; set; } = string.Empty;
    }

    /// <summary>
    /// One kitchen order card: joins Orders + OrderDetails + MenuItems + Tables.
    /// </summary>
    public class KitchenOrderItem
    {
        // From Orders
        public int OrderID { get; set; }
        public int TableID { get; set; }
        public string TableType { get; set; } = string.Empty;
        public DateTime OrderTime { get; set; }
        public string Status { get; set; } = string.Empty;

        // Line items
        public List<KitchenLineItem> Lines { get; set; } = new();

        public int TotalQty => Lines.Sum(l => l.Quantity);

        // Status helpers
        public string StatusCssClass => Status.ToLower().Replace(" ", "-");

        public string StatusLabel => Status switch
        {
            "Pending" => "🆕 New",
            "Seated" => "🆕 New",
            "In Progress" => "🔥 Cooking",
            "Ready" => "✅ Ready",
            _ => Status
        };

        public bool CanStartCooking => Status is "Pending" or "Seated";
        public bool CanMarkReady => Status == "In Progress";
        public bool CanMarkServed => Status == "Ready";
    }

    /// <summary>
    /// One line item on a kitchen order: joins OrderDetails + MenuItems.
    /// </summary>
    public class KitchenLineItem
    {
        public int OrderDetailsID { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}