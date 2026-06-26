# Trattoria Bella — Restaurant Management System

A role-based ASP.NET Core MVC web application for managing a restaurant's day-to-day operations. Each staff role gets their own dashboard: Admin, Receptionist, Waiter, Chef, and Bartender.


## Setup

### 1. Install packages
```bash
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
```

### 2. Configure connection string — `appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=TratториаDB;Trusted_Connection=True;"
  }
}
```

### 3. Register services — `Program.cs`
```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();          // must be after UseRouting, before MapControllerRoute
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

### 4. Run migrations
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 5. Seed an admin user
Insert one row into `Users` directly in SQL or via a seeder:
```sql
INSERT INTO Users (Name, Surname, Username, Password, Role)
VALUES ('Giovanni', 'Admin', 'admin', 'admin123', 'Admin');
```

---

## Login & Session

`POST /Home/Index` authenticates against the `Users` table and stores three session keys:

| Key | Value |
|---|---|
| `UserID` | `int` — the logged-in user's PK |
| `UserName` | `string` — "Name Surname" |
| `UserRole` | `string` — role string |

The controller then redirects to the correct home view based on `user.Role`.

---

## Role-Based Order Flow

```
Receptionist  →  adds Reservation  →  Table marked reserved
Waiter        →  seats guest        →  starts Food + Drinks Orders (both Pending)
Waiter        →  sends to kitchen   →  Food Order → In Progress
Chef          →  cooks              →  Food Order → Ready
Waiter        →  marks food served  →  Food Order → Served
Waiter        →  sends to bar       →  Drinks Order → In Progress
Bartender     →  prepares           →  Drinks Order → Ready
Waiter        →  marks drinks served→  Drinks Order → Served
Waiter        →  Close & Pay        →  Both Orders → Closed + IsPaid, Table freed
```

---

## Views

---

### `Index.cshtml` — Login

**Route:** `GET /` and `POST /`

The entry point for all users. A single form with Username and Password fields. On successful login the controller reads `user.Role` and redirects to the appropriate dashboard. No role-specific content is shown here.

---

### `AdminHome.cshtml` — Admin Dashboard

**Route:** `GET /Home/AdminHome?tab=overview|staff|menu|reports`

**ViewModel:** `AdminHomeViewModel`

The admin dashboard is divided into four tabs, all server-rendered. Switching tabs is a full page navigation via `asp-route-tab` — no JavaScript routing.

#### Tab: Overview
Displays today's reservations as a list with guest name, table, time, and status badge. Below that is a 7-day bar chart (pure CSS bars sized by percentage of max revenue) alongside three summary cards: Today's revenue, This Week's revenue, and average order value.

#### Tab: Staff
Left side shows all `Users` rows as cards with initials avatar, full name, username, and role badge. Each card has a **Remove** button (with confirmation prompt) that posts to `RemoveStaff`. The admin cannot remove their own account — the controller checks the session `UserID`.

Right side is an **Add Staff Member** form with fields for first name, surname, username, password, and a role dropdown populated from `AdminHomeViewModel.Roles`. Submits to `AddStaff`. The controller validates that the username is unique before inserting.

