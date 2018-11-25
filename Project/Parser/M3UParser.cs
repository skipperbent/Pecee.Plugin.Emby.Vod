using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using Pecee.Emby.Plugin.Vod.Configuration;
using Pecee.Emby.Plugin.Vod.Entities;
using Pecee.Emby.Plugin.Vod.Models;

namespace Pecee.Emby.Plugin.Vod.Parser
{
	public class M3UParser
	{
		private readonly Regex _regexPattern = new Regex(@"([^=\s]+)\=[""']([^""']+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
		private readonly ILogger _logger = Plugin.Instance.Logger;
		private readonly IHttpClient _httpClient;

		public M3UParser(IHttpClient httpClient)
		{
			_httpClient = httpClient;
		}

		public Dictionary<string, string> ParseAttributes(string line)
		{
			var attributes = new Dictionary<string, string>();

			// Skip if no attribute matches were found
			var attributeMatches = _regexPattern.Matches(line);
			if (attributeMatches.Count == 0)
			{
				_logger.Debug("[VOD] No attributes found", attributeMatches.Count);
				return attributes;
			}

			_logger.Debug("[VOD] Found {0} attributes", attributeMatches.Count);

			foreach (Match attribute in attributeMatches)
			{
				if (attribute.Groups == null || attribute.Groups.Count <= 1)
				{
					continue;
				}

				var key = attribute.Groups[1].Value;
				var value = (attribute.Groups[2] != null) ? attribute.Groups[2].Value : string.Empty;

				if (String.IsNullOrEmpty(key))
				{
					continue;
				}

				_logger.Debug("[VOD] Key: {0} - Value: {1}", key, value);
				attributes.Add(key, value);
			}

			return attributes;
		}

		public async Task<List<Media>> GetMediaItems(VodPlaylist playlist, CancellationToken cancellationToken)
		{
			var items = new List<Media>();

			_logger.Debug("[VOD] {0}: Starting to parse: {1}", playlist.Name, playlist.PlaylistUrl);

			using (Stream stream = await _httpClient.Get(new HttpRequestOptions()
			{
                Url = playlist.PlaylistUrl,
                TimeoutMs = 9000
			}).ConfigureAwait(false))
			{
				using (var reader = new StreamReader(stream))
				{
					while (!reader.EndOfStream)
					{
						var line = reader.ReadLine();

						if (line.IndexOf("#EXTINF", StringComparison.CurrentCulture) != 0)
						{
							_logger.Debug("[VOD] {0}: Non-valid line, skipping", playlist.Name);
							continue;
						}

						_logger.Debug("[VOD] Found line with #EXTINF meta information");

						var attributes = ParseAttributes(line);

						Uri streamUrl = null;
						Uri imgUri = null;

						if (attributes.Count == 0 || !attributes.ContainsKey("tvg-name") || !Uri.TryCreate(reader.ReadLine(), UriKind.Absolute, out streamUrl))
						{
							continue;
						}

						// Fetch logo
						if (attributes.ContainsKey("tvg-logo"))
						{
							_logger.Debug("[VOD] Found tvg-logo: {0}", attributes["tvg-logo"]);
							Uri.TryCreate(attributes["tvg-logo"], UriKind.Absolute, out imgUri);
						}

						var imageUrl = (imgUri != null) ? imgUri.ToString() : null;
						_logger.Debug("[VOD] Adding: {0}, stream playlistUrl: {0}, image: {0}", attributes["tvg-name"], streamUrl, imageUrl);

						var media = new Media()
						{
							Name = attributes["tvg-name"].Trim(),
							Url = streamUrl.ToString(),
							PlaylistId = playlist.Id,
							Image = (imageUrl != null) ? imageUrl : null
						};

						items.Add(media);
					}
				}
			}

			return items;
		}

	}
}
