using MAPS.API.Data;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace MAPS.API.Services.Scheduling;

public interface IAppointmentPriorityEngine
{
    Task<UrgencyTier>    GetPatientPriorityTierAsync(Guid patientId);
    Task<DateTime?>      GetNextAvailableSlotAsync(Guid doctorId, UrgencyTier tier, int durationMinutes = 30);
    Task<bool>           HasConflictAsync(Guid doctorId, DateTime proposedTime, int durationMinutes = 30);
}

public class AppointmentPriorityEngine : IAppointmentPriorityEngine
{
    private readonly AppDbContext      _context;
    private readonly IRiskRepository   _riskRepo;

    public AppointmentPriorityEngine(AppDbContext context, IRiskRepository riskRepo)
    {
        _context  = context;
        _riskRepo = riskRepo;
    }

    // Determine patient urgency tier from latest risk score
    public async Task<UrgencyTier> GetPatientPriorityTierAsync(Guid patientId)
    {
        var risk = await _riskRepo.GetLatestByPatientAsync(patientId);
        if (risk is null) return UrgencyTier.Normal;
        return risk.UrgencyTier;
    }

    // Get next available slot based on urgency tier
    public async Task<DateTime?> GetNextAvailableSlotAsync(
        Guid doctorId, UrgencyTier tier, int durationMinutes = 30)
    {
        // Define the search window based on urgency
        var searchFrom = DateTime.UtcNow;
        var searchTo   = tier switch
        {
            UrgencyTier.Emergency => searchFrom.AddHours(8),   // Same day
            UrgencyTier.Urgent    => searchFrom.AddHours(24),  // Within 24 hours
            UrgencyTier.Normal    => searchFrom.AddDays(3),    // Within 3 days
            UrgencyTier.Followup  => searchFrom.AddDays(7),    // Within 1 week
            _                     => searchFrom.AddDays(3)
        };

        // Clinic working hours: 08:00 – 17:00
        var clinicStart = new TimeSpan(8, 0, 0);
        var clinicEnd   = new TimeSpan(17, 0, 0);

        // Get all booked appointments for this doctor in the window
        var booked = await _context.Appointments
            .Where(a => a.DoctorId == doctorId &&
                        a.DateTime >= searchFrom &&
                        a.DateTime <= searchTo &&
                        a.Status != AppointmentStatus.Cancelled)
            .Select(a => a.DateTime)
            .OrderBy(d => d)
            .ToListAsync();

        // Walk through candidate slots in 30-min increments
        var candidate = searchFrom;
        if (candidate.TimeOfDay < clinicStart)
            candidate = candidate.Date.Add(clinicStart);

        while (candidate <= searchTo)
        {
            // Skip outside clinic hours
            if (candidate.TimeOfDay < clinicStart ||
                candidate.TimeOfDay.Add(TimeSpan.FromMinutes(durationMinutes)) > clinicEnd)
            {
                candidate = candidate.Date.AddDays(1).Add(clinicStart);
                continue;
            }

            // Skip weekends
            if (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                candidate = candidate.Date.AddDays(1).Add(clinicStart);
                continue;
            }

            // Check for conflict
            var endTime = candidate.AddMinutes(durationMinutes);
            var hasConflict = booked.Any(b =>
                b < endTime && b.AddMinutes(durationMinutes) > candidate);

            if (!hasConflict)
                return candidate;

            candidate = candidate.AddMinutes(30);
        }

        return null; // No slot found in window
    }

    // Check if a specific time slot conflicts with existing appointments
    public async Task<bool> HasConflictAsync(
        Guid doctorId, DateTime proposedTime, int durationMinutes = 30)
    {
        var endTime = proposedTime.AddMinutes(durationMinutes);
        return await _context.Appointments
            .AnyAsync(a => a.DoctorId == doctorId &&
                           a.Status   != AppointmentStatus.Cancelled &&
                           a.DateTime < endTime &&
                           a.DateTime.AddMinutes(a.DurationMinutes) > proposedTime);
    }
}