#### Tab: Menu
Shows the full `MenuItems` table with type badge, description, price, an **Edit** button (opens an inline modal pre-filled with the item's data), and a **Remove** button. Below the table is an **Add Menu Item** form with name, type/category, price, and description. The edit modal submits to `EditMenuItem`; the add form submits to `AddMenuItem`; remove submits to `RemoveMenuItem`.

#### Tab: Reports
Shows a 7-day income breakdown: a bar chart followed by a table with Date, Orders, Revenue, and Avg/Order columns including a totals row. Below that is a 6-month table grouped by `Orders.OrderTime` year+month showing the same columns. Revenue is calculated from `Orders.TotalAmount + Orders.TipAmount` where `IsPaid = true`. Five summary cards at the bottom show Today, This Week, This Month, Today's order count, and average per order.

**Controller actions:**
- `GET AdminHome(string tab)` — queries staff, menu, today's reservations, last 7 days of paid orders, last 6 months of paid orders
- `POST AddStaff` — creates a `Users` row; validates unique username
- `POST RemoveStaff` — deletes `Users` row; blocks self-deletion
- `POST AddMenuItem` — creates a `MenuItems` row
- `POST EditMenuItem` — updates an existing `MenuItems` row
- `POST RemoveMenuItem` — deletes a `MenuItems` row

---

### `ReceptionistHome.cshtml` — Receptionist Dashboard

**Route:** `GET /Home/ReceptionistHome`

**ViewModel:** `ReceptionistHomeViewModel` → `ReservationDisplayItem`

#### Layout
Two panels side by side: Reservations on the left (wider), Table Map on the right.

#### Stats row
Four stat cards: Total reservations, Seated count, Waiting/Confirmed count, Available tables + occupied count.

#### Reservations panel
Lists all `Reservations` rows sorted by `ReservationDate`. Each card shows the guest name, guest count + table ID, time, and a colour-coded status pill (Confirmed = green, Waiting = red, Seated = orange, Completed = grey). Action buttons appear based on status:
- **Seat** — visible when Confirmed or Waiting. Finds the assigned table (or the first available table if the assigned one is taken), sets `table.IsAvailable = false`, and sets `reservation.Status = "Seated"`.
- **Done** — visible when Seated. Sets status to Completed and frees the table.
- **✕** — visible for any non-Completed reservation. Sets status to Cancelled and frees the table.

#### Walk-in
A small form at the bottom with a guest count input and a **Walk-in guest** button. Posts to `WalkIn`, which creates a new `Reservations` row with `Status = "Seated"` on the first available table large enough for the guest count, and marks that table unavailable.

#### Table Map panel
A grid of tiles — one per table — coloured green (available) or orange (occupied) based on `Tables.IsAvailable`. Each tile shows the table number, type, and capacity.

#### Add Reservation modal
Triggered by **➕ Add reservation** button. A form with First Name, Surname, Phone, Email, Date & Time (datetime-local, pre-filled to now), Guest count, and a Table dropdown (only shows `IsAvailable = true` tables). Posts to `AddReservation`, which creates a `Reservations` row with `Status = "Confirmed"` and marks the table unavailable.

**Controller actions:**
- `GET ReceptionistHome` — LEFT JOINs `Reservations → Tables`, builds ViewModel
- `POST SeatReservation` — seats a guest; finds an available table if needed
- `POST CompleteReservation` — marks done, frees table
- `POST CancelReservation` — marks cancelled, frees table
- `POST AddReservation` — creates reservation, marks table reserved
- `POST WalkIn` — creates seated reservation on first available table

---

### `WaiterHome.cshtml` — Waiter Dashboard

**Route:** `GET /Home/WaiterHome?selectedTableID=N`

**ViewModel:** `WaiterHomeViewModel` → `TableDisplayItem` → `OrderDisplayItem` → `OrderLineItem`

#### Layout
Two columns: a table grid on the left (wider) and a sticky order panel on the right.

#### Table grid
All `Tables` rows rendered as clickable cards. Each card is an `<a>` tag pointing to `WaiterHome?selectedTableID=N` — clicking a table is a server navigation, not JavaScript. The selected card gets a gold ring border. Cards show table number, type, capacity, status badge (Available / Occupied / Reserved), and a red item-count badge in the top-right corner if the table has active order items.

#### Order panel — states
The panel renders differently based on the selected table's state:

**Available** — shows a "Start Order" button. Posting to `StartOrder` creates two `Orders` rows: one tagged `__FOOD` and one `__DRINKS`, and sets `table.IsAvailable = false`.

**Reserved, no order** — shows "Seat & Start Order" (same as above) and a Cancel button that clears the table.

**Active order** — the panel splits into two coloured sections:

- **🍽️ Food section** (warm gold header) — shows all `OrderDetails` lines whose `MenuItems.Type` is not a drink type. Displays each line with quantity, name, and subtotal. A ✕ remove button appears on each line while the order is still editable. Below the lines: subtotal row, then action buttons appropriate to the current status:
  - *Pending/Seated* → **🔥 Send to Kitchen** (posts `SendToStation`, sets `Status = "In Progress"`)
  - *In Progress* → no waiter action (chef controls this stage)
  - *Ready* → **✅ Food Served** (posts `UpdateOrderStatus` with `"Served"`)

- **🍹 Drinks section** (blue header) — same structure but for drink items:
  - *Pending/Seated* → **🍹 Send to Bar**
  - *Ready* → **✅ Drinks Served**

Below both sections: **Grand Total** (food + drinks combined) and **💳 Close & Pay** button. The pay button only appears when both orders are `"Served"`.

**Close & Pay modal** — shows grand total (read-only), payment method dropdown (Cash/Card/EFT), tip amount field. Posts to `CloseOrder(tableID, paymentMethod, tipAmount)` which marks all open orders on the table as `IsPaid = true` and `Status = "Closed"`, and sets `table.IsAvailable = true`.

#### Add Items modals
Two separate modals — **Add Food** and **Add Drinks** — each listing only the relevant menu items grouped by type. Each row has a `−`/`+` quantity counter (JavaScript, client-side only) and an **Add** button that posts to `AddOrderItem`. The modal stamps the correct `orderID` into every hidden input when it opens.

**Controller actions:**
- `GET WaiterHome(selectedTableID)` — queries all active orders, groups Food/Drinks by `__` tag + line type classification
- `POST StartOrder` — creates two `Orders` rows (`__FOOD` + `__DRINKS`)
- `POST AddOrderItem` — adds/increments `OrderDetails` line, recalculates `TotalAmount`
- `POST RemoveOrderItem` — removes `OrderDetails` line, recalculates total
- `POST SendToStation` — sets `Status = "In Progress"`
- `POST UpdateOrderStatus` — generic status update (used for Served)
- `POST CloseOrder` — closes all orders on the table, frees table
- `POST ClearTable` — voids all open orders, frees table

---

### `ChefsHome.cshtml` — Chef / Kitchen Dashboard

**Route:** `GET /Home/ChefsHome`

**ViewModel:** `ChefsHomeViewModel` → `KitchenOrderItem` → `KitchenLineItem`

#### Layout
Full-width — no sidebar. The order cards fill a responsive two-column grid (single column on small screens).

#### Stats row
Five cards: Active Orders, New/Pending, Cooking, Ready to Serve, Items in Prep (sum of all quantities on non-Ready orders).

#### Order cards
Each card represents one `Orders` row that is not paid and not closed and not Served. The card's left border is colour-coded by status:
- Gold border — Pending or Seated (new order, not yet started)
- Orange border — In Progress (cooking)
- Teal border — Ready (plated, waiting for collection)

The card shows: Table ID + order number + table type, order time, status badge, and all item pills (quantity × name + type label).

Action buttons:
- **🔪 Start Cooking** — visible when Pending or Seated. Posts to `StartCooking`, sets `Status = "In Progress"`.
- **🔔 Mark Ready** — visible when In Progress. Posts to `MarkOrderReady`, sets `Status = "Ready"`.
- **🍽️ Collected** — visible when Ready. Posts to `MarkOrderServed`, sets `Status = "Served"` (the waiter then confirms delivery on their dashboard).
- **✕ Cancel** — visible on Pending/In Progress cards. Posts to `CancelKitchenOrder`, sets `Status = "Closed"` and frees the table if no other active orders exist.

> The chef only sees **Food** orders. Drinks orders (tagged `__DRINKS`) are routed to the Bartender dashboard. Classification is by the `__DRINKS` payment tag and by whether all line items are drink types.

**Controller actions:**
- `GET ChefsHome` — queries `Orders` where `!IsPaid && Status != "Closed" && Status != "Served"`, excludes drinks-only orders, LEFT JOINs lines
- `POST StartCooking(orderID)` — `Status = "In Progress"`
- `POST MarkOrderReady(orderID)` — `Status = "Ready"`
- `POST MarkOrderServed(orderID)` — `Status = "Served"`
- `POST CancelKitchenOrder(orderID)` — `Status = "Closed"`, conditionally frees table

---

### `BartenderHome.cshtml` — Bartender Dashboard

**Route:** `GET /Home/BartenderHome`

This view mirrors the Chef dashboard but filters for drink orders only (orders tagged `__DRINKS` or whose lines are all drink types). The status flow and action buttons are identical to the chef's:
- **🍹 Start Preparing** → In Progress
- **🔔 Mark Ready** → Ready
- **✅ Collected** → Served
- **✕ Cancel** → Closed

The bartender sees only drink items on each order card, so they know exactly what to prepare without food items cluttering the view.

---

## Order Status Flow (complete)

```
Receptionist creates reservation
        ↓
Table: IsAvailable = false

Waiter starts order
        ↓
Orders row (Food):   Status = "Pending",  PaymentMethod = "__FOOD"
Orders row (Drinks): Status = "Pending",  PaymentMethod = "__DRINKS"

Waiter adds items → OrderDetails rows inserted

Waiter sends to kitchen
        ↓
Food Order: Status = "In Progress"   ← Chef sees this

Chef starts cooking
        ↓
Food Order: Status = "In Progress"

Chef marks ready
        ↓
Food Order: Status = "Ready"         ← Waiter sees Ready badge

Waiter marks food served
        ↓
Food Order: Status = "Served"

[Same flow for Drinks with Bartender]

Both orders Served
        ↓
Waiter: Close & Pay unlocks

Waiter closes order
        ↓
Food Order:   IsPaid = true, Status = "Closed", PaymentMethod = "Cash/Card/EFT"
Drinks Order: IsPaid = true, Status = "Closed", PaymentMethod = "Cash/Card/EFT"
Table: IsAvailable = true
```

---

## Common Patterns Across All Views

**Anti-forgery tokens** — every `<form method="post">` includes `@Html.AntiForgeryToken()` and every POST action has `[ValidateAntiForgeryToken]`.

**Toast notifications** — all POST actions set `TempData["ToastMessage"]` before redirecting. Each view reads this on `DOMContentLoaded` and shows a slide-in toast bar for 3 seconds.

**Session usage** — `UserID`, `UserName`, and `UserRole` are stored in session on login and read by each controller action.

**Post-Redirect-Get** — every POST action redirects back to a GET after saving, preventing duplicate form submissions on browser refresh.

**No JavaScript navigation** — table selection (Waiter), tab switching (Admin), and page refresh (all views) use standard anchor tags with route parameters. JavaScript is only used for modals and the client-side quantity counter.
