using System;

namespace Pecee.Emby.Plugin.Vod.Models
{
	public interface IVodMedia
	{
		Guid IdentifierId { get; set; }

		Guid ParentId { get; set; }
	}
}
