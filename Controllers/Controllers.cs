using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using BCrypt.Net;
using StoreInventory.DAL;
using StoreInventory.Models;

namespace StoreInventory.Controllers
{
    // ══════════════════════════════════════════════════════════════════════════
    //  ACCOUNT CONTROLLER
    // ══════════════════════════════════════════════════════════════════════════
    public class AccountController : Controller
    {
        [AllowAnonymous]
        public ActionResult Login() => View();

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            using (var ctx = new ApplicationDbContext())
            {
                var user = ctx.Users
                    .FirstOrDefault(u => u.Username == model.Username && u.IsActive);

                if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                {
                    ModelState.AddModelError("", "Invalid username or password.");
                    return View(model);
                }

                user.LastLogin = DateTime.Now;
                AuditService.Log(ctx, user.UserId, "LOGIN", "USERS",
                    user.UserId, null, user.Username);
                ctx.SaveChanges();

                SessionHelper.Set(user.UserId, user.Username, user.Role, user.Email);
                return RedirectToAction("Index", "Home");
            }
        }

        public ActionResult Logout()
        {
            SessionHelper.Clear();
            return RedirectToAction("Login");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HOME / DASHBOARD CONTROLLER
    // ══════════════════════════════════════════════════════════════════════════
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            using (var ctx = new ApplicationDbContext())
            {
                var today      = DateTime.Today;
                var monthStart = new DateTime(today.Year, today.Month, 1);
                var weekAgo    = today.AddDays(-6);

                var vm = new DashboardViewModel
                {
                    TotalProducts   = ctx.Products.Count(),
                    LowStockCount   = ctx.Products.Count(p => p.Quantity <= p.MinStock),
                    TotalCategories = ctx.Categories.Count(),
                    TotalSuppliers  = ctx.Suppliers.Count(),
                    TotalUsers      = ctx.Users.Count(u => u.IsActive),
                    PendingOrders   = ctx.PurchaseOrders.Count(p => p.Status == "Pending"),
                    InventoryValue  = ctx.Products.Sum(p => (decimal?)( p.Quantity * p.CostPrice)) ?? 0,
                    TodaySales      = ctx.Sales.Where(s => DbFunctions.TruncateTime(s.SaleDate) == today)
                                              .Sum(s => (decimal?)s.TotalAmount) ?? 0,
                    MonthSales      = ctx.Sales.Where(s => s.SaleDate >= monthStart)
                                              .Sum(s => (decimal?)s.TotalAmount) ?? 0,

                    LowStockProducts = ctx.Products
                        .Include(p => p.Category)
                        .Where(p => p.Quantity <= p.MinStock)
                        .OrderBy(p => p.Quantity)
                        .Take(6).ToList(),

                    RecentSales = ctx.Sales
                        .Include(s => s.User)
                        .Include(s => s.Items)
                        .OrderByDescending(s => s.SaleDate)
                        .Take(5).ToList(),

                    RecentTransactions = ctx.StockTransactions
                        .Include(t => t.Product)
                        .Include(t => t.User)
                        .OrderByDescending(t => t.TxDate)
                        .Take(7).ToList(),

                    // Last 7 days sales chart
                    SalesChart = Enumerable.Range(0, 7)
                        .Select(i => today.AddDays(-6 + i))
                        .Select(d => new ChartPoint
                        {
                            Label = d.ToString("ddd"),
                            Value = ctx.Sales
                                .Where(s => DbFunctions.TruncateTime(s.SaleDate) == d)
                                .Sum(s => (decimal?)s.TotalAmount) ?? 0
                        }).ToList(),

                    // Top 5 products by revenue
                    TopProducts = ctx.SaleItems
                        .Include(i => i.Product)
                        .GroupBy(i => i.Product.Name)
                        .Select(g => new ChartPoint
                        {
                            Label = g.Key,
                            Value = g.Sum(i => i.Quantity * i.UnitPrice)
                        })
                        .OrderByDescending(x => x.Value)
                        .Take(5).ToList(),

                    // Stock by category
                    CategoryStock = ctx.Products
                        .Include(p => p.Category)
                        .GroupBy(p => p.Category.Name)
                        .Select(g => new ChartPoint
                        {
                            Label = g.Key,
                            Value = g.Sum(p => p.Quantity)
                        }).ToList()
                };

                return View(vm);
            }
        }

        public ActionResult Error()   => View();
        public ActionResult NotFound() => View();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PRODUCTS CONTROLLER
    // ══════════════════════════════════════════════════════════════════════════
    public class ProductsController : Controller
    {
        public ActionResult Index(string search = "", int? categoryId = null, string stockFilter = "")
        {
            using (var ctx = new ApplicationDbContext())
            {
                var q = ctx.Products.Include(p => p.Category).AsQueryable();

                if (!string.IsNullOrEmpty(search))
                    q = q.Where(p => p.Name.Contains(search) || p.SKU.Contains(search));
                if (categoryId.HasValue)
                    q = q.Where(p => p.CategoryId == categoryId.Value);
                if (stockFilter == "low")
                    q = q.Where(p => p.Quantity <= p.MinStock);
                else if (stockFilter == "ok")
                    q = q.Where(p => p.Quantity > p.MinStock);

                ViewBag.Categories  = ctx.Categories.OrderBy(c => c.Name).ToList();
                ViewBag.Search      = search;
                ViewBag.CategoryId  = categoryId;
                ViewBag.StockFilter = stockFilter;
                return View(q.OrderBy(p => p.Name).ToList());
            }
        }

        public ActionResult Create()
        {
            using (var ctx = new ApplicationDbContext())
            {
                ViewBag.Categories = new SelectList(
                    ctx.Categories.OrderBy(c => c.Name), "CategoryId", "Name");
                return View(new Product());
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(Product model)
        {
            if (!ModelState.IsValid)
            {
                using (var ctx = new ApplicationDbContext())
                    ViewBag.Categories = new SelectList(
                        ctx.Categories.OrderBy(c => c.Name), "CategoryId", "Name");
                return View(model);
            }

            using (var ctx = new ApplicationDbContext())
            {
                if (ctx.Products.Any(p => p.SKU == model.SKU))
                {
                    ModelState.AddModelError("SKU", "SKU already exists.");
                    ViewBag.Categories = new SelectList(
                        ctx.Categories.OrderBy(c => c.Name), "CategoryId", "Name");
                    return View(model);
                }

                model.CreatedAt = model.UpdatedAt = DateTime.Now;
                ctx.Products.Add(model);
                AuditService.Log(ctx, SessionHelper.UserId, "CREATE",
                    "PRODUCTS", null, null, model.Name);
                ctx.SaveChanges();

                TempData["Success"] = $"Product '{model.Name}' added successfully.";
                return RedirectToAction("Index");
            }
        }

        public ActionResult Edit(int id)
        {
            using (var ctx = new ApplicationDbContext())
            {
                var p = ctx.Products.Find(id);
                if (p == null) return HttpNotFound();
                ViewBag.Categories = new SelectList(
                    ctx.Categories.OrderBy(c => c.Name), "CategoryId", "Name", p.CategoryId);
                return View(p);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(Product model)
        {
            if (!ModelState.IsValid)
            {
                using (var ctx = new ApplicationDbContext())
                    ViewBag.Categories = new SelectList(
                        ctx.Categories.OrderBy(c => c.Name), "CategoryId", "Name");
                return View(model);
            }

            using (var ctx = new ApplicationDbContext())
            {
                var p = ctx.Products.Find(model.ProductId);
                if (p == null) return HttpNotFound();

                string old = $"Name:{p.Name},Price:{p.UnitPrice},Qty:{p.Quantity}";
                p.Name        = model.Name;
                p.SKU         = model.SKU;
                p.Barcode     = model.Barcode;
                p.CategoryId  = model.CategoryId;
                p.UnitPrice   = model.UnitPrice;
                p.CostPrice   = model.CostPrice;
                p.Quantity    = model.Quantity;
                p.MinStock    = model.MinStock;
                p.Description = model.Description;
                p.UpdatedAt   = DateTime.Now;

                AuditService.Log(ctx, SessionHelper.UserId, "UPDATE",
                    "PRODUCTS", p.ProductId, old, $"Name:{p.Name},Price:{p.UnitPrice},Qty:{p.Quantity}");
                ctx.SaveChanges();

                TempData["Success"] = $"Product '{p.Name}' updated.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            using (var ctx = new ApplicationDbContext())
            {
                var p = ctx.Products.Find(id);
                if (p == null) return HttpNotFound();

                if (ctx.OrderItems.Any(i => i.ProductId == id)
                 || ctx.SaleItems.Any(i => i.ProductId == id))
                {
                    TempData["Error"] = "Cannot delete: product has associated orders/sales.";
                    return RedirectToAction("Index");
                }

                AuditService.Log(ctx, SessionHelper.UserId, "DELETE",
                    "PRODUCTS", id, p.Name, null);
                ctx.Products.Remove(p);
                ctx.SaveChanges();

                TempData["Success"] = $"Product '{p.Name}' deleted.";
                return RedirectToAction("Index");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CATEGORIES CONTROLLER
    // ══════════════════════════════════════════════════════════════════════════
    public class CategoriesController : Controller
    {
        public ActionResult Index()
        {
            using (var ctx = new ApplicationDbContext())
                return View(ctx.Categories.Include(c => c.Products).OrderBy(c => c.Name).ToList());
        }

        public ActionResult Create() => View(new Category());

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(Category model)
        {
            if (!ModelState.IsValid) return View(model);
            using (var ctx = new ApplicationDbContext())
            {
                model.CreatedAt = DateTime.Now;
                ctx.Categories.Add(model);
                AuditService.Log(ctx, SessionHelper.UserId, "CREATE",
                    "CATEGORIES", null, null, model.Name);
                ctx.SaveChanges();
                TempData["Success"] = $"Category '{model.Name}' created.";
                return RedirectToAction("Index");
            }
        }

        public ActionResult Edit(int id)
        {
            using (var ctx = new ApplicationDbContext())
            {
                var c = ctx.Categories.Find(id);
                if (c == null) return HttpNotFound();
                return View(c);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(Category model)
        {
            if (!ModelState.IsValid) return View(model);
            using (var ctx = new ApplicationDbContext())
            {
                var c = ctx.Categories.Find(model.CategoryId);
                if (c == null) return HttpNotFound();
                string old = c.Name;
                c.Name = model.Name; c.Description = model.Description;
                AuditService.Log(ctx, SessionHelper.UserId, "UPDATE",
                    "CATEGORIES", c.CategoryId, old, c.Name);
                ctx.SaveChanges();
                TempData["Success"] = $"Category updated.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            using (var ctx = new ApplicationDbContext())
            {
                if (ctx.Products.Any(p => p.CategoryId == id))
                {
                    TempData["Error"] = "Cannot delete: category has products.";
                    return RedirectToAction("Index");
                }
                var c = ctx.Categories.Find(id);
                if (c == null) return HttpNotFound();
                AuditService.Log(ctx, SessionHelper.UserId, "DELETE",
                    "CATEGORIES", id, c.Name, null);
                ctx.Categories.Remove(c);
                ctx.SaveChanges();
                TempData["Success"] = "Category deleted.";
                return RedirectToAction("Index");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SUPPLIERS CONTROLLER
    // ══════════════════════════════════════════════════════════════════════════
    public class SuppliersController : Controller
    {
        public ActionResult Index()
        {
            using (var ctx = new ApplicationDbContext())
                return View(ctx.Suppliers.OrderBy(s => s.Name).ToList());
        }

        public ActionResult Create() => View(new Supplier());

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(Supplier model)
        {
            if (!ModelState.IsValid) return View(model);
            using (var ctx = new ApplicationDbContext())
            {
                model.CreatedAt = DateTime.Now;
                ctx.Suppliers.Add(model);
                AuditService.Log(ctx, SessionHelper.UserId, "CREATE",
                    "SUPPLIERS", null, null, model.Name);
                ctx.SaveChanges();
                TempData["Success"] = $"Supplier '{model.Name}' added.";
                return RedirectToAction("Index");
            }
        }

        public ActionResult Edit(int id)
        {
            using (var ctx = new ApplicationDbContext())
            {
                var s = ctx.Suppliers.Find(id);
                if (s == null) return HttpNotFound();
                return View(s);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(Supplier model)
        {
            if (!ModelState.IsValid) return View(model);
            using (var ctx = new ApplicationDbContext())
            {
                var s = ctx.Suppliers.Find(model.SupplierId);
                if (s == null) return HttpNotFound();
                s.Name = model.Name; s.ContactPerson = model.ContactPerson;
                s.Phone = model.Phone; s.Email = model.Email; s.Address = model.Address;
                AuditService.Log(ctx, SessionHelper.UserId, "UPDATE",
                    "SUPPLIERS", s.SupplierId, null, s.Name);
                ctx.SaveChanges();
                TempData["Success"] = "Supplier updated.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            using (var ctx = new ApplicationDbContext())
            {
                if (ctx.PurchaseOrders.Any(po => po.SupplierId == id))
                {
                    TempData["Error"] = "Cannot delete: supplier has purchase orders.";
                    return RedirectToAction("Index");
                }
                var s = ctx.Suppliers.Find(id);
                if (s == null) return HttpNotFound();
                ctx.Suppliers.Remove(s);
                AuditService.Log(ctx, SessionHelper.UserId, "DELETE",
                    "SUPPLIERS", id, s.Name, null);
                ctx.SaveChanges();
                TempData["Success"] = "Supplier deleted.";
                return RedirectToAction("Index");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PURCHASE ORDERS CONTROLLER
    // ══════════════════════════════════════════════════════════════════════════
    public class PurchaseOrdersController : Controller
    {
        public ActionResult Index()
        {
            using (var ctx = new ApplicationDbContext())
                return View(ctx.PurchaseOrders
                    .Include(po => po.Supplier)
                    .Include(po => po.User)
                    .Include(po => po.Items)
                    .OrderByDescending(po => po.OrderDate).ToList());
        }

        public ActionResult Details(int id)
        {
            using (var ctx = new ApplicationDbContext())
            {
                var po = ctx.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.User)
                    .Include(p => p.Items.Select(i => i.Product))
                    .FirstOrDefault(p => p.PoId == id);
                if (po == null) return HttpNotFound();
                return View(po);
            }
        }

        public ActionResult Create()
        {
            using (var ctx = new ApplicationDbContext())
            {
                ViewBag.Suppliers = new SelectList(
                    ctx.Suppliers.OrderBy(s => s.Name), "SupplierId", "Name");
                ViewBag.Products  = ctx.Products
                    .Include(p => p.Category)
                    .OrderBy(p => p.Name).ToList();
                return View(new PurchaseOrder());
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(int supplierId, DateTime? expectedDelivery,
                                   string notes,
                                   int[] productIds, int[] quantities, decimal[] unitCosts)
        {
            if (productIds == null || productIds.Length == 0)
            {
                TempData["Error"] = "Add at least one item.";
                return RedirectToAction("Create");
            }

            using (var ctx = new ApplicationDbContext())
            {
                var po = new PurchaseOrder
                {
                    SupplierId       = supplierId,
                    UserId           = SessionHelper.UserId,
                    OrderDate        = DateTime.Now,
                    ExpectedDelivery = expectedDelivery,
                    Status           = "Pending",
                    Notes            = notes,
                    CreatedAt        = DateTime.Now,
                    Items            = new List<OrderItem>()
                };

                decimal total = 0;
                for (int i = 0; i < productIds.Length; i++)
                {
                    po.Items.Add(new OrderItem
                    {
                        ProductId = productIds[i],
                        Quantity  = quantities[i],
                        UnitCost  = unitCosts[i]
                    });
                    total += quantities[i] * unitCosts[i];
                }
                po.TotalAmount = total;

                ctx.PurchaseOrders.Add(po);
                AuditService.Log(ctx, SessionHelper.UserId, "CREATE",
                    "PURCHASE_ORDERS", null, null,
                    $"PO for supplier {supplierId}, Rs {total:N0}");
                ctx.SaveChanges();

                TempData["Success"] = $"Purchase Order PO-{po.PoId:D3} created.";
                return RedirectToAction("Details", new { id = po.PoId });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string status)
        {
            using (var ctx = new ApplicationDbContext())
            {
                var po = ctx.PurchaseOrders
                    .Include(p => p.Items.Select(i => i.Product))
                    .FirstOrDefault(p => p.PoId == id);
                if (po == null) return HttpNotFound();

                string old = po.Status;
                po.Status = status;

                if (status == "Received")
                {
                    foreach (var item in po.Items)
                        StockService.AddStock(ctx, item.ProductId, item.Quantity,
                            SessionHelper.UserId, $"PO-{id:D3}");
                }

                AuditService.Log(ctx, SessionHelper.UserId, "UPDATE",
                    "PURCHASE_ORDERS", id, old, status);
                ctx.SaveChanges();

                TempData["Success"] = $"Order status updated to {status}" +
                    (status == "Received" ? " — stock updated." : ".");
                return RedirectToAction("Details", new { id });
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SALES CONTROLLER
    // ══════════════════════════════════════════════════════════════════════════
    public class SalesController : Controller
    {
        public ActionResult Index()
        {
            using (var ctx = new ApplicationDbContext())
                return View(ctx.Sales
                    .Include(s => s.User)
                    .Include(s => s.Items)
                    .OrderByDescending(s => s.SaleDate).ToList());
        }

        public ActionResult Details(int id)
        {
            using (var ctx = new ApplicationDbContext())
            {
                var s = ctx.Sales
                    .Include(x => x.User)
                    .Include(x => x.Items.Select(i => i.Product))
                    .FirstOrDefault(x => x.SaleId == id);
                if (s == null) return HttpNotFound();
                return View(s);
            }
        }

        public ActionResult Create()
        {
            using (var ctx = new ApplicationDbContext())
            {
                ViewBag.Products = ctx.Products
                    .Include(p => p.Category)
                    .Where(p => p.Quantity > 0)
                    .OrderBy(p => p.Name).ToList();
                return View(new Sale());
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(string paymentMethod, string notes,
                                   int[] productIds, int[] quantities)
        {
            if (productIds == null || productIds.Length == 0)
            {
                TempData["Error"] = "Add at least one item.";
                return RedirectToAction("Create");
            }

            using (var ctx = new ApplicationDbContext())
            {
                var sale = new Sale
                {
                    UserId        = SessionHelper.UserId,
                    SaleDate      = DateTime.Now,
                    PaymentMethod = paymentMethod ?? "Cash",
                    Notes         = notes,
                    CreatedAt     = DateTime.Now,
                    Items         = new List<SaleItem>()
                };

                decimal total = 0;
                for (int i = 0; i < productIds.Length; i++)
                {
                    var prod = ctx.Products.Find(productIds[i]);
                    if (prod == null) continue;

                    if (prod.Quantity < quantities[i])
                    {
                        TempData["Error"] = $"Insufficient stock for '{prod.Name}'. Available: {prod.Quantity}";
                        return RedirectToAction("Create");
                    }

                    sale.Items.Add(new SaleItem
                    {
                        ProductId = prod.ProductId,
                        Quantity  = quantities[i],
                        UnitPrice = prod.UnitPrice
                    });
                    total += quantities[i] * prod.UnitPrice;

                    // Deduct stock
                    prod.Quantity -= quantities[i];
                    prod.UpdatedAt = DateTime.Now;
                }

                sale.TotalAmount = total;
                ctx.Sales.Add(sale);
                AuditService.Log(ctx, SessionHelper.UserId, "CREATE",
                    "SALES", null, null, $"Sale Rs {total:N0}");
                ctx.SaveChanges();

                // Record stock transactions with sale reference
                foreach (var item in sale.Items)
                {
                    ctx.StockTransactions.Add(new StockTransaction
                    {
                        ProductId = item.ProductId,
                        UserId    = SessionHelper.UserId,
                        TxType    = "OUT",
                        Quantity  = item.Quantity,
                        Reference = $"SALE-{sale.SaleId:D3}",
                        TxDate    = DateTime.Now
                    });
                }
                ctx.SaveChanges();

                TempData["Success"] = $"Sale SALE-{sale.SaleId:D3} recorded — Rs {total:N0}.";
                return RedirectToAction("Details", new { id = sale.SaleId });
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  USERS CONTROLLER  (Admin only)
    // ══════════════════════════════════════════════════════════════════════════
    public class UsersController : Controller
    {
        public ActionResult Index()
        {
            using (var ctx = new ApplicationDbContext())
                return View(ctx.Users.OrderBy(u => u.Username).ToList());
        }

        public ActionResult Create() => View(new User());

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(User model, string password)
        {
            if (string.IsNullOrEmpty(password))
                ModelState.AddModelError("", "Password is required.");
            if (!ModelState.IsValid) return View(model);

            using (var ctx = new ApplicationDbContext())
            {
                if (ctx.Users.Any(u => u.Username == model.Username))
                {
                    ModelState.AddModelError("Username", "Username already exists.");
                    return View(model);
                }
                model.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                model.CreatedAt    = DateTime.Now;
                model.IsActive     = true;
                ctx.Users.Add(model);
                AuditService.Log(ctx, SessionHelper.UserId, "CREATE",
                    "USERS", null, null, model.Username);
                ctx.SaveChanges();
                TempData["Success"] = $"User '{model.Username}' created.";
                return RedirectToAction("Index");
            }
        }

        public ActionResult Edit(int id)
        {
            using (var ctx = new ApplicationDbContext())
            {
                var u = ctx.Users.Find(id);
                if (u == null) return HttpNotFound();
                return View(u);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(User model, string newPassword)
        {
            using (var ctx = new ApplicationDbContext())
            {
                var u = ctx.Users.Find(model.UserId);
                if (u == null) return HttpNotFound();
                u.Username = model.Username;
                u.Email    = model.Email;
                u.Role     = model.Role;
                u.IsActive = model.IsActive;
                if (!string.IsNullOrEmpty(newPassword))
                    u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                AuditService.Log(ctx, SessionHelper.UserId, "UPDATE",
                    "USERS", u.UserId, null, u.Username);
                ctx.SaveChanges();
                TempData["Success"] = "User updated.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (id == SessionHelper.UserId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction("Index");
            }
            using (var ctx = new ApplicationDbContext())
            {
                var u = ctx.Users.Find(id);
                if (u == null) return HttpNotFound();
                u.IsActive = false;  // soft-delete
                AuditService.Log(ctx, SessionHelper.UserId, "DELETE",
                    "USERS", id, u.Username, "Deactivated");
                ctx.SaveChanges();
                TempData["Success"] = $"User '{u.Username}' deactivated.";
                return RedirectToAction("Index");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  REPORTS CONTROLLER
    // ══════════════════════════════════════════════════════════════════════════
    public class ReportsController : Controller
    {
        public ActionResult Index() => View();

        public ActionResult StockReport()
        {
            using (var ctx = new ApplicationDbContext())
                return View(ctx.Products.Include(p => p.Category)
                    .OrderBy(p => p.Name).ToList());
        }

        public ActionResult SalesReport(DateTime? from, DateTime? to)
        {
            from ??= DateTime.Today.AddDays(-30);
            to   ??= DateTime.Today;
            ViewBag.From = from.Value.ToString("yyyy-MM-dd");
            ViewBag.To   = to.Value.ToString("yyyy-MM-dd");

            using (var ctx = new ApplicationDbContext())
                return View(ctx.Sales
                    .Include(s => s.User)
                    .Include(s => s.Items)
                    .Where(s => s.SaleDate >= from && s.SaleDate <= to.Value.AddDays(1))
                    .OrderByDescending(s => s.SaleDate).ToList());
        }

        public ActionResult TransactionReport()
        {
            using (var ctx = new ApplicationDbContext())
                return View(ctx.StockTransactions
                    .Include(t => t.Product)
                    .Include(t => t.User)
                    .OrderByDescending(t => t.TxDate).ToList());
        }

        public ActionResult AuditLog()
        {
            using (var ctx = new ApplicationDbContext())
                return View(ctx.AuditLogs
                    .Include(a => a.User)
                    .OrderByDescending(a => a.LogTimestamp).ToList());
        }
    }
}
