using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Pecee.Emby.Plugin.Vod.Configuration;

namespace Pecee.Emby.Plugin.Vod.Models
{
	public sealed class VodPlaylist : MediaBrowser.Controller.Entities.Folder
	{
		public Guid IdentifierId { get; set; }

		public String PlaylistUrl { get; set; }

		public String UserId { get; set; }

		private String _collectionType;

		public String CollectionType
		{
			get { return _collectionType; }
			set
			{
				if (!PluginConfiguration.AllowedCollectionTypes.Contains(value))
				{
					throw new ArgumentException("Invalid collection-type");
				}

				_collectionType = value;
			}
		}

		public DateTime LastImportDate { get; set; }

		public VodPlaylist()
		{
			IdentifierId = Guid.NewGuid();
			SourceType = SourceType.Library;
			ChannelId = PluginConfiguration.PluginId;
		}

		public async Task<bool> Merge(VodPlaylist remote)
		{
			var hasUpdate = false;

			if (remote.PlaylistUrl != this.PlaylistUrl)
			{
				hasUpdate = true;
				this.PlaylistUrl = remote.PlaylistUrl;
			}

			if (remote.SourceType != this.SourceType)
			{
				hasUpdate = true;
				this.SourceType = remote.SourceType;
			}

			if (hasUpdate)
			{
				await this.UpdateToRepository(ItemUpdateType.None, CancellationToken.None);
			}

			return hasUpdate;
		}
	}
}
