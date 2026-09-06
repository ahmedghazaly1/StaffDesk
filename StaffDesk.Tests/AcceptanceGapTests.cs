using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Services;
using StaffDesk.Infrastructure.Data;
using StaffDesk.Infrastructure.Services;

namespace StaffDesk.Tests;

public class AcceptanceGapTests
{
    [Fact]
    public void Overdue_Resolution_Target_Is_Breached()
    {
        var cal = WeekdayUtc();
        var sla = new SlaService(null!, null!, null!, null!, new WorkingCalendarService(new CalRepo(cal)), null!);
        var task = new WorkTask
        {
            Status = "IN_PROGRESS",
            DepartmentId = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ResolutionTargetAt = DateTime.UtcNow.AddDays(-2)
        };
        Assert.True(sla.SignedRemainingWorkingMinutes(task.ResolutionTargetAt.Value, 0, 1) < 0);
        Assert.Equal("BREACHED", sla.DetermineBreachState(task, 0));
    }

    [Fact]
    public void Webhook_Signature_RoundTrips()
    {
        var ts = "1710000000";
        var body = "{\"ok\":true}";
        var sig = WebhookDispatchService.Sign("secret", ts, body);
        Assert.True(WebhookDispatchService.Verify("secret", ts, body, sig));
        Assert.False(WebhookDispatchService.Verify("other", ts, body, sig));
    }

    [Fact]
    public async Task Webhook_Failures_Retry_Then_Disable()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var db = new AppDbContext(options);
        var sub = new WebhookSubscription
        {
            Label = "t",
            TargetUrl = "http://localhost/hook",
            SigningSecret = "abc",
            OwnerId = 1,
            IsActive = true,
            DisableAfterConsecutiveFailures = 2
        };
        db.WebhookSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var handler = new FailHandler();
        var factory = new SimpleFactory(new HttpClient(handler));
        var svc = new WebhookDispatchService(db, factory);

        await svc.DispatchAsync(sub.Id, "TASK_CREATED", "{}");
        await svc.DispatchAsync(sub.Id, "TASK_CREATED", "{}");

        await db.Entry(sub).ReloadAsync();
        Assert.Equal(2, sub!.ConsecutiveFailures);
        Assert.False(sub.IsActive);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public void Idempotency_Hash_Differs_When_Body_Differs()
    {
        static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
        var a = Hash("POST:/v1/tasks:{\"t\":1}");
        var b = Hash("POST:/v1/tasks:{\"t\":1}");
        var c = Hash("POST:/v1/tasks:{\"t\":2}");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    private static WorkCalendar WeekdayUtc() => new()
    {
        Name = "UTC",
        TimeZoneId = "UTC",
        WorkDaysMask = WorkDayFlags.Weekdays,
        WorkStartHour = 9,
        WorkEndHour = 17,
        IsOrganizationDefault = true,
        EffectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private sealed class CalRepo : IWorkCalendarRepository
    {
        private readonly WorkCalendar _cal;
        public CalRepo(WorkCalendar cal) => _cal = cal;
        public Task<WorkCalendar> CreateAsync(WorkCalendar calendar) => throw new NotImplementedException();
        public Task UpdateAsync(WorkCalendar calendar) => throw new NotImplementedException();
        public Task<WorkCalendar?> GetByIdAsync(int id) => Task.FromResult<WorkCalendar?>(_cal);
        public Task<WorkCalendar?> GetOrganizationDefaultAsync(DateTime? asOfUtc = null) => Task.FromResult<WorkCalendar?>(_cal);
        public Task<WorkCalendar?> GetForDepartmentAsync(int departmentId, DateTime? asOfUtc = null) => Task.FromResult<WorkCalendar?>(_cal);
        public Task<IReadOnlyList<WorkCalendar>> ListAsync() => Task.FromResult<IReadOnlyList<WorkCalendar>>(new[] { _cal });
        public Task<CalendarHoliday> AddHolidayAsync(CalendarHoliday holiday) => throw new NotImplementedException();
        public Task<bool> RemoveHolidayAsync(int holidayId) => throw new NotImplementedException();
        public Task SetDepartmentCalendarAsync(int departmentId, int? calendarId) => throw new NotImplementedException();
        public Task SeedDefaultIfEmptyAsync() => Task.CompletedTask;
    }

    private sealed class FailHandler : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }

    private sealed class SimpleFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public SimpleFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }
}
