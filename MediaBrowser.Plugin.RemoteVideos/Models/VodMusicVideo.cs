using System;
using System.Runtime.Serialization;
using MediaBrowser.Controller.Entities;

namespace Pecee.Emby.Plugin.Vod.Models
{
	public class VodMusicVideo : MusicVideo, IVodMedia
	{
		public Guid IdentifierId { get; set; }

		[IgnoreDataMember]
		public VodPlaylist Playlist { get; set; }

	}
}
