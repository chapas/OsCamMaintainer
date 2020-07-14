
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace OSCam.Maintainer.Configurations
{
    /// <summary>
    /// Implementation of the configurable oscam maintiainer options
    /// </summary>
    public class ConfigureMaintainerOptions : IConfigureOptions<MaintainerOptions>
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Instantiates a <see cref="ConfigureMaintainerOptions"/>
        /// </summary>
        /// <param name="configuration">Service configuration</param>
        public ConfigureMaintainerOptions(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // <inherit/>
        public void Configure(MaintainerOptions options)
        {
            _configuration.GetSection("OsCam").Bind(options);
        }
    }
}
