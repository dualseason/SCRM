using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Data;

namespace SCRM.Tests;

/// <summary>
/// SMS/CallLog 手机记录链路防回归测试。
/// <para>覆盖 DbHelper 持久化幂等、通知占位、IMEI 空过滤兜底，以及 PhoneMessageHandler 的失败判定边界。</para>
/// </summary>
public sealed class PhoneSmsCallRecordsPersistenceTests
{
    private const string OwnerWxid = "wxid_phone_owner";
    private const string OtherOwnerWxid = "wxid_phone_other";
    private const string ImeiA = "861200000000001";
    private const string ImeiB = "861200000000002";

    [Fact]
    public async Task UpsertSmsRecord_ShouldBeIdempotentByOwnerImeiAndSmsId()
    {
        await using var db = CreateDbContext();

        var first = await db.UpsertSmsRecord(
            OwnerWxid,
            ImeiA,
            smsId: 1001,
            threadId: 91,
            number: " 13800000001 ",
            type: 1,
            rawDate: 1_700_000_000_000,
            content: "旧短信",
            isRead: false,
            simId: 0,
            blockType: 0,
            source: "Push");

        var second = await db.UpsertSmsRecord(
            OwnerWxid,
            ImeiA,
            smsId: 1001,
            threadId: 91,
            number: "13800000001",
            type: 2,
            rawDate: 1_700_000_001_000,
            content: "更新后的短信",
            isRead: true,
            simId: 1,
            blockType: 3,
            source: "Pull");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.id, second.id);
        Assert.Equal(1, await db.SmsRecords.CountAsync());

