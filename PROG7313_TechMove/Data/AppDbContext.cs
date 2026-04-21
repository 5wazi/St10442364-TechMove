using Microsoft.EntityFrameworkCore;
using PROG7313_TechMove.Models;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Diagnostics.Contracts;
using Contract = PROG7313_TechMove.Models.Contract;

namespace PROG7313_TechMove.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Contract> Contracts => Set<Contract>();
        public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Client configuration
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.ContactDetails).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Region).IsRequired().HasMaxLength(100);
            });

            // Contract configuration
            modelBuilder.Entity<Contract>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ServiceLevel).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .IsRequired();

                // One Client has many Contracts
                entity.HasOne(e => e.Client)
                    .WithMany(c => c.Contracts)
                    .HasForeignKey(e => e.ClientId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ServiceRequest configuration
            modelBuilder.Entity<ServiceRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.CostUsd).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CostZar).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ExchangeRateUsed).HasColumnType("decimal(18,6)");
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .IsRequired();

                // One Contract has many ServiceRequests
                entity.HasOne(e => e.Contract)
                    .WithMany(c => c.ServiceRequests)
                    .HasForeignKey(e => e.ContractId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed data
            modelBuilder.Entity<Client>().HasData(
                new Client { Id = 1, Name = "Global Freight Ltd", ContactDetails = "info@globalfreight.com | +27 31 000 0001", Region = "Africa" },
                new Client { Id = 2, Name = "Euro Cargo GmbH", ContactDetails = "contact@eurocargo.de | +49 30 000 0002", Region = "Europe" },
                new Client { Id = 3, Name = "Pacific Shipping Co", ContactDetails = "ops@pacshipping.com | +1 310 000 0003", Region = "Americas" }
            );

            modelBuilder.Entity<Contract>().HasData(
                new Contract { Id = 1, ClientId = 1, StartDate = new DateTime(2025, 1, 1), EndDate = new DateTime(2026, 12, 31), Status = ContractStatus.Active, ServiceLevel = "Gold", SignedAgreementPath = null, SignedAgreementFileName = null },
                new Contract { Id = 2, ClientId = 2, StartDate = new DateTime(2024, 6, 1), EndDate = new DateTime(2025, 5, 31), Status = ContractStatus.Expired, ServiceLevel = "Silver", SignedAgreementPath = null, SignedAgreementFileName = null },
                new Contract { Id = 3, ClientId = 3, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), Status = ContractStatus.Draft, ServiceLevel = "Bronze", SignedAgreementPath = null, SignedAgreementFileName = null }
            );
        }
    }
}
