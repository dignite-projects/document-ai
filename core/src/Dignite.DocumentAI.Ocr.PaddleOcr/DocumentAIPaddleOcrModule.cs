using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.Modularity;

namespace Dignite.DocumentAI.Ocr.PaddleOcr;

[DependsOn(typeof(DocumentAIOcrModule))]
public class DocumentAIPaddleOcrModule : AbpModule
{
    internal const string HttpClientName = "PaddleOcr";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.Configure<PaddleOcrOptions>(
            configuration.GetSection("PaddleOcr"));

        // 使用具名 HttpClient，超时从 PaddleOcrOptions.TimeoutSeconds 读取。
        // PP-StructureV3 在 CPU 上处理多页图片 PDF 可能远超默认 100s，此处延长上限。
        context.Services.AddHttpClient(HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<PaddleOcrOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });
    }
}
