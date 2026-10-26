using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using WholesaleRetailStore.Models;

namespace WholesaleRetailStore.Data
{
    /// <summary>
    /// Application database context for the Wholesale Retail Store.
    /// Configures entity relationships, constraints, and database schema.
    /// Follows 3rd Normal Form principles for data integrity.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        /// <summary>
        /// Products available in the store inventory
        /// </summary>
        public DbSet<Product> Products { get; set; }

        /// <summary>
        /// Customers who can place orders (both Retail and Wholesale types)
        /// </summary>
        public DbSet<Customer> Customers { get; set; }

        /// <summary>
        /// Orders placed by customers
        /// </summary>
        public DbSet<Order> Orders { get; set; }

        /// <summary>
        /// Individual items within orders
        /// </summary>
        public DbSet<OrderItem> OrderItems { get; set; }

        /// <summary>
        /// Pricing rules that define discounts for different customer types
        /// </summary>
        public DbSet<PricingRule> PricingRules { get; set; }

        /// <summary>
        /// Configures entity relationships, constraints, and database schema
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Product Configuration
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Description).HasMaxLength(500);
                entity.Property(p => p.Stock).IsRequired().HasDefaultValue(0);
                entity.Property(p => p.BasePrice).IsRequired().HasColumnType("decimal(18,2)");
                entity.HasIndex(p => p.Name);
            });

            // Customer Configuration
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Email).IsRequired().HasMaxLength(255);
                entity.Property(c => c.CustomerType).IsRequired();
                entity.HasIndex(c => c.Email).IsUnique();
            });

            // Order Configuration
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);
                entity.Property(o => o.CreatedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");
                entity.Property(o => o.TotalAmount).IsRequired().HasColumnType("decimal(18,2)");
                entity.Property(o => o.DiscountAmount).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                entity.Property(o => o.DiscountDescription).HasMaxLength(200);
                entity.Property(o => o.Status).IsRequired();

                // Relationships
                entity.HasOne(o => o.Customer)
                      .WithMany(c => c.Orders)
                      .HasForeignKey(o => o.CustomerId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // OrderItem Configuration
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(oi => oi.Id);
                entity.Property(oi => oi.Quantity).IsRequired();
                entity.Property(oi => oi.UnitPrice).IsRequired().HasColumnType("decimal(18,2)");

                // Relationships
                entity.HasOne(oi => oi.Order)
                      .WithMany(o => o.OrderItems)
                      .HasForeignKey(oi => oi.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(oi => oi.Product)
                      .WithMany(p => p.OrderItems)
                      .HasForeignKey(oi => oi.ProductId)
                      .OnDelete(DeleteBehavior.Restrict); // Prevent deletion if referenced in orders
            });

            // PricingRule Configuration
            modelBuilder.Entity<PricingRule>(entity =>
            {
                entity.HasKey(pr => pr.Id);
                entity.Property(pr => pr.CustomerType).IsRequired();
                entity.Property(pr => pr.DiscountPercentage).IsRequired().HasColumnType("decimal(5,2)");
                entity.Property(pr => pr.MinimumOrderAmount).HasColumnType("decimal(18,2)");
                entity.Property(pr => pr.IsActive).IsRequired().HasDefaultValue(true);
                entity.Property(pr => pr.Description).HasMaxLength(200);

                // Index for efficient querying of active rules
                entity.HasIndex(pr => new { pr.CustomerType, pr.IsActive });
            });

            // Seed initial data
            SeedData(modelBuilder);
        }

        /// <summary>
        /// Seeds initial data for testing and demonstration
        /// </summary>
        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed Products
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "Wireless Headphones",
                    Description = "High-quality wireless headphones with noise cancellation",
                    Stock = 50,
                    BasePrice = 99.99m
                },
                new Product
                {
                    Id = 2,
                    Name = "Smart Watch",
                    Description = "Fitness tracking smart watch with heart rate monitor",
                    Stock = 30,
                    BasePrice = 199.99m
                },
                new Product
                {
                    Id = 3,
                    Name = "Laptop Stand",
                    Description = "Adjustable aluminum laptop stand for ergonomic working",
                    Stock = 75,
                    BasePrice = 49.99m
                },
                new Product
                {
                    Id = 4,
                    Name = "USB-C Cable",
                    Description = "Fast charging USB-C cable, 6ft length",
                    Stock = 200,
                    BasePrice = 12.99m
                },
                new Product
                {
                    Id = 5,
                    Name = "Bluetooth Speaker",
                    Description = "Portable Bluetooth speaker with 12-hour battery life",
                    Stock = 40,
                    BasePrice = 79.99m
                },
                new Product
                {
                    Id = 6,
                    Name = "Webcam Cover",
                    Description = "Privacy webcam cover slider for laptop security",
                    Stock = 150,
                    BasePrice = 4.99m
                }
            );

            // Seed Customers
            modelBuilder.Entity<Customer>().HasData(
                // Retail Customers
                new Customer
                {
                    Id = 1,
                    Name = "John Smith",
                    Email = "john.smith@email.com",
                    CustomerType = CustomerType.Retail
                },
                new Customer
                {
                    Id = 2,
                    Name = "Sarah Johnson",
                    Email = "sarah.johnson@email.com",
                    CustomerType = CustomerType.Retail
                },
                // Wholesale Customers
                new Customer
                {
                    Id = 3,
                    Name = "ABC Electronics Corp",
                    Email = "procurement@abcelectronics.com",
                    CustomerType = CustomerType.Wholesale
                },
                new Customer
                {
                    Id = 4,
                    Name = "Tech Wholesale Inc",
                    Email = "orders@techwholesale.com",
                    CustomerType = CustomerType.Wholesale
                }
            );

            // Seed Pricing Rules
            modelBuilder.Entity<PricingRule>().HasData(
                new PricingRule
                {
                    Id = 1,
                    CustomerType = CustomerType.Wholesale,
                    DiscountPercentage = 10.0m,
                    MinimumOrderAmount = null,
                    IsActive = true,
                    Description = "Standard wholesale discount - 10% off"
                },
                new PricingRule
                {
                    Id = 2,
                    CustomerType = CustomerType.Wholesale,
                    DiscountPercentage = 15.0m,
                    MinimumOrderAmount = 500.0m,
                    IsActive = true,
                    Description = "Volume discount - 15% off for orders over R500"
                }
            );
        }
    }
}
