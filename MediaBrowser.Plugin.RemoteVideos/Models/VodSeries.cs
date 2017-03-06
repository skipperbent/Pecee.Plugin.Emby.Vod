using System;
using MediaBrowser.Controller.Entities.TV;

namespace Pecee.Emby.Plugin.Vod.Models
{
	public class VodSeries : Series, IVodMedia
	{
		public Guid IdentifierId { get; set; }

	}
}
