using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System.Threading.Tasks;
using MediaBrowser.Common.Progress;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Pecee.Emby.Plugin.Vod.Configuration;
using Pecee.Emby.Plugin.Vod.Entities;
using Pecee.Emby.Plugin.Vod.Models;
using Pecee.Emby.Plugin.Vod.Parser;

namespace Pecee.Emby.Plugin.Vod.Scheduler
{
	public class PlaylistLibrarySync : IScheduledTask
	{
		private readonly IProviderManager _providerManager;
		private readonly M3UParser _m3UParser;

		public string Name => "Video on Demand Playlist Sync";

		public string Key => "VodPlaylistLibrarySync";

		public string Description => "Sync all your remote video-on-demand with your local library.";

		public string Category => "Library";

	    private readonly ILogger _logger = Plugin.Instance.Logger;
	    private readonly ILibraryManager _libraryManager = Plugin.Instance.LibraryManager;

		public PlaylistLibrarySync(
			IProviderManager providerManager,
			M3UParser m3UParser
		)
		{
			
			_providerManager = providerManager;
			_m3UParser = m3UParser;
		}

		public async Task Cleanup(PlaylistConfig[] playlists, CancellationToken cancellationToken)
		{
		    _logger.Info("[VOD] Cleaning up old and removed items");

			var deleteItems = _libraryManager.GetUserRootFolder()
				.RecursiveChildren.OfType<VodPlaylist>()
				.Where(p => p.Id == Guid.Empty || p.IdentifierId == Guid.Empty || 
				(playlists.Length > 0 && playlists.FirstOrDefault(p1 => p1.IdentifierId == p.IdentifierId) == null) || 
				 playlists.Length == 0)
				.Cast<BaseItem>().ToList();

		    _logger.Info("[VOD] Found {0} playlists to remove", deleteItems.Count);

			var mediaItems = _libraryManager.GetUserRootFolder()
				.RecursiveChildren.OfType<VodMovie>()
				.Where(p1 => p1.Id == Guid.Empty || p1.IdentifierId == Guid.Empty || playlists.FirstOrDefault(p2 => p2.IdentifierId == p1.ParentId) != null || playlists.Length == 0).Cast<BaseItem>().ToList();

			deleteItems.AddRange(mediaItems);

		    _logger.Info("[VOD] Found {0} items to remove", mediaItems.Count);

			foreach (BaseItem item in deleteItems)
			{
			    _logger.Debug("[VOD] Removing item: {0}, id:  {1}, parentId: {2}", item.Name, item.Id, item.ParentId);
			    _libraryManager.DeleteItem(item, new DeleteOptions()
				{
                    DeleteFileLocation = true
				}, true);
			}
		}

		public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
		{
			var playlists = Plugin.Instance.Configuration.Playlists;

			await Cleanup(playlists, cancellationToken);

			// No point going further if we don't have users.
			if (playlists.Length == 0)
			{
			    _logger.Info("[VOD] No internal playlists added");
				return;
			}

		    _logger.Info("[VOD] Library sync starting");

			var existingPlaylists =
			    _libraryManager.GetUserRootFolder()
					.RecursiveChildren.OfType<VodPlaylist>()
					.Where(p1 => playlists.FirstOrDefault(p2 => p2.IdentifierId == p1.IdentifierId) != null)
					.ToList();

			var i = 1;

			foreach (PlaylistConfig playlistConfig in playlists)
			{
				var playlist = playlistConfig.ToPlaylist();
				progress.Report((i / playlists.Length) * 100);
				var existingPlaylist = existingPlaylists.FirstOrDefault(p1 => p1.IdentifierId == playlist.IdentifierId);

				if (existingPlaylist != null)
				{
                    // UPDATE
				    _logger.Debug("[VOD] {0}: playlist with id {1} and identifier {2} found, updating...", existingPlaylist.Name, existingPlaylist.Id, existingPlaylist.IdentifierId);
					existingPlaylist.Merge(existingPlaylist);
				}
				else
				{
                    // CREATE
				    _logger.Debug("[VOD] {0}: playlist with identifier {1} not found, creating...", playlist.Name, playlist.IdentifierId);
					playlist.Id = _libraryManager.GetNewItemId(playlist.IdentifierId.ToString(), typeof(VodPlaylist));
				    _libraryManager.GetUserRootFolder().AddChild(playlist, cancellationToken);
				}

				var innerProgress = new ActionableProgress<double>();

				// Avoid implicitly captured closure
				innerProgress.RegisterAction(progress.Report);

				await SyncMedia(playlist, innerProgress, cancellationToken).ConfigureAwait(false);

				i++;
			}

			progress.Report(100);
		    _libraryManager.QueueLibraryScan();
		}

