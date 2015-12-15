using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

using AirfoilMetadataAgent;

namespace MusicBeePlugin
{
	public partial class Plugin : AirfoilAgentListener
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();
		private Agent airfoilAgent;

		public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
			// Set up all the information about the plugin.
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);
			about.PluginInfoVersion = PluginInfoVersion;
			about.Name = GetAssemblyAttribute<AssemblyProductAttribute>(a => a.Product);
			about.Description = GetAssemblyAttribute<AssemblyDescriptionAttribute>(a => a.Description);
			about.Author = GetAssemblyAttribute<AssemblyCompanyAttribute>(a => a.Company);
			about.TargetApplication = "";
            about.Type = PluginType.General;
			var version = GetType().Assembly.GetName().Version;
            about.VersionMajor = (short)version.Major;
			about.VersionMinor = (short)version.Minor;
			about.Revision = (short)version.Revision;
			about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ConfigurationPanelHeight = 0;

			// Start listening for Airfoil messages.
			airfoilAgent = new Agent(this);

			return about;
        }

		// Indicate to Airfoil that we support remote control (play/pause/next/previous).
		public bool SupportsRemoteControl { get { return true; } }

		public bool HandleRemoteControl(RemoteControlType messageType)
		{
			// We received a remote control request -- handle it!
			switch (messageType)
			{
				case RemoteControlType.PlayPause:
					return mbApiInterface.Player_PlayPause();
				case RemoteControlType.NextTrack:
					return mbApiInterface.Player_PlayNextTrack();
				case RemoteControlType.PreviousTrack:
					return mbApiInterface.Player_PlayPreviousTrack();
			}
			return false;
		}

		public bool SupportsMetadata { get { return true; } }

		public string HandleMetadata(MetadataType messageType)
		{
			// We received a metadata request -- handle it!
			switch (messageType)
			{
				case MetadataType.TrackTitle:
					return GetFirstAvailableTag(MetaDataType.TrackTitle) 
						// This is a little hack to get Airfoil to requery the album art once it's been downloaded.
						// Airfoil appears to poll for the basic metadata at a regular interval, and only if that's 
						// changed does it ask for the artwork. So we send a slightly different track title depending
						// on if the artwork is present or not, and should that change, Airfoil will re-ask for the art.
						+ (HasArtwork() ? "" : " ");
				case MetadataType.TrackArtist:
					// Usually Artist should suffice here, but try a couple backups just in case.
					return GetFirstAvailableTag(MetaDataType.Artist, MetaDataType.PrimaryArtist, MetaDataType.AlbumArtist);
				case MetadataType.TrackAlbum:
					return GetFirstAvailableTag(MetaDataType.Album);
				case MetadataType.AlbumArt:
					return GetArtwork();
			}
			return "";
		}

		protected bool HasArtwork()
		{
			return !String.IsNullOrEmpty(GetArtwork());
		}

		protected String GetArtwork()
		{
			// Start by checking for local album art.
			String artwork = mbApiInterface.NowPlaying_GetArtwork();
			// If that missed, then see if MusicBee has downloaded anything.
			// If THAT missed, then we'll send no artwork back.
			// There's unfortunately no way to initiate a message to Airfoil, so the approach of waiting to be notified about
			// the artwork download completing won't work.
			if (String.IsNullOrEmpty(artwork))
			{
				artwork = mbApiInterface.NowPlaying_GetDownloadedArtwork();
			}
			return artwork;
		}

		/// <summary>
		/// Given a collection of tag types, return the value of the first one that exists in the currently playing file.
		/// </summary>
		/// <param name="tagTypes">The tag types to cycle through, looking for one that's set.</param>
		/// <returns></returns>
		protected String GetFirstAvailableTag(params MetaDataType[] tagTypes)
		{
			String result = null;
			for (int i = 0; i < tagTypes.Length && String.IsNullOrEmpty(result); i++)
			{
				result = mbApiInterface.NowPlaying_GetFileTag(tagTypes[i]);
			}
			return result ?? "";
		}

		protected string GetAssemblyAttribute<T>(Func<T, string> value) where T : Attribute
		{
			T attribute = (T)Attribute.GetCustomAttribute(GetType().Assembly, typeof(T));
			return value.Invoke(attribute);
		}

		public bool Configure(IntPtr panelHandle)
        {
            return false;
        }
       
        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason)
        {
			airfoilAgent.Stop();
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
        }
	}
}