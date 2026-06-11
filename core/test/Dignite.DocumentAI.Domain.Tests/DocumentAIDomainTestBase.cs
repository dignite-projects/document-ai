using Volo.Abp.Modularity;

namespace Dignite.DocumentAI;

/* Inherit from this class for your domain layer tests.
 */
public abstract class DocumentAIDomainTestBase<TStartupModule> : DocumentAITestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
