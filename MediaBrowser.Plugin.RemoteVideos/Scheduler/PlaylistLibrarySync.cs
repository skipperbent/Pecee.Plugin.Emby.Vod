﻿using System;
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

		public async Task Cleanup(PlaylistConfig[] playlists, CancellationToken cancellationToken)
		{
			_logger.Debug("Cleaning up old and removed items");

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
					DeleteFileLocation = true
				});
			}
		}

		public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
		{
			var playlists = Plugin.Instance.Configuration.Playlists;

			await Cleanup(playlists, cancellationToken);

			// No point going further if we don't have users.
			if (playlists.Length == 0)
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

			foreach (PlaylistConfig playlistConfig in playlists)
			{
				var playlist = playlistConfig.ToPlaylist();
				progress.Report((i / playlists.Length) * 100);
				var existingPlaylist = existingPlaylists.FirstOrDefault(p1 => p1.IdentifierId == playlist.IdentifierId);

				if (existingPlaylist != null)
				{
					// UPDATE
					_logger.Debug("{0}: playlist with id {1} and identifier {2} found, updating...", existingPlaylist.Name, existingPlaylist.Id, existingPlaylist.IdentifierId);
					await existingPlaylist.Merge(existingPlaylist);
				}
				else
				{
					// CREATE
					_logger.Debug("{0}: playlist with identifier {1} not found, creating...", playlist.Name, playlist.IdentifierId);
					playlist.Id = _libraryManager.GetNewItemId(playlist.IdentifierId.ToString(), typeof(VodPlaylist));
					await _libraryManager.GetUserRootFolder().AddChild(playlist, cancellationToken).ConfigureAwait(false);
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
			_logger.Info("{0}: parsing remote playlist: {1}", playlist.Name, playlist.PlaylistUrl);
			
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
					await item.Delete(new DeleteOptions()
					{
						DeleteFileLocation = true
					});
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
						_logger.Debug("{0}: found item with id {1} and identifier {2}, updating...", playlist.Name, existingMediaItem.Id, existingMediaItem.IdentifierId);
						await existingMediaItem.Merge(vodItem);
						await RefreshMetaData(vodItem, cancellationToken).ConfigureAwait(false);
					}
					else
					{
						// CREATE
						_logger.Debug("{0}: media {1} with  identifier {2} not found, adding...", playlist.Name, vodItem.Name, vodItem.IdentifierId);
						vodItem.Id = _libraryManager.GetNewItemId(vodItem.IdentifierId.ToString(), typeof(VodMovie));
						await playlist.AddChild(vodItem, cancellationToken).ConfigureAwait(false);

						await RefreshMetaData(vodItem, cancellationToken).ConfigureAwait(false);
						await vodItem.RefreshMetadata(cancellationToken).ConfigureAwait(false);
					}
				}
				catch (Exception e)
				{
					_logger.ErrorException(e.Message, e);
				}

				i++;
			}

			progress.Report(100);
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

				item.ImageInfos = newImages;
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