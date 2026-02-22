using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Shared.Entities;

namespace Procument.Data;

public class AppDbContext : DbContext
{
  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

  // Identity
  public DbSet<User> Users => Set<User>();
  public DbSet<EntityPermission> EntityPermissions => Set<EntityPermission>();

  // Shared
  public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

  // Catalog
  public DbSet<Customer> Customers => Set<Customer>();
  public DbSet<Supplier> Suppliers => Set<Supplier>();
  public DbSet<PartNumber> PartNumbers => Set<PartNumber>();
  public DbSet<Alternative> Alternatives => Set<Alternative>();
  public DbSet<PartNumberSupplier> PartNumberSuppliers => Set<PartNumberSupplier>();

  // RFQ
  public DbSet<RFQHeader> RFQs => Set<RFQHeader>();
  public DbSet<RFQItem> RFQItems => Set<RFQItem>();

  // Sales
  public DbSet<Quote> Quotes => Set<Quote>();
  public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();
  public DbSet<Invoice> Invoices => Set<Invoice>();
  public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

  // Purchasing
  public DbSet<ProcumentRecord> ProcumentRecords => Set<ProcumentRecord>();
  public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
  public DbSet<POItem> POItems => Set<POItem>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // ───────────────────────────────────────────
    // Identity Module
    // ───────────────────────────────────────────
    modelBuilder.Entity<User>(entity =>
    {
      entity.ToTable("Users");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).HasMaxLength(200);
      entity.Property(e => e.Email).HasMaxLength(200);
      entity.Property(e => e.Password).HasMaxLength(500);
      entity.Property(e => e.Role).HasMaxLength(50);
      entity.HasIndex(e => e.Email).IsUnique();
    });

    modelBuilder.Entity<EntityPermission>(entity =>
    {
      entity.ToTable("EntityPermissions");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.EntityName).HasMaxLength(100);
      entity.Property(e => e.EntityId).HasMaxLength(100);
      entity.Property(e => e.Permission).HasMaxLength(50);

      entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasIndex(e => new { e.EntityName, e.EntityId, e.UserId });
    });

    // ───────────────────────────────────────────
    // Shared Module
    // ───────────────────────────────────────────
    modelBuilder.Entity<AuditLog>(entity =>
    {
      entity.ToTable("AuditLogs");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Action).HasMaxLength(100);
      entity.Property(e => e.EntityName).HasMaxLength(100);
      entity.Property(e => e.EntityId).HasMaxLength(100);
      entity.Property(e => e.IPAddress).HasMaxLength(50);

      entity.HasIndex(e => new { e.EntityName, e.EntityId });
      entity.HasIndex(e => e.UserId);
      entity.HasIndex(e => e.Timestamp);
    });

    // ───────────────────────────────────────────
    // Catalog Module
    // ───────────────────────────────────────────
    modelBuilder.Entity<Customer>(entity =>
    {
      entity.ToTable("Customers");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).HasMaxLength(300);
      entity.Property(e => e.Email).HasMaxLength(200);
      entity.Property(e => e.Phone).HasMaxLength(50);
      entity.Property(e => e.ShipTo).HasMaxLength(500);
      entity.Property(e => e.BillTo).HasMaxLength(500);
    });

    modelBuilder.Entity<Supplier>(entity =>
    {
      entity.ToTable("Suppliers");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).HasMaxLength(300);
      entity.Property(e => e.Email).HasMaxLength(200);
      entity.Property(e => e.Phone).HasMaxLength(50);
      entity.Property(e => e.Address).HasMaxLength(500);
    });

    modelBuilder.Entity<PartNumber>(entity =>
    {
      entity.ToTable("PartNumbers");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).HasMaxLength(200);
      entity.Property(e => e.Description).HasMaxLength(1000);

      entity.HasOne(e => e.Supplier)
                .WithMany(s => s.PartNumbers)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.SupplierId);
    });

    modelBuilder.Entity<PartNumberSupplier>(entity =>
    {
      entity.ToTable("PartNumberSuppliers");
      entity.HasKey(e => e.Id);

      entity.HasOne(e => e.PartNumber)
                .WithMany(p => p.PartNumberSuppliers)
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(e => e.Supplier)
                .WithMany(s => s.PartNumberSuppliers)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasIndex(e => new { e.PartNumberId, e.SupplierId }).IsUnique();
    });

    modelBuilder.Entity<Alternative>(entity =>
    {
      entity.ToTable("Alternatives");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).HasMaxLength(200);

      entity.HasOne(e => e.PartNumber)
                .WithMany(p => p.Alternatives)
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasIndex(e => e.PartNumberId);
    });

    // ───────────────────────────────────────────
    // RFQ Module
    // ───────────────────────────────────────────
    modelBuilder.Entity<RFQHeader>(entity =>
    {
      entity.ToTable("RFQs");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).HasMaxLength(300);

      entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

      entity.HasIndex(e => e.CustomerId);
      entity.HasIndex(e => e.UserId);
      entity.HasIndex(e => e.CreatedAt);
    });

    modelBuilder.Entity<RFQItem>(entity =>
    {
      entity.ToTable("RFQItems");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Alt).HasMaxLength(200);
      entity.Property(e => e.Condition).HasMaxLength(100);

      entity.HasOne(e => e.RFQ)
                .WithMany(r => r.RFQItems)
                .HasForeignKey(e => e.RFQId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(e => e.PartNumber)
                .WithMany()
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.RFQId);
    });

    // ───────────────────────────────────────────
    // Sales Module
    // ───────────────────────────────────────────
    modelBuilder.Entity<Quote>(entity =>
    {
      entity.ToTable("Quotes");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.QuoteNumber).HasMaxLength(100);
      entity.Property(e => e.Status).HasMaxLength(50);
      entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");

      entity.HasIndex(e => e.QuoteNumber).IsUnique();

      entity.HasOne(e => e.RFQ)
                .WithMany()
                .HasForeignKey(e => e.RFQId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.RFQId);
      entity.HasIndex(e => e.CreatedAt);
    });

    modelBuilder.Entity<QuoteItem>(entity =>
    {
      entity.ToTable("QuoteItems");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Alt).HasMaxLength(200);
      entity.Property(e => e.Condition).HasMaxLength(100);
      entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");

      entity.HasOne(e => e.Quote)
                .WithMany(q => q.QuoteItems)
                .HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(e => e.RFQItem)
                .WithMany()
                .HasForeignKey(e => e.RFQItemId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.PartNumber)
                .WithMany()
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.QuoteId);
    });

    modelBuilder.Entity<Invoice>(entity =>
    {
      entity.ToTable("Invoices");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.InvoiceNumber).HasMaxLength(100);
      entity.Property(e => e.Status).HasMaxLength(50);
      entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");

      entity.HasIndex(e => e.InvoiceNumber).IsUnique();

      entity.HasOne(e => e.Quote)
                .WithMany(q => q.Invoices)
                .HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.QuoteId);
      entity.HasIndex(e => e.CreatedAt);
    });

    modelBuilder.Entity<InvoiceItem>(entity =>
    {
      entity.ToTable("InvoiceItems");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");

      entity.HasOne(e => e.Invoice)
                .WithMany(i => i.InvoiceItems)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(e => e.QuoteItem)
                .WithMany(qi => qi.InvoiceItems)
                .HasForeignKey(e => e.QuoteItemId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.InvoiceId);
    });

    // ───────────────────────────────────────────
    // Purchasing Module
    // ───────────────────────────────────────────
    modelBuilder.Entity<ProcumentRecord>(entity =>
    {
      entity.ToTable("Procument");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Alt).HasMaxLength(200);
      entity.Property(e => e.Condition).HasMaxLength(100);
      entity.Property(e => e.Price).HasColumnType("decimal(18,2)");

      entity.HasOne(e => e.RFQItem)
                .WithMany()
                .HasForeignKey(e => e.RFQItemId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

      entity.HasIndex(e => e.RFQItemId);
      entity.HasIndex(e => e.SupplierId);
      entity.HasIndex(e => e.UserId);
    });

    modelBuilder.Entity<PurchaseOrder>(entity =>
    {
      entity.ToTable("PurchaseOrders");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.PONumber).HasMaxLength(100);
      entity.Property(e => e.Status).HasMaxLength(50);
      entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");

      entity.HasIndex(e => e.PONumber).IsUnique();

      entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.RFQ)
                .WithMany()
                .HasForeignKey(e => e.RFQId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.SupplierId);
      entity.HasIndex(e => e.CreatedAt);
    });

    modelBuilder.Entity<POItem>(entity =>
    {
      entity.ToTable("POItems");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Condition).HasMaxLength(100);
      entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");

      entity.HasOne(e => e.PurchaseOrder)
                .WithMany(po => po.POItems)
                .HasForeignKey(e => e.POId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(e => e.ProcumentRecord)
                .WithMany(pr => pr.POItems)
                .HasForeignKey(e => e.ProcumentId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.PartNumber)
                .WithMany()
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.POId);
    });
  }
}
