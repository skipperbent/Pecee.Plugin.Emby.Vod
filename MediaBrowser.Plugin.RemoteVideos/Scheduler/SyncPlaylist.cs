using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Progress;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Plugin.VOD.Configuration;
using MediaBrowser.Plugin.VOD.Entities;

namespace MediaBrowser.Plugin.VOD.Scheduler
{
	public class SyncPlaylist : IScheduledTask
	{
		private readonly ILogManager _logManager;
		private readonly IHttpClient _httpClient;
		private readonly ILogger _logger;
		private readonly ILibraryManager _libraryManager;
		private readonly IProviderManager _providerManager;

		public string Name => "VOD Playlist Sync";

		public string Key => "VodPlaylistSync";

		public string Description => "Sync your remote VOD playlists";

		public string Category => "Library";

		public SyncPlaylist(
			ILogManager logger,
			ILibraryManager libraryManager,
			IProviderManager providerManager,
			IHttpClient httpClient
		)
		{
			_logManager = logger;
			_libraryManager = libraryManager;
			_logger = logger.GetLogger(PluginConfiguration.Name);
			_providerManager = providerManager;
			_httpClient = httpClient;
		}

		public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
		{
			Playlist[] playlists = Plugin.Instance.Configuration.Playlists;

			// No point going further if we don't have users.
			if (playlists.Length == 0)
			{
				_logger.Info("No internal playlists added");
				progress.Report(100);
				return;
			}

			_logger.Info("Starting VOD sync");

			int i = 1;
			foreach (Playlist playlist in playlists)
			{
				_logger.Info("{0}: Starting sync", playlist.Name);
				await SyncMedia(playlist, progress, cancellationToken).ConfigureAwait(false);
				return;

				var percentage = Math.Ceiling((double) ((i / playlist.Media.Count) * 100));
				progress.Report(percentage);

				i++;
			}

			progress.Report(100);
			_libraryManager.QueueLibraryScan();
		}

		private async Task<BaseItem> RefreshMetaData(Playlist playlist, BaseItem item, CancellationToken cancellationToken)
		{
			_logger.Info("{0}: Refreshing meta-data", item.Name);
			// TODO: refresh meta-data for multiple types

			var meta = await _providerManager.GetRemoteSearchResults<Movie, MovieInfo>(new RemoteSearchQuery<MovieInfo>
			{
				IncludeDisabledProviders = true,
				SearchInfo = new MovieInfo
				{
					Name = item.Name,
				}

			}, cancellationToken).ConfigureAwait(false);

			var metaResult = meta.FirstOrDefault();

			if (meta == null)
			{
				_logger.Debug("{0}: Meta-data not found", metaResult.Name);
				return item;
			}

			_logger.Debug("{0}: Found meta-data", metaResult.Name);
			item.OriginalTitle = item.Name;
			item.Name = metaResult.Name;
			item.Overview = metaResult.Overview;
			item.PremiereDate = metaResult.PremiereDate;
			item.ProductionYear = metaResult.ProductionYear;
			item.ProviderIds = metaResult.ProviderIds;

			return item;
		}

		private async Task SyncMedia(Playlist playlist, IProgress<double> progress, CancellationToken cancellationToken)
		{
			var innerProgress = new ActionableProgress<double>();
			innerProgress.RegisterAction(p => progress.Report(0));

			_logger.Info("Parsing items from: {0}", playlist.Url);

			var folder = new Controller.Entities.Folder()
			{
				Id = _libraryManager.GetNewItemId("IMDB List 4", typeof(Controller.Entities.Folder)),
				Name = "IMDB List 4",
				SourceType = SourceType.Library,
				DisplayMediaType = Name
			};

			_logger.Debug("Image path: " + Plugin.GetImage("movies").Path);

			await _libraryManager.GetUserRootFolder().AddChild(folder, cancellationToken).ConfigureAwait(false);

			//folder.SetImagePath(ImageType.Primary, Plugin.GetImage("movies").Path);
			//folder.SetImagePath(ImageType.Backdrop, Plugin.GetImage("backdrop").Path);

			/*var folder = _channelManager.GetChannel(PluginConfiguration.PluginId);

			if (folder == null)
			{
				_logger.Error("Failed to find channel folder, aborting...");
				return;
			}*/

			_logger.Debug("Using root-folder: {0}", folder.Id);

			await playlist.RefreshMedia(_logManager, _httpClient, cancellationToken).ConfigureAwait(false);

			var i = 1;

			foreach (var media in playlist.Media)
			{
				_logger.Debug("{0}: Starting import", media.Name);

				var percentage = Math.Ceiling((double)((i / playlist.Media.Count) * 100));
				innerProgress.RegisterAction(p => progress.Report(percentage));

				var item = new Movie
				{
					Id = _libraryManager.GetNewItemId("yeahdudZe" + media.Url, typeof(Movie)),
					Name = media.Name,
					OriginalTitle = media.Name,
					VideoType = VideoType.VideoFile,
					Path = media.Url,
					DefaultVideoStreamIndex = -1,
					ParentId = folder.Id,
				};

				//item.SetParent(folder);

				if (media.Image != null)
				{
					_logger.Debug("Adding image: {0}", media.Image);
					item.ImageInfos = new List<ItemImageInfo>()
					{
						new ItemImageInfo()
						{
							Path = media.Image,
							Type = ImageType.Primary,
						}
					};
				}

				_logger.Debug("Adding stream: {0}", media.Url);

				item.ChannelMediaSources = new List<ChannelMediaInfo>
				{
					new ChannelMediaInfo
					{
						Path = media.Url,
						Protocol = MediaProtocol.Http
					}
				};

				item = await RefreshMetaData(playlist, item, cancellationToken).ConfigureAwait(false) as Movie;

				await folder.AddChild(item, cancellationToken).ConfigureAwait(false);
				await item.RefreshMetadata(cancellationToken).ConfigureAwait(false);

				return;

				i++;
			}

			innerProgress.RegisterAction(p => progress.Report(100));
		}

		public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
		{
			return new[]
			{
				// Every so often
				new TaskTriggerInfo {Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(12).Ticks}
			};
		}

	}
}