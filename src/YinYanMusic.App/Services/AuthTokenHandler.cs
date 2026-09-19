using Microsoft.Extensions.DependencyInjection;
using YinYanMusic.App.Services;

public class AuthTokenHandler(IServiceProvider sp) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var auth = sp.GetRequiredService<IAuthService>();
        if (auth.Token is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
        return base.SendAsync(request, cancellationToken);
    }
}
