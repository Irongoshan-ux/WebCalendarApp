namespace WebCalendarApp.DataObjects.Calendar;

public sealed class TimeAppointment
{
    public required string Id { get; set; }
    public required DateTimeRange TimeRange { get; set; }
    public string? Email { get; set; }

    public override int GetHashCode() => Id.GetHashCode();
}
