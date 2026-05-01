using System;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using StoreInventory.Models;

namespace StoreInventory.DAL
{
    // ══════════════════════════════════════════════════════════════════════════
    //  ApplicationDbContext
    //  Uses Oracle.ManagedDataAccess.EntityFramework provider.
    //  Tables are created in the schema defined by OracleSchema in Web.config.
    // ══════════════════════════════════════════════════════════════════════════
    public class ApplicationDbContext : DbContext
    {
        // ── Schema name (uppercase) – read from Web.config appSettings ────────
        public static readonly string Schema =
            ConfigurationManager.AppSettings["OracleSchema"] ?? "STOREINV";

        public ApplicationDbContext()
            : base("name=StoreInventoryContext")
        {
            // Disable lazy loading to avoid N+1 in views; use explicit .Include()
            this.Configuration.LazyLoadingEnabled  = false;
            this.Configuration.ProxyCreationEnabled = false;
        }

        // ── DbSets ─────────────────────────────────────────────────────────────
        public DbSet<User>             Users             { get; set; }
        public DbSet<Category>         Categories        { get; set; }
        public DbSet<Supplier>         Suppliers         { get; set; }
        public DbSet<Product>          Products          { get; set; }
        public DbSet<PurchaseOrder>    PurchaseOrders    { get; set; }
        public DbSet<OrderItem>        OrderItems        { get; set; }
        public DbSet<Sale>             Sales             { get; set; }
        public DbSet<SaleItem>         SaleItems         { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }
        public DbSet<AuditLog>         AuditLogs         { get; set; }

        // ── Model Builder ──────────────────────────────────────────────────────
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Oracle does not support plural table names auto-convention
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            // ── Set Oracle schema for all entities ────────────────────────────
            modelBuilder.HasDefaultSchema(Schema);

            // ── USER ──────────────────────────────────────────────────────────
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username).IsUnique().HasName("UX_USERS_USERNAME");
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique().HasName("UX_USERS_EMAIL");
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .IsRequired()
                .HasMaxLength(20);

            // ── CATEGORY ──────────────────────────────────────────────────────
            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Name).IsUnique().HasName("UX_CATEGORIES_NAME");

            // ── PRODUCT ───────────────────────────────────────────────────────
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SKU).IsUnique().HasName("UX_PRODUCTS_SKU");
            modelBuilder.Entity<Product>()
                .Property(p => p.UnitPrice)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Product>()
                .Property(p => p.CostPrice)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .WillCascadeOnDelete(false);

            // ── PURCHASE ORDER ────────────────────────────────────────────────
            modelBuilder.Entity<PurchaseOrder>()
                .Property(po => po.TotalAmount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseOrder>()
                .HasRequired(po => po.Supplier)
                .WithMany(s => s.PurchaseOrders)
                .HasForeignKey(po => po.SupplierId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<PurchaseOrder>()
                .HasRequired(po => po.User)
                .WithMany(u => u.PurchaseOrders)
                .HasForeignKey(po => po.UserId)
                .WillCascadeOnDelete(false);

            // ── ORDER ITEM ────────────────────────────────────────────────────
            modelBuilder.Entity<OrderItem>()
                .Property(i => i.UnitCost)
                .HasPrecision(18, 2);
            modelBuilder.Entity<OrderItem>()
                .HasRequired(i => i.PurchaseOrder)
                .WithMany(po => po.Items)
                .HasForeignKey(i => i.PoId)
                .WillCascadeOnDelete(true);  // cascade delete items when PO deleted
            modelBuilder.Entity<OrderItem>()
                .HasRequired(i => i.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(i => i.ProductId)
                .WillCascadeOnDelete(false);

            // ── SALE ──────────────────────────────────────────────────────────
            modelBuilder.Entity<Sale>()
                .Property(s => s.TotalAmount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Sale>()
                .HasRequired(s => s.User)
                .WithMany(u => u.Sales)
                .HasForeignKey(s => s.UserId)
                .WillCascadeOnDelete(false);

            // ── SALE ITEM ─────────────────────────────────────────────────────
            modelBuilder.Entity<SaleItem>()
                .Property(i => i.UnitPrice)
                .HasPrecision(18, 2);
            modelBuilder.Entity<SaleItem>()
                .HasRequired(i => i.Sale)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.SaleId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<SaleItem>()
                .HasRequired(i => i.Product)
                .WithMany(p => p.SaleItems)
                .HasForeignKey(i => i.ProductId)
                .WillCascadeOnDelete(false);

            // ── STOCK TRANSACTION ─────────────────────────────────────────────
            modelBuilder.Entity<StockTransaction>()
                .HasRequired(t => t.Product)
                .WithMany(p => p.StockTransactions)
                .HasForeignKey(t => t.ProductId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<StockTransaction>()
                .HasRequired(t => t.User)
                .WithMany(u => u.StockTransactions)
                .HasForeignKey(t => t.UserId)
                .WillCascadeOnDelete(false);

            // ── AUDIT LOG ─────────────────────────────────────────────────────
            modelBuilder.Entity<AuditLog>()
                .HasRequired(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .WillCascadeOnDelete(false);

            base.OnModelCreating(modelBuilder);
        }

        // ── Factory helper ─────────────────────────────────────────────────────
        public static ApplicationDbContext Create() => new ApplicationDbContext();
    }
}
