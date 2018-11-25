using System;
using System.Linq;
using System.Xml.Serialization;
using Pecee.Emby.Plugin.Vod.Configuration;
using Pecee.Emby.Plugin.Vod.Models;

namespace Pecee.Emby.Plugin.Vod.Entities
{
	public class PlaylistConfig
	{
		[XmlIgnore]
		private String _collectionType;

		public Boolean CreateLocalCollection { get; set; }
		public Boolean StrictSync { get; set; }

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
			CreateLocalCollection = true;
			StrictSync = false;
		}

		public VodPlaylist ToPlaylist()
		{
			return new VodPlaylist()
			{
				UserId = this.UserId,
				Name = this.Name,
				Config = this,
				PlaylistUrl = this.Url,
				//CollectionType = playlistConf.CollectionType,
				IdentifierId = this.IdentifierId,
				IsHidden = !CreateLocalCollection,
			};
		}
	}
}
