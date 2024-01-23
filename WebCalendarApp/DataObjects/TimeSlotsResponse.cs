namespace WebCalendarApp.DataObjects.Calendar;

public sealed class TimeSlotsResponse
{
    public SortedSet<DateTime>? Slots30Dates { get; set; }
    public SortedSet<DateTime>? Slots60Dates { get; set; }
    public HashSet<TimeAppointment>? Appointments { get; set; }
}
