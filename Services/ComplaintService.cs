using Microsoft.EntityFrameworkCore;
using MajlisManagement.Data;
using MajlisManagement.DTOs;
using MajlisManagement.Models;
using MajlisManagement.Services.Interfaces;

namespace MajlisManagement.Services;

public class ComplaintService : IComplaintService
{
    private readonly AppDbContext _db;

    public ComplaintService(AppDbContext db) => _db = db;

    public async Task<ComplaintResponseDto> CreateAsync(int? userId, CreateComplaintDto dto)
    {
        var type = dto.Type == "Suggestion" ? ComplaintType.Suggestion : ComplaintType.Complaint;
        var complaint = new Complaint
        {
            UserId = dto.IsAnonymous ? null : userId,
            Type = type,
            Title = dto.Title,
            Content = dto.Content,
            IsAnonymous = dto.IsAnonymous,
            Status = ComplaintStatus.New
        };

        _db.Complaints.Add(complaint);
        await _db.SaveChangesAsync();
        return await GetByIdAsync(complaint.Id);
    }

    public async Task<List<ComplaintResponseDto>> GetAllAsync()
    {
        var complaints = await _db.Complaints
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return complaints.Select(MapToDto).ToList();
    }

    public async Task<ComplaintResponseDto> GetByIdAsync(int id)
    {
        var complaint = await _db.Complaints
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("الملاحظة غير موجودة");

        return MapToDto(complaint);
    }

    public async Task<ComplaintResponseDto> RespondAsync(int id, RespondComplaintDto dto)
    {
        var complaint = await _db.Complaints.FindAsync(id)
            ?? throw new KeyNotFoundException("الملاحظة غير موجودة");

        complaint.AdminResponse = dto.AdminResponse;
        complaint.Status = dto.Status;
        complaint.RespondedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<List<ComplaintResponseDto>> GetPublicAsync()
    {
        var complaints = await _db.Complaints
            .Include(c => c.User)
            .Where(c => c.IsPublic)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return complaints.Select(MapToDto).ToList();
    }

    public async Task<ComplaintResponseDto> TogglePublicAsync(int id)
    {
        var complaint = await _db.Complaints.FindAsync(id)
            ?? throw new KeyNotFoundException("الملاحظة غير موجودة");
        complaint.IsPublic = !complaint.IsPublic;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    private static ComplaintResponseDto MapToDto(Complaint c) => new()
    {
        Id = c.Id,
        SenderName = c.IsAnonymous ? null : c.User?.FullName,
        Type = c.Type.ToString(),
        Title = c.Title,
        Content = c.Content,
        IsAnonymous = c.IsAnonymous,
        Status = c.Status.ToString(),
        AdminResponse = c.AdminResponse,
        IsPublic = c.IsPublic,
        CreatedAt = c.CreatedAt,
        RespondedAt = c.RespondedAt
    };
}
