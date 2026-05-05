using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Tasks.Entities;
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
  public DbSet<Notification> Notifications => Set<Notification>();
  public DbSet<SatelliteNode> SatelliteNodes => Set<SatelliteNode>();
  public DbSet<SyncRegistry> SyncRegistries => Set<SyncRegistry>();

  // Catalog
  public DbSet<Customer> Customers => Set<Customer>();
  public DbSet<Supplier> Suppliers => Set<Supplier>();
  public DbSet<PartNumber> PartNumbers => Set<PartNumber>();
  public DbSet<Alternative> Alternatives => Set<Alternative>();
  public DbSet<PartNumberSupplier> PartNumberSuppliers => Set<PartNumberSupplier>();
  public DbSet<CompanyPreset> CompanyPresets => Set<CompanyPreset>();

  // RFQ
  public DbSet<RFQHeader> RFQs => Set<RFQHeader>();
  public DbSet<RFQItem> RFQItems => Set<RFQItem>();
  public DbSet<RFQUserRead> RFQUserReads => Set<RFQUserRead>();

  // Sales
  public DbSet<Quote> Quotes => Set<Quote>();
  public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();
  public DbSet<Invoice> Invoices => Set<Invoice>();
  public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
  public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
  public DbSet<FinalInvoice> FinalInvoices => Set<FinalInvoice>();
  public DbSet<FinalInvoiceItem> FinalInvoiceItems => Set<FinalInvoiceItem>();

  // Purchasing
  public DbSet<ProcumentRecord> ProcumentRecords => Set<ProcumentRecord>();
  public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
  public DbSet<POItem> POItems => Set<POItem>();
  public DbSet<POImportDetail> POImportDetails => Set<POImportDetail>();
  public DbSet<POItemTrackNumber> POItemTrackNumbers => Set<POItemTrackNumber>();
  public DbSet<ILSItem> ILSItems => Set<ILSItem>();
  public DbSet<ILSCustomer> ILSCustomers => Set<ILSCustomer>();
  public DbSet<ILSQuote> ILSQuotes => Set<ILSQuote>();
  public DbSet<ILSQuoteItem> ILSQuoteItems => Set<ILSQuoteItem>();
  public DbSet<CapListItem> CapListItems => Set<CapListItem>();
  public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
  public DbSet<Procurement> Procurements => Set<Procurement>();
  public DbSet<ProcurementItem> ProcurementItems => Set<ProcurementItem>();
  public DbSet<ProcurementSupplierQuote> ProcurementSupplierQuotes => Set<ProcurementSupplierQuote>();
  public DbSet<TaskItem> TaskItems => Set<TaskItem>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);




        modelBuilder.Entity<PartNumber>()
        .ToTable(tb => tb.HasTrigger("trg_PopulateNewName"));
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
      // Composite index used by permission-filtered list queries (e.g. RFQService.GetAllAsync)
      entity.HasIndex(e => new { e.UserId, e.EntityName });
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
      entity.Property(e => e.CustomerCode).HasMaxLength(100);
      entity.Property(e => e.Email).HasMaxLength(200);
      entity.Property(e => e.Phone).HasMaxLength(50);
      entity.Property(e => e.ShipTo).HasMaxLength(500);
      entity.Property(e => e.BillTo).HasMaxLength(500);
      entity.Property(e => e.Base).IsRequired(false);
      entity.Property(e => e.ExWork).IsRequired(false);

      // Speeds up catalog search-by-name
      entity.HasIndex(e => e.Name);
    });

    modelBuilder.Entity<Supplier>(entity =>
    {
      entity.ToTable("Suppliers");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).HasMaxLength(300);
      entity.Property(e => e.Email).HasMaxLength(200);
      entity.Property(e => e.Phone).HasMaxLength(50);
      entity.Property(e => e.Address).HasMaxLength(500);

      // Speeds up search-by-name, username lookups, and the "Approved" filter used by
      // supplier dropdowns and the ResolveSupplierAsync lookup in SupplierQuoteService.
      entity.HasIndex(e => e.Name);
      entity.HasIndex(e => e.Username);
      entity.HasIndex(e => e.Status);
    });

    modelBuilder.Entity<PartNumber>(entity =>
    {
      entity.ToTable("PartNumbers");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.NewName).HasMaxLength(200);
      entity.Property(e => e.Name).HasMaxLength(200);
      entity.Property(e => e.Description).HasMaxLength(1000);

      entity.HasOne(e => e.Supplier)
                .WithMany(s => s.PartNumbers)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.SupplierId);
      // Speeds up part-number search-by-name (used everywhere in the app)
      entity.HasIndex(e => e.Name);
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
      // Composite for the RFQ list filtered by owner + status
      entity.HasIndex(e => new { e.UserId, e.Status });
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

    modelBuilder.Entity<RFQUserRead>(entity =>
    {
      entity.ToTable("RFQUserReads");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => new { e.RFQId, e.UserId }).IsUnique();
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
      // Speeds up alternate-part-number searches across quote items
      entity.HasIndex(e => e.Alt);
    });

    modelBuilder.Entity<CustomerPayment>(entity =>
    {
      entity.ToTable("CustomerPayments");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.FileName).HasMaxLength(500);
      entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
      entity.Property(e => e.Notes).HasMaxLength(1000);
      entity.HasOne(e => e.Invoice)
            .WithMany()
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
      entity.HasIndex(e => e.InvoiceId);
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

    modelBuilder.Entity<FinalInvoice>(entity =>
    {
      entity.ToTable("FinalInvoices");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.InvoiceNumber).HasMaxLength(100);
      entity.Property(e => e.Status).HasMaxLength(50);
      entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
      entity.Property(e => e.ShippingCost).HasColumnType("decimal(18,2)");
      entity.Property(e => e.ShippingMethod).HasMaxLength(100);
      entity.Property(e => e.Notes).HasMaxLength(2000);

      entity.HasIndex(e => e.InvoiceNumber).IsUnique();

      entity.HasOne(e => e.ProformaInvoice)
                .WithMany()
                .HasForeignKey(e => e.ProformaInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.ProformaInvoiceId);
      entity.HasIndex(e => e.CreatedAt);
    });

    modelBuilder.Entity<FinalInvoiceItem>(entity =>
    {
      entity.ToTable("FinalInvoiceItems");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.Condition).HasMaxLength(100);
      entity.Property(e => e.CertName).HasMaxLength(200);
      entity.Property(e => e.TrackNumber).HasMaxLength(500);
      entity.Property(e => e.Carrier).HasMaxLength(200);

      entity.HasOne(e => e.FinalInvoice)
                .WithMany(fi => fi.Items)
                .HasForeignKey(e => e.FinalInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(e => e.PartNumber)
                .WithMany()
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.FinalInvoiceId);
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
      entity.Property(e => e.Type).HasMaxLength(50).HasDefaultValue("Procument");
      entity.Property(e => e.FixPrice).HasColumnType("decimal(18,2)");

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

      entity.HasOne(e => e.ParentProcument)
                .WithMany(e => e.ShopRecords)
                .HasForeignKey(e => e.ParentProcumentId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.RFQItemId);
      entity.HasIndex(e => e.SupplierId);
      entity.HasIndex(e => e.UserId);
      entity.HasIndex(e => e.ParentProcumentId);
      entity.HasIndex(e => e.Type);
    });

    modelBuilder.Entity<PurchaseOrder>(entity =>
    {
      entity.ToTable("PurchaseOrders");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.PONumber).HasMaxLength(100);
      entity.Property(e => e.Status).HasMaxLength(50);
      entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
      entity.Property(e => e.AdminApproval).HasMaxLength(20).HasDefaultValue("Pending");
      entity.Property(e => e.AdminApprovalNote).HasMaxLength(1000);
      entity.Property(e => e.PaymentStatus).HasMaxLength(20).HasDefaultValue("NotStarted");
      entity.Property(e => e.ReturnReason).HasMaxLength(1000);

      entity.HasIndex(e => e.PONumber).IsUnique();
      entity.HasIndex(e => e.AdminApproval);
      entity.HasIndex(e => e.PaymentStatus);
      entity.HasIndex(e => e.ReturnedAt);

      entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.InvoiceId);
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
      entity.Property(e => e.ReturnReason).HasMaxLength(1000);

      entity.HasOne(e => e.PurchaseOrder)
                .WithMany(po => po.POItems)
                .HasForeignKey(e => e.POId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

      entity.HasOne(e => e.ProcumentRecord)
                .WithMany(pr => pr.POItems)
                .HasForeignKey(e => e.ProcumentId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.PartNumber)
                .WithMany()
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.POId);
      entity.HasIndex(e => e.SourceProcurementItemId);
      entity.HasIndex(e => e.ReturnedAt);

      entity.HasOne(e => e.SourceProcurementItem)
                .WithMany()
                .HasForeignKey(e => e.SourceProcurementItemId)
                .OnDelete(DeleteBehavior.SetNull);
    });

    // ───────────────────────────────────────────
    // Procurement (post-acceptance editing layer)
    // ───────────────────────────────────────────
    modelBuilder.Entity<Procurement>(entity =>
    {
      entity.ToTable("Procurements");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.ProcurementNumber).HasMaxLength(100);
      entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Open");
      entity.Property(e => e.Notes).HasMaxLength(2000);

      entity.HasOne<Invoice>()
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.InvoiceId).IsUnique();
      entity.HasIndex(e => e.Status);
      entity.HasIndex(e => e.CreatedAt);
    });

    modelBuilder.Entity<ProcurementItem>(entity =>
    {
      entity.ToTable("ProcurementItems");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.RfqName).HasMaxLength(300);
      entity.Property(e => e.PartNumberName).HasMaxLength(200);
      entity.Property(e => e.PartNumberDescription).HasMaxLength(1000);
      entity.Property(e => e.RfqCondition).HasMaxLength(100);
      entity.Property(e => e.RfqUnit).HasMaxLength(50);
      entity.Property(e => e.RfqPriority).HasMaxLength(50);
      entity.Property(e => e.RfqAlt).HasMaxLength(200);

      entity.Property(e => e.QuoteNumber).HasMaxLength(100);
      entity.Property(e => e.QuoteUnitPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.QuoteCondition).HasMaxLength(100);
      entity.Property(e => e.QuoteAlt).HasMaxLength(200);

      entity.Property(e => e.SupplierName).HasMaxLength(300);
      entity.Property(e => e.SupplierPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.SupplierLeadTime).HasMaxLength(100);
      entity.Property(e => e.SupplierCondition).HasMaxLength(100);
      entity.Property(e => e.SupplierCertName).HasMaxLength(200);

      entity.Property(e => e.AcceptedUnitPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.LeadTime).HasMaxLength(100);
      entity.Property(e => e.Condition).HasMaxLength(100);
      entity.Property(e => e.Alt).HasMaxLength(200);
      entity.Property(e => e.ItemStatus).HasMaxLength(50).HasDefaultValue("Open");
      entity.Property(e => e.LastReturnReason).HasMaxLength(1000);

      entity.HasOne(e => e.Procurement)
                .WithMany(p => p.Items)
                .HasForeignKey(e => e.ProcurementId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(e => e.PartNumber)
                .WithMany()
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.CurrentSupplier)
                .WithMany()
                .HasForeignKey(e => e.CurrentSupplierId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.ProcurementId);
      entity.HasIndex(e => e.SourceInvoiceItemId);
      entity.HasIndex(e => e.ItemStatus);
    });

    modelBuilder.Entity<ProcurementSupplierQuote>(entity =>
    {
      entity.ToTable("ProcurementSupplierQuotes");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.SupplierName).HasMaxLength(300);
      entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
      entity.Property(e => e.Condition).HasMaxLength(100);
      entity.Property(e => e.Unit).HasMaxLength(50);
      entity.Property(e => e.Alt).HasMaxLength(200);
      entity.Property(e => e.LeadTime).HasMaxLength(100);
      entity.Property(e => e.CertName).HasMaxLength(200);
      entity.Property(e => e.ShippingPoint).HasMaxLength(200);

      entity.HasOne(e => e.ProcurementItem)
                .WithMany(pi => pi.SupplierQuotes)
                .HasForeignKey(e => e.ProcurementItemId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.ProcurementItemId);
      entity.HasIndex(e => new { e.ProcurementItemId, e.IsSelected });
    });

    modelBuilder.Entity<POImportDetail>(entity =>
    {
      entity.ToTable("POImportDetails");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.BankName).HasMaxLength(200);
      entity.Property(e => e.BankAccountNumber).HasMaxLength(100);
      entity.Property(e => e.BankAddress).HasMaxLength(500);
      entity.Property(e => e.BankCity).HasMaxLength(200);
      entity.Property(e => e.BankCountry).HasMaxLength(200);
      entity.Property(e => e.FedExAccount).HasMaxLength(200);
      entity.Property(e => e.CourierName).HasMaxLength(200);
      entity.Property(e => e.ShippingMethod).HasMaxLength(100);
      entity.Property(e => e.Incoterms).HasMaxLength(100);
      entity.Property(e => e.Notes).HasMaxLength(2000);

      entity.HasOne(e => e.PurchaseOrder)
                .WithOne(po => po.ImportDetail)
                .HasForeignKey<POImportDetail>(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasIndex(e => e.PurchaseOrderId).IsUnique();
    });

    modelBuilder.Entity<POItemTrackNumber>(entity =>
    {
      entity.ToTable("POItemTrackNumbers");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.TrackNumber).HasMaxLength(200);
      entity.Property(e => e.Carrier).HasMaxLength(200);
      entity.Property(e => e.Notes).HasMaxLength(1000);

      entity.HasOne(e => e.POItem)
                .WithMany(i => i.TrackNumbers)
                .HasForeignKey(e => e.POItemId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasIndex(e => e.POItemId);
    });

    modelBuilder.Entity<PaymentRequest>(entity =>
    {
      entity.ToTable("PaymentRequests");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Status).HasMaxLength(50);

      entity.HasOne(e => e.PO)
                .WithMany()
                .HasForeignKey(e => e.POId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasIndex(e => e.POId);
    });

    modelBuilder.Entity<ILSItem>(entity =>
    {
      entity.ToTable("ILSItems");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Description).HasMaxLength(1000);
      entity.Property(e => e.AltPartNumber).HasMaxLength(200);
      entity.Property(e => e.Condition).HasMaxLength(100);
      entity.Property(e => e.CertName).HasMaxLength(200);
      entity.Property(e => e.LeadTime).HasMaxLength(100);
      entity.Property(e => e.Price).HasColumnType("decimal(18,2)");

      entity.HasOne(e => e.PartNumber)
                .WithMany()
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.ProcumentRecord)
                .WithMany()
                .HasForeignKey(e => e.ProcumentRecordId)
                .OnDelete(DeleteBehavior.SetNull);

      entity.HasIndex(e => e.PartNumberId);
      entity.HasIndex(e => e.CreatedAt);
    });

    modelBuilder.Entity<ILSCustomer>(entity =>
    {
      entity.ToTable("ILSCustomers");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).HasMaxLength(300);
      entity.Property(e => e.CustomerCode).HasMaxLength(100);
      entity.Property(e => e.Email).HasMaxLength(200);
      entity.Property(e => e.Phone).HasMaxLength(50);
    });

    modelBuilder.Entity<ILSQuote>(entity =>
    {
      entity.ToTable("ILSQuotes");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.QuoteNumber).HasMaxLength(100);
      entity.Property(e => e.Status).HasMaxLength(50);
      entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
      entity.Property(e => e.Notes).HasMaxLength(2000);
      entity.Property(e => e.RfqReference).HasMaxLength(200);

      entity.HasOne(e => e.ILSCustomer)
                .WithMany()
                .HasForeignKey(e => e.ILSCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.ILSCustomerId);
      entity.HasIndex(e => e.CreatedAt);
    });

    modelBuilder.Entity<ILSQuoteItem>(entity =>
    {
      entity.ToTable("ILSQuoteItems");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.SellPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
      entity.Property(e => e.Condition).HasMaxLength(100);
      entity.Property(e => e.CertName).HasMaxLength(200);
      entity.Property(e => e.LeadTime).HasMaxLength(100);

      entity.HasOne(e => e.ILSQuote)
                .WithMany(q => q.Items)
                .HasForeignKey(e => e.ILSQuoteId)
                .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(e => e.PartNumber)
                .WithMany()
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.ILSItem)
                .WithMany()
                .HasForeignKey(e => e.ILSItemId)
                .OnDelete(DeleteBehavior.SetNull);

      entity.HasIndex(e => e.ILSQuoteId);
    });

    modelBuilder.Entity<CapListItem>(entity =>
    {
      entity.ToTable("CapListItems");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Description).HasMaxLength(1000);

      entity.HasOne(e => e.PartNumber)
                .WithMany()
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.ProcumentRecord)
                .WithMany()
                .HasForeignKey(e => e.ProcumentRecordId)
                .OnDelete(DeleteBehavior.SetNull);

      entity.HasIndex(e => e.PartNumberId);
      entity.HasIndex(e => e.CompanyId);
      entity.HasIndex(e => e.IsRepair);
      entity.HasIndex(e => e.CreatedAt);
    });

    modelBuilder.Entity<InventoryItem>(entity =>
    {
      entity.ToTable("InventoryItems");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Description).HasMaxLength(1000);
      entity.Property(e => e.Condition).HasMaxLength(100);
      entity.Property(e => e.Price).HasColumnType("decimal(18,2)");

      entity.HasOne(e => e.PartNumber)
                .WithMany()
                .HasForeignKey(e => e.PartNumberId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(e => e.PartNumberId);
      entity.HasIndex(e => e.CompanyId);
      entity.HasIndex(e => e.CreatedAt);
    });

    // ───────────────────────────────────────────
    // Company Presets
    // ───────────────────────────────────────────
    modelBuilder.Entity<CompanyPreset>(entity =>
    {
      entity.ToTable("CompanyPresets");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).HasMaxLength(300).IsRequired();
      entity.Property(e => e.Location).HasMaxLength(1000);
      entity.Property(e => e.Phone).HasMaxLength(100);
      entity.Property(e => e.Website).HasMaxLength(300);
      entity.Property(e => e.Email).HasMaxLength(200);
      entity.Property(e => e.LogoMimeType).HasMaxLength(100);
      entity.Property(e => e.PrimaryColor).HasMaxLength(20).HasDefaultValue("#1a2744");
      entity.Property(e => e.AccentColor).HasMaxLength(20).HasDefaultValue("#2563eb");
      entity.Property(e => e.CustomPdfHtml).HasColumnType("TEXT");
    });

    // ───────────────────────────────────────────
    // Notifications
    // ───────────────────────────────────────────
    modelBuilder.Entity<Notification>(entity =>
    {
      entity.ToTable("Notifications");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.UserId);
      entity.HasIndex(e => new { e.UserId, e.IsDismissed });
    });

    // ───────────────────────────────────────────
    // Tasks Module
    // ───────────────────────────────────────────
    modelBuilder.Entity<TaskItem>(entity =>
    {
      entity.ToTable("Tasks");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
      entity.Property(e => e.Description).HasMaxLength(2000);
      entity.Property(e => e.AssignedTo).HasMaxLength(100).IsRequired();
      entity.Property(e => e.CreatedByCode).HasMaxLength(100).IsRequired();

      entity.HasOne(e => e.AssignedToUser)
                .WithMany()
                .HasForeignKey(e => e.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

      entity.HasIndex(e => e.AssignedTo);
      entity.HasIndex(e => e.Status);
      entity.HasIndex(e => e.CreatedAt);
    });

    // ───────────────────────────────────────────
    // Sync Module
    // ───────────────────────────────────────────
    modelBuilder.Entity<SatelliteNode>(entity =>
    {
      entity.ToTable("SatelliteNodes");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).HasMaxLength(200);
      entity.Property(e => e.EndpointUrl).HasMaxLength(500);
      entity.HasIndex(e => e.BaseNumber);
    });

    modelBuilder.Entity<SyncRegistry>(entity =>
    {
      entity.ToTable("SyncRegistries");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.EntityName).HasMaxLength(100);
      entity.HasIndex(e => new { e.EntityName, e.MainAppId });
      entity.HasIndex(e => new { e.EntityName, e.SatelliteAppId, e.SatelliteNodeId });

      entity.HasOne(e => e.SatelliteNode)
                .WithMany()
                .HasForeignKey(e => e.SatelliteNodeId)
                .OnDelete(DeleteBehavior.Cascade);
    });
  }
}
