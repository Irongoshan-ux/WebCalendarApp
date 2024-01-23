namespace WebCalendarApp.DataObjects.Calendar;

public sealed class DateTimeRange
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public DateTimeRange()
    {
    }

    public DateTimeRange(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }
}