using System.Linq;
using Shouldly;
using Xunit;

namespace Dignite.Paperbase.Mcp.Documents;

/// <summary>
/// #210：MCP 出口（检索 tool 的 <see cref="DocumentSearchResultItem"/>）<b>完全不透出</b>原生 payload /
/// provenance 元数据——AI 客户端用不到，且内部归档 BlobName 是存储 key。纯反射断言，无需 ABP 宿主。
/// </summary>
public class DocumentSearchResultItemExposure_Tests
{
    [Fact]
    public void Mcp_Search_Result_Excludes_Extraction_Provenance_And_Native_Payload()
    {
        var propertyNames = typeof(DocumentSearchResultItem).GetProperties().Select(p => p.Name).ToArray();

        propertyNames.ShouldNotContain("ExtractionProviderName");
        propertyNames.ShouldNotContain("ExtractionMetadata");
        propertyNames.ShouldNotContain("ExtractionPath");
        propertyNames.ShouldNotContain("ProviderSteps");
        propertyNames.ShouldNotContain("HasNativePayload");

        // 任何含 "NativePayload" / "BlobName" 的字段都不应出现在 MCP 投影里。
        propertyNames.ShouldAllBe(n =>
            !n.Contains("NativePayload") && !n.Contains("BlobName"));
    }
}
