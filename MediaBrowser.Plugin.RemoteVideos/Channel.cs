using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using Pecee.Emby.Plugin.Vod.Configuration;
using Pecee.Emby.Plugin.Vod.Folder;
using Pecee.Emby.Plugin.Vod.Models;
using ChannelFolderType = Pecee.Emby.Plugin.Vod.Folder.ChannelFolderType;

namespace Pecee.Emby.Plugin.Vod
{
	public class Channel : IChannel, IIndexableChannel
	{
		public string Name => PluginConfiguration.Name;

		public string Description => PluginConfiguration.Description;

		public string DataVersion
		{
			get { return "112"; }
		}

		public string HomePageUrl => PluginConfiguration.HomepageUrl;

		public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

		private readonly ILogger _logger;
		private readonly IProviderManager _providerManager;
		private readonly ILibraryManager _libraryManager;

		public Channel(ILogManager logManager, IProviderManager providerManager, ILibraryManager libraryManager)
		{
			_logger = logManager.GetLogger(PluginConfiguration.Name);
			_providerManager = providerManager;
			_libraryManager = libraryManager;
		}

		public InternalChannelFeatures GetChannelFeatures()
		{
			return new InternalChannelFeatures
			{
				ContentTypes = new List<ChannelMediaContentType>
				 {
					 ChannelMediaContentType.Movie,
					 ChannelMediaContentType.MovieExtra,
				 },

				MediaTypes = new List<ChannelMediaType>
				{
					ChannelMediaType.Video,
				},

				SupportsSortOrderToggle = true,
				SupportsContentDownloading = true,

				DefaultSortFields = new List<ChannelItemSortField>
				{
					ChannelItemSortField.Name,
					ChannelItemSortField.CommunityRating,
					ChannelItemSortField.CommunityPlayCount,
					ChannelItemSortField.PremiereDate,
					ChannelItemSortField.Runtime,
					ChannelItemSortField.DateCreated,
				},
				AutoRefreshLevels = 2
			};
		}

		public bool IsEnabledFor(string userId)
		{
			// TODO: change channel disabled
			return Plugin.Instance.Configuration.ChannelEnabled;
		}

		public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
		{
			switch (type)
			{
				case ImageType.Primary:
				case ImageType.Thumb:
				case ImageType.Backdrop:
					return new Task<DynamicImageResponse>(() => Plugin.GetImage(type.ToString().ToLower()));
			}
			throw new ArgumentException("Unsupported image type: " + type);
		}

		public IEnumerable<ImageType> GetSupportedChannelImages()
		{
			return new List<ImageType>
			{
				ImageType.Thumb,
				ImageType.Backdrop,
				ImageType.Primary
			};
		}

		public ChannelItemResult GetChannelPlaylists(InternalChannelItemQuery query)
		{
			var playlists = Plugin.Instance.Configuration.Playlists;

			var channelItems = new List<ChannelItemInfo>();

			foreach (var playlist in playlists)
			{	
				var item = new ChannelItemInfo()
				{
					Id = ChannelFolder.GetUrl(ChannelFolderType.Playlist, playlist.IdentifierId.ToString()),
					Type = ChannelItemType.Folder,
					Name = playlist.Name,
				};

				channelItems.Add(item);
			}

			return new ChannelItemResult()
			{
				Items = channelItems.OrderBy(i => i.Name).ToList(),
				TotalRecordCount = channelItems.Count
			};
		}

		public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
		{
			var folder = ChannelFolder.Parse(query.FolderId);

			_logger.Debug("Render channel folder id: {0}", query.FolderId);

			switch (folder.Type)
			{
				case ChannelFolderType.Playlist:
				{
					var playlist = _libraryManager.GetUserRootFolder().RecursiveChildren.OfType<VodPlaylist>().FirstOrDefault(p => p.IdentifierId.ToString() == folder.Id);
					if (playlist != null)
					{
						return GetPlaylistItems(playlist, cancellationToken);
					}
					break;
				}
			}

			return GetChannelPlaylists(query);
		}

		public ChannelItemResult GetPlaylistItems(VodPlaylist playlist, CancellationToken cancellationToken)
		{
			var mediaItems = playlist.RecursiveChildren.OfType<VodMovie>().ToList();
		
			var items = (from media in mediaItems
				let image = media.GetImages(ImageType.Primary).FirstOrDefault()
				select new ChannelItemInfo()
				{
					Id = media.IdentifierId.ToString(), 
					Name = media.Name, 
					ImageUrl = image?.Path, 
					Type = ChannelItemType.Media, 
					MediaType = ChannelMediaType.Video, 
					ContentType = ChannelMediaContentType.Movie,
					MediaSources = media.ChannelMediaSources,
					Tags = media.Tags,
					CommunityRating = media.CommunityRating,
					Studios = media.Studios,
					DateCreated = media.DateCreated,
					ProductionYear = media.ProductionYear,
					ProviderIds = media.ProviderIds,
					Overview = media.Overview,
					Genres = media.Genres,
					HomePageUrl = media.HomePageUrl,
					OfficialRating = media.OfficialRating,
					PremiereDate = media.PremiereDate,
					RunTimeTicks = media.RunTimeTicks,
				}).ToList();

			return new ChannelItemResult()
			{
				Items = items.OrderBy(i =>i.Name).ToList()
			};

		}

		public async Task<ChannelItemResult> GetAllMedia(InternalAllChannelMediaQuery query, CancellationToken cancellationToken)
		{
			var playlists = _libraryManager.GetUserRootFolder().RecursiveChildren.OfType<VodPlaylist>().ToList();

			var items = new List<ChannelItemInfo>();

			foreach (var playlist in playlists)
			{
				var result = GetPlaylistItems(playlist, cancellationToken);
				items.AddRange(result.Items);
			}

			return new ChannelItemResult()
			{
				Items = items,
				TotalRecordCount = items.Count
			};
		}

		/*public Task LoadRegistrationInfoAsync()
		{
			Plugin.Instance.Registration = await PluginSecurityManager.GetRegistrationStatus(PluginConfiguration.Name).ConfigureAwait(false);
			Plugin.Logger.Debug("PodCasts Registration Status - Registered: {0} In trial: {2} Expiration Date: {1} Is Valid: {3}", Plugin.Instance.Registration.IsRegistered, Plugin.Instance.Registration.ExpirationDate, Plugin.Instance.Registration.TrialVersion, Plugin.Instance.Registration.IsValid);
		}*/
	}
}

