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
				return type + "-";
			}
			return string.Format("{0}-{1}", type, id);
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

			var match = Regex.Match(folderId, "(?<type>.*?)-(?<id>.*)?");
			if (!match.Success)
			{
				throw new ArgumentException("Failed to parse folderId", "folderId");
			}

			ChannelFolderType type;

			if (!Enum.TryParse(match.Groups["type"].Value, out type))
			{
				throw new ArgumentException("Invalid or unknown folder-type: " + match.Groups["type"].Value);
			}
		
			var result = new ChannelFolder(type);

			var id = match.Groups["id"];
			if (id != null)
			{
				result.Id = id.Value;
			}
			return result;
		}
	}
}
