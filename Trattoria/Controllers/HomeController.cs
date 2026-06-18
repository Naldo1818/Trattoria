
using Trattoria.ViewModels;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Trattoria.Models;
using Trattoria.Data;

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
        [HttpGet]
        public IActionResult Index()
        {
            return View();

        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(IndexViewModel index)
        {
            if (ModelState.IsValid)
            {
                var user = _dbContext.Users.SingleOrDefault(u => u.Username == index.Username && u.Password == index.Password);
                if (user != null)
                {
                    if (user.Role == "Admin")
                    {
                        int UserID = _dbContext.Users.FirstOrDefault(p => p.Username == index.Username)?.UserID ?? 0;


                        var User = _dbContext.Users
                                       .Where(a => a.UserID == UserID)
                                       .Select(a => new Users
                                       {
                                           UserID = a.UserID,
                                           Name = a.Name,
                                           Surname = a.Surname,
                                           Username = a.Username
                                       })
                                       .SingleOrDefault();

                        if (user == null)
                        {
                            return NotFound();
                        }

                        // Store user data in session
                     


                        return RedirectToAction("AdminHome", new { UserID });

                    }
                    else if (user.Role == "Waiter")
                    {
                        if (ModelState.IsValid)
                        {
                            int UserID = _dbContext.Users.FirstOrDefault(p => p.Username == index.Username)?.UserID ?? 0;
                            var User = _dbContext.Users.Where(a => a.UserID == UserID)
                                                       .Select(a => new Users
                                                       {
                                                           UserID = a.UserID,
                                                           Name = a.Name,
                                                           Surname = a.Surname,
                                                           Username = a.Username
                                                       })
                                                       .SingleOrDefault();

                                    if (user == null)
                                    {
                                        return NotFound();
                                    }

                                    // Store user data in session
                                   


                                    return RedirectToAction("WaiterHome");
                                }
                            }
                    else if (user.Role == "Chefs")
                    {
                        if (ModelState.IsValid)
                        {
                            int UserID = _dbContext.Users.FirstOrDefault(p => p.Username == index.Username)?.UserID ?? 0;
                            var User = _dbContext.Users.Where(a => a.UserID == UserID)
                                                       .Select(a => new Users
                                                       {
                                                           UserID = a.UserID,
                                                           Name = a.Name,
                                                           Surname = a.Surname,
                                                           Username = a.Username
                                                       })
                                                       .SingleOrDefault();

                            if (user == null)
                            {
                                return NotFound();
                            }

                          


                            return RedirectToAction("ChefsHome");
                        }
                    }
                    else if (user.Role == "Bartender")
                    {
                        if (ModelState.IsValid)
                        {
                            int UserID = _dbContext.Users.FirstOrDefault(p => p.Username == index.Username)?.UserID ?? 0;
                            var User = _dbContext.Users.Where(a => a.UserID == UserID)
                                                       .Select(a => new Users
                                                       {
                                                           UserID = a.UserID,
                                                           Name = a.Name,
                                                           Surname = a.Surname,
                                                           Username = a.Username
                                                       })
                                                       .SingleOrDefault();

                            if (user == null)
                            {
                                return NotFound();
                            }

                         
                            return RedirectToAction("BartenderHome");
                        }
                    }
                    else if (user.Role == "Receptionist")
                    {
                        if (ModelState.IsValid)
                        {
                            int UserID = _dbContext.Users.FirstOrDefault(p => p.Username == index.Username)?.UserID ?? 0;
                            var User = _dbContext.Users.Where(a => a.UserID == UserID)
                                                       .Select(a => new Users
                                                       {
                                                           UserID = a.UserID,
                                                           Name = a.Name,
                                                           Surname = a.Surname,
                                                           Username = a.Username
                                                       })
                                                       .SingleOrDefault();

                            if (user == null)
                            {
                                return NotFound();
                            }

                        

                            return RedirectToAction("ReceptionistHome");
                        }
                    }
                }
            }
            return View(index);
        }
        public IActionResult AdminHome()
        {
            return View();
        }
        public IActionResult ChefsHome()
        {
            return View();
        }
        public IActionResult BartenderHome()
        {
            return View();
        }
        public IActionResult WaiterHome()
        {
            return View();
        }
        public IActionResult ReceptionistHome()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
