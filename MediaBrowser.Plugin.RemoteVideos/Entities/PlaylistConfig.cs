using System;
using System.Linq;
using System.Xml.Serialization;
using Pecee.Emby.Plugin.Vod.Configuration;

namespace Pecee.Emby.Plugin.Vod.Entities
{
	public class PlaylistConfig
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

		public Guid IdentifierId { get; set; }

		public PlaylistConfig()
		{
			IdentifierId = Guid.NewGuid();
		}
	}
}
