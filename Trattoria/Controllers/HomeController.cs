using Trattoria.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Trattoria.Models;
using Trattoria.Data;
using Microsoft.EntityFrameworkCore;

namespace Trattoria.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HomeController(ApplicationDbContext dbContext, IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
        }

        // ── Login ────────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(IndexViewModel index)
        {
            if (!ModelState.IsValid)
                return View(index);

            var user = _dbContext.Users
                .SingleOrDefault(u => u.Username == index.Username && u.Password == index.Password);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(index);
            }

            // Store minimal session data (UserID + Name) so downstream views can greet the user.
            HttpContext.Session.SetInt32("UserID", user.UserID);
            HttpContext.Session.SetString("UserName", $"{user.Name} {user.Surname}");
            HttpContext.Session.SetString("UserRole", user.Role);

            return user.Role switch
            {
                "Admin" => RedirectToAction("AdminHome", new { userID = user.UserID }),
                "Waiter" => RedirectToAction("WaiterHome"),
                "Chefs" => RedirectToAction("ChefsHome"),
                "Bartender" => RedirectToAction("BartenderHome"),
                "Receptionist" => RedirectToAction("ReceptionistHome"),
                _ => View(index)
            };
        }

        // ── Admin ────────────────────────────────────────────────────────────────

        public IActionResult AdminHome(int userID = 0)
        {
            return View();
        }

        // ── Chef ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// GET: Kitchen dashboard — all active (unpaid, non-closed) orders with their items.
        /// </summary>
        [HttpGet]
        public IActionResult ChefsHome()
        {
            var chefName = HttpContext.Session.GetString("UserName") ?? "Chef";

            // All active kitchen orders (not paid, not closed, not yet served)
            var kitchenOrders = (
                from o in _dbContext.Orders
                where !o.IsPaid && o.Status != "Closed" && o.Status != "Served"
                join t in _dbContext.Tables on o.TablesID equals t.TableID into tj
                from t in tj.DefaultIfEmpty()
                orderby o.OrderTime
                select new { o, t }
            ).ToList();

            var orderIDs = kitchenOrders.Select(x => x.o.OrderID).ToList();

            var allLines = (
                from od in _dbContext.OrderDetails
                where orderIDs.Contains(od.OrderID)
                join m in _dbContext.MenuItems on od.MenuItemID equals m.MenuItemID
                select new { od, m }
            ).ToList();

            var linesByOrder = allLines
                .GroupBy(x => x.od.OrderID)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new KitchenLineItem
                    {
                        OrderDetailsID = x.od.OrderDetailsID,
                        ItemName = x.m.Name,
                        ItemType = x.m.Type,
                        Quantity = x.od.Quantity
                    }).ToList()
                );

            var orders = kitchenOrders.Select(x => new KitchenOrderItem
            {
                OrderID = x.o.OrderID,
                TableID = x.o.TablesID,
                TableType = x.t != null ? x.t.Type : "—",
                OrderTime = x.o.OrderTime,
                Status = x.o.Status,
                Lines = linesByOrder.TryGetValue(x.o.OrderID, out var lines) ? lines : new()
            }).ToList();

            var vm = new ChefsHomeViewModel
            {
                Orders = orders,
                ActiveOrdersCount = orders.Count,
                PendingCount = orders.Count(o => o.CanStartCooking),
                CookingCount = orders.Count(o => o.Status == "In Progress"),
                ReadyCount = orders.Count(o => o.Status == "Ready"),
                TotalItemsInPrep = orders.Where(o => o.Status != "Ready").Sum(o => o.TotalQty),
                ChefName = chefName
            };

            return View(vm);
        }

        /// <summary>
        /// POST: Chef moves an order to "In Progress" (start cooking).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult StartCooking(int orderID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null) return NotFound();
            order.Status = "In Progress";
            _dbContext.SaveChanges();
            TempData["ToastMessage"] = $"🔪 Order #{orderID} — cooking started.";
            return RedirectToAction("ChefsHome");
        }

        /// <summary>
        /// POST: Chef marks an order as "Ready" (plated, ready to collect).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkOrderReady(int orderID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null) return NotFound();
            order.Status = "Ready";
            _dbContext.SaveChanges();
            TempData["ToastMessage"] = $"✅ Order #{orderID} is ready to serve!";
            return RedirectToAction("ChefsHome");
        }

        /// <summary>
        /// POST: Chef marks a ready order as collected by the waiter.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkOrderServed(int orderID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null) return NotFound();
            order.Status = "Served";
            _dbContext.SaveChanges();
            TempData["ToastMessage"] = $"🍽️ Order #{orderID} collected by waiter.";
            return RedirectToAction("ChefsHome");
        }

        /// <summary>
        /// POST: Chef voids/cancels an order from the kitchen.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelKitchenOrder(int orderID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null) return NotFound();
            order.Status = "Closed";
            var otherActive = _dbContext.Orders
                .Any(o => o.TablesID == order.TablesID && o.OrderID != orderID
                          && !o.IsPaid && o.Status != "Closed");
            if (!otherActive)
            {
                var table = _dbContext.Tables.Find(order.TablesID);
                if (table != null) table.IsAvailable = true;
            }
            _dbContext.SaveChanges();
            TempData["ToastMessage"] = $"✕ Order #{orderID} cancelled.";
            return RedirectToAction("ChefsHome");
        }


        // ── Bartender ────────────────────────────────────────────────────────────

        /// <summary>
        /// GET: Bartender dashboard — shows all drink orders from active orders.
        /// </summary>
        [HttpGet]
        public IActionResult BartenderHome()
        {
            var bartenderName = HttpContext.Session.GetString("UserName") ?? "Elena";
            var bartenderID = HttpContext.Session.GetInt32("UserID") ?? 0;

            // Get all active (unpaid, not closed) orders
            var activeOrders = _dbContext.Orders
                .Where(o => !o.IsPaid && o.Status != "Closed")
                .ToList();

            // Get all order details for these orders
            var orderIds = activeOrders.Select(o => o.OrderID).ToList();

            // Join OrderDetails with MenuItems manually
            var orderDetailsWithMenu = _dbContext.OrderDetails
                .Where(od => orderIds.Contains(od.OrderID))
                .Join(_dbContext.MenuItems,
                      od => od.MenuItemID,
                      m => m.MenuItemID,
                      (od, m) => new { OrderDetail = od, MenuItem = m })
                .ToList();

            // Get all tables
            var tables = _dbContext.Tables.ToDictionary(t => t.TableID, t => t);

            // Filter orders that have drink items (Wine & Drinks category)
            var drinkOrders = new List<DrinkOrderDisplayItem>();

            foreach (var order in activeOrders)
            {
                var drinkItems = orderDetailsWithMenu
                    .Where(x => x.OrderDetail.OrderID == order.OrderID &&
                                x.MenuItem.Type == "Wine & Drinks")
                    .Select(x => new DrinkLineItem
                    {
                        OrderDetailsID = x.OrderDetail.OrderDetailsID,
                        MenuItemID = x.OrderDetail.MenuItemID,
                        ItemName = x.MenuItem.Name,
                        Quantity = x.OrderDetail.Quantity,
                        Price = x.OrderDetail.Price,
                        IsDrink = true
                    })
                    .ToList();

                if (drinkItems.Any())
                {
                    var table = tables.GetValueOrDefault(order.TablesID);
                    drinkOrders.Add(new DrinkOrderDisplayItem
                    {
                        OrderID = order.OrderID,
                        TableID = order.TablesID,
                        TableType = table?.Type ?? "Standard",
                        OrderTime = order.OrderTime,
                        Status = order.Status,
                        Items = drinkItems
                    });
                }
            }

            // Order by status priority: Pending first, then In Progress, then Ready
            var orderedDrinkOrders = drinkOrders
                .OrderBy(o => o.Status == "Pending" ? 0 :
                             o.Status == "In Progress" ? 1 :
                             o.Status == "Ready" ? 2 : 3)
                .ThenBy(o => o.OrderTime)
                .ToList();

            var vm = new BartenderHomeViewModel
            {
                DrinkOrders = orderedDrinkOrders,
                ActiveDrinkOrders = orderedDrinkOrders.Count,
                MixingCount = orderedDrinkOrders.Count(o => o.Status == "In Progress"),
                ReadyCount = orderedDrinkOrders.Count(o => o.Status == "Ready"),
                TotalTables = _dbContext.Tables.Count(),
                BartenderName = bartenderName,
                BartenderID = bartenderID
            };

            return View(vm);
        }

        /// <summary>
        /// POST: Start mixing a drink order (Pending → In Progress)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult StartMixingDrinkOrder(int orderID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null)
            {
                TempData["ToastMessage"] = "❌ Order not found.";
                return RedirectToAction("BartenderHome");
            }

            if (order.Status != "Pending")
            {
                TempData["ToastMessage"] = "⚠️ Order is not in Pending status.";
                return RedirectToAction("BartenderHome");
            }

            order.Status = "In Progress";
            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"🥄 Order #{orderID} is now mixing.";
            return RedirectToAction("BartenderHome");
        }

        /// <summary>
        /// POST: Mark drink order as ready (In Progress → Ready)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkDrinkOrderReady(int orderID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null)
            {
                TempData["ToastMessage"] = "❌ Order not found.";
                return RedirectToAction("BartenderHome");
            }

            if (order.Status != "In Progress")
            {
                TempData["ToastMessage"] = "⚠️ Order is not in In Progress status.";
                return RedirectToAction("BartenderHome");
            }

            order.Status = "Ready";
            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"✅ Order #{orderID} is ready to serve!";
            return RedirectToAction("BartenderHome");
        }

        /// <summary>
        /// POST: Serve and complete a drink order (Ready → Served)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ServeDrinkOrder(int orderID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null)
            {
                TempData["ToastMessage"] = "❌ Order not found.";
                return RedirectToAction("BartenderHome");
            }

            if (order.Status != "Ready")
            {
                TempData["ToastMessage"] = "⚠️ Order is not ready to serve.";
                return RedirectToAction("BartenderHome");
            }

            order.Status = "Served";
            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"🍸 Order #{orderID} has been served.";
            return RedirectToAction("BartenderHome");
        }

        /// <summary>
        /// POST: Cancel a drink order
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelDrinkOrder(int orderID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null)
            {
                TempData["ToastMessage"] = "❌ Order not found.";
                return RedirectToAction("BartenderHome");
            }

            // Check if order has already been served
            if (order.Status == "Served")
            {
                TempData["ToastMessage"] = "⚠️ Cannot cancel a served order.";
                return RedirectToAction("BartenderHome");
            }

            // Get all drink items from this order (Wine & Drinks category)
            var drinkItems = _dbContext.OrderDetails
                .Where(od => od.OrderID == orderID)
                .Join(_dbContext.MenuItems,
                      od => od.MenuItemID,
                      m => m.MenuItemID,
                      (od, m) => new { OrderDetail = od, MenuItem = m })
                .Where(x => x.MenuItem.Type == "Wine & Drinks")
                .Select(x => x.OrderDetail)
                .ToList();

            foreach (var item in drinkItems)
            {
                _dbContext.OrderDetails.Remove(item);
            }

            // If no items left on order, close it
            var remainingItems = _dbContext.OrderDetails
                .Where(od => od.OrderID == orderID)
                .Count();

            if (remainingItems == 0)
            {
                order.Status = "Closed";
                order.IsPaid = false; // Voided
            }

            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"✕ Drink order #{orderID} cancelled.";
            return RedirectToAction("BartenderHome");
        }

        // ── Waiter ───────────────────────────────────────────────────────────────

        public IActionResult WaiterHome()
        {
            return View();
        }
        // ── Waiter ───────────────────────────────────────────────────────────────

        /// <summary>
        /// GET: Waiter dashboard — shows all tables with their active orders.
        /// Optional selectedTableID highlights a table and shows its order panel.
        /// </summary>
        [HttpGet]
        public IActionResult WaiterHome(int? selectedTableID = null)
        {
            var waiterID = HttpContext.Session.GetInt32("UserID") ?? 0;
            var waiterName = HttpContext.Session.GetString("UserName") ?? "Waiter";

            // All tables
            var allTables = _dbContext.Tables.OrderBy(t => t.TableID).ToList();

            // All active (unpaid) orders keyed by TableID
            var activeOrders = (
                from o in _dbContext.Orders
                where !o.IsPaid && o.Status != "Closed"
                join od in _dbContext.OrderDetails on o.OrderID equals od.OrderID into odj
                from od in odj.DefaultIfEmpty()
                join m in _dbContext.MenuItems on od.MenuItemID equals m.MenuItemID into mj
                from m in mj.DefaultIfEmpty()
                select new { o, od, m }
            ).ToList();

            // Group order lines per order
            var orderMap = activeOrders
                .GroupBy(x => x.o.OrderID)
                .ToDictionary(
                    g => g.First().o.TablesID,
                    g =>
                    {
                        var first = g.First().o;
                        return new OrderDisplayItem
                        {
                            OrderID = first.OrderID,
                            OrderTime = first.OrderTime,
                            Status = first.Status,
                            TotalAmount = first.TotalAmount,
                            IsPaid = first.IsPaid,
                            PaymentMethod = first.PaymentMethod ?? string.Empty,
                            Lines = g
                                .Where(x => x.od != null && x.m != null)
                                .Select(x => new OrderLineItem
                                {
                                    OrderDetailsID = x.od.OrderDetailsID,
                                    MenuItemID = x.od.MenuItemID,
                                    ItemName = x.m.Name,
                                    ItemType = x.m.Type,
                                    Quantity = x.od.Quantity,
                                    Price = x.od.Price
                                }).ToList()
                        };
                    }
                );

            // Build table display items
            var tableItems = allTables.Select(t =>
            {
                var hasActiveOrder = orderMap.TryGetValue(t.TableID, out var ord);
                var item = new TableDisplayItem
                {
                    TableID = t.TableID,
                    TableType = t.Type,
                    Capacity = t.Capacity,
                    IsAvailable = t.IsAvailable,
                    ActiveOrder = hasActiveOrder ? ord : null
                };

                // Check if table is "Seated" (reserved but customer arrived)
                var hasSeatedReservation = _dbContext.Reservations
                    .Any(r => r.TablesID == t.TableID && r.Status == "Seated");

                if (hasSeatedReservation && !hasActiveOrder)
                {
                    item.CustomStatus = "seated";
                }

                return item;
            }).ToList();

            var vm = new WaiterHomeViewModel
            {
                Tables = tableItems,
                MenuItems = _dbContext.MenuItems.OrderBy(m => m.Type).ThenBy(m => m.Name).ToList(),
                TotalTables = allTables.Count,
                OccupiedCount = allTables.Count(t => !t.IsAvailable),
                AvailableCount = allTables.Count(t => t.IsAvailable),
                ReservedCount = _dbContext.Reservations.Count(r => r.Status == "Confirmed" || r.Status == "Waiting" || r.Status == "Seated"),
                SelectedTableID = selectedTableID,
                WaiterName = waiterName,
                WaiterID = waiterID
            };

            return View(vm);
        }

        /// <summary>
        /// POST: Start a new order on a table (sets table occupied).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult StartOrder(int tableID)
        {
            var table = _dbContext.Tables.Find(tableID);
            if (table == null) return NotFound();

            var waiterID = HttpContext.Session.GetInt32("UserID") ?? 0;

            // Check no active order already exists
            var existing = _dbContext.Orders
                .FirstOrDefault(o => o.TablesID == tableID && !o.IsPaid && o.Status != "Closed");
            if (existing != null)
            {
                TempData["ToastMessage"] = "⚠️ Table already has an active order.";
                return RedirectToAction("WaiterHome", new { selectedTableID = tableID });
            }

            var order = new Orders
            {
                UserID = waiterID,
                TablesID = tableID,
                OrderTime = DateTime.Now,
                Status = "Pending",
                TotalAmount = 0,
                TipAmount = 0,
                PaymentMethod = string.Empty,
                IsPaid = false
            };

            table.IsAvailable = false;

            // If there's a "Seated" reservation, update its status
            var seatedReservation = _dbContext.Reservations
                .FirstOrDefault(r => r.TablesID == tableID && r.Status == "Seated");
            if (seatedReservation != null)
            {
                seatedReservation.Status = "Occupied"; // Or "Completed" - whatever your flow requires
            }

            _dbContext.Orders.Add(order);
            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"🆕 Order started for Table {tableID}.";
            return RedirectToAction("WaiterHome", new { selectedTableID = tableID });
        }

        /// <summary>
        /// POST: Add a menu item to an existing active order.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddOrderItem(int orderID, int menuItemID, int quantity = 1)
        {
            var order = _dbContext.Orders.Find(orderID);
            var menuItem = _dbContext.MenuItems.Find(menuItemID);
            if (order == null || menuItem == null) return NotFound();

            // Check if item already on order — increase qty
            var existing = _dbContext.OrderDetails
                .FirstOrDefault(od => od.OrderID == orderID && od.MenuItemID == menuItemID);

            if (existing != null)
            {
                existing.Quantity += quantity;
            }
            else
            {
                _dbContext.OrderDetails.Add(new OrderDetails
                {
                    OrderID = orderID,
                    MenuItemID = menuItemID,
                    Quantity = quantity,
                    Price = menuItem.Price
                });
            }

            // Recalculate total
            _dbContext.SaveChanges();
            var lines = _dbContext.OrderDetails
                .Where(od => od.OrderID == orderID)
                .Join(_dbContext.MenuItems, od => od.MenuItemID, m => m.MenuItemID,
                      (od, m) => od.Price * od.Quantity)
                .Sum();
            order.TotalAmount = lines;
            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"➕ {menuItem.Name} added to order.";
            return RedirectToAction("WaiterHome", new { selectedTableID = order.TablesID });
        }

        /// <summary>
        /// POST: Remove one line item from an order.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveOrderItem(int orderDetailsID, int tableID)
        {
            var line = _dbContext.OrderDetails.Find(orderDetailsID);
            if (line == null) return NotFound();

            var orderID = line.OrderID;
            _dbContext.OrderDetails.Remove(line);
            _dbContext.SaveChanges();

            // Recalculate total
            var total = _dbContext.OrderDetails
                .Where(od => od.OrderID == orderID)
                .Join(_dbContext.MenuItems, od => od.MenuItemID, m => m.MenuItemID,
                      (od, m) => od.Price * od.Quantity)
                .Sum();
            var order = _dbContext.Orders.Find(orderID);
            if (order != null) { order.TotalAmount = total; _dbContext.SaveChanges(); }

            TempData["ToastMessage"] = "🗑️ Item removed from order.";
            return RedirectToAction("WaiterHome", new { selectedTableID = tableID });
        }

        /// <summary>
        /// POST: Update order status (Pending → In Progress → Ready → Served).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateOrderStatus(int orderID, string newStatus, int tableID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null) return NotFound();

            order.Status = newStatus;
            _dbContext.SaveChanges();

            var emoji = newStatus switch
            {
                "In Progress" => "🔥",
                "Ready" => "🔔",
                "Served" => "✅",
                _ => "📋"
            };
            TempData["ToastMessage"] = $"{emoji} Order marked as {newStatus} for Table {tableID}.";
            return RedirectToAction("WaiterHome", new { selectedTableID = tableID });
        }

        /// <summary>
        /// POST: Close an order — mark paid, free the table.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CloseOrder(int orderID, int tableID, string paymentMethod, decimal tipAmount = 0)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null) return NotFound();

            order.IsPaid = true;
            order.Status = "Closed";
            order.PaymentMethod = paymentMethod;
            order.TipAmount = tipAmount;

            var table = _dbContext.Tables.Find(tableID);
            if (table != null) table.IsAvailable = true;

            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"💳 Table {tableID} paid ({paymentMethod}). Table cleared.";
            return RedirectToAction("WaiterHome");
        }

        /// <summary>
        /// POST: Clear a table without payment (e.g. reservation cancelled, no-show).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearTable(int tableID)
        {
            var table = _dbContext.Tables.Find(tableID);
            if (table != null) table.IsAvailable = true;

            // If there's a "Seated" reservation, revert it to "Confirmed" or cancel it
            var seatedReservation = _dbContext.Reservations
                .FirstOrDefault(r => r.TablesID == tableID && r.Status == "Seated");
            if (seatedReservation != null)
            {
                seatedReservation.Status = "Confirmed"; // Or "Cancelled" - depending on your flow
            }

            // Cancel any pending orders on this table
            var openOrders = _dbContext.Orders
                .Where(o => o.TablesID == tableID && !o.IsPaid && o.Status != "Closed")
                .ToList();
            foreach (var o in openOrders)
            {
                o.Status = "Closed";
                o.IsPaid = false; // not paid — just voided
            }

            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"🧹 Table {tableID} cleared.";
            return RedirectToAction("WaiterHome");
        }

        // ── Receptionist ─────────────────────────────────────────────────────────

        /// <summary>
        /// GET: Build the ReceptionistHomeViewModel from live DB data and render the dashboard.
        /// </summary>
        [HttpGet]
        public IActionResult ReceptionistHome()
        {
            // --- Tables (all) ---
            var allTables = _dbContext.Tables.OrderBy(t => t.TableID).ToList();

            // --- Reservations joined with Tables ---
            var reservations = (
                from r in _dbContext.Reservations
                join t in _dbContext.Tables on r.TablesID equals t.TableID into tj
                from t in tj.DefaultIfEmpty()           // LEFT JOIN – keep reservation even if table deleted
                orderby r.ReservationDate
                select new ReservationDisplayItem
                {
                    ReservationID = r.ReservationsID,
                    GuestName = r.Name + " " + r.Surname,
                    ContactPhone = r.ContactPhone,
                    Email = r.Email,
                    ReservationDate = r.ReservationDate,
                    GuestCapacity = r.Capacity,
                    Status = r.Status,
                    TableID = r.TablesID,
                    TableType = t != null ? t.Type : "—",
                    TableCapacity = t != null ? t.Capacity : 0,
                    TableIsAvailable = t != null && t.IsAvailable
                }
            ).ToList();

            // --- Stats ---
            var vm = new ReceptionistHomeViewModel
            {
                Tables = allTables,
                Reservations = reservations,
                TotalReservations = reservations.Count,
                SeatedCount = reservations.Count(r => r.Status == "Seated"),
                WaitingOrConfirmedCount = reservations.Count(r => r.Status is "Waiting" or "Confirmed"),
                AvailableTablesCount = allTables.Count(t => t.IsAvailable),
                OccupiedTablesCount = allTables.Count(t => !t.IsAvailable),
                ReceptionistName = HttpContext.Session.GetString("UserName") ?? "Receptionist"
            };

            return View(vm);
        }

        // ── Receptionist POST actions ─────────────────────────────────────────────

        /// <summary>
        /// POST: Seat a confirmed/waiting reservation.
        /// Marks the table IsAvailable = false, sets Status = "Seated".
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SeatReservation(int reservationID)
        {
            var reservation = _dbContext.Reservations.Find(reservationID);
            if (reservation == null)
                return NotFound();

            var table = _dbContext.Tables.Find(reservation.TablesID);

            // If this table is somehow unavailable, find another free one.
            if (table == null || !table.IsAvailable)
            {
                table = _dbContext.Tables.FirstOrDefault(t => t.IsAvailable && t.Capacity >= reservation.Capacity);
                if (table == null)
                {
                    TempData["ToastMessage"] = "⚠️ No available tables right now.";
                    return RedirectToAction("ReceptionistHome");
                }
                reservation.TablesID = table.TableID;
            }

            table.IsAvailable = false;
            reservation.Status = "Seated";

            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"🪑 {reservation.Name} {reservation.Surname} seated at Table {table.TableID}.";
            return RedirectToAction("ReceptionistHome");
        }

        /// <summary>
        /// POST: Mark a seated reservation as completed and free the table.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteReservation(int reservationID)
        {
            var reservation = _dbContext.Reservations.Find(reservationID);
            if (reservation == null)
                return NotFound();

            reservation.Status = "Completed";

            var table = _dbContext.Tables.Find(reservation.TablesID);
            if (table != null)
                table.IsAvailable = true;

            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"✅ {reservation.Name} {reservation.Surname} completed. Table {reservation.TablesID} freed.";
            return RedirectToAction("ReceptionistHome");
        }

        /// <summary>
        /// POST: Cancel a reservation and free the associated table.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelReservation(int reservationID)
        {
            var reservation = _dbContext.Reservations.Find(reservationID);
            if (reservation == null)
                return NotFound();

            reservation.Status = "Cancelled";

            var table = _dbContext.Tables.Find(reservation.TablesID);
            if (table != null)
                table.IsAvailable = true;

            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"✕ Reservation for {reservation.Name} {reservation.Surname} cancelled.";
            return RedirectToAction("ReceptionistHome");
        }

        /// <summary>
        /// POST: Add a new reservation from the modal form.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddReservation(string name, string surname, string contactPhone,
                                            string email, DateTime reservationDate, int capacity, int tableID)
        {
            var table = _dbContext.Tables.Find(tableID);
            if (table == null || !table.IsAvailable)
            {
                TempData["ToastMessage"] = "⚠️ Selected table is not available.";
                return RedirectToAction("ReceptionistHome");
            }

            var receptionistID = HttpContext.Session.GetInt32("UserID") ?? 0;

            var reservation = new Reservations
            {
                UserID = receptionistID,
                TablesID = tableID,
                Name = name,
                Surname = surname,
                ContactPhone = contactPhone,
                Email = email,
                ReservationDate = reservationDate,
                Capacity = capacity,
                Status = "Confirmed"
            };

            table.IsAvailable = false;

            _dbContext.Reservations.Add(reservation);
            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"➕ Reservation added for {name} {surname} at Table {tableID}.";
            return RedirectToAction("ReceptionistHome");
        }

        /// <summary>
        /// POST: Create a walk-in reservation on the first available table.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult WalkIn(int guests)
        {
            if (guests < 1) guests = 2;

            var table = _dbContext.Tables.FirstOrDefault(t => t.IsAvailable && t.Capacity >= guests);
            if (table == null)
            {
                TempData["ToastMessage"] = "⚠️ No available tables for walk-in.";
                return RedirectToAction("ReceptionistHome");
            }

            var receptionistID = HttpContext.Session.GetInt32("UserID") ?? 0;

            var walkIn = new Reservations
            {
                UserID = receptionistID,
                TablesID = table.TableID,
                Name = "Walk-in",
                Surname = $"Guest",
                ContactPhone = "—",
                Email = "—",
                ReservationDate = DateTime.Now,
                Capacity = guests,
                Status = "Seated"
            };

            table.IsAvailable = false;

            _dbContext.Reservations.Add(walkIn);
            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"🚶 Walk-in ({guests} guests) seated at Table {table.TableID}.";
            return RedirectToAction("ReceptionistHome");
        }

       

        // ── Misc ─────────────────────────────────────────────────────────────────

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}