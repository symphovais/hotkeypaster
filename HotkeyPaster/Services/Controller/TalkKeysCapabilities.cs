using System.Collections.Generic;

namespace TalkKeys.Services.Controller
{
    /// <summary>
    /// Describes TalkKeys capabilities for API discovery
    /// </summary>
    public class TalkKeysCapabilities
    {
        public string Name { get; set; } = "TalkKeys";
        public string Version { get; set; } = "1.0.0";
        public List<Capability> Capabilities { get; set; } = new();
        public List<EndpointInfo> Endpoints { get; set; } = new();
    }

    /// <summary>
    /// Describes a specific capability/feature of TalkKeys
    /// </summary>
    public class Capability
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Shortcut { get; set; }
        public List<string> Actions { get; set; } = new();
    }

    /// <summary>
    /// Describes an API endpoint
    /// </summary>
    public class EndpointInfo
    {
        public string Method { get; set; } = "GET";
        public string Path { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
