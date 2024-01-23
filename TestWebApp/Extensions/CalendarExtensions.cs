using Microsoft.Graph;
using WebCalendarApp.DataObjects.Calendar;

namespace WebCalendarApp.Services.Extensions
{
    internal static class CalendarExtensions
    {
        internal static DateTimeTimeZone ConvertToUtcDateTimeTimeZone(this DateTime dateTime)
        {
            return DateTimeTimeZone.FromDateTime(dateTime, TimeZoneInfo.Utc);
        }

        internal static ISet<TimeAppointment> ConvertToTimeAppointments(this CalendarSchedule calendarSchedulePage)
        {
            HashSet<TimeAppointment> timeAppointments = new();

            foreach (var page in calendarSchedulePage.CalendarSchedulePerTimeRange!.Values)
            {
                timeAppointments.UnionWith(page.ToTimeAppointmentsUtc());
            }

            return timeAppointments;
        }

        internal static ISet<TimeAppointment> ToTimeAppointmentsUtc(this IList<ICalendarGetScheduleCollectionPage> resultPages)
        {
            var timeAppointments = new HashSet<TimeAppointment>();

            foreach (var page in resultPages)
            {
                page.Select(x => (x.ScheduleItems, x.ScheduleId)).ToList().ForEach(x =>
                {
                    timeAppointments.UnionWith(x.ScheduleItems.ToTimeAppointmentsUtc(x.ScheduleId));
                });
            }

            return timeAppointments.OrderBy(x => x.TimeRange.Start).ToHashSet();
        }

        internal static ISet<TimeAppointment> ToTimeAppointmentsUtc(this IEnumerable<ScheduleItem> scheduleItems, string email)
        {
            return scheduleItems.Select(x => new TimeAppointment
            {
                Id = Guid.NewGuid().ToString(),
                TimeRange = new()
                {
                    Start = DateTime.SpecifyKind(DateTime.Parse(x.Start.DateTime), DateTimeKind.Utc),
                    End = DateTime.SpecifyKind(DateTime.Parse(x.End.DateTime), DateTimeKind.Utc),
                },
                Email = email
            }).ToHashSet();
        }
    }
}