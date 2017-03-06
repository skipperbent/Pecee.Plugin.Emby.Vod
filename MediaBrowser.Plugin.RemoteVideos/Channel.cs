using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugin.VOD.Configuration;
using MediaBrowser.Plugin.VOD.Entities;
using MediaBrowser.Plugin.VOD.Folder;
using ChannelFolderType = MediaBrowser.Plugin.VOD.Folder.ChannelFolderType;

namespace MediaBrowser.Plugin.VOD
{
	public class Channel : IChannel, IIndexableChannel
	{
		public string Name => PluginConfiguration.Name;

		public string Description => PluginConfiguration.Description;

		public string DataVersion
		{
			get { return "102"; }
		}

		public string HomePageUrl => PluginConfiguration.HomepageUrl;

		public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

		private readonly ILogManager _logManager;
		private readonly ILogger _logger;
		private readonly IProviderManager _providerManager;
		private readonly IHttpClient _httpClient;

		public Channel(ILogManager logManager, IProviderManager providerManager, IHttpClient httpClient)
		{
			_logManager = logManager;
			_logger = logManager.GetLogger(PluginConfiguration.Name);
			_providerManager = providerManager;
			_httpClient = httpClient;
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
			return true;
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
					Id = ChannelFolder.GetUrl(ChannelFolderType.Playlist, playlist.Identifier),
					Type = ChannelItemType.Folder,
					Name = playlist.Name,
					ImageUrl = Plugin.GetImage("movies").Path,
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
					var playlist = Plugin.Instance.Configuration.Playlists.FirstOrDefault(p => p.Identifier == folder.Id);
					if (playlist != null)
					{
						return await GetPlaylistItems(playlist, cancellationToken).ConfigureAwait(false);
					}
					break;
				}
			}

			return GetChannelPlaylists(query);
		}

		public async Task<ChannelItemResult> GetPlaylistItems(Playlist playlist, CancellationToken cancellationToken)
		{
			await playlist.RefreshMedia(_logManager, _httpClient, cancellationToken).ConfigureAwait(false);

			var items = new List<ChannelItemInfo>();

			foreach (var media in playlist.Media)
			{
				var info = new ChannelItemInfo()
				{
					Id = media.Identifier,
					Name = media.Name,
					ImageUrl = media.Image,
					Type = ChannelItemType.Media,
					MediaType = ChannelMediaType.Video,
					ContentType = ChannelMediaContentType.Movie,
					MediaSources = new List<ChannelMediaInfo>
					{
						new ChannelMediaInfo
						{
							Path = media.Url
						}
					}
				};

				var meta = await _providerManager.GetRemoteSearchResults<Movie, MovieInfo>(new RemoteSearchQuery<MovieInfo>
				{
					IncludeDisabledProviders = true,
					SearchInfo = new MovieInfo
					{
						Name = media.Name,
					}

				}, cancellationToken).ConfigureAwait(false);

				var metaResult = meta.FirstOrDefault();

				if (meta != null)
				{
					_logger.Debug("Found meta-data for {0}", metaResult.Name);
					info.Name = metaResult.Name;
					info.Overview = metaResult.Overview;
					info.ImageUrl = metaResult.ImageUrl;
					info.PremiereDate = metaResult.PremiereDate;
					info.ProductionYear = metaResult.ProductionYear;
					info.ProviderIds = metaResult.ProviderIds;
				}

				items.Add(info);
			}

			return new ChannelItemResult()
			{
				Items = items.OrderBy(i =>i.Name).ToList()
			};

		}

		public async Task<ChannelItemResult> GetAllMedia(InternalAllChannelMediaQuery query, CancellationToken cancellationToken)
		{
			var playlists = Plugin.Instance.Configuration.Playlists;

			var items = new List<ChannelItemInfo>();

			foreach (var playlist in playlists)
			{
				var result = await GetPlaylistItems(playlist, cancellationToken).ConfigureAwait(false);
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
