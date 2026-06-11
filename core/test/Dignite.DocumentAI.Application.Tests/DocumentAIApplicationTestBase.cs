using Volo.Abp.Modularity;

namespace Dignite.DocumentAI;

/* Inherit from this class for your application layer tests.
 */
public abstract class DocumentAIApplicationTestBase<TStartupModule> : DocumentAITestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
