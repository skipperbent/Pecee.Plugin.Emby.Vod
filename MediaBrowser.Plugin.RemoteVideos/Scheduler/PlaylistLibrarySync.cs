using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Pecee.Emby.Plugin.Vod.Configuration;
using Pecee.Emby.Plugin.Vod.Models;
using Pecee.Emby.Plugin.Vod.Parser;

namespace Pecee.Emby.Plugin.Vod.Scheduler
{
	public class PlaylistLibrarySync : IScheduledTask
	{
		private readonly ILogger _logger;
		private readonly ILibraryManager _libraryManager;
		private readonly IProviderManager _providerManager;
		private readonly M3UParser _m3UParser;

		public string Name => "Video on Demand Playlist";

		public string Key => "VodPlaylistLibrarySync";

		public string Description => "Sync all your remote video-on-demand with your local library.";

		public string Category => "Library";

		public PlaylistLibrarySync(
			ILogManager logger,
			ILibraryManager libraryManager,
			IProviderManager providerManager,
			M3UParser m3UParser
		)
		{
			_libraryManager = libraryManager;
			_logger = logger.GetLogger(PluginConfiguration.Name);
			_providerManager = providerManager;
			_m3UParser = m3UParser;
		}

		public async Task Cleanup(CancellationToken cancellationToken)
		{
			_logger.Debug("Cleaning up old and removed items");

			var playlists = Plugin.Instance.GetPlaylists();

			var deleteItems = _libraryManager.GetUserRootFolder()
				.RecursiveChildren.OfType<VodPlaylist>()
				.Where(p => p.Id == Guid.Empty || p.IdentifierId == Guid.Empty || 
				playlists.FirstOrDefault(p1 => p1.IdentifierId == p.IdentifierId) == null)
				.Cast<BaseItem>().ToList();

			_logger.Info("Found {0} playlists to remove", deleteItems.Count);

			var mediaItems = _libraryManager.GetUserRootFolder()
				.RecursiveChildren.OfType<VodMovie>()
				.Where(p1 => p1.Id == Guid.Empty || p1.IdentifierId == Guid.Empty || playlists.FirstOrDefault(p2 => p2.IdentifierId == p1.ParentId) != null).Cast<BaseItem>().ToList();

			deleteItems.AddRange(mediaItems);

			_logger.Info("Found {0} mediaItems to remove", mediaItems.Count);

			foreach (BaseItem item in deleteItems)
			{
				_logger.Debug("Removing dead item: {0}, id:  {1}, parentId: {2}", item.Name, item.Id, item.ParentId);
				await item.Delete(new DeleteOptions()
				{
					DeleteFileLocation = false
				});
			}
		}

		public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
		{
			await Cleanup(cancellationToken);

			var playlists = Plugin.Instance.GetPlaylists();

			// No point going further if we don't have users.
			if (playlists.Count == 0)
			{
				_logger.Info("No internal playlists added");
				return;
			}

			_logger.Info("Library sync started");

			var existingPlaylists =
				_libraryManager.GetUserRootFolder()
					.RecursiveChildren.OfType<VodPlaylist>()
					.Where(p1 => playlists.FirstOrDefault(p2 => p2.IdentifierId == p1.IdentifierId) != null)
					.ToList();

			var i = 1;

			foreach (VodPlaylist playlist in playlists)
			{
				progress.Report((i / playlists.Count) * 100);
				var existingPlaylist = existingPlaylists.FirstOrDefault(p1 => p1.IdentifierId == playlist.IdentifierId);
				if (existingPlaylist != null)
				{
					// UPDATE
					_logger.Debug("{0}: playlist with id {1} and identifier {2} found, updating...", existingPlaylist.Name,
						existingPlaylist.Id, existingPlaylist.IdentifierId);
					await existingPlaylist.Merge(existingPlaylist);
				}
				else
				{
					// CREATE
					_logger.Debug("{0}: playlist with identifier {1} not found, creating...", playlist.Name, playlist.IdentifierId);
					playlist.Id = _libraryManager.GetNewItemId(playlist.IdentifierId.ToString(), typeof(VodPlaylist));
					await _libraryManager.GetUserRootFolder().AddChild(playlist, cancellationToken).ConfigureAwait(false);
				}

				await SyncMedia(playlist, cancellationToken).ConfigureAwait(false);

				i++;
			}

			progress.Report(100);
			_libraryManager.QueueLibraryScan();
		}

		private async Task SyncMedia(VodPlaylist playlist, CancellationToken cancellationToken)
		{
			_logger.Info("{0}: parsing remote playlist: {1}", playlist.Name, playlist.PlaylistUrl);
			
			List<VodMovie> mediaItems = await _m3UParser.GetMediaItems(playlist, cancellationToken);

			var existingMediaItems =
				_libraryManager.GetUserRootFolder()
					.RecursiveChildren.OfType<VodMovie>()
					.Where(p1 => mediaItems.FirstOrDefault(p2 => p2.IdentifierId == p1.IdentifierId) != null)
					.ToList();

			foreach (var media in mediaItems)
			{
				try
				{
					var existingMediaItem = existingMediaItems.FirstOrDefault(m => m.IdentifierId == media.IdentifierId);
					if (existingMediaItem != null)
					{
						// UPDATE
						_logger.Debug("{0}: found item with id {1} and identifier {2}, updating...", playlist.Name, existingMediaItem.Id,
							existingMediaItem.IdentifierId);
						await existingMediaItem.Merge(media);
					}
					else
					{
						// CREATE
						_logger.Debug("{0}: media {1} with  identifier {2} not found, adding...", playlist.Name, media.Name, media.IdentifierId);
						media.Id = _libraryManager.GetNewItemId(media.IdentifierId.ToString(), typeof(VodMovie));
						await playlist.AddChild(media, cancellationToken).ConfigureAwait(false);
					}

					await RefreshMetaData(media, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					_logger.ErrorException(e.Message, e);
				}
			}
		}

		private async Task<BaseItem> RefreshMetaData(BaseItem item, CancellationToken cancellationToken)
		{
			_logger.Info("{0}: Refreshing meta-data", item.Name);

			// TODO: refresh meta-data for multiple content-types

			var meta = await _providerManager.GetRemoteSearchResults<Movie, MovieInfo>(new RemoteSearchQuery<MovieInfo>
			{
				IncludeDisabledProviders = true,
				SearchInfo = new MovieInfo
				{
					Name = item.Name,
				}

			}, cancellationToken);

			var metaResult = meta.FirstOrDefault();

			if (metaResult == null)
			{
				_logger.Debug("{0}: Meta-data not found", item.Name);
				return item;
			}

			_logger.Debug("{0}: Found meta-data", item.Name);

			item.Name = metaResult.Name;
			item.Overview = metaResult.Overview;
			item.PremiereDate = metaResult.PremiereDate;
			item.ProductionYear = metaResult.ProductionYear;
			item.ProviderIds = metaResult.ProviderIds;

			if (metaResult.ImageUrl != null)
			{
				if (item.ImageInfos == null)
				{
					item.ImageInfos = new List<ItemImageInfo>();
				}

				item.ImageInfos.Add(new ItemImageInfo()
				{
					Path = metaResult.ImageUrl,
					Type = ImageType.Primary,
				});
			}

			return item;
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