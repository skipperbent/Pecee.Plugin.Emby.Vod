using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Pecee.Emby.Plugin.Vod.Configuration;

namespace Pecee.Emby.Plugin.Vod.Models
{
	public class VodMovie : Movie, IVodMedia
	{
		public Guid IdentifierId { get; set; }

		public VodMovie()
		{
			this.VideoType = VideoType.VideoFile;
			DefaultVideoStreamIndex = -1;
			ChannelId = PluginConfiguration.PluginId;
		}

		public async Task<bool> Merge(VodMovie vodMovie)
		{
			var hasUpdate = false;

			if (vodMovie.Path != this.Path)
			{
				hasUpdate = true;
				this.Path = vodMovie.Path;
				this.ChannelMediaSources = vodMovie.ChannelMediaSources;
			}

			if (hasUpdate)
			{
				await this.UpdateToRepository(ItemUpdateType.None, CancellationToken.None);
			}

			return hasUpdate;
		}

	}
}
