using Microsoft.EntityFrameworkCore;
using MajlisManagement.Models;

namespace MajlisManagement.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberPayment> MemberPayments => Set<MemberPayment>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<MajlisEvent> Events => Set<MajlisEvent>();
    public DbSet<EventMedia> EventMediaFiles => Set<EventMedia>();
    public DbSet<UserLoginLog> LoginLogs => Set<UserLoginLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.PhoneNumber).IsUnique();
            e.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("User");
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(150).IsRequired();
        });

        // Booking
        modelBuilder.Entity<Booking>(e =>
        {
            e.Property(b => b.Cost).HasColumnType("decimal(10,2)");
            e.Property(b => b.GuestName).HasMaxLength(100).IsRequired();
            e.Property(b => b.PhoneNumber).HasMaxLength(20).IsRequired();
            e.Property(b => b.Notes).HasMaxLength(500);
            e.Property(b => b.AdminNote).HasMaxLength(500);

            e.HasOne(b => b.User)
             .WithMany(u => u.Bookings)
             .HasForeignKey(b => b.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            // index لتسريع فحص التعارض
            e.HasIndex(b => new { b.StartDate, b.EndDate, b.Status });
        });

        // Payment
        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(p => p.TotalAmount).HasColumnType("decimal(10,2)");
            e.Property(p => p.PaidAmount).HasColumnType("decimal(10,2)");
            e.Ignore(p => p.RemainingAmount); // computed property

            e.HasOne(p => p.Booking)
             .WithOne(b => b.Payment)
             .HasForeignKey<Payment>(p => p.BookingId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // PaymentTransaction
        modelBuilder.Entity<PaymentTransaction>(e =>
        {
            e.Property(t => t.Amount).HasColumnType("decimal(10,2)");

            e.HasOne(t => t.Payment)
             .WithMany(p => p.Transactions)
             .HasForeignKey(t => t.PaymentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Complaint
        modelBuilder.Entity<Complaint>(e =>
        {
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.Property(c => c.Content).HasMaxLength(2000).IsRequired();

            e.HasOne(c => c.User)
             .WithMany(u => u.Complaints)
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        // Member
        modelBuilder.Entity<Member>(e =>
        {
            e.Property(m => m.FullName).HasMaxLength(100).IsRequired();
            e.Property(m => m.PhoneNumber).HasMaxLength(20).IsRequired();
            e.Property(m => m.NationalId).HasMaxLength(20);
            e.Property(m => m.MonthlySubscription).HasColumnType("decimal(10,2)");
            e.Property(m => m.TotalPaymentDue).HasColumnType("decimal(10,2)");
            e.Property(m => m.PaidAmount).HasColumnType("decimal(10,2)");
            e.HasIndex(m => m.PhoneNumber).IsUnique();
        });

        // MemberPayment
        modelBuilder.Entity<MemberPayment>(e =>
        {
            e.Property(mp => mp.Amount).HasColumnType("decimal(10,2)");

            // منع تكرار الدفع لنفس الشهر والسنة لنفس العضو
            e.HasIndex(mp => new { mp.MemberId, mp.Year, mp.Month }).IsUnique();

            e.HasOne(mp => mp.Member)
             .WithMany(m => m.Payments)
             .HasForeignKey(mp => mp.MemberId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // MajlisEvent
        modelBuilder.Entity<MajlisEvent>(e =>
        {
            e.Property(ev => ev.Title).HasMaxLength(200).IsRequired();
            e.Property(ev => ev.Description).HasMaxLength(2000);
            e.Property(ev => ev.MediaPath).HasMaxLength(500);
            e.Property(ev => ev.MediaType).HasMaxLength(10).HasDefaultValue("none");

            e.HasOne(ev => ev.CreatedBy)
             .WithMany()
             .HasForeignKey(ev => ev.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // EventMedia — MediaPath is TEXT (no limit) to support base64 data URLs
        modelBuilder.Entity<EventMedia>(e =>
        {
            e.Property(m => m.MediaPath).HasColumnType("text").IsRequired();
            e.Property(m => m.MediaType).HasMaxLength(10).HasDefaultValue("image");

            e.HasOne(m => m.Event)
             .WithMany(ev => ev.Media)
             .HasForeignKey(m => m.EventId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // UserLoginLog
        modelBuilder.Entity<UserLoginLog>(e =>
        {
            e.HasOne(l => l.User)
             .WithMany()
             .HasForeignKey(l => l.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(l => l.IpAddress).HasMaxLength(45);
        });

        // UserPermission
        modelBuilder.Entity<UserPermission>(e =>
        {
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasOne(p => p.User)
             .WithOne()
             .HasForeignKey<UserPermission>(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
