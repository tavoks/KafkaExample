using Common.Infrastructure;
using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Data
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OutboxMessage> OutBoxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>()
                .HasKey(o => o.Id);

            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasConversion<string>();

            modelBuilder.Entity<OrderItem>()
                .HasKey(oi => new { oi.CategoryId, oi.ProductId });

            modelBuilder.Entity<OutboxMessage>()
                .HasKey(m => m.Id);

            modelBuilder.Entity<OutboxMessage>()
                .Property(m => m.Content)
                .IsRequired();
        }
    }
}
