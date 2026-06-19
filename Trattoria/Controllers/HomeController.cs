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

        public IActionResult ChefsHome()
        {
            return View();
        }

        // ── Bartender ────────────────────────────────────────────────────────────

        public IActionResult BartenderHome()
        {
            return View();
        }

        // ── Waiter ───────────────────────────────────────────────────────────────

        public IActionResult WaiterHome()
        {
            return View();
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