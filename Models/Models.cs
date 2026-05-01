using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StoreInventory.Models
{
    // ══════════════════════════════════════════════════════════════════════════
    //  USER
    //  Oracle table: USERS
    //  NOTE: Oracle column names are UPPERCASE by convention.
    //        [Column("NAME")] attributes enforce this everywhere.
    // ══════════════════════════════════════════════════════════════════════════
    [Table("USERS")]
    public class User
    {
        [Key, Column("USER_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [Required, MaxLength(50), Column("USERNAME")]
        public string Username { get; set; }

        [Required, MaxLength(150), Column("EMAIL")]
        [EmailAddress]
        public string Email { get; set; }

        [Required, MaxLength(256), Column("PASSWORD_HASH")]
        public string PasswordHash { get; set; }

        [Required, MaxLength(20), Column("ROLE")]
        public string Role { get; set; }   // Admin | Manager | Staff

        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("LAST_LOGIN")]
        public DateTime? LastLogin { get; set; }

        [Column("IS_ACTIVE")]
        public bool IsActive { get; set; } = true;

        // Navigation
        public virtual ICollection<Sale>           Sales           { get; set; }
        public virtual ICollection<PurchaseOrder>  PurchaseOrders  { get; set; }
        public virtual ICollection<AuditLog>       AuditLogs       { get; set; }
        public virtual ICollection<StockTransaction> StockTransactions { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CATEGORY
    // ══════════════════════════════════════════════════════════════════════════
    [Table("CATEGORIES")]
    public class Category
    {
        [Key, Column("CATEGORY_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CategoryId { get; set; }

        [Required, MaxLength(100), Column("NAME")]
        public string Name { get; set; }

        [MaxLength(500), Column("DESCRIPTION")]
        public string Description { get; set; }

        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public virtual ICollection<Product> Products { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SUPPLIER
    // ══════════════════════════════════════════════════════════════════════════
    [Table("SUPPLIERS")]
    public class Supplier
    {
        [Key, Column("SUPPLIER_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SupplierId { get; set; }

        [Required, MaxLength(150), Column("NAME")]
        public string Name { get; set; }

        [MaxLength(100), Column("CONTACT_PERSON")]
        public string ContactPerson { get; set; }

        [MaxLength(30), Column("PHONE")]
        public string Phone { get; set; }

        [MaxLength(150), Column("EMAIL")]
        [EmailAddress]
        public string Email { get; set; }

        [MaxLength(300), Column("ADDRESS")]
        public string Address { get; set; }

        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PRODUCT
    // ══════════════════════════════════════════════════════════════════════════
    [Table("PRODUCTS")]
    public class Product
    {
        [Key, Column("PRODUCT_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductId { get; set; }

        [Required, MaxLength(200), Column("NAME")]
        public string Name { get; set; }

        [Required, MaxLength(50), Column("SKU")]
        public string SKU { get; set; }

        [MaxLength(50), Column("BARCODE")]
        public string Barcode { get; set; }

        [Column("CATEGORY_ID")]
        public int CategoryId { get; set; }

        [Required, Column("UNIT_PRICE"), DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Required, Column("COST_PRICE"), DataType(DataType.Currency)]
        public decimal CostPrice { get; set; }

        [Required, Column("QUANTITY")]
        public int Quantity { get; set; }

        [Required, Column("MIN_STOCK")]
        public int MinStock { get; set; }

        [MaxLength(1000), Column("DESCRIPTION")]
        public string Description { get; set; }

        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("UPDATED_AT")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public bool IsLowStock => Quantity <= MinStock;

        [NotMapped]
        public decimal StockValue => Quantity * CostPrice;

        // Navigation
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        public virtual ICollection<OrderItem>       OrderItems        { get; set; }
        public virtual ICollection<SaleItem>        SaleItems         { get; set; }
        public virtual ICollection<StockTransaction> StockTransactions { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PURCHASE ORDER
    // ══════════════════════════════════════════════════════════════════════════
    [Table("PURCHASE_ORDERS")]
    public class PurchaseOrder
    {
        [Key, Column("PO_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PoId { get; set; }

        [Column("SUPPLIER_ID")]
        public int SupplierId { get; set; }

        [Column("USER_ID")]
        public int UserId { get; set; }

        [Column("ORDER_DATE")]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Column("EXPECTED_DELIVERY")]
        public DateTime? ExpectedDelivery { get; set; }

        [Required, MaxLength(20), Column("STATUS")]
        public string Status { get; set; } = "Pending"; // Pending|Approved|Received|Cancelled

        [Column("TOTAL_AMOUNT")]
        public decimal TotalAmount { get; set; }

        [MaxLength(500), Column("NOTES")]
        public string Notes { get; set; }

        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        public virtual ICollection<OrderItem> Items { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ORDER ITEM  (PO line)
    // ══════════════════════════════════════════════════════════════════════════
    [Table("ORDER_ITEMS")]
    public class OrderItem
    {
        [Key, Column("ITEM_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ItemId { get; set; }

        [Column("PO_ID")]
        public int PoId { get; set; }

        [Column("PRODUCT_ID")]
        public int ProductId { get; set; }

        [Required, Column("QUANTITY")]
        public int Quantity { get; set; }

        [Required, Column("UNIT_COST")]
        public decimal UnitCost { get; set; }

        [NotMapped]
        public decimal Subtotal => Quantity * UnitCost;

        // Navigation
        [ForeignKey("PoId")]
        public virtual PurchaseOrder PurchaseOrder { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SALE
    // ══════════════════════════════════════════════════════════════════════════
    [Table("SALES")]
    public class Sale
    {
        [Key, Column("SALE_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SaleId { get; set; }

        [Column("USER_ID")]
        public int UserId { get; set; }

        [Column("SALE_DATE")]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        [Column("TOTAL_AMOUNT")]
        public decimal TotalAmount { get; set; }

        [Required, MaxLength(20), Column("PAYMENT_METHOD")]
        public string PaymentMethod { get; set; } = "Cash"; // Cash|Card|Transfer

        [MaxLength(500), Column("NOTES")]
        public string Notes { get; set; }

        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        public virtual ICollection<SaleItem> Items { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SALE ITEM
    // ══════════════════════════════════════════════════════════════════════════
    [Table("SALE_ITEMS")]
    public class SaleItem
    {
        [Key, Column("ITEM_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ItemId { get; set; }

        [Column("SALE_ID")]
        public int SaleId { get; set; }

        [Column("PRODUCT_ID")]
        public int ProductId { get; set; }

        [Required, Column("QUANTITY")]
        public int Quantity { get; set; }

        [Required, Column("UNIT_PRICE")]
        public decimal UnitPrice { get; set; }

        [NotMapped]
        public decimal Subtotal => Quantity * UnitPrice;

        // Navigation
        [ForeignKey("SaleId")]
        public virtual Sale Sale { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  STOCK TRANSACTION
    // ══════════════════════════════════════════════════════════════════════════
    [Table("STOCK_TRANSACTIONS")]
    public class StockTransaction
    {
        [Key, Column("TX_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TxId { get; set; }

        [Column("PRODUCT_ID")]
        public int ProductId { get; set; }

        [Column("USER_ID")]
        public int UserId { get; set; }

        [Required, MaxLength(10), Column("TX_TYPE")]
        public string TxType { get; set; }  // IN | OUT | ADJUSTMENT

        [Required, Column("QUANTITY")]
        public int Quantity { get; set; }

        [MaxLength(50), Column("REFERENCE")]
        public string Reference { get; set; }

        [MaxLength(300), Column("NOTES")]
        public string Notes { get; set; }

        [Column("TX_DATE")]
        public DateTime TxDate { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  AUDIT LOG
    // ══════════════════════════════════════════════════════════════════════════
    [Table("AUDIT_LOGS")]
    public class AuditLog
    {
        [Key, Column("LOG_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; }

        [Column("USER_ID")]
        public int UserId { get; set; }

        [Required, MaxLength(20), Column("ACTION")]
        public string Action { get; set; }  // CREATE | UPDATE | DELETE | LOGIN

        [Required, MaxLength(50), Column("TABLE_NAME")]
        public string TableName { get; set; }

        [Column("RECORD_ID")]
        public int? RecordId { get; set; }

        [MaxLength(500), Column("OLD_VALUE")]
        public string OldValue { get; set; }

        [MaxLength(500), Column("NEW_VALUE")]
        public string NewValue { get; set; }

        [MaxLength(50), Column("IP_ADDRESS")]
        public string IpAddress { get; set; }

        [Column("LOG_TIMESTAMP")]
        public DateTime LogTimestamp { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  VIEW MODELS  (not mapped to DB tables)
    // ══════════════════════════════════════════════════════════════════════════
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }

    public class DashboardViewModel
    {
        public int     TotalProducts    { get; set; }
        public int     LowStockCount    { get; set; }
        public int     TotalCategories  { get; set; }
        public int     TotalSuppliers   { get; set; }
        public decimal TodaySales       { get; set; }
        public decimal MonthSales       { get; set; }
        public int     PendingOrders    { get; set; }
        public int     TotalUsers       { get; set; }
        public decimal InventoryValue   { get; set; }

        public List<Product>          LowStockProducts   { get; set; } = new List<Product>();
        public List<Sale>             RecentSales        { get; set; } = new List<Sale>();
        public List<StockTransaction> RecentTransactions { get; set; } = new List<StockTransaction>();
        public List<ChartPoint>       SalesChart         { get; set; } = new List<ChartPoint>();
        public List<ChartPoint>       TopProducts        { get; set; } = new List<ChartPoint>();
        public List<ChartPoint>       CategoryStock      { get; set; } = new List<ChartPoint>();
    }

    public class ChartPoint
    {
        public string  Label { get; set; }
        public decimal Value { get; set; }
    }

    public class SaleCreateViewModel
    {
        public string PaymentMethod { get; set; } = "Cash";
        public string Notes         { get; set; }
        public List<SaleLineItem> Lines { get; set; } = new List<SaleLineItem>();
    }

    public class SaleLineItem
    {
        public int     ProductId { get; set; }
        public int     Quantity  { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class PoCreateViewModel
    {
        public int      SupplierId       { get; set; }
        public DateTime? ExpectedDelivery { get; set; }
        public string   Notes            { get; set; }
        public List<PoLineItem> Lines    { get; set; } = new List<PoLineItem>();
    }

    public class PoLineItem
    {
        public int     ProductId { get; set; }
        public int     Quantity  { get; set; }
        public decimal UnitCost  { get; set; }
    }
}
