using System.Collections.Generic;

namespace Dignite.Paperbase.Ocr;

public class OcrProbeOptions
{
    public IList<string> LanguageHints { get; set; } = new List<string>();
    public string ContentType { get; set; } = string.Empty;
    public string RequestedOcrProfileCode { get; set; } = OcrProfileCodes.Auto;
    public int MaxPages { get; set; } = 2;
}
