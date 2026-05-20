using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Paperbase.Ocr;

public interface IOcrProbeProvider
{
    Task<OcrProbeResult> ProbeAsync(
        Stream fileStream,
        OcrProbeOptions options,
        CancellationToken cancellationToken = default);
}
