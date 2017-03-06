using System;
using MediaBrowser.Controller.Entities.Movies;

namespace MediaBrowser.Plugin.VOD.Models
{
	public class VodMovie : Movie
	{
		public String IdentifierId { get; set; }
	}
}
