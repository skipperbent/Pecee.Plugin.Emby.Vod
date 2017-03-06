using System;
using System.Linq;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using MediaBrowser.Plugin.VOD.Configuration;
using MediaBrowser.Plugin.VOD.Entities;

namespace MediaBrowser.Plugin.VOD.Api
{
	[Route("/vod/playlists", "POST", Summary = "Add new playlist")]
	[Authenticated]
	public class PlaylistSend : IReturnVoid
	{
		[ApiMember(Name = "Name", Description = "Name", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
		public string Name { get; set; }

		[ApiMember(Name = "CollectionType", Description = "Collection type", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
		public string CollectionType { get; set; }

		[ApiMember(Name = "Url", Description = "Playlist url", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
		public Uri Url { get; set; }

		[ApiMember(Name = "UserId", Description = "UserId", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
		public string UserId { get; set; }
	}

	[Route("/vod/collectiontypes", "GET", Summary = "Get supported collection-types")]
	[Authenticated]
	public class CollectionTypesSend : IReturn<string[]>
	{
	}

	class PlaylistEndpoint : IService
	{
		public object Get(CollectionTypesSend request)
		{
			return PluginConfiguration.AllowedCollectionTypes;
		}

		public void Post(PlaylistSend request)
		{
			if (string.IsNullOrWhiteSpace(request.Name))
			{
				throw new ArgumentException("Name cannot be empty.");
			}

			if (!request.Url.IsAbsoluteUri)
			{
				throw new ArgumentException("Url cannot be relative");
			}

			if (string.IsNullOrWhiteSpace(request.UserId))
			{
				throw new ArgumentException("UserId cannot be empty");
			}

			if (string.IsNullOrWhiteSpace(request.CollectionType))
			{
				throw new ArgumentException("CollectionType cannot be empty");
			}

			var list = Plugin.Instance.Configuration.Playlists.ToList();

			list.Add(new Playlist()
			{
				UserId = request.UserId,
				Name = request.Name,
				Url = request.Url.ToString(),
				CollectionType = request.CollectionType
			});

			Plugin.Instance.Configuration.Playlists = list.ToArray();
			Plugin.Instance.SaveConfiguration();
		}

	}
}
