using System;
using MediaBrowser.Model.Serialization;

namespace Pecee.Emby.Plugin.Vod.Models
{
	public interface IVodMedia
	{
		Guid IdentifierId { get; set; }

		Guid ParentId { get; set; }

		[IgnoreDataMember]
		VodPlaylist Playlist { get; set; }
	}
}
