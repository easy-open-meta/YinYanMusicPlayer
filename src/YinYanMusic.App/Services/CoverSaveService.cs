#if ANDROID
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
#endif

namespace YinYanMusic.App.Services;

/// <summary>
/// 「保存封面」的落盘实现（平台条件编译对称，全局约束 4）：
/// - Android 10+：写入公共 <c>Download/YinYanMusic/</c>（MediaStore.Downloads，无需任何存储权限，
///   文件管理器立即可见 —— 与网易云"下载到 Download"同一套机制）；
/// - Android 7~9：公共 Download 直写（WRITE_EXTERNAL_STORAGE 运行时权限，manifest 仅 maxSdk=28 声明）；
/// - Windows：用户"图片"库下的 YinYanMusic（资源管理器可见）。
/// 返回用于展示的保存路径；Android 10+ 重名文件由系统自动追加 " (n)"，展示名以媒体库回读为准。
/// </summary>
public static class CoverSaveService
{
	public static async Task<string> SaveAsync(string fileName, string extension, byte[] bytes)
	{
#if ANDROID
		var mime = extension switch
		{
			".png" => "image/png",
			".webp" => "image/webp",
			".gif" => "image/gif",
			_ => "image/jpeg",
		};
		if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
			return await SaveViaMediaStoreAsync(fileName, mime, bytes);
		return await SaveLegacyAsync(fileName, bytes);
#elif WINDOWS
		var picturesDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
		var saveDir = Path.Combine(picturesDir, "YinYanMusic");
		Directory.CreateDirectory(saveDir);
		var path = Path.Combine(saveDir, fileName);
		File.WriteAllBytes(path, bytes);
		return path;
#else
		await Task.CompletedTask;
		throw new PlatformNotSupportedException();
#endif
	}

#if ANDROID
	/// <summary>
	/// Android 10+：插入 <see cref="MediaStore.Downloads"/>。这属于"向共享空间贡献自己的文件"，
	/// 系统不要求任何存储权限；RELATIVE_PATH 限定在 Download 之下。
	/// </summary>
	private static async Task<string> SaveViaMediaStoreAsync(string fileName, string mime, byte[] bytes)
	{
		var resolver = global::Android.App.Application.Context.ContentResolver
			?? throw new InvalidOperationException("系统 ContentResolver 不可用。");

		var values = new ContentValues();
		// 列名直接用字面量（即媒体库协议值），不依赖各 API 绑定里 MediaColumns / IMediaColumns 的形态差异。
		values.Put("_display_name", fileName);
		values.Put("mime_type", mime);
		values.Put("relative_path", Android.OS.Environment.DirectoryDownloads + "/YinYanMusic");

		var uri = resolver.Insert(MediaStore.Downloads.ExternalContentUri, values)
			?? throw new InvalidOperationException("系统拒绝了写入请求。");
		try
		{
			using (var os = resolver.OpenOutputStream(uri))
			{
				if (os is null) throw new InvalidOperationException("无法打开系统媒体库的写入流。");
				await os.WriteAsync(bytes, 0, bytes.Length);
			}
		}
		catch
		{
			// 半截文件别留在媒体库里
			try { resolver.Delete(uri, null, null); } catch { }
			throw;
		}
		// 重名时系统自动改名，展示名以回读为准
		return $"Download/YinYanMusic/{QueryDisplayName(resolver, uri, fileName)}";
	}

	private static string QueryDisplayName(ContentResolver resolver, Android.Net.Uri uri, string fallback)
	{
		try
		{
			using var c = resolver.Query(uri, ["_display_name"], null, null, null);
			if (c is not null && c.MoveToFirst())
			{
				var name = c.GetString(0);
				if (!string.IsNullOrEmpty(name)) return name;
			}
		}
		catch { }
		return fallback;
	}

	/// <summary>
	/// Android 7~9（Scoped Storage 之前）：公共 Download 直写，需要运行时存储权限。
	/// 权限申请复用本地扫描的同一个桥接器（RequestCode 1002，同一时刻只有一次申请）。
	/// </summary>
	private static async Task<string> SaveLegacyAsync(string fileName, byte[] bytes)
	{
		const string permission = global::Android.Manifest.Permission.WriteExternalStorage;
		var context = global::Android.App.Application.Context;
		if (context.CheckSelfPermission(permission) != Permission.Granted &&
			!await AndroidLocalMediaScanner.RequestNativePermissionAsync(permission))
		{
			throw new InvalidOperationException("未授予存储权限，无法保存到 Download。");
		}

		var downloads = global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads)!;
		var dir = new Java.IO.File(downloads.AbsolutePath, "YinYanMusic");
		if (!dir.Exists() && !dir.Mkdirs())
			throw new InvalidOperationException("无法创建 Download/YinYanMusic 目录。");

		var stem = Path.GetFileNameWithoutExtension(fileName);
		var ext = Path.GetExtension(fileName);
		var target = new Java.IO.File(dir, fileName);
		for (var n = 1; target.Exists(); n++)
		{
			target = new Java.IO.File(dir, $"{stem} ({n}){ext}");
		}
		await File.WriteAllBytesAsync(target.AbsolutePath, bytes);
		return target.AbsolutePath;
	}
#endif
}
