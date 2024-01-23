using Microsoft.Graph;
using WebCalendarApp.DataObjects.Calendar;

namespace WebCalendarApp.Services.Interfaces.Calendar
{
    public interface ICalendarService
    {
        Task<TimeSlotsResponse> GetFreeSlotsAsync(IList<string> emails, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken);
        Task DeleteAppointmentAsync(string email, string appointmentId, CancellationToken cancellationToken);
        Task<string> CreateAppointmentAsync(string email, Event appointment, CancellationToken cancellationToken);
        Task<bool> CheckIfSlotIsFreeAsync(IList<string> emails, DateTime startTime, DateTime endTime, CancellationToken cancellationToken);
    }
}