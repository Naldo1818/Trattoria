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

        [HttpGet]
        public IActionResult AdminHome(string tab = "overview")
        {
            var adminName = HttpContext.Session.GetString("UserName") ?? "Admin";
            var today = DateTime.Today;

            // ── Staff ─────────────────────────────────────────────
            var staff = _dbContext.Users.OrderBy(u => u.Role).ThenBy(u => u.Name).ToList();

            // ── Menu ──────────────────────────────────────────────
            var menu = _dbContext.MenuItems.OrderBy(m => m.Type).ThenBy(m => m.Name).ToList();

            // ── Today reservations ────────────────────────────────
            var todaysRes = (
                from r in _dbContext.Reservations
                where r.ReservationDate.Date == today
                join t in _dbContext.Tables on r.TablesID equals t.TableID into tj
                from t in tj.DefaultIfEmpty()
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

            // ── Today revenue ─────────────────────────────────────
            var todayOrders = _dbContext.Orders
                .Where(o => o.IsPaid && o.OrderTime.Date == today)
                .ToList();

            // ── Daily sales — last 7 days ─────────────────────────
            var sevenDaysAgo = today.AddDays(-6);
            var paidOrders = _dbContext.Orders
                .Where(o => o.IsPaid && o.OrderTime.Date >= sevenDaysAgo)
                .ToList();

            var dailySales = Enumerable.Range(0, 7)
                .Select(i => today.AddDays(-i))
                .Select(date =>
                {
                    var dayOrders = paidOrders.Where(o => o.OrderTime.Date == date).ToList();
                    return new DailySalesItem
                    {
                        Date = date,
                        Revenue = dayOrders.Sum(o => o.TotalAmount + o.TipAmount),
                        OrderCount = dayOrders.Count
                    };
                })
                .ToList();

            // ── Monthly sales — last 6 months ─────────────────────
            var sixMonthsAgo = today.AddMonths(-5);
            var allPaidOrders = _dbContext.Orders
                .Where(o => o.IsPaid && o.OrderTime >= new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1))
                .ToList();

            var monthlySales = allPaidOrders
                .GroupBy(o => new { o.OrderTime.Year, o.OrderTime.Month })
                .Select(g => new MonthlySalesItem
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = g.Sum(o => o.TotalAmount + o.TipAmount),
                    OrderCount = g.Count()
                })
                .OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
                .ToList();

            var vm = new AdminHomeViewModel
            {
                AdminName = adminName,
                ActiveTab = tab,
                Staff = staff,
                MenuItems = menu,
                TodaysReservations = todaysRes,
                DailySales = dailySales,
                MonthlySales = monthlySales,
                TotalStaff = staff.Count,
                ActiveStaff = staff.Count, // no IsActive field — all are active
                TotalMenuItems = menu.Count,
                TodayReservations = todaysRes.Count,
                TodayRevenue = todayOrders.Sum(o => o.TotalAmount + o.TipAmount),
                TodayOrderCount = todayOrders.Count
            };

            return View(vm);
        }

        // ── Admin: Staff actions ──────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddStaff(string name, string surname, string username,
                                      string password, string role)
        {
            if (_dbContext.Users.Any(u => u.Username == username))
            {
                TempData["ToastMessage"] = "⚠️ Username already exists.";
                return RedirectToAction("AdminHome", new { tab = "staff" });
            }

            _dbContext.Users.Add(new Users
            {
                Name = name,
                Surname = surname,
                Username = username,
                Password = password,
                Role = role
            });
            _dbContext.SaveChanges();
            TempData["ToastMessage"] = $"✅ {name} {surname} ({role}) added.";
            return RedirectToAction("AdminHome", new { tab = "staff" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveStaff(int userID)
        {
            var user = _dbContext.Users.Find(userID);
            if (user == null) return NotFound();

            // Prevent removing yourself
            var currentID = HttpContext.Session.GetInt32("UserID");
            if (currentID == userID)
            {
                TempData["ToastMessage"] = "⚠️ You cannot remove your own account.";
                return RedirectToAction("AdminHome", new { tab = "staff" });
            }

            _dbContext.Users.Remove(user);
            _dbContext.SaveChanges();
            TempData["ToastMessage"] = $"✕ {user.Name} {user.Surname} removed.";
            return RedirectToAction("AdminHome", new { tab = "staff" });
        }

        // ── Admin: Menu actions ───────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddMenuItem(string name, string type, string description, decimal price)
        {
            _dbContext.MenuItems.Add(new MenuItems
            {
                Name = name,
                Type = type,
                Description = description,
                Price = price
            });
            _dbContext.SaveChanges();
            TempData["ToastMessage"] = $"✅ {name} added to menu.";
            return RedirectToAction("AdminHome", new { tab = "menu" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditMenuItem(int menuItemID, string name, string type,
                                          string description, decimal price)
        {
            var item = _dbContext.MenuItems.Find(menuItemID);
            if (item == null) return NotFound();

            item.Name = name;
            item.Type = type;
            item.Description = description;
            item.Price = price;
            _dbContext.SaveChanges();
            TempData["ToastMessage"] = $"✏️ {name} updated.";
            return RedirectToAction("AdminHome", new { tab = "menu" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveMenuItem(int menuItemID)
        {
            var item = _dbContext.MenuItems.Find(menuItemID);
            if (item == null) return NotFound();

            _dbContext.MenuItems.Remove(item);
            _dbContext.SaveChanges();
            TempData["ToastMessage"] = $"✕ {item.Name} removed from menu.";
            return RedirectToAction("AdminHome", new { tab = "menu" });
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
        /// GET: Waiter dashboard — each table shows a Food order and a Drinks order separately.
        /// Order category is derived from MenuItems.Type via WaiterHomeViewModel.IsDrinkType().
        /// </summary>
        [HttpGet]
        public IActionResult WaiterHome(int? selectedTableID = null)
        {
            var waiterID = HttpContext.Session.GetInt32("UserID") ?? 0;
            var waiterName = HttpContext.Session.GetString("UserName") ?? "Waiter";

            var allTables = _dbContext.Tables.OrderBy(t => t.TableID).ToList();
            var allMenuItems = _dbContext.MenuItems.OrderBy(m => m.Type).ThenBy(m => m.Name).ToList();

            // Active (unpaid, non-closed) orders with their lines
            var activeOrders = (
                from o in _dbContext.Orders
                where !o.IsPaid && o.Status != "Closed"
                join od in _dbContext.OrderDetails on o.OrderID equals od.OrderID into odj
                from od in odj.DefaultIfEmpty()
                join m in _dbContext.MenuItems on od.MenuItemID equals m.MenuItemID into mj
                from m in mj.DefaultIfEmpty()
                select new { o, od, m }
            ).ToList();

            // Group lines per OrderID
            var linesByOrder = activeOrders
                .GroupBy(x => x.o.OrderID)
                .ToDictionary(
                    g => g.Key,
                    g => g.Where(x => x.od != null && x.m != null)
                          .Select(x => new OrderLineItem
                          {
                              OrderDetailsID = x.od.OrderDetailsID,
                              MenuItemID = x.od.MenuItemID,
                              ItemName = x.m.Name,
                              ItemType = x.m.Type,
                              Quantity = x.od.Quantity,
                              Price = x.od.Price
                          }).ToList()
                );

            // Distinct orders keyed by TableID — group into Food / Drinks by line types
            // An order is "Drinks" if ALL its lines are drink types; otherwise "Food"
            var ordersByTable = activeOrders
                .GroupBy(x => x.o.OrderID)
                .Select(g =>
                {
                    var first = g.First().o;
                    var lines = linesByOrder.TryGetValue(first.OrderID, out var l) ? l : new();
                    // Classify by whether ALL items are drinks
                    bool isDrinks = lines.Any() && lines.All(li => WaiterHomeViewModel.IsDrinkType(li.ItemType));
                    // If no lines yet, classify by PaymentMethod field used as a type tag
                    if (!lines.Any())
                        isDrinks = first.PaymentMethod?.StartsWith("__DRINKS") == true;

                    return new OrderDisplayItem
                    {
                        OrderID = first.OrderID,
                        OrderTime = first.OrderTime,
                        Status = first.Status,
                        IsPaid = first.IsPaid,
                        PaymentMethod = first.PaymentMethod ?? string.Empty,
                        OrderCategory = isDrinks ? "Drinks" : "Food",
                        Lines = lines
                    };
                })
                .GroupBy(o => activeOrders.First(x => x.o.OrderID == o.OrderID).o.TablesID)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Build table display items
            var tableItems = allTables.Select(t =>
            {
                var orders = ordersByTable.TryGetValue(t.TableID, out var ol) ? ol : new();
                return new TableDisplayItem
                {
                    TableID = t.TableID,
                    TableType = t.Type,
                    Capacity = t.Capacity,
                    IsAvailable = t.IsAvailable,
                    FoodOrder = orders.FirstOrDefault(o => o.OrderCategory == "Food"),
                    DrinksOrder = orders.FirstOrDefault(o => o.OrderCategory == "Drinks")
                };
            }).ToList();

            var vm = new WaiterHomeViewModel
            {
                Tables = tableItems,
                FoodItems = allMenuItems.Where(m => !WaiterHomeViewModel.IsDrinkType(m.Type)).ToList(),
                DrinkItems = allMenuItems.Where(m => WaiterHomeViewModel.IsDrinkType(m.Type)).ToList(),
                TotalTables = allTables.Count,
                OccupiedCount = allTables.Count(t => !t.IsAvailable),
                AvailableCount = allTables.Count(t => t.IsAvailable),
                ReservedCount = _dbContext.Reservations.Count(r => r.Status == "Confirmed" || r.Status == "Waiting"),
                SelectedTableID = selectedTableID,
                WaiterName = waiterName,
                WaiterID = waiterID
            };

            return View(vm);
        }

        /// <summary>
        /// POST: Start TWO orders on a table — one Food, one Drinks.
        /// The Drinks order is tagged by setting PaymentMethod = "__DRINKS" temporarily.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult StartOrder(int tableID)
        {
            var table = _dbContext.Tables.Find(tableID);
            if (table == null) return NotFound();

            var waiterID = HttpContext.Session.GetInt32("UserID") ?? 0;

            // Prevent duplicate active orders
            var existing = _dbContext.Orders
                .Any(o => o.TablesID == tableID && !o.IsPaid && o.Status != "Closed");
            if (existing)
            {
                TempData["ToastMessage"] = "⚠️ Table already has active orders.";
                return RedirectToAction("WaiterHome", new { selectedTableID = tableID });
            }

            // Food order
            _dbContext.Orders.Add(new Orders
            {
                UserID = waiterID,
                TablesID = tableID,
                OrderTime = DateTime.Now,
                Status = "Pending",
                TotalAmount = 0,
                TipAmount = 0,
                PaymentMethod = "__FOOD",
                IsPaid = false
            });

            // Drinks order — tagged so the system knows it's drinks before any lines exist
            _dbContext.Orders.Add(new Orders
            {
                UserID = waiterID,
                TablesID = tableID,
                OrderTime = DateTime.Now,
                Status = "Pending",
                TotalAmount = 0,
                TipAmount = 0,
                PaymentMethod = "__DRINKS",
                IsPaid = false
            });

            table.IsAvailable = false;
            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"🆕 Food & Drinks orders started for Table {tableID}.";
            return RedirectToAction("WaiterHome", new { selectedTableID = tableID });
        }

        /// <summary>
        /// POST: Add a menu item to a specific order (food or drinks).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddOrderItem(int orderID, int menuItemID, int quantity, int tableID)
        {
            var order = _dbContext.Orders.Find(orderID);
            var menuItem = _dbContext.MenuItems.Find(menuItemID);
            if (order == null || menuItem == null) return NotFound();

            var existing = _dbContext.OrderDetails
                .FirstOrDefault(od => od.OrderID == orderID && od.MenuItemID == menuItemID);
            if (existing != null)
                existing.Quantity += quantity;
            else
                _dbContext.OrderDetails.Add(new OrderDetails
                {
                    OrderID = orderID,
                    MenuItemID = menuItemID,
                    Quantity = quantity,
                    Price = menuItem.Price
                });

            // Clear the type tag now that real lines exist; set PaymentMethod to the real category
            if (order.PaymentMethod == "__FOOD" || order.PaymentMethod == "__DRINKS")
            {
                bool isDrink = WaiterHomeViewModel.IsDrinkType(menuItem.Type);
                // keep the tag for classification — do not overwrite
            }

            _dbContext.SaveChanges();

            // Recalculate total
            order.TotalAmount = _dbContext.OrderDetails
                .Where(od => od.OrderID == orderID)
                .Join(_dbContext.MenuItems, od => od.MenuItemID, m => m.MenuItemID,
                      (od, m) => od.Price * od.Quantity)
                .Sum();
            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"➕ {menuItem.Name} added.";
            return RedirectToAction("WaiterHome", new { selectedTableID = tableID });
        }

        /// <summary>
        /// POST: Remove a line item from an order.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveOrderItem(int orderDetailsID, int tableID)
        {
            var line = _dbContext.OrderDetails.Find(orderDetailsID);
            if (line == null) return NotFound();

            int orderID = line.OrderID;
            _dbContext.OrderDetails.Remove(line);
            _dbContext.SaveChanges();

            var total = _dbContext.OrderDetails
                .Where(od => od.OrderID == orderID)
                .Join(_dbContext.MenuItems, od => od.MenuItemID, m => m.MenuItemID,
                      (od, m) => od.Price * od.Quantity)
                .Sum();
            var order = _dbContext.Orders.Find(orderID);
            if (order != null) { order.TotalAmount = total; _dbContext.SaveChanges(); }

            TempData["ToastMessage"] = "🗑️ Item removed.";
            return RedirectToAction("WaiterHome", new { selectedTableID = tableID });
        }

        /// <summary>
        /// POST: Send a Food or Drinks order to its station (In Progress).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendToStation(int orderID, int tableID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null) return NotFound();

            order.Status = "In Progress";
            _dbContext.SaveChanges();

            bool isDrinks = order.PaymentMethod?.StartsWith("__DRINKS") == true;
            TempData["ToastMessage"] = isDrinks
                ? $"🍹 Drinks order sent to bar (Table {tableID})."
                : $"🔥 Food order sent to kitchen (Table {tableID}).";
            return RedirectToAction("WaiterHome", new { selectedTableID = tableID });
        }

        /// <summary>
        /// POST: Update order status — used by waiter to mark Served after station marks Ready.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateOrderStatus(int orderID, string newStatus, int tableID)
        {
            var order = _dbContext.Orders.Find(orderID);
            if (order == null) return NotFound();

            order.Status = newStatus;
            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"✅ Order #{orderID} marked as {newStatus}.";
            return RedirectToAction("WaiterHome", new { selectedTableID = tableID });
        }

        /// <summary>
        /// POST: Close & Pay — marks both food and drinks orders as paid, frees the table.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CloseOrder(int tableID, string paymentMethod, decimal tipAmount = 0)
        {
            var openOrders = _dbContext.Orders
                .Where(o => o.TablesID == tableID && !o.IsPaid && o.Status != "Closed")
                .ToList();

            foreach (var o in openOrders)
            {
                o.IsPaid = true;
                o.Status = "Closed";
                // preserve __FOOD/__DRINKS tag? No — overwrite with real payment method
                o.PaymentMethod = paymentMethod;
                o.TipAmount = tipAmount;
            }

            var table = _dbContext.Tables.Find(tableID);
            if (table != null) table.IsAvailable = true;

            _dbContext.SaveChanges();

            TempData["ToastMessage"] = $"💳 Table {tableID} paid ({paymentMethod}). Table cleared.";
            return RedirectToAction("WaiterHome");
        }

        /// <summary>
        /// POST: Clear a table — voids all open orders, frees the table.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearTable(int tableID)
        {
            var openOrders = _dbContext.Orders
                .Where(o => o.TablesID == tableID && !o.IsPaid && o.Status != "Closed")
                .ToList();
            foreach (var o in openOrders) o.Status = "Closed";

            var table = _dbContext.Tables.Find(tableID);
            if (table != null) table.IsAvailable = true;

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