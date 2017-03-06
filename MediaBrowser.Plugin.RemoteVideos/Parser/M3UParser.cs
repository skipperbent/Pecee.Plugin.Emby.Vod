using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugin.VOD.Configuration;
using MediaBrowser.Plugin.VOD.Entities;

namespace MediaBrowser.Plugin.VOD.Parser
{
	public class M3UParser
	{
		private readonly Regex _regexPattern = new Regex(@"([^=\s]+)\=[""']([^""']+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
		private readonly ILogger _logger;
		private readonly IHttpClient _httpClient;

		public M3UParser(ILogManager logManager, IHttpClient httpClient)
		{
			_httpClient = httpClient;
			_logger = logManager.GetLogger(PluginConfiguration.Name);
		}

		public Dictionary<string, string> ParseAttributes(string line)
		{
			var attributes = new Dictionary<string, string>();

			// Skip if no attribute matches were found
			var attributeMatches = _regexPattern.Matches(line);
			if (attributeMatches.Count == 0)
			{
				return attributes;
			}

			_logger.Debug("Found {0} attributes", attributeMatches.Count);

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

				_logger.Debug("Key: {0} - Value: {1}", key, value);
				attributes.Add(key, value);
			}

			return attributes;
		}

		public async Task<List<Media>> GetMediaItems(string playlistUrl, CancellationToken cancellationToken)
		{
			var items = new List<Media>();

			using (Stream stream = await _httpClient.Get(playlistUrl, CancellationToken.None).ConfigureAwait(false))
			{
				using (var reader = new StreamReader(stream))
				{
					while (!reader.EndOfStream)
					{
						var line = reader.ReadLine();

						if (line.IndexOf("#EXTINF", StringComparison.CurrentCulture) != 0)
						{
							continue;
						}

						_logger.Debug("Found line with #EXTINF meta information");

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
							_logger.Debug("Found tvg-logo: {0}", attributes["tvg-logo"]);
							Uri.TryCreate(attributes["tvg-logo"], UriKind.Absolute, out imgUri);
						}

						var imageUrl = (imgUri != null) ? imgUri.ToString() : null;
						_logger.Debug("Adding: {0}, stream playlistUrl: {0}, image: {0}", attributes["tvg-name"], streamUrl, imageUrl);

						var media = new Media
						{
							Url = streamUrl.ToString(),
							Name = attributes["tvg-name"],
							Image = imageUrl,
						};

						items.Add(media);
					}
				}
			}

			return items;
		}

	}
}
