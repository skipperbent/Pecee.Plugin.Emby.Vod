using System;
using System.Collections.Generic;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Pecee.Emby.Plugin.Vod.Models;

namespace Pecee.Emby.Plugin.Vod.Entities
{
	public class Media
	{
		public String Name { get; set; }

		public String Image { get; set; }

		public String Url { get; set; }

		public Guid IdentifierId => Url.GetMD5();

		public Guid PlaylistId { get; set; }

		public IVodMedia ToVodItem ()
		{
			var media = new VodMovie()
			{
				Name = this.Name,
				OriginalTitle = this.Name,
				Path = this.Url,
				DefaultVideoStreamIndex = -1,
				ParentId = PlaylistId,
				IdentifierId = this.Url.ToString().GetMD5(),
			};

			media.ChannelMediaSources = new List<ChannelMediaInfo>
			{
				new ChannelMediaInfo
				{
					Path = Url,
					Protocol = MediaProtocol.Http,
				}
			};

			if (Image != null)
			{
				media.ImageInfos = new List<ItemImageInfo>()
				{
					new ItemImageInfo()
					{
						Path = Image,
						Type = ImageType.Primary,
						DateModified = DateTime.Now
					}
				};
			}

			return media;
		}
	}
}
