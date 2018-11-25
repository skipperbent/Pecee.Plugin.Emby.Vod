using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Pecee.Emby.Plugin.Vod.Configuration;
using Pecee.Emby.Plugin.Vod.Entities;
using Pecee.Emby.Plugin.Vod.Folder;
using Pecee.Emby.Plugin.Vod.Models;

namespace Pecee.Emby.Plugin.Vod
{
	public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {

		public static Plugin Instance { get; private set; }

		public override string Name => PluginConfiguration.Name;

		public override string Description => PluginConfiguration.Description;

        public ILogger Logger { get; }
        public ILibraryManager LibraryManager { get; }

		public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogManager logger, ILibraryManager libraryManager) : base(applicationPaths, xmlSerializer)
		{
			Instance = this;
		    Logger = logger.GetLogger(PluginConfiguration.Name);
		    LibraryManager = libraryManager;

		}

		public IEnumerable<PluginPageInfo> GetPages()
		{
			return new[]
			{
				new PluginPageInfo
				{
					Name = "vod.config",
					EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
				},
			};
		}

        public override void OnUninstalling()
        {

            // Cleanup
            Logger.Debug("[VOD] Start uninstall cleanup");

            var types = new []
            {
                typeof(VodPlaylist), typeof(Channel), typeof(Media), typeof(ChannelFolder)
            };

            foreach (var playlist in LibraryManager.GetItemList(new InternalItemsQuery(), false).Where(i => types.Contains(i.GetType())).ToList())
            {
                Logger.Debug("[VOD] Removing playlist: {0}", playlist.Name);
                
                LibraryManager.DeleteItem(playlist, new DeleteOptions()
                {
                    DeleteFileLocation = false,
                    DeleteFromExternalProvider = true,
                }, true);
            }

            Logger.Debug("[VOD] Finished uninstall cleanup");

            base.OnUninstalling();
        }

        public static DynamicImageResponse GetImage(string name)
		{
			var type = typeof(Plugin);
			var path = String.Format("{0}.Images.{1}.png", type.Namespace, name);
			return new DynamicImageResponse
			{
				Format = ImageFormat.Png,
				HasImage = true,
				Stream = type.GetTypeInfo().Assembly.GetManifestResourceStream(path)
			};
		}

    }
}
