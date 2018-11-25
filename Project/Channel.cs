using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using Pecee.Emby.Plugin.Vod.Configuration;
using Pecee.Emby.Plugin.Vod.Folder;
using Pecee.Emby.Plugin.Vod.Models;
using ChannelFolderType = Pecee.Emby.Plugin.Vod.Folder.ChannelFolderType;

namespace Pecee.Emby.Plugin.Vod
{
	public class Channel : IChannel, ISearchableChannel
	{
		public string Name => PluginConfiguration.Name;

		public string Description => PluginConfiguration.Description;

		public string DataVersion => "10";

	    public string HomePageUrl => PluginConfiguration.HomepageUrl;

		public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

		private readonly ILibraryManager _libraryManager = Plugin.Instance.LibraryManager;
		private readonly ILogger _logger = Plugin.Instance.Logger;


		public InternalChannelFeatures GetChannelFeatures()
		{
			return new InternalChannelFeatures
			{
				ContentTypes = new List<ChannelMediaContentType>
				 {
					 ChannelMediaContentType.Movie,
					 ChannelMediaContentType.MovieExtra,
                     ChannelMediaContentType.Trailer,
                     ChannelMediaContentType.Clip,
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
			};
		}

		public bool IsEnabledFor(string userId)
		{
			return Plugin.Instance.Configuration.ChannelEnabled;
		}

		public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
		{
		    try
		    {
		        switch (type)
		        {
		            case ImageType.Primary:
		            case ImageType.Thumb:
		            case ImageType.Backdrop:
		                return new Task<DynamicImageResponse>(() => Plugin.GetImage(type.ToString().ToLower()),
		                    cancellationToken);
		        }

		        throw new ArgumentException("Unsupported image type: " + type);
		    }
		    catch (Exception e)
		    {
                Plugin.Instance.Logger.ErrorException("[VOD] Error: " + e.Message, e);
		    }

		    return null;
		}

		public IEnumerable<ImageType> GetSupportedChannelImages()
		{
			return new List<ImageType>
			{
				ImageType.Thumb,
				ImageType.Backdrop,
				ImageType.Primary,
                ImageType.Banner,
                ImageType.Art,
			};
		}

		public Task<ChannelItemResult> GetChannelPlaylists(CancellationToken cancellationToken)
		{
            Plugin.Instance.Logger.Debug("[VOD] Get channel playlists");
		    var items = Plugin.Instance.Configuration.Playlists.Select(p => new ChannelItemInfo()
		    {
		        Id = ChannelFolder.GetUrl(ChannelFolderType.Playlist, p.IdentifierId.ToString()),
                MediaType = ChannelMediaType.Video,
                ContentType = ChannelMediaContentType.Movie,
                FolderType = MediaBrowser.Model.Channels.ChannelFolderType.Container,
                Type = ChannelItemType.Media,
		        Name = p.Name,
            }).OrderBy(i => i.Name).ToList();

            _logger.Debug("[VOD] found {0} items", items.Count);

			return Task.FromResult(new ChannelItemResult()
			{
				Items = items,
				TotalRecordCount = items.Count(),
			});
		}

		public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
		{

		    Plugin.Instance.Logger.Debug("[VOD] Get channel items");

		    if (string.IsNullOrWhiteSpace(query.FolderId))
		    {
		        _logger.Debug("[VOD] Channel items default");
		        return await GetAllMedia(query, cancellationToken).ConfigureAwait(false);
		        //return await GetChannelPlaylists(cancellationToken).ConfigureAwait(false);
		    }


		    var folder = ChannelFolder.Parse(query.FolderId);

            Plugin.Instance.Logger.Debug("[VOD] Render channel folder id: {0}", query.FolderId);

            switch (folder.Type)
		    {
                default:
		        case ChannelFolderType.Playlist:
		        {
		            Plugin.Instance.Logger.Debug("[VOD] Get channel items playlist for id: {0} - {1}", folder.Id, folder.Type.ToString());
		            var playlist = _libraryManager.GetUserRootFolder().GetRecursiveChildren().OfType<VodPlaylist>()
		                .FirstOrDefault(p => p.IdentifierId.ToString() == folder.Id.ToString());

		            if (playlist != null)
		            {
		                Plugin.Instance.Logger.Debug("[VOD] Found channel playlist: {0}", playlist.Name);
                            return await new Task<ChannelItemResult>(() => GetPlaylistItems(playlist), cancellationToken).ConfigureAwait(false);
		            }

		            break;
		        }
		    }

		    return new ChannelItemResult()
		    {
		        Items = new List<ChannelItemInfo>()
		    };
           
		}

		public ChannelItemResult GetPlaylistItems(VodPlaylist playlist)
		{

		    Plugin.Instance.Logger.Debug("[VOD] Get playlist items");
            var mediaItems = playlist.GetRecursiveChildren().OfType<VodMovie>();

		    var items = (from media in mediaItems
		        let image = media.GetImages(ImageType.Primary).FirstOrDefault()
		        select new ChannelItemInfo()
		        {
		            Id = media.Id.ToString(),
		            Name = media.Name,
		            ImageUrl = image?.Path,
		            Type = ChannelItemType.Media,
		            MediaType = ChannelMediaType.Video,
		            ContentType = ChannelMediaContentType.Movie,
                    FolderType = MediaBrowser.Model.Channels.ChannelFolderType.Container,
		            MediaSources = media.GetMediaSources(false),
		            Tags = media.Tags.ToList(),
		            CommunityRating = media.CommunityRating,
		            Studios = media.Studios.ToList(),
		            DateCreated = media.DateCreated,
		            ProductionYear = media.ProductionYear,
		            ProviderIds = media.ProviderIds,
		            Overview = media.Overview,
		            Genres = media.Genres.ToList(),
		            HomePageUrl = media.GetRelatedUrls().Select(u => u.Url).FirstOrDefault(),
		            OfficialRating = media.OfficialRating,
		            PremiereDate = media.PremiereDate,
		            RunTimeTicks = media.RunTimeTicks,
		        }).ToList();

		    return new ChannelItemResult()
		    {
		        Items = items.OrderBy(i => i.Name).ToList(),
                TotalRecordCount = items.Count
		    };

        }

	    public Task<ChannelItemResult> GetAllMedia(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var playlists = _libraryManager.GetUserRootFolder().GetRecursiveChildren().OfType<VodPlaylist>().ToList();

            var items = new List<ChannelItemInfo>();

            foreach (var playlist in playlists)
            {
                items.AddRange(GetPlaylistItems(playlist).Items);
            }

            var result = new ChannelItemResult()
            {
                Items = items,
                TotalRecordCount = items.Count
            };

            return Task.FromResult(result);
        }

        public Task<IEnumerable<ChannelItemInfo>> Search(ChannelSearchInfo searchInfo, CancellationToken cancellationToken)
	    {

	        Plugin.Instance.Logger.Debug("[VOD] Search");

            return new Task<IEnumerable<ChannelItemInfo>>(delegate 
            {
                var playlists = _libraryManager.GetUserRootFolder().GetRecursiveChildren().OfType<VodPlaylist>().Where(i => i.Name.Contains(searchInfo.SearchTerm) || i.Overview.Contains(searchInfo.SearchTerm)).ToList();

                var mediaItems = playlists.SelectMany(i => i.RecursiveChildren.OfType<VodMovie>());

                return (from media in mediaItems
                    select new ChannelItemInfo()
                    {
                        Id = ChannelFolder.GetUrl(ChannelFolderType.Playlist, media.IdentifierId.ToString()),
                        Name = media.Name,
                        ImageUrl = media.PrimaryImagePath,
                        Type = ChannelItemType.Media,
                        MediaType = ChannelMediaType.Video,
                        ContentType = ChannelMediaContentType.Movie,
                        MediaSources = media.GetMediaSources(false),
                        Tags = media.Tags.ToList(),
                        CommunityRating = media.CommunityRating,
                        Studios = media.Studios.ToList(),
                        DateCreated = media.DateCreated,
                        ProductionYear = media.ProductionYear,
                        ProviderIds = media.ProviderIds,
                        Overview = media.Overview,
                        Genres = media.Genres.ToList(),
                        HomePageUrl = media.GetRelatedUrls().Select(u => u.Url).FirstOrDefault(),
                        OfficialRating = media.OfficialRating,
                        PremiereDate = media.PremiereDate,
                        RunTimeTicks = media.RunTimeTicks,
                    }).ToList();
                
            }, cancellationToken);
           

	    }
	}
}