		private async Task SyncMedia(VodPlaylist playlist, IProgress<double> progress, CancellationToken cancellationToken)
		{
		    _logger.Info("[VOD] {0}: parsing remote playlist: {1}", playlist.Name, playlist.PlaylistUrl);
			
			List<Media> mediaItems = await _m3UParser.GetMediaItems(playlist, cancellationToken);

			var existingMediaItems =
			    _libraryManager.GetUserRootFolder()
					.RecursiveChildren.OfType<VodMovie>()
					.Where(p1 => mediaItems.FirstOrDefault(p2 => p2.IdentifierId == p1.IdentifierId) != null)
					.ToList();

			if (playlist.Config.StrictSync)
			{
				// Remove local items that no longer exist on remote playlist
				var deleteLocally =
					playlist.RecursiveChildren.OfType<VodMovie>()
						.Where(m => mediaItems.Select(m1 => m1.IdentifierId).Contains(m.IdentifierId) == false).ToList();

				foreach (var item in deleteLocally)
				{
				    _libraryManager.DeleteItem(item, new DeleteOptions()
                    {
                        DeleteFileLocation = true
                    }, true);
				}

			}

			var i = 1;

			foreach (var media in mediaItems)
			{
				progress.Report((i / mediaItems.Count) * 100);
				var vodItem = (VodMovie)media.ToVodItem();
				try
				{
					var existingMediaItem = existingMediaItems.FirstOrDefault(m => m.IdentifierId == vodItem.IdentifierId);

					if (existingMediaItem != null)
					{
                        // UPDATE
					    _logger.Debug("[VOD] {0}: found item with id {1} and identifier {2}, updating...", playlist.Name, existingMediaItem.Id, existingMediaItem.IdentifierId);
						await existingMediaItem.Merge(vodItem);
						await RefreshMetaData(vodItem, cancellationToken).ConfigureAwait(false);
					}
					else
					{
                        // CREATE
					    _logger.Debug("[VOD] {0}: media {1} with  identifier {2} not found, adding...", playlist.Name, vodItem.Name, vodItem.IdentifierId);
						vodItem.Id = _libraryManager.GetNewItemId(vodItem.IdentifierId.ToString(), typeof(VodMovie));
						playlist.AddChild(vodItem, cancellationToken);

						await RefreshMetaData(vodItem, cancellationToken).ConfigureAwait(false);
						await vodItem.RefreshMetadata(cancellationToken).ConfigureAwait(false);
					}
				}
				catch (Exception e)
				{
				    _logger.ErrorException("[VOD] Error: " + e.Message, e);

                }

				i++;
			}

			progress.Report(100);
		}

		private async Task<BaseItem> RefreshMetaData(BaseItem item, CancellationToken cancellationToken)
		{
		    _logger.Info("[VOD] {0}: Refreshing meta-data", item.Name);

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
			    _logger.Debug("[VOD] {0}: Meta-data not found", item.Name);
				return item;
			}

		    _logger.Debug("[VOD] {0}: Found meta-data", item.Name);

			item.Name = metaResult.Name;
			item.Overview = metaResult.Overview;
			item.PremiereDate = metaResult.PremiereDate;
			item.ProductionYear = metaResult.ProductionYear;
			item.ProviderIds = metaResult.ProviderIds;

			if (metaResult.ImageUrl != null)
			{
				var newImages = new List<ItemImageInfo>();

				newImages.Add(new ItemImageInfo()
				{
					Path = metaResult.ImageUrl,
					Type = ImageType.Primary,
				});

				if (item.ImageInfos!= null)
				{
					newImages.AddRange(item.ImageInfos);
				}

				item.ImageInfos = newImages.ToArray();
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