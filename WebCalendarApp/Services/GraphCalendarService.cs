using Microsoft.Graph;
using WebCalendarApp.Services.Extensions;
using System.Diagnostics;
using WebCalendarApp.Services.Interfaces.Calendar;
using WebCalendarApp.DataObjects.Calendar;

namespace WebCalendarApp.Services.Services.Calendar;

public class GraphCalendarService : ICalendarService
{
    private const string UTCName = "UTC";
    private readonly IConfiguration _configuration;
    private readonly ILogger<GraphCalendarService> _logger;
    private readonly GraphServiceClient _graphClient;
    private readonly string? _devModeEmail;

    public GraphCalendarService(IConfiguration configuration, GraphServiceClient graphClient, ILogger<GraphCalendarService> logger)
    {
        _configuration = configuration;
        _graphClient = graphClient;
        _logger = logger;
        var devMode = Convert.ToBoolean(_configuration["testPortal:devMode"]);
        _devModeEmail = devMode ? _configuration["testPortal:developmentEmailAddress"] : null;
    }

    public async Task<string> CreateAppointmentAsync(string email, Event appointment, CancellationToken cancellationToken)
    {
        if (_devModeEmail is not null)
        {
            email = _devModeEmail;

            appointment.Attendees
                .ToList()
                .ForEach(x => x.EmailAddress.Address = email);
        }

        var result = await _graphClient.Users[email].Calendar.Events
            .Request()
            .AddAsync(appointment, cancellationToken);

        return result.Id;
    }

    public Task DeleteAppointmentAsync(string email, string appointmentId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(nameof(appointmentId));

        email = _devModeEmail ?? email;

        return _graphClient.Users[email].Calendar.Events[appointmentId]
            .Request()
            .DeleteAsync(cancellationToken);
    }

    public async Task<bool> CheckIfSlotIsFreeAsync(IList<string> emails, DateTime startTime, DateTime endTime,
                                                   CancellationToken cancellationToken)
    {
        var appointments = await GetAllTimeAppointmentsByTimeRangeAsync(emails, startTime, endTime, cancellationToken);

        return IsAvailableTimeSlot(appointments, startTime, endTime);
    }

    public async Task<TimeSlotsResponse> GetFreeSlotsAsync(IList<string> emails, DateTime startDateUtc,
                                                           DateTime endDateUtc, CancellationToken cancellationToken)
    {
        var resultPages = await GetAllScheduleInformation(emails, startDateUtc, endDateUtc, cancellationToken);

        if (resultPages.CalendarSchedulePerTimeRange is null)
            return new();

        return GetAllFreeSlots(resultPages, startDateUtc, endDateUtc);
    }

    private async Task<ICollection<TimeAppointment>> GetAllTimeAppointmentsByTimeRangeAsync(IList<string> emails,
                                                                                            DateTime startTime,
                                                                                            DateTime endTime,
                                                                                            CancellationToken cancellationToken)
    {
        var schedulePage = await GetAllScheduleInformation(emails, startTime, endTime, cancellationToken);

        return schedulePage.ConvertToTimeAppointments();
    }

