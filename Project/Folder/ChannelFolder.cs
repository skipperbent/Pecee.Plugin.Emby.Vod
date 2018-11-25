using System;
using System.Text.RegularExpressions;

namespace Pecee.Emby.Plugin.Vod.Folder
{
	class ChannelFolder
	{
		public ChannelFolderType Type { get; private set; }
		public string Id { get; private set; }

		private ChannelFolder(ChannelFolderType type, string id = null)
		{
			Type = type;
			if (!string.IsNullOrEmpty(id))
			{
				Id = id;
			}
		}

		public static string GetUrl(ChannelFolderType type, string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return type + "_";
			}

			return string.Format("{0}_{1}", type.ToString().ToLower(), id.ToString());
		}

		public override string ToString()
		{
			return GetUrl(Type, Id);
		}

		public static ChannelFolder Parse(string folderId)
		{
			if (string.IsNullOrEmpty(folderId))
			{
				return new ChannelFolder(ChannelFolderType.Home);
			}

		    var match = folderId.Split('_');
           
			if (match.Length == 0)
			{
				throw new ArgumentException("Failed to parse folderId", "folderId");
			}

			ChannelFolderType type;

			if (!Enum.TryParse(match[0], out type))
			{
				throw new ArgumentException("Invalid or unknown folder-type: " + match[0]);
			}

		    var result = new ChannelFolder(type)
		    {
		        Id = String.IsNullOrEmpty(match[1]) ? null : match[1],
                Type = type,
		    };

			return result;
		}
	}
}
