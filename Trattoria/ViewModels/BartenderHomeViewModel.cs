using System;
using System.Collections.Generic;
using System.Linq;

namespace Trattoria.ViewModels
{
    /// <summary>
    /// ViewModel for the Bartender dashboard.
    /// Shows drink orders from all active orders.
    /// </summary>
    public class BartenderHomeViewModel
    {
        // ── Stats ────────────────────────────────────────────────
        public int ActiveDrinkOrders { get; set; }
        public int MixingCount { get; set; }
        public int ReadyCount { get; set; }
        public int TotalTables { get; set; }

        // ── Collections ──────────────────────────────────────────
        public List<DrinkOrderDisplayItem> DrinkOrders { get; set; } = new();

        // ── Bartender info ──────────────────────────────────────
        public string BartenderName { get; set; } = string.Empty;
        public int BartenderID { get; set; }
    }

    /// <summary>
    /// One drink order display item.
    /// </summary>
    public class DrinkOrderDisplayItem
    {
        public int OrderID { get; set; }
        public int TableID { get; set; }
        public string TableType { get; set; } = string.Empty;
        public DateTime OrderTime { get; set; }
        public string Status { get; set; } = string.Empty; // Pending, In Progress, Ready, Served
        public List<DrinkLineItem> Items { get; set; } = new();

        // Display helpers
        public string TimeDisplay => OrderTime.ToString("HH:mm");
        public string StatusLabel => Status switch
        {
            "Pending" => "🆕 New",
            "In Progress" => "🥄 Mixing",
            "Ready" => "✅ Ready",
            "Served" => "🍸 Served",
            _ => Status
        };
        public string StatusCssClass => Status.ToLower().Replace(" ", "-");

        // Action availability
        public bool CanStartMixing => Status == "Pending";
        public bool CanMarkReady => Status == "In Progress";
        public bool CanServe => Status == "Ready";
        public bool CanCancel => Status != "Served";
    }

    /// <summary>
    /// One drink line item.
    /// </summary>
    public class DrinkLineItem
    {
        public int OrderDetailsID { get; set; }
        public int MenuItemID { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public bool IsDrink { get; set; } = true;
        public decimal Subtotal => Price * Quantity;
    }
}