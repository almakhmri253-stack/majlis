namespace MajlisManagement.Models;

public class UserPermission
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // ── الحجوزات ──────────────────────────────────────
    public bool ViewBookings    { get; set; } = false;
    public bool CreateBookings  { get; set; } = false;
    public bool ConfirmBookings { get; set; } = false;
    public bool DeleteBookings  { get; set; } = false;

    // ── المدفوعات ─────────────────────────────────────
    public bool ViewPayments   { get; set; } = false;
    public bool ManagePayments { get; set; } = false;

    // ── الأعضاء ───────────────────────────────────────
    public bool ViewMembers   { get; set; } = false;
    public bool ManageMembers { get; set; } = false;

    // ── الشكاوى ───────────────────────────────────────
    public bool ViewAllComplaints { get; set; } = false;
    public bool RespondComplaints { get; set; } = false;

    // ── لوحة التحكم والتقارير ─────────────────────────
    public bool ViewDashboard { get; set; } = false;
    public bool ViewReports   { get; set; } = false;
}