        var saved = await db.SmsRecords.AsNoTracking().SingleAsync();
        Assert.Equal("13800000001", saved.number);
        Assert.Equal(2, saved.type);
        Assert.Equal("更新后的短信", saved.content);
        Assert.True(saved.isRead);
        Assert.Equal(1, saved.simId);
        Assert.Equal(3, saved.blockType);
        Assert.Equal("Pull", saved.source);
    }

    [Fact]
    public async Task MarkSmsRead_ShouldUpdateLatestRecordInThreadWhenSmsIdMissing()
    {
        await using var db = CreateDbContext();
        await db.UpsertSmsRecord(OwnerWxid, ImeiA, 1101, 77, "10010", 1, 1_700_000_000_000, "较早短信", false, 0, 0, "Pull");
        await db.UpsertSmsRecord(OwnerWxid, ImeiA, 1102, 77, "10010", 1, 1_700_000_010_000, "较新短信", false, 0, 0, "Pull");

        var updated = await db.MarkSmsRead(OwnerWxid, ImeiA, smsId: 0, threadId: 77);

        Assert.NotNull(updated);
        Assert.Equal(1102, updated.smsId);
        Assert.True(updated.isRead);
        Assert.NotNull(updated.readAt);
        Assert.Equal("ReadNotice", updated.source);
        Assert.Equal(2, await db.SmsRecords.CountAsync());
    }

    [Fact]
    public async Task MarkSmsRead_ShouldCreatePlaceholderWhenOnlyThreadIdKnown()
    {
        await using var db = CreateDbContext();

        var placeholder = await db.MarkSmsRead(OwnerWxid, ImeiA, smsId: 0, threadId: 88);

        Assert.NotNull(placeholder);
        Assert.Equal(OwnerWxid, placeholder.ownerWxid);
        Assert.Equal(ImeiA, placeholder.imei);
        Assert.Equal(0, placeholder.smsId);
        Assert.Equal(88, placeholder.threadId);
        Assert.True(placeholder.isRead);
        Assert.NotNull(placeholder.readAt);
        Assert.Equal("ReadNotice", placeholder.source);
        Assert.Equal(1, await db.SmsRecords.CountAsync());
    }

    [Fact]
    public async Task MarkSmsSent_ShouldCreatePlaceholderAndKeepSentNoticeState()
    {
        await using var db = CreateDbContext();

        var placeholder = await db.MarkSmsSent(OwnerWxid, ImeiA, smsId: 1201, type: 6);

        Assert.NotNull(placeholder);
        Assert.Equal(OwnerWxid, placeholder.ownerWxid);
        Assert.Equal(ImeiA, placeholder.imei);
        Assert.Equal(1201, placeholder.smsId);
        Assert.Equal(6, placeholder.type);
        Assert.Equal(6, placeholder.sentNoticeType);
        Assert.True(placeholder.isSentNoticeReceived);
        Assert.NotNull(placeholder.sentNoticeAt);
        Assert.Equal("SentNotice", placeholder.source);
        Assert.Equal(1, await db.SmsRecords.CountAsync());
    }

    [Fact]
    public async Task UpsertCallLogRecord_ShouldBeIdempotentByOwnerImeiAndCallLogId()
    {
        await using var db = CreateDbContext();

        var first = await db.UpsertCallLogRecord(
            OwnerWxid,
            ImeiA,
            callLogId: 2001,
            number: " 13800000002 ",
            type: 1,
            rawDate: 1_700_000_020_000,
            durationSeconds: 15,
            recordUrl: "/uploads/call/old.amr",
            simId: 0,
            blockType: 0,
            source: "Push");

        var second = await db.UpsertCallLogRecord(
            OwnerWxid,
            ImeiA,
            callLogId: 2001,
            number: "13800000002",
            type: 2,
            rawDate: 1_700_000_030_000,
            durationSeconds: 25,
            recordUrl: "/uploads/call/new.amr",
            simId: 1,
            blockType: 4,
            source: "Pull");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.id, second.id);
        Assert.Equal(1, await db.CallLogRecords.CountAsync());

        var saved = await db.CallLogRecords.AsNoTracking().SingleAsync();
        Assert.Equal("13800000002", saved.number);
        Assert.Equal(2, saved.type);
        Assert.Equal(25, saved.durationSeconds);
        Assert.Equal("/uploads/call/new.amr", saved.recordUrl);
        Assert.Equal(1, saved.simId);
        Assert.Equal(4, saved.blockType);
        Assert.Equal("Pull", saved.source);
    }

    [Fact]
    public async Task GetSmsRecords_ShouldReturnAllImeiRecordsWhenImeiFilterEmpty()
    {
        await using var db = CreateDbContext();
        await db.UpsertSmsRecord(OwnerWxid, ImeiA, 1301, 1, "10010", 1, 1_700_000_000_000, "A", false, 0, 0, "Pull");
        await db.UpsertSmsRecord(OwnerWxid, ImeiB, 1302, 2, "10086", 1, 1_700_000_010_000, "B", false, 0, 0, "Pull");
        await db.UpsertSmsRecord(OtherOwnerWxid, ImeiA, 1303, 3, "10000", 1, 1_700_000_020_000, "其他账号", false, 0, 0, "Pull");

        var allForOwner = await db.GetSmsRecords(OwnerWxid, imei: "", count: 10);
        var onlyA = await db.GetSmsRecords(OwnerWxid, imei: ImeiA, count: 10);

        Assert.Equal(2, allForOwner.Count);
        Assert.Contains(allForOwner, item => item.imei == ImeiA);
        Assert.Contains(allForOwner, item => item.imei == ImeiB);
        Assert.Single(onlyA);
        Assert.Equal(ImeiA, onlyA[0].imei);
    }

    [Fact]
    public async Task GetCallLogRecords_ShouldReturnAllImeiRecordsWhenImeiFilterEmpty()
    {
        await using var db = CreateDbContext();
        await db.UpsertCallLogRecord(OwnerWxid, ImeiA, 2301, "13800000001", 1, 1_700_000_000_000, 3, "", 0, 0, "Pull");
        await db.UpsertCallLogRecord(OwnerWxid, ImeiB, 2302, "13800000002", 2, 1_700_000_010_000, 5, "", 0, 0, "Pull");
        await db.UpsertCallLogRecord(OtherOwnerWxid, ImeiA, 2303, "13800000003", 3, 1_700_000_020_000, 7, "", 0, 0, "Pull");

        var allForOwner = await db.GetCallLogRecords(OwnerWxid, imei: "", count: 10);
        var onlyA = await db.GetCallLogRecords(OwnerWxid, imei: ImeiA, count: 10);

        Assert.Equal(2, allForOwner.Count);
        Assert.Contains(allForOwner, item => item.imei == ImeiA);
        Assert.Contains(allForOwner, item => item.imei == ImeiB);
        Assert.Single(onlyA);
        Assert.Equal(ImeiA, onlyA[0].imei);
    }

    [Fact]
    public void PhoneRecordsPage_ShouldNotUseDeviceUuidAsImeiFallback()
    {
        var method = ExtractMethod(
            ReadSource("SCRM.UI", "Components", "Pages", "PhoneRecords.razor"),
            "private string ResolveSelectedImei()",
            "private static bool IsDeviceUsableForWechatTask");

        Assert.Contains("return device?.device?.IMEI?.Trim() ?? string.Empty;", method);
        Assert.DoesNotContain("FirstNonEmpty(device?.device?.IMEI, device?.uuid)", method);
        Assert.DoesNotContain("return FirstNonEmpty", method);
    }

    [Fact]
    public void PhoneMessageHandler_ShouldPersistPullSmsRecordsBeforeCompletingFailedTask()
    {
        var method = ExtractMethod(
            ReadSource("SCRM.API", "Services", "Netty", "Handlers", "PhoneMessageHandler.cs"),
            "private async Task HandlePullSmsResult",
            "private async Task HandleCallLogPush");

        Assert.Contains("var saved = await _db.UpsertSmsRecords(msg.WeChatId, msg.IMEI, records, \"Pull\");", method);
        Assert.Contains("var success = msg.Success && string.IsNullOrWhiteSpace(msg.ErrMsg);", method);
        Assert.Contains("已落库 {saved.Count}/{msg.Messages.Count} 条", method);
        Assert.True(method.IndexOf("UpsertSmsRecords", StringComparison.Ordinal) < method.IndexOf("CompleteAndPublishTaskResultAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void PhoneMessageHandler_ShouldTreatPullCallLogErrMsgAsFailureEvenWhenSuccessTrue()
    {
        var method = ExtractMethod(
            ReadSource("SCRM.API", "Services", "Netty", "Handlers", "PhoneMessageHandler.cs"),
            "private async Task HandlePullCallLogResult",
            "private async Task CompleteAndPublishTaskResultAsync");

        Assert.Contains("var saved = await _db.UpsertCallLogRecords(msg.WeChatId, msg.IMEI, records, \"Pull\");", method);
        Assert.Contains("var success = msg.Success && string.IsNullOrWhiteSpace(msg.ErrMsg);", method);
        Assert.Contains("Success=true + ErrMsg", method);
        Assert.Contains("已落库 {saved.Count}/{msg.Messages.Count} 条", method);
        Assert.True(method.IndexOf("UpsertCallLogRecords", StringComparison.Ordinal) < method.IndexOf("CompleteAndPublishTaskResultAsync", StringComparison.Ordinal));
    }

    private static PhoneRecordsTestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PhoneRecordsTestDbContext>()
            .UseInMemoryDatabase($"phone-records-{Guid.NewGuid():N}")
            .Options;

        return new PhoneRecordsTestDbContext(options);
    }

    private static string ReadSource(params string[] parts)
    {
        var solutionRoot = FindSolutionRoot();
        return File.ReadAllText(Path.Combine(new[] { solutionRoot }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到开始标记：{startMarker}");

        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"未找到结束标记：{endMarker}");

        return source[start..end];
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SCRM.SOLUTION.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("未找到 SCRM.SOLUTION.sln，无法定位 SCRM 源码。");
    }

    private sealed class PhoneRecordsTestDbContext : DbContext
    {
        public PhoneRecordsTestDbContext(DbContextOptions<PhoneRecordsTestDbContext> options)
            : base(options)
        {
        }

        public DbSet<SmsRecord> SmsRecords => Set<SmsRecord>();

        public DbSet<CallLogRecord> CallLogRecords => Set<CallLogRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SmsRecord>(entity =>
            {
                entity.HasKey(item => item.id);
                entity.Property(item => item.id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<CallLogRecord>(entity =>
            {
                entity.HasKey(item => item.id);
                entity.Property(item => item.id).ValueGeneratedOnAdd();
            });
        }
    }
}
