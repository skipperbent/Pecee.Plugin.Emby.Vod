using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Plugins;
using Pecee.Emby.Plugin.Vod.Entities;

namespace Pecee.Emby.Plugin.Vod.Configuration
{
	public class PluginConfiguration : BasePluginConfiguration
	{
		public static string[] AllowedCollectionTypes = new string[]
		{
			CollectionType.Movies,
			CollectionType.MusicVideos,
			CollectionType.TvShows,
		};

		public string ChannelName { get; set; }

		public static string Name = "Video On Demand";

		public static string Description =
			"Add remote playlist for instant Video On Demand that will be integrated with your local library.";

		public static string Version = "1.0.0.0";

		public static string HomepageUrl = "http://www.pecee.dk";

		public static string PluginId = "7A54F58C-F05E-4BFB-AD64-7E92FAEB0DA2";

		public bool ChannelEnabled { get; set; }

		public PlaylistConfig[] Playlists { get; set; }

		public PluginConfiguration()
		{
			Playlists = new PlaylistConfig[] { };
			ChannelEnabled = true;
		}
	}
	
}
