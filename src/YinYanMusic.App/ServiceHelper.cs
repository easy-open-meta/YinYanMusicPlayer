using Microsoft.Extensions.DependencyInjection;

namespace YinYanMusic.App;

public static class ServiceHelper
{
	public static IServiceProvider? Provider { get; set; }

	public static T GetRequiredService<T>() where T : class =>
		Provider?.GetRequiredService<T>()
		?? Application.Current?.Handler?.MauiContext?.Services.GetService<T>()
		?? throw new InvalidOperationException($"服务 {typeof(T).Name} 未注册。");

	/// <summary>
	/// 拿不到就返回 null 的版本。用于**可选依赖**（如 V2.6 的本地曲库）：
	/// 调用方据此走降级分支，而不是让整个启动/播放流程因为一个可选服务缺失而崩掉。
	/// </summary>
	public static T? GetService<T>() where T : class =>
		Provider?.GetService<T>()
		?? Application.Current?.Handler?.MauiContext?.Services.GetService<T>();
}
