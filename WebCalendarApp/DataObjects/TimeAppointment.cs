namespace WebCalendarApp.DataObjects.Calendar;

public struct TimeAppointment
{
    public required string Id { get; set; }
    public required DateTimeRange TimeRange { get; set; }
    public string? Email { get; set; }

    public override readonly int GetHashCode() => Id.GetHashCode();
}
