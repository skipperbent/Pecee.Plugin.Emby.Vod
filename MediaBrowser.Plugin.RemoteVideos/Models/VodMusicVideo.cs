using System;
using MediaBrowser.Controller.Entities;

namespace Pecee.Emby.Plugin.Vod.Models
{
	public class VodMusicVideo : MusicVideo, IVodMedia
	{
		public Guid IdentifierId { get; set; }

	}
}
