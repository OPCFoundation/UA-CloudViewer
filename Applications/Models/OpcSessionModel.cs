
using Microsoft.AspNetCore.Mvc.Rendering;

namespace UANodesetWebViewer.Models
{
    public class OpcSessionModel
    {
        public string SessionId { get; set; }

        public SelectList NodesetIDs { get; set; }

        public string EndpointUrl { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string StatusMessage { get; set; }
    }
}
