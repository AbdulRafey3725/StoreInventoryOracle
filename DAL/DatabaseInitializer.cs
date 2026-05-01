using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Oracle.ManagedDataAccess.Client;
using StoreInventory.Models;

namespace StoreInventory.DAL
{
    // ══════════════════════════════════════════════════════════════════════════
    //  OracleDatabaseInitializer
    //
    //  Strategy: CreateDatabaseIfNotExists
    //  On first run (or when RecreateDatabase=true in Web.config):
    //    1. EF creates / migrates all tables in the Oracle schema
    //    2. Oracle SEQUENCES are created for each PK (EF6 + Oracle needs them)
    //    3. BEFORE INSERT TRIGGERS wire each sequence to its table PK
    //    4. Seed data is inserted
    //
    //  This class is registered in Global.asax via:
    //    Database.SetInitializer(new OracleDatabaseInitializer());
    // ══════════════════════════════════════════════════════════════════════════
    public class OracleDatabaseInitializer
        : CreateDatabaseIfNotExists<ApplicationDbContext>
    {
        private readonly string _schema = ApplicationDbContext.Schema;

        // ── Called by EF after it creates the database schema ─────────────────
        protected override void Seed(ApplicationDbContext ctx)
        {
            CreateSequencesAndTriggers(ctx);
            SeedUsers(ctx);
            SeedCategories(ctx);
            SeedSuppliers(ctx);
            SeedProducts(ctx);
            SeedPurchaseOrders(ctx);
            SeedSales(ctx);
            ctx.SaveChanges();
        }

        // ──────────────────────────────────────────────────────────────────────
        //  SEQUENCES + TRIGGERS
        //  Oracle 12c+ supports IDENTITY columns via EF, but using explicit
        //  sequences keeps it compatible with Oracle 11g and XE.
        // ──────────────────────────────────────────────────────────────────────
        private void CreateSequencesAndTriggers(ApplicationDbContext ctx)
        {
            var conn = (OracleConnection)ctx.Database.Connection;
            bool opened = conn.State != System.Data.ConnectionState.Open;
            if (opened) conn.Open();

            var tables = new[]
            {
                ("USERS",             "USER_ID",    "SEQ_USERS"),
                ("CATEGORIES",        "CATEGORY_ID","SEQ_CATEGORIES"),
                ("SUPPLIERS",         "SUPPLIER_ID","SEQ_SUPPLIERS"),
                ("PRODUCTS",          "PRODUCT_ID", "SEQ_PRODUCTS"),
                ("PURCHASE_ORDERS",   "PO_ID",      "SEQ_PURCHASE_ORDERS"),
                ("ORDER_ITEMS",       "ITEM_ID",    "SEQ_ORDER_ITEMS"),
                ("SALES",             "SALE_ID",    "SEQ_SALES"),
                ("SALE_ITEMS",        "ITEM_ID",    "SEQ_SALE_ITEMS"),
                ("STOCK_TRANSACTIONS","TX_ID",      "SEQ_STOCK_TX"),
                ("AUDIT_LOGS",        "LOG_ID",     "SEQ_AUDIT_LOGS"),
            };

            foreach (var (table, col, seq) in tables)
            {
                // Create sequence if not exists
                ExecuteSafe(conn, $@"
                    DECLARE
                        v_cnt NUMBER;
                    BEGIN
                        SELECT COUNT(*) INTO v_cnt
                          FROM all_sequences
                         WHERE sequence_owner = '{_schema}'
                           AND sequence_name  = '{seq}';
                        IF v_cnt = 0 THEN
                            EXECUTE IMMEDIATE
                                'CREATE SEQUENCE {_schema}.{seq}
                                    START WITH 1
                                    INCREMENT BY 1
                                    NOCACHE
                                    NOCYCLE';
                        END IF;
                    END;");

                // Create BEFORE INSERT trigger if not exists
                string trigName = $"TRG_{table}_BI";
                ExecuteSafe(conn, $@"
                    DECLARE
                        v_cnt NUMBER;
                    BEGIN
                        SELECT COUNT(*) INTO v_cnt
                          FROM all_triggers
                         WHERE owner        = '{_schema}'
                           AND trigger_name = '{trigName}';
                        IF v_cnt = 0 THEN
                            EXECUTE IMMEDIATE
                                'CREATE OR REPLACE TRIGGER {_schema}.{trigName}
                                    BEFORE INSERT ON {_schema}.{table}
                                    FOR EACH ROW
                                    WHEN (NEW.{col} IS NULL OR NEW.{col} = 0)
                                 BEGIN
                                    :NEW.{col} := {_schema}.{seq}.NEXTVAL;
                                 END;';
                        END IF;
                    END;");
            }

            if (opened) conn.Close();
        }

        private static void ExecuteSafe(OracleConnection conn, string sql)
        {
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // Log but don't abort seed – sequence/trigger may already exist
                System.Diagnostics.Debug.WriteLine($"[OracleInit] {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        //  SEED DATA
        // ──────────────────────────────────────────────────────────────────────
        private void SeedUsers(ApplicationDbContext ctx)
        {
            if (ctx.Users.Any()) return;

            ctx.Users.AddRange(new[]
            {
                new User { Username="admin",   Email="admin@store.com",
                           PasswordHash=BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                           Role="Admin",   CreatedAt=DateTime.Now, IsActive=true },
                new User { Username="manager", Email="manager@store.com",
                           PasswordHash=BCrypt.Net.BCrypt.HashPassword("Manager@123"),
                           Role="Manager", CreatedAt=DateTime.Now, IsActive=true },
                new User { Username="staff",   Email="staff@store.com",
                           PasswordHash=BCrypt.Net.BCrypt.HashPassword("Staff@123"),
                           Role="Staff",   CreatedAt=DateTime.Now, IsActive=true },
            });
            ctx.SaveChanges();
        }

        private void SeedCategories(ApplicationDbContext ctx)
        {
            if (ctx.Categories.Any()) return;

            ctx.Categories.AddRange(new[]
            {
                new Category { Name="Electronics",     Description="Electronic items and gadgets" },
                new Category { Name="Clothing",        Description="Apparel and fashion accessories" },
                new Category { Name="Food & Grocery",  Description="Perishables and dry goods" },
                new Category { Name="Stationery",      Description="Office and school supplies" },
                new Category { Name="Furniture",       Description="Home and office furniture" },
            });
            ctx.SaveChanges();
        }

        private void SeedSuppliers(ApplicationDbContext ctx)
        {
            if (ctx.Suppliers.Any()) return;

            ctx.Suppliers.AddRange(new[]
            {
                new Supplier { Name="TechSource Ltd",  ContactPerson="Ali Raza",    Phone="0300-1234567", Email="ali@techsource.com",    Address="Lahore, Punjab" },
                new Supplier { Name="Fashion Hub",     ContactPerson="Sara Khan",   Phone="0321-9876543", Email="sara@fashionhub.com",   Address="Karachi, Sindh" },
                new Supplier { Name="GroceryCo",       ContactPerson="Usman Malik", Phone="0333-5556677", Email="usman@groceryco.com",   Address="Rawalpindi" },
                new Supplier { Name="OfficeWorld",     ContactPerson="Fatima Noor", Phone="0311-4445566", Email="fatima@officeworld.pk", Address="Islamabad" },
            });
            ctx.SaveChanges();
        }

        private void SeedProducts(ApplicationDbContext ctx)
        {
            if (ctx.Products.Any()) return;

            int elec = ctx.Categories.First(c => c.Name == "Electronics").CategoryId;
            int clth = ctx.Categories.First(c => c.Name == "Clothing").CategoryId;
            int food = ctx.Categories.First(c => c.Name == "Food & Grocery").CategoryId;
            int stat = ctx.Categories.First(c => c.Name == "Stationery").CategoryId;
            int furn = ctx.Categories.First(c => c.Name == "Furniture").CategoryId;

            ctx.Products.AddRange(new[]
            {
                new Product { Name="Samsung Galaxy A54",   SKU="EL-001", Barcode="8801643870096", CategoryId=elec, UnitPrice=65000,  CostPrice=55000,  Quantity=15,  MinStock=5,  Description="6.4\" AMOLED smartphone" },
                new Product { Name="iPhone 15",            SKU="EL-002", Barcode="0194253716952", CategoryId=elec, UnitPrice=290000, CostPrice=260000, Quantity=3,   MinStock=5,  Description="Apple iPhone 15 128GB" },
                new Product { Name="Laptop Dell Inspiron", SKU="EL-003", Barcode="0191120451273", CategoryId=elec, UnitPrice=120000, CostPrice=100000, Quantity=8,   MinStock=3,  Description="Dell Inspiron 15 Core i5" },
                new Product { Name="USB-C Charging Cable", SKU="EL-004", Barcode="0194252729723", CategoryId=elec, UnitPrice=500,    CostPrice=250,    Quantity=80,  MinStock=15, Description="2m braided USB-C cable" },
                new Product { Name="Men's Cotton Shirt",   SKU="CL-001", Barcode="6900123456789", CategoryId=clth, UnitPrice=1500,   CostPrice=900,    Quantity=50,  MinStock=10, Description="Premium cotton formal shirt" },
                new Product { Name="Women's Lawn Suit",    SKU="CL-002", Barcode="6900987654321", CategoryId=clth, UnitPrice=3500,   CostPrice=2200,   Quantity=30,  MinStock=10, Description="Printed 3-piece lawn suit" },
                new Product { Name="Basmati Rice 5kg",     SKU="FG-001", Barcode="7350011830155", CategoryId=food, UnitPrice=950,    CostPrice=750,    Quantity=100, MinStock=20, Description="Premium long-grain basmati" },
                new Product { Name="Cooking Oil 5L",       SKU="FG-002", Barcode="7350011830156", CategoryId=food, UnitPrice=1800,   CostPrice=1500,   Quantity=4,   MinStock=10, Description="Sunflower cooking oil" },
                new Product { Name="A4 Paper Ream",        SKU="ST-001", Barcode="5901234123457", CategoryId=stat, UnitPrice=800,    CostPrice=600,    Quantity=200, MinStock=30, Description="500 sheets 80gsm A4" },
                new Product { Name="Office Chair",         SKU="FU-001", Barcode="6941453320015", CategoryId=furn, UnitPrice=18000,  CostPrice=13000,  Quantity=7,   MinStock=2,  Description="Ergonomic mesh office chair" },
            });
            ctx.SaveChanges();
        }

        private void SeedPurchaseOrders(ApplicationDbContext ctx)
        {
            if (ctx.PurchaseOrders.Any()) return;

            int adminId  = ctx.Users.First(u => u.Username == "admin").UserId;
            int mgrId    = ctx.Users.First(u => u.Username == "manager").UserId;
            int techSuppId = ctx.Suppliers.First(s => s.Name == "TechSource Ltd").SupplierId;
            int grocSuppId = ctx.Suppliers.First(s => s.Name == "GroceryCo").SupplierId;
            int offSuppId  = ctx.Suppliers.First(s => s.Name == "OfficeWorld").SupplierId;

            int iphoneId  = ctx.Products.First(p => p.SKU == "EL-002").ProductId;
            int samsungId = ctx.Products.First(p => p.SKU == "EL-001").ProductId;
            int riceId    = ctx.Products.First(p => p.SKU == "FG-001").ProductId;
            int oilId     = ctx.Products.First(p => p.SKU == "FG-002").ProductId;
            int paperId   = ctx.Products.First(p => p.SKU == "ST-001").ProductId;

            ctx.PurchaseOrders.AddRange(new[]
            {
                new PurchaseOrder {
                    SupplierId=techSuppId, UserId=adminId,
                    OrderDate=DateTime.Now.AddDays(-15),
                    ExpectedDelivery=DateTime.Now.AddDays(-8),
                    Status="Received", TotalAmount=330000,
                    Notes="Urgent restock",
                    Items = new List<OrderItem> {
                        new OrderItem { ProductId=iphoneId,  Quantity=2, UnitCost=260000 },
                        new OrderItem { ProductId=samsungId, Quantity=2, UnitCost=55000  },
                    }
                },
                new PurchaseOrder {
                    SupplierId=grocSuppId, UserId=mgrId,
                    OrderDate=DateTime.Now.AddDays(-5),
                    ExpectedDelivery=DateTime.Now.AddDays(2),
                    Status="Pending", TotalAmount=90000,
                    Notes="Monthly grocery order",
                    Items = new List<OrderItem> {
                        new OrderItem { ProductId=riceId, Quantity=60, UnitCost=750  },
                        new OrderItem { ProductId=oilId,  Quantity=20, UnitCost=1500 },
                    }
                },
                new PurchaseOrder {
                    SupplierId=offSuppId, UserId=mgrId,
                    OrderDate=DateTime.Now.AddDays(-2),
                    ExpectedDelivery=DateTime.Now.AddDays(3),
                    Status="Approved", TotalAmount=42000,
                    Notes="Stationery restock",
                    Items = new List<OrderItem> {
                        new OrderItem { ProductId=paperId, Quantity=70, UnitCost=600 },
                    }
                },
            });
            ctx.SaveChanges();
        }

        private void SeedSales(ApplicationDbContext ctx)
        {
            if (ctx.Sales.Any()) return;

            int staffId = ctx.Users.First(u => u.Username == "staff").UserId;
            int mgrId   = ctx.Users.First(u => u.Username == "manager").UserId;

            int samsungId = ctx.Products.First(p => p.SKU == "EL-001").ProductId;
            int iphoneId  = ctx.Products.First(p => p.SKU == "EL-002").ProductId;
            int shirtId   = ctx.Products.First(p => p.SKU == "CL-001").ProductId;
            int suitId    = ctx.Products.First(p => p.SKU == "CL-002").ProductId;
            int riceId    = ctx.Products.First(p => p.SKU == "FG-001").ProductId;
            int oilId     = ctx.Products.First(p => p.SKU == "FG-002").ProductId;
            int cableId   = ctx.Products.First(p => p.SKU == "EL-004").ProductId;

            ctx.Sales.AddRange(new[]
            {
                new Sale {
                    UserId=staffId, SaleDate=DateTime.Now.AddDays(-7),
                    TotalAmount=66500, PaymentMethod="Card",
                    Items = new List<SaleItem> {
                        new SaleItem { ProductId=samsungId, Quantity=1, UnitPrice=65000 },
                        new SaleItem { ProductId=cableId,   Quantity=3, UnitPrice=500   },
                    }
                },
                new Sale {
                    UserId=staffId, SaleDate=DateTime.Now.AddDays(-4),
                    TotalAmount=290000, PaymentMethod="Transfer",
                    Notes="Corporate sale",
                    Items = new List<SaleItem> {
                        new SaleItem { ProductId=iphoneId, Quantity=1, UnitPrice=290000 },
                    }
                },
                new Sale {
                    UserId=mgrId, SaleDate=DateTime.Now.AddDays(-2),
                    TotalAmount=7000, PaymentMethod="Cash",
                    Items = new List<SaleItem> {
                        new SaleItem { ProductId=shirtId, Quantity=2, UnitPrice=1500 },
                        new SaleItem { ProductId=suitId,  Quantity=1, UnitPrice=3500 },
                    }
                },
                new Sale {
                    UserId=staffId, SaleDate=DateTime.Now,
                    TotalAmount=5700, PaymentMethod="Cash",
                    Notes="Walk-in customer",
                    Items = new List<SaleItem> {
                        new SaleItem { ProductId=riceId,  Quantity=3, UnitPrice=950  },
                        new SaleItem { ProductId=oilId,   Quantity=1, UnitPrice=1800 },
                        new SaleItem { ProductId=cableId, Quantity=3, UnitPrice=500  },
                    }
                },
            });
            ctx.SaveChanges();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Drop-and-Recreate initializer (DEV only — set RecreateDatabase=true)
    // ══════════════════════════════════════════════════════════════════════════
    public class OracleDropCreateInitializer
        : DropCreateDatabaseAlways<ApplicationDbContext>
    {
        private readonly OracleDatabaseInitializer _inner = new OracleDatabaseInitializer();

        protected override void Seed(ApplicationDbContext ctx)
        {
            // reuse same seed logic via reflection workaround
            var method = typeof(OracleDatabaseInitializer)
                .GetMethod("Seed",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
            method?.Invoke(_inner, new object[] { ctx });
        }
    }
}