    private async Task<CalendarSchedule> GetAllScheduleInformation(IList<string> emails, DateTime startTime,
                                                                   DateTime endTime, CancellationToken cancellationToken)
    {
        emails = _devModeEmail is null ? emails : new List<string> { _devModeEmail };

        List<ICalendarGetScheduleCollectionPage> resultPages = new();
        int maxDays = 62;

        var requestDates = GetRangeOfDates(startTime, endTime, maxDays);

        CalendarSchedule schedulePages = new();
        var tasks = new List<Task>(requestDates.Count);

        Stopwatch watcher = new();
        watcher.Start();
        try
        {
            foreach (var date in requestDates)
            {
                tasks.Add(Task.Run(async () =>
                {
                    resultPages.Add(await RequestScheduleAsync(emails, date.Key, date.Value, cancellationToken));

                    while (resultPages.Last().NextPageRequest is not null)
                    {
                        resultPages.Add(await resultPages.Last().NextPageRequest.PostAsync(cancellationToken));
                    }

                    AddSchedulePageToResultPages(resultPages, schedulePages, date);

                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, message: $"Fault in {nameof(GetAllScheduleInformation)}");
        }

        watcher.Stop();

        _logger.LogDebug($"Elapsed {watcher.Elapsed.TotalSeconds} for time range of {startTime:g} to {endTime:g}" +
            $"\nThreadId: {Environment.CurrentManagedThreadId}");

        return schedulePages;
    }

    private TimeSlotsResponse GetAllFreeSlots(CalendarSchedule schedule, DateTime startDate, DateTime endDate)
    {
        List<TimeAppointment> appointments = new();
        List<DateTime> slots30Dates = new();
        List<DateTime> slots60Dates = new();

        Stopwatch watcher = Stopwatch.StartNew();

        var allUsersAppointments = GetAllUsersAppointments(schedule);

        foreach (var email in allUsersAppointments.Keys)
        {
            var newFreeSlots = CalculateFreeSlots(startDate, endDate, allUsersAppointments[email]);

            appointments.AddRange(newFreeSlots.Appointments!);
            slots30Dates.AddRange(newFreeSlots.Slots30Dates!);
            slots60Dates.AddRange(newFreeSlots.Slots60Dates!);
        }

        _logger.LogDebug("Elapsed calculating all users free slots: {seconds}", watcher.Elapsed.TotalSeconds);

        return new TimeSlotsResponse
        {
            Appointments = appointments.ToHashSet(),
            Slots30Dates = new SortedSet<DateTime>(slots30Dates),
            Slots60Dates = new SortedSet<DateTime>(slots60Dates)
        };
    }

    private static IDictionary<string, List<TimeAppointment>> GetAllUsersAppointments(CalendarSchedule schedule)
    {
        var allAppointments = new Dictionary<string, List<TimeAppointment>>();

        foreach (var datePage in schedule.CalendarSchedulePerTimeRange!)
        {
            GetAppointmentsForDatePage(allAppointments, datePage);
        }
        return allAppointments;
    }

    private static void GetAppointmentsForDatePage(Dictionary<string, List<TimeAppointment>> allAppointments,
                                                   KeyValuePair<DateTimeRange, IList<ICalendarGetScheduleCollectionPage>> datePage)
    {
        foreach (var page in datePage.Value)
        {
            page.Select(x => x.ScheduleId).ToList().ForEach(email =>
            {
                var userAppointments = page
                    .Where(x => x.ScheduleId == email)
                    .Take(1)
                    .Select(y => y.ScheduleItems.ToTimeAppointmentsUtc(y.ScheduleId))
                    .First()
                    .ToList();

                if (allAppointments.TryGetValue(email, out List<TimeAppointment>? appointments))
                {
                    appointments.AddRange(userAppointments);
                }
                else
                {
                    allAppointments.Add(email, userAppointments);
                }
            });
        }
    }

    private Task<ICalendarGetScheduleCollectionPage> RequestScheduleAsync(IList<string> emails, DateTime startTime,
                                                                          DateTime endTime,
                                                                          CancellationToken cancellationToken) =>
        _graphClient.Users[emails.First()].Calendar
            .GetSchedule(Schedules: emails,
                         StartTime: startTime.ConvertToUtcDateTimeTimeZone(),
                         EndTime: endTime.ConvertToUtcDateTimeTimeZone())
            .Request()
            .Header("Prefer", $"outlook.timezone=\"{UTCName}\"")
            .PostAsync(cancellationToken);
    
    private static TimeSlotsResponse CalculateFreeSlots(DateTime startDateUtc, DateTime endDateUtc,
                                                        ICollection<TimeAppointment> appointments)
    {
        var slots30Mins = new SortedSet<DateTime>();
        var slots60Mins = new SortedSet<DateTime>();

        var slotStartTime = startDateUtc;
        DateTime slot30EndTime;
        DateTime slot60EndTime;

        var slotOffsetMins = 15;

        do
        {
            slot30EndTime = slotStartTime.AddMinutes(30);
            slot60EndTime = slotStartTime.AddMinutes(60);

            if (IsAvailableTimeSlot(appointments, slotStartTime, slot30EndTime))
            {
                slots30Mins.Add(slotStartTime);

                if (IsAvailableTimeSlot(appointments, slotStartTime, slot60EndTime) &&
                    (slot60EndTime <= endDateUtc))
                {
                    slots60Mins.Add(slotStartTime);
                }
            }
            slotStartTime = slotStartTime.AddMinutes(slotOffsetMins);

        } while (slotStartTime <= endDateUtc && slot30EndTime <= endDateUtc);

        return new TimeSlotsResponse
        {
            Slots30Dates = slots30Mins,
            Slots60Dates = slots60Mins,
            Appointments = appointments.ToHashSet()
        };
    }

    private static bool IsAvailableTimeSlot(ICollection<TimeAppointment> appointments, DateTime slotStartTime, DateTime slotEndTime) =>
        !appointments.Any(x => (x.TimeRange.Start < slotEndTime && slotStartTime < x.TimeRange.End) ||
                               (slotStartTime >= x.TimeRange.Start && slotEndTime <= x.TimeRange.End));

    private static Dictionary<DateTime, DateTime> GetRangeOfDates(DateTime startTime, DateTime endTime, int maxDays)
    {
        Dictionary<DateTime, DateTime> requestDates = new();

        while ((endTime - startTime).Days > maxDays)
        {
            requestDates.Add(startTime, startTime.AddDays(maxDays));
            startTime = startTime.AddDays(maxDays);
        }

        requestDates.Add(startTime, endTime);

        return requestDates;
    }

    private static void AddSchedulePageToResultPages(List<ICalendarGetScheduleCollectionPage> resultPages,
                                                     CalendarSchedule schedulePages, KeyValuePair<DateTime, DateTime> date)
    {
        if (schedulePages.CalendarSchedulePerTimeRange is not null)
        {
            schedulePages.CalendarSchedulePerTimeRange.Add(new DateTimeRange(date.Key, date.Value), resultPages);
            return;
        }

        schedulePages.CalendarSchedulePerTimeRange = new Dictionary<DateTimeRange, IList<ICalendarGetScheduleCollectionPage>>()
        {
            { new DateTimeRange(date.Key, date.Value), resultPages }
        };
    }
}