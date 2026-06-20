using Trattoria.Models;

namespace Trattoria.ViewModels
{
    /// <summary>
    /// ViewModel for the Waiter dashboard.
    /// Aggregates Tables, Orders, OrderDetails, and MenuItems.
    /// </summary>
    public class WaiterHomeViewModel
    {
        // ── Stats ────────────────────────────────────────────────
        public int TotalTables { get; set; }
        public int OccupiedCount { get; set; }
        public int AvailableCount { get; set; }
        public int ReservedCount { get; set; }

        // ── Collections ──────────────────────────────────────────
        public List<TableDisplayItem> Tables { get; set; } = new();
        public List<MenuItems> MenuItems { get; set; } = new();

        // ── Selected table (when one is tapped) ─────────────────
        public int? SelectedTableID { get; set; }
        public TableDisplayItem? SelectedTable => Tables.FirstOrDefault(t => t.TableID == SelectedTableID);

        // ── Waiter info ──────────────────────────────────────────
        public string WaiterName { get; set; } = string.Empty;
        public int WaiterID { get; set; }
    }

    /// <summary>
    /// One table card: joins Tables + active Order + OrderDetails + MenuItems.
    /// </summary>
    public class TableDisplayItem
    {
        // From Tables
        public int TableID { get; set; }
        public string TableType { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public bool IsAvailable { get; set; }
        public string? CustomStatus { get; set; } // For "Seated" status

        // Derived status label
        public string Status
        {
            get
            {
                // If custom status is set (like "Seated"), use it
                if (!string.IsNullOrEmpty(CustomStatus))
                    return CustomStatus;

                // Otherwise derive from availability and order
                return IsAvailable ? "available" : (ActiveOrder != null ? "occupied" : "reserved");
            }
        }

        // Active (unpaid) order on this table, if any
        public OrderDisplayItem? ActiveOrder { get; set; }

        // Helpers
        public bool HasOrder => ActiveOrder != null && ActiveOrder.Lines.Any();
        public int TotalItems => ActiveOrder?.Lines.Sum(l => l.Quantity) ?? 0;
    }

    /// <summary>
    /// One active order with its line items.
    /// </summary>
    public class OrderDisplayItem
    {
        public int OrderID { get; set; }
        public DateTime OrderTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;

        public List<OrderLineItem> Lines { get; set; } = new();

        // Recalculate total from lines (source of truth)
        public decimal ComputedTotal => Lines.Sum(l => l.Subtotal);

        // Status helpers
        public bool CanAddItems => Status is "Pending" or "In Progress";
        public bool CanMarkReady => Status == "Pending";
        public bool CanServe => Status == "Ready";
        public bool CanClose => Status == "Served" && !IsPaid;

        public string StatusCssClass => Status.ToLower().Replace(" ", "-");
        public string StatusLabel => Status switch
        {
            "Pending" => "⏳ Pending",
            "In Progress" => "🔥 In Progress",
            "Ready" => "🔔 Ready",
            "Served" => "✅ Served",
            "Closed" => "✓ Closed",
            _ => Status
        };
    }

    /// <summary>
    /// One line on an order: joins OrderDetails + MenuItems.
    /// </summary>
    public class OrderLineItem
    {
        public int OrderDetailsID { get; set; }
        public int MenuItemID { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Subtotal => Price * Quantity;
    }
}