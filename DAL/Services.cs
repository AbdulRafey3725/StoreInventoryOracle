using System;
using System.Web;
using StoreInventory.DAL;
using StoreInventory.Models;

namespace StoreInventory.DAL
{
    // ══════════════════════════════════════════════════════════════════════════
    //  AuditService  –  writes a row to AUDIT_LOGS
    // ══════════════════════════════════════════════════════════════════════════
    public static class AuditService
    {
        public static void Log(ApplicationDbContext ctx,
                               int    userId,
                               string action,
                               string tableName,
                               int?   recordId  = null,
                               string oldValue  = null,
                               string newValue  = null)
        {
            try
            {
                string ip = HttpContext.Current?.Request?.UserHostAddress;
                ctx.AuditLogs.Add(new AuditLog
                {
                    UserId       = userId,
                    Action       = action,
                    TableName    = tableName,
                    RecordId     = recordId,
                    OldValue     = oldValue,
                    NewValue     = newValue,
                    IpAddress    = ip,
                    LogTimestamp = DateTime.Now
                });
                // Do NOT call SaveChanges here – caller controls the transaction
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuditService] {ex.Message}");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  StockService  –  creates STOCK_TRANSACTIONS rows and adjusts quantities
    // ══════════════════════════════════════════════════════════════════════════
    public static class StockService
    {
        /// <summary>
        /// Deducts stock when a sale is recorded.
        /// Throws InvalidOperationException if stock is insufficient.
        /// </summary>
        public static void DeductStock(ApplicationDbContext ctx,
                                       int productId,
                                       int quantity,
                                       int userId,
                                       string reference)
        {
            var product = ctx.Products.Find(productId);
            if (product == null)
                throw new ArgumentException($"Product {productId} not found.");

            if (product.Quantity < quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock for '{product.Name}'. " +
                    $"Available: {product.Quantity}, Requested: {quantity}");

            product.Quantity  -= quantity;
            product.UpdatedAt  = DateTime.Now;

            ctx.StockTransactions.Add(new StockTransaction
            {
                ProductId = productId,
                UserId    = userId,
                TxType    = "OUT",
                Quantity  = quantity,
                Reference = reference,
                TxDate    = DateTime.Now
            });
        }

        /// <summary>
        /// Adds stock when a Purchase Order is marked Received.
        /// </summary>
        public static void AddStock(ApplicationDbContext ctx,
                                    int productId,
                                    int quantity,
                                    int userId,
                                    string reference,
                                    string notes = null)
        {
            var product = ctx.Products.Find(productId);
            if (product == null)
                throw new ArgumentException($"Product {productId} not found.");

            product.Quantity  += quantity;
            product.UpdatedAt  = DateTime.Now;

            ctx.StockTransactions.Add(new StockTransaction
            {
                ProductId = productId,
                UserId    = userId,
                TxType    = "IN",
                Quantity  = quantity,
                Reference = reference,
                Notes     = notes,
                TxDate    = DateTime.Now
            });
        }

        /// <summary>
        /// Manual stock adjustment (e.g. after physical stocktake).
        /// </summary>
        public static void AdjustStock(ApplicationDbContext ctx,
                                       int productId,
                                       int newQuantity,
                                       int userId,
                                       string notes)
        {
            var product = ctx.Products.Find(productId);
            if (product == null)
                throw new ArgumentException($"Product {productId} not found.");

            int diff = newQuantity - product.Quantity;
            product.Quantity  = newQuantity;
            product.UpdatedAt = DateTime.Now;

            ctx.StockTransactions.Add(new StockTransaction
            {
                ProductId = productId,
                UserId    = userId,
                TxType    = "ADJUSTMENT",
                Quantity  = Math.Abs(diff),
                Reference = $"ADJ-{DateTime.Now:yyyyMMddHHmmss}",
                Notes     = $"Manual adjustment ({(diff >= 0 ? "+" : "")}{diff}). {notes}",
                TxDate    = DateTime.Now
            });
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SessionHelper  –  typed session accessors
    // ══════════════════════════════════════════════════════════════════════════
    public static class SessionHelper
    {
        public static int    UserId   => (int)(HttpContext.Current.Session["UserId"] ?? 0);
        public static string Username => HttpContext.Current.Session["Username"]?.ToString() ?? "";
        public static string Role     => HttpContext.Current.Session["Role"]?.ToString() ?? "";
        public static bool   IsAdmin  => Role == "Admin";
        public static bool   IsMgr    => Role == "Admin" || Role == "Manager";

        public static void Set(int userId, string username, string role, string email)
        {
            HttpContext.Current.Session["UserId"]   = userId;
            HttpContext.Current.Session["Username"] = username;
            HttpContext.Current.Session["Role"]     = role;
            HttpContext.Current.Session["Email"]    = email;
        }

        public static void Clear() => HttpContext.Current.Session.Clear();
    }
}
