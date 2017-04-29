using System;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Pecee.Emby.Plugin.Vod.Models
{
	public class VodMovie : Movie, IVodMedia
	{
		public Guid IdentifierId { get; set; }

		public override LocationType LocationType => LocationType.Remote;

		[IgnoreDataMember]
		public VodPlaylist Playlist { get; set; }

		public VodMovie()
		{
			this.VideoType = VideoType.VideoFile;
			DefaultVideoStreamIndex = -1;
			IsVirtualItem = false;
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

		public override bool SupportsLocalMetadata => true;

		protected override string GetInternalMetadataPath(string basePath)
		{
			return VodPlaylist.GetInternalMetadataPath(basePath, this.Id);
		}

		public static string GetInternalMetadataPath(string basePath, Guid id)
		{
			return System.IO.Path.Combine(basePath, "channels", id.ToString("N"), "metadata");
		}

	}
}
