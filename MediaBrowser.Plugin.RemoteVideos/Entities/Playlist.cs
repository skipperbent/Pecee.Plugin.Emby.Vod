using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugin.VOD.Configuration;
using MediaBrowser.Plugin.VOD.Parser;

namespace MediaBrowser.Plugin.VOD.Entities
{
	public class Playlist
	{
		[XmlIgnore]
		private String _collectionType;

		public String Name { get; set; }

		public String Url { get; set; }

		public String UserId { get; set; }

		public String CollectionType
		{
			get { return _collectionType; }
			set
			{
				if (!PluginConfiguration.AllowedCollectionTypes.Contains(value))
				{
					throw new ArgumentException("Invalid collection-type");
				}

				_collectionType = value;
			}
		}

		public String Identifier => Url.GetMD5().ToString("N");

		[XmlIgnore]
		public List<Media> Media = new List<Media>();

		public async Task RefreshMedia(ILogManager logManager, IHttpClient httpClient, CancellationToken cancellationToken)
		{
			var m3UParser = new M3UParser(logManager, httpClient);
			Media = await m3UParser.GetMediaItems(Url, cancellationToken);
		}
	}
}
