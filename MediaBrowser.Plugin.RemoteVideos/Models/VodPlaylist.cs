using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Pecee.Emby.Plugin.Vod.Models
{
	public sealed class VodPlaylist : MediaBrowser.Controller.Entities.Folder, ICollectionFolder
	{
		public Guid IdentifierId { get; set; }

		public String PlaylistUrl { get; set; }

		public String UserId { get; set; }

		public override bool SupportsThemeMedia => true;

		//private String _collectionType;

		public override bool IsDisplayedAsFolder => false;

		public string CollectionType { get { return "VodMovie"; } }

		public override LocationType LocationType => LocationType.Remote;

		/*public String CollectionType
		{
			get { return "VodMovie"; }
			set
			{
				if (!PluginConfiguration.AllowedCollectionTypes.Contains(value))
				{
					throw new ArgumentException("Invalid collection-type");
				}

				if (_collectionType == MediaBrowser.Model.Entities.CollectionType.Movies)
				{
					_collectionType = "VodMovie";
				}
			}
		}*/

		public override bool SupportsLocalMetadata => true;

		public override bool SupportsDateLastMediaAdded => true;

		public DateTime LastImportDate { get; set; }
		
		public VodPlaylist()
		{
			IdentifierId = Guid.NewGuid();
			SourceType = SourceType.Library;
			IsVirtualItem = false;
			//ChannelId = PluginConfiguration.PluginId;
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
