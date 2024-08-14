
using Microsoft.AspNetCore.Mvc.Rendering;

namespace UANodesetWebViewer.Models
{
    public class OpcSessionModel
    {
        public SelectList NodesetIDs { get; set; }

        public string EndpointUrl { get; set; }

        public string StatusMessage { get; set; }

        public string NodesetFile { get; set; }

        public string WoTFile { get; set; }

        public SelectList WoTProperties { get; set; }
    }
}
