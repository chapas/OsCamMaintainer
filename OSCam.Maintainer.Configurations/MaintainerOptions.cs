namespace OSCam.Maintainer.Configurations
{
    /// <summary>
    /// Maintainer configurations
    /// </summary>
    public class MaintainerOptions
    {
        /// <summary>
        /// Gets or sets the server path
        /// </summary>
        /// <value>
        /// The oscam.server file path
        /// </value>
        public string OscamServerPath { get; set; }

        /// <summary>
        /// Gets or sets the number of backups
        /// </summary>
        /// <value>
        /// Number of backups to keep of oscam.server
        /// </value>
        public int NumberOfBackupsToKeep { get; set; }

        /// <summary>
        /// Gets or sets endpoint to be scrapped
        /// </summary>
        /// <value>
        /// The URL to be scrapped for C lines
        /// </value>
        public string URLToScrap { get; set; }

        /// <summary>
        /// Gets or sets OsCam status endpoint
        /// </summary>
        /// <value>
        /// The URL with status page from OsCam
        /// </value>
        public string OsCamStatusPageURL { get; set; }
    }
}
