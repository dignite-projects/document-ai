using Dignite.Paperbase.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;

namespace Dignite.Paperbase.Host.Data;

public class PaperbaseHostDbContext
    : AbpDbContext<PaperbaseHostDbContext>, IHasEventInbox, IHasEventOutbox
{

    public const string DbTablePrefix = "App";
    public const string DbSchema = null;

    /// <summary>
    /// ABP transactional outbox 入队表（<see cref="IHasEventOutbox"/>）。
    /// Paperbase 出口事件由调用方在 UoW 内 <c>IDistributedEventBus.PublishAsync</c> 入队，
    /// ABP 后台 worker 异步真正投递到消息中间件，保证 at-least-once 投递。
    /// </summary>
    public DbSet<OutgoingEventRecord> OutgoingEvents { get; set; } = default!;

    /// <summary>
    /// ABP transactional inbox 投递表（<see cref="IHasEventInbox"/>）。
    /// 用于 Paperbase 自身订阅外部分布式事件时的 exactly-once 消费追踪。
    /// </summary>
    public DbSet<IncomingEventRecord> IncomingEvents { get; set; } = default!;

    public PaperbaseHostDbContext(DbContextOptions<PaperbaseHostDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigurePermissionManagement();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();

        // ABP transactional outbox / inbox 表（替代 issue #188 删除的自造 OutboxEvent）。
        // 调用方在 UoW 内 publish 时，事件自动写入 AbpEventOutbox；后台 worker 扫表真正投递。
        builder.ConfigureEventInbox();
        builder.ConfigureEventOutbox();

        // Paperbase core module
        builder.ConfigurePaperbase();
    }
}
