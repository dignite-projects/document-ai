using System.Collections.Generic;

namespace Dignite.Paperbase.Ocr.AzureDocumentIntelligence;

public class AzureDocumentIntelligenceOptions
{
    public string Endpoint { get; set; } = default!;
    public string ApiKey { get; set; } = default!;

    /// <summary>使用的预建模型。默认 "prebuilt-read"，日文识别推荐 "prebuilt-document"。</summary>
    public string ModelId { get; set; } = "prebuilt-read";

    /// <summary>
    /// Optional provider-local profile → model override. Core never knows these IDs;
    /// hosts may map high-accuracy/table-heavy profiles to provider-specific models here.
    /// </summary>
    public Dictionary<string, string> ProfileModelIds { get; set; } = new();
}
