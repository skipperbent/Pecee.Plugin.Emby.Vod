using System;
using MediaBrowser.Common.Extensions;

namespace Pecee.Emby.Plugin.Vod.Entities
{
	public class Media
	{
		public String Name { get; set; }

		public String Image { get; set; }

		public String Url { get; set; }

		public String Identifier => Url.GetMD5().ToString("N");
	}
}
