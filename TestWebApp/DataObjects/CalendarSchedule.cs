using Microsoft.Graph;

namespace WebCalendarApp.DataObjects.Calendar;

public sealed class CalendarSchedule
{
    public IDictionary<DateTimeRange, IList<ICalendarGetScheduleCollectionPage>>? CalendarSchedulePerTimeRange { get; set; }
}
