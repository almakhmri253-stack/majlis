using Microsoft.EntityFrameworkCore;
using MajlisManagement.Data;
using MajlisManagement.DTOs;
using MajlisManagement.Models;
using MajlisManagement.Services.Interfaces;

namespace MajlisManagement.Services;

public class EventService : IEventService
{
    private readonly AppDbContext _db;

    public EventService(AppDbContext db) => _db = db;

    public async Task<List<EventResponseDto>> GetAllAsync()
    {
        var events = await _db.Events
            .Include(e => e.CreatedBy)
            .Include(e => e.Media)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        return events.Select(Map).ToList();
    }

    public async Task<EventResponseDto> CreateAsync(int userId, CreateEventDto dto)
    {
        var ev = new MajlisEvent
        {
            Title           = dto.Title.Trim(),
            Description     = dto.Description?.Trim(),
            CreatedAt       = DateTime.UtcNow,
            CreatedByUserId = userId,
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        if (dto.Media.Count > 0)
        {
            var mediaEntries = dto.Media.Select((dataUrl, i) => new EventMedia
            {
                EventId   = ev.Id,
                MediaPath = dataUrl,
                MediaType = dataUrl.StartsWith("data:video/") ? "video" : "image",
                SortOrder = i
            });
            _db.EventMediaFiles.AddRange(mediaEntries);
            await _db.SaveChangesAsync();
        }

        await _db.Entry(ev).Reference(e => e.CreatedBy).LoadAsync();
        await _db.Entry(ev).Collection(e => e.Media).LoadAsync();
        return Map(ev);
    }

    public async Task<EventResponseDto> UpdateAsync(int id, UpdateEventDto dto)
    {
        var ev = await _db.Events
            .Include(e => e.Media)
            .Include(e => e.CreatedBy)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("الحدث غير موجود");

        ev.Title       = dto.Title.Trim();
        ev.Description = dto.Description?.Trim();

        // حذف الوسائط المحددة
        var toRemove = ev.Media.Where(m => dto.DeleteMediaIds.Contains(m.Id)).ToList();
        _db.EventMediaFiles.RemoveRange(toRemove);

        // إضافة الوسائط الجديدة
        if (dto.Media.Count > 0)
        {
            int nextOrder = ev.Media
                .Where(m => !dto.DeleteMediaIds.Contains(m.Id))
                .Select(m => m.SortOrder)
                .DefaultIfEmpty(-1).Max() + 1;

            var newMedia = dto.Media.Select((dataUrl, i) => new EventMedia
            {
                EventId   = ev.Id,
                MediaPath = dataUrl,
                MediaType = dataUrl.StartsWith("data:video/") ? "video" : "image",
                SortOrder = nextOrder + i
            });
            _db.EventMediaFiles.AddRange(newMedia);
        }

        await _db.SaveChangesAsync();
        await _db.Entry(ev).Collection(e => e.Media).LoadAsync();
        return Map(ev);
    }

    public async Task DeleteAsync(int id)
    {
        var ev = await _db.Events
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("الحدث غير موجود");

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
    }

    private static EventResponseDto Map(MajlisEvent e) => new()
    {
        Id          = e.Id,
        Title       = e.Title,
        Description = e.Description,
        Media       = e.Media.OrderBy(m => m.SortOrder).Select(m => new EventMediaDto
        {
            Id        = m.Id,
            MediaUrl  = m.MediaPath,
            MediaType = m.MediaType,
            SortOrder = m.SortOrder
        }).ToList(),
        MediaUrl  = e.MediaPath,
        MediaType = e.MediaType,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy?.FullName ?? "المدير",
    };
}
