using Microsoft.AspNetCore.HttpOverrides;

namespace ReviewPlatform.Api.Infrastructure;

/// <summary>
/// За nginx адрес соединения — адрес прокси, и лимиты запросов по IP делились бы на всех пользователей.
/// Реальный адрес берётся из X-Forwarded-For, но только если запрос пришёл от доверенной сети (по умолчанию —
/// частные сети, где живут контейнеры): внешний клиент, обратившийся к API напрямую, не подменит свой IP.
/// </summary>
internal static class ReverseProxy
{
    private static readonly string[] DefaultKnownNetworks = ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "::1/128"];

    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // nginx дописывает адрес клиента последним — берём только его
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            var networks = configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() is { Length: > 0 } configured ? configured : DefaultKnownNetworks;
            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });
}
