using System.Threading.Tasks;
using Dignite.DocumentAI.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace Dignite.DocumentAI.Host.Data;

public class DocumentAIHostRoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IdentityRoleManager _roleManager;
    private readonly IPermissionManager _permissionManager;

    public DocumentAIHostRoleDataSeedContributor(
        IIdentityRoleRepository roleRepository,
        IdentityRoleManager roleManager,
        IPermissionManager permissionManager)
    {
        _roleRepository = roleRepository;
        _roleManager = roleManager;
        _permissionManager = permissionManager;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await SeedRoleAsync("DocumentManager", new[]
        {
            DocumentAIPermissions.Documents.Default,
            DocumentAIPermissions.Documents.Upload,
            DocumentAIPermissions.Documents.Export,
        });

        await SeedRoleAsync("Viewer", new[]
        {
            DocumentAIPermissions.Documents.Default,
        });
    }

    private async Task SeedRoleAsync(string roleName, string[] permissions)
    {
        var role = await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
        if (role == null)
        {
            await _roleManager.CreateAsync(new IdentityRole(System.Guid.NewGuid(), roleName));
            role = await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
        }

        foreach (var permission in permissions)
        {
            await _permissionManager.SetForRoleAsync(roleName, permission, true);
        }
    }
}
