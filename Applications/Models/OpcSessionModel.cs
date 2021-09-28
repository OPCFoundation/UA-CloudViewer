
namespace UANodesetWebViewer.Models
{
    /// <summary>
    /// A view model for the Browser view.
    /// </summary>
    public class OpcSessionModel
    {
        /// <summary>
        /// The ID of the active session.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// The OPC UA server IP address
        /// </summary>
        public string ServerIP { get; set; }

        /// <summary>
        /// The OPC UA server port it is running on
        /// </summary>
        public string ServerPort { get; set; }

        /// <summary>
        /// Error text for the error view.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}