using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Dignite.DocumentAI.Host.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Authentication;
using Shouldly;
using Xunit;

namespace Dignite.DocumentAI.Host.Authentication;

/// <summary>
/// #278 回归护栏：把"为什么 /mcp 必须用 scheme-free 授权策略 + <see cref="McpDiscoveryAuthorizationResultHandler"/>
/// 定向 challenge、而不是把 McpAuth 直接挂进策略的 AuthenticationSchemes"这条隐性约束锁死。
///
/// 用最小 ASP.NET 管线（TestServer）精确镜像 host 的 MCP 接线：默认 token scheme +
/// 一个模仿 ABP UseDynamicClaims 的富化中间件（把认证结果改写成带 stage=enriched 的 principal）+
/// 真实的 <see cref="McpDiscoveryAuthorizationResultHandler"/> / <see cref="McpDiscoveryChallengeMarker"/> +
/// SDK 的 AddMcp。不引 ABP 宿主 / DB / OpenIddict。
///
/// 关键对照（<see cref="Authenticated_request_keeps_dynamic_claims_enriched_principal"/> vs
/// <see cref="Explicit_scheme_policy_drops_enrichment_documents_the_regression"/>）：scheme-free 策略 →
/// 端点看到 enriched principal；显式挂 scheme → PolicyEvaluator 重新认证、覆盖 context.User → 退回 raw。
/// 后人若"顺手简化"把 McpAuth 挂回策略，前一个测试会变红。
/// </summary>
public class McpDiscoveryAuthorization_Tests
{
    private const string TokenScheme = "Token";
    private const string EnrichedClaim = "enriched";
    private const string RawClaim = "raw";

    [Fact]
    public async Task Authenticated_request_keeps_dynamic_claims_enriched_principal()
    {
        using var server = await BuildServerAsync(withDiscovery: true, useExplicitScheme: false);
        using var client = server.CreateClient();

        var response = await WithTokenAsync(client).GetAsync("/mcp");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // scheme-free 策略 → 授权读取 UseDynamicClaims 富化过的 ambient principal，不重新认证。
        (await response.Content.ReadAsStringAsync()).ShouldBe(EnrichedClaim);
    }

    [Fact]
    public async Task Unauthenticated_request_gets_401_with_resource_metadata_pointer()
    {
        using var server = await BuildServerAsync(withDiscovery: true, useExplicitScheme: false);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/mcp");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // McpDiscoveryAuthorizationResultHandler 把 challenge 定向到 McpAuth，注入 RFC 9728 发现指针。
        var wwwAuth = response.Headers.WwwAuthenticate.ToString();
        wwwAuth.ShouldContain("resource_metadata");
        wwwAuth.ShouldContain("/.well-known/oauth-protected-resource");
    }

    [Fact]
    public async Task Explicit_scheme_policy_drops_enrichment_documents_the_regression()
    {
        // 反例：把 token scheme 挂进策略 AuthenticationSchemes（= 被否决的"简单版"）。
        using var server = await BuildServerAsync(withDiscovery: false, useExplicitScheme: true);
        using var client = server.CreateClient();

        var response = await WithTokenAsync(client).GetAsync("/mcp");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // PolicyEvaluator 因显式 scheme 重新认证 → 覆盖 context.User → 富化丢失，退回 raw。
        (await response.Content.ReadAsStringAsync()).ShouldBe(RawClaim);
    }

    private static HttpClient WithTokenAsync(HttpClient client)
    {
        // StubTokenHandler 只看 Authorization header 是否存在，不校验内容。
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        return client;
    }

    private static async Task<TestServer> BuildServerAsync(bool withDiscovery, bool useExplicitScheme)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();

                    var auth = services
                        .AddAuthentication(TokenScheme)
                        .AddScheme<AuthenticationSchemeOptions, StubTokenHandler>(TokenScheme, null);

                    if (withDiscovery)
                    {
                        auth.AddMcp(options => options.ResourceMetadata = new ProtectedResourceMetadata
                        {
                            AuthorizationServers = new List<string> { "https://auth.example/" },
                            ScopesSupported = new List<string> { "DocumentAI" },
                            BearerMethodsSupported = new List<string> { "header" }
                        });
                    }

                    services.AddAuthorization();

                    if (withDiscovery)
                    {
                        services.Replace(ServiceDescriptor.Singleton<IAuthorizationMiddlewareResultHandler, McpDiscoveryAuthorizationResultHandler>());
                    }
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();

                    // 模仿 ABP UseDynamicClaims：认证后把 IAuthenticateResultFeature 改写成富化 principal。
                    app.Use(async (ctx, next) =>
                    {
                        if (ctx.User.Identity?.IsAuthenticated == true)
                        {
                            var feature = ctx.Features.Get<IAuthenticateResultFeature>();
                            if (feature?.AuthenticateResult is not null)
                            {
                                var identity = new ClaimsIdentity(TokenScheme);
                                identity.AddClaim(new Claim("stage", EnrichedClaim));
                                feature.AuthenticateResult = AuthenticateResult.Success(
                                    new AuthenticationTicket(new ClaimsPrincipal(identity), TokenScheme));
                            }
                        }

                        await next();
                    });

                    app.UseAuthorization();

                    app.UseEndpoints(endpoints =>
                    {
                        var endpoint = endpoints.MapGet(
                            "/mcp",
                            (HttpContext context) => context.User.FindFirst("stage")?.Value ?? "none");

                        if (useExplicitScheme)
                        {
                            endpoint.RequireAuthorization(policy => policy
                                .RequireAuthenticatedUser()
                                .AddAuthenticationSchemes(TokenScheme));
                        }
                        else
                        {
                            endpoint.RequireAuthorization();
                        }

                        if (withDiscovery)
                        {
                            endpoint.WithMetadata(McpDiscoveryChallengeMarker.Instance);
                        }
                    });
                });
            })
            .StartAsync();

        return host.GetTestServer();
    }

    // 站位的 token scheme：有 Authorization header 即认证成功（raw principal），否则 NoResult（触发 challenge）。
    // 对应 OpenIddict validation 从 bearer token 重新解析出未富化 principal。
    private sealed class StubTokenHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public StubTokenHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(TokenScheme);
            identity.AddClaim(new Claim("stage", RawClaim));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), TokenScheme)));
        }
    }
}
