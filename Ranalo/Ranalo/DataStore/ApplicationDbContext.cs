using System.Data;
using System;
using Microsoft.EntityFrameworkCore;
using Ranalo.DataStore.DataModels;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Dealer> Dealers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Enrolment> Enrolments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Dealer>(entity =>
            {
                entity.HasKey(d => d.DealerId);

                entity.Property(d => d.DealerReference)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(d => d.CompanyName)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(d => d.Email)
                      .HasMaxLength(150);

                // Relationship configuration
                entity.HasOne(d => d.User)
                      .WithMany(u => u.Dealers)
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            var converter = new ValueConverter<List<string>, string>(
                   v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                   v => string.IsNullOrEmpty(v)
                           ? new List<string>()
                           : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null)
               );

            modelBuilder.Entity<User>()
                .Property(u => u.OtherSelectedRoles)
                .HasConversion(converter)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<User>()
            .Ignore(u => u.Role);

            modelBuilder.Entity<User>()
            .Property(u => u.RoleId)
            .HasConversion<int>();

            modelBuilder.Entity<User>()
            .Property(u => u.Status)
            .HasConversion<int>();

            base.OnModelCreating(modelBuilder);
        }
    }
}
