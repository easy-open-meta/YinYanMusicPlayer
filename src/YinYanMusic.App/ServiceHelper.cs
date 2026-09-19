using Microsoft.Extensions.DependencyInjection;

namespace YinYanMusic.App;

public static class ServiceHelper
{
	public static IServiceProvider? Provider { get; set; }

	public static T GetRequiredService<T>() where T : class =>
		Provider?.GetRequiredService<T>()
		?? Application.Current?.Handler?.MauiContext?.Services.GetService<T>()
		?? throw new InvalidOperationException($"服务 {typeof(T).Name} 未注册。");
}
