using Trattoria.Models;

namespace Trattoria.ViewModels
{
    /// <summary>
    /// ViewModel for the Waiter dashboard.
    /// Each table now has TWO active orders: one Food, one Drinks.
    /// Order type is derived from MenuItems.Type:
    ///   - "Drink" / "Beverage" / "Cocktail" / "Wine" / "Beer" → Drinks order
    ///   - Everything else                                      → Food order
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

        // Menu split by category for the Add Items modal
        public List<MenuItems> FoodItems { get; set; } = new();
        public List<MenuItems> DrinkItems { get; set; } = new();

        // Grouped food items for modal sections
        public IEnumerable<IGrouping<string, MenuItems>> FoodGroups =>
            FoodItems.GroupBy(m => m.Type).OrderBy(g => g.Key);
        public IEnumerable<IGrouping<string, MenuItems>> DrinkGroups =>
            DrinkItems.GroupBy(m => m.Type).OrderBy(g => g.Key);

        // ── Selected table ───────────────────────────────────────
        public int? SelectedTableID { get; set; }
        public TableDisplayItem? SelectedTable =>
            Tables.FirstOrDefault(t => t.TableID == SelectedTableID);

        // ── Waiter info ──────────────────────────────────────────
        public string WaiterName { get; set; } = string.Empty;
        public int WaiterID { get; set; }

        // ── Helper: is a MenuItems type considered a drink? ──────
        public static bool IsDrinkType(string type) =>
            type != null &&
            (type.Contains("Drink", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("Beverage", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("Cocktail", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("Wine", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("Beer", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("Spirit", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("Juice", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("Coffee", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("Tea", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// One table card. Now carries separate FoodOrder and DrinksOrder.
    /// </summary>
    public class TableDisplayItem
    {
        public int TableID { get; set; }
        public string TableType { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public bool IsAvailable { get; set; }

        public string Status => IsAvailable ? "available"
                              : (FoodOrder != null || DrinksOrder != null) ? "occupied"
                              : "reserved";

        // The two separate orders
        public OrderDisplayItem? FoodOrder { get; set; }
        public OrderDisplayItem? DrinksOrder { get; set; }

        // Badge helpers
        public bool HasAnyOrder => (FoodOrder?.Lines.Any() ?? false) || (DrinksOrder?.Lines.Any() ?? false);
        public int TotalItems => (FoodOrder?.Lines.Sum(l => l.Quantity) ?? 0)
                                  + (DrinksOrder?.Lines.Sum(l => l.Quantity) ?? 0);

        // Can close only when both orders are served (or absent)
        public bool CanClose =>
            (FoodOrder == null || FoodOrder.Status == "Served") &&
            (DrinksOrder == null || DrinksOrder.Status == "Served") &&
            (FoodOrder != null || DrinksOrder != null);
    }

    /// <summary>
    /// One order (Food or Drinks) with its line items.
    /// Status flow: Pending / Seated → In Progress → Ready → Served
    /// </summary>
    public class OrderDisplayItem
    {
        public int OrderID { get; set; }
        public DateTime OrderTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsPaid { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string OrderCategory { get; set; } = string.Empty; // "Food" or "Drinks"

        public List<OrderLineItem> Lines { get; set; } = new();

        public decimal ComputedTotal => Lines.Sum(l => l.Subtotal);

        // Status helpers
        public bool CanAddItems => Status is "Pending" or "Seated" or "In Progress";
        public bool CanSendToStation => Status is "Pending" or "Seated";
        public bool CanMarkServed => Status == "Ready";
        public bool IsServedOrAbsent => Status == "Served";

        public string StatusCssClass => Status.ToLower().Replace(" ", "-");

        public string StatusLabel => Status switch
        {
            "Pending" => "⏳ Pending",
            "Seated" => "🪑 Seated",
            "In Progress" => OrderCategory == "Drinks" ? "🍹 At Bar" : "🔥 In Kitchen",
            "Ready" => OrderCategory == "Drinks" ? "🍸 Drinks Ready" : "🔔 Food Ready",
            "Served" => "✅ Served",
            "Closed" => "✓ Closed",
            _ => Status
        };

        public string SendLabel => OrderCategory == "Drinks" ? "🍹 Send to Bar" : "🔥 Send to Kitchen";
    }

    /// <summary>One line item: OrderDetails + MenuItems joined.</summary>
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