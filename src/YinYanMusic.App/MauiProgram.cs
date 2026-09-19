﻿﻿﻿﻿﻿using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.App.Views;

namespace YinYanMusic.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()

			.UseMauiCommunityToolkitMediaElement(true)
			.ConfigureFonts(fonts =>
			{
			fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
			});

		Routing.RegisterRoute("playlist", typeof(PlaylistDetailPage));
		Routing.RegisterRoute("nowplaying", typeof(NowPlayingPage));
		Routing.RegisterRoute("artist", typeof(ArtistDetailPage));
		Routing.RegisterRoute("zones", typeof(ZonesPage));
		Routing.RegisterRoute("zone", typeof(ZonePage));
		Routing.RegisterRoute("followArtists", typeof(FollowArtistsPage));
		Routing.RegisterRoute("myPlaylists", typeof(MyPlaylistsPage));
		Routing.RegisterRoute("followers", typeof(FollowersPage));
		Routing.RegisterRoute("userDetail", typeof(UserPage));

		// 基础设施
		builder.Services.AddSingleton<IAuthService, AuthService>();
		builder.Services.AddSingleton<IMusicApi, MusicApiService>();
		builder.Services.AddSingleton<PlayerService>();
		builder.Services.AddTransient<AuthTokenHandler>();

		// HttpClient（自动注入令牌）
		builder.Services.AddSingleton<HttpClient>(sp =>
		{
			var handler = new HttpClientHandler();
			return new HttpClient(new AuthTokenHandler(sp)
			{
				InnerHandler = handler
			})
			{
				BaseAddress = new Uri(ApiConfig.BaseUrl),
				Timeout = TimeSpan.FromSeconds(30)
			};
		});

		// ViewModel
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<RegisterViewModel>();
		builder.Services.AddSingleton<HomeViewModel>();
		builder.Services.AddSingleton<LibraryViewModel>();
		builder.Services.AddSingleton<SearchViewModel>();
		builder.Services.AddTransient<PlaylistDetailViewModel>();
		builder.Services.AddTransient<NowPlayingViewModel>();
		builder.Services.AddTransient<ArtistDetailViewModel>();
		builder.Services.AddTransient<ZonesViewModel>();
		builder.Services.AddTransient<ZoneViewModel>();
		builder.Services.AddTransient<FollowArtistsViewModel>();
		builder.Services.AddTransient<MyPlaylistsViewModel>();
		builder.Services.AddTransient<FollowersViewModel>();
		builder.Services.AddTransient<UserDetailViewModel>();

		// View
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<RegisterPage>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<HomeView>();
		builder.Services.AddTransient<LibraryView>();
		builder.Services.AddTransient<SearchView>();
		builder.Services.AddTransient<PlaylistDetailPage>();
		builder.Services.AddTransient<NowPlayingPage>();
		builder.Services.AddTransient<ArtistDetailPage>();
		builder.Services.AddTransient<ZonesPage>();
		builder.Services.AddTransient<ZonePage>();
		builder.Services.AddTransient<FollowArtistsPage>();
		builder.Services.AddTransient<MyPlaylistsPage>();
		builder.Services.AddTransient<FollowersPage>();
		builder.Services.AddTransient<UserPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		ServiceHelper.Provider = app.Services;
		return app;
	}
}
