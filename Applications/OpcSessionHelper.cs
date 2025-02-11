using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace UANodesetWebViewer
{
    public class OpcSessionCacheData
    {
        public bool Trusted { get; set; }

        public Session OPCSession { get; set; }

        public string CertThumbprint { get; set; }

        public string EndpointURL { get; set; }

        public OpcSessionCacheData()
        {
            Trusted = false;
            EndpointURL = string.Empty;
            CertThumbprint = string.Empty;
            OPCSession = null;
        }
    }

    public class OpcSessionHelper
    {
        public ConcurrentDictionary<string, OpcSessionCacheData> OpcSessionCache = new ConcurrentDictionary<string, OpcSessionCacheData>();

        private readonly ApplicationInstance _app;

        public OpcSessionHelper(ApplicationInstance app)
        {
            _app = app;
        }

        public void Disconnect(string sessionID)
        {
            OpcSessionCacheData entry;
            if (OpcSessionCache.TryRemove(sessionID, out entry))
            {
                try
                {
                    if (entry.OPCSession != null)
                    {
                        entry.OPCSession.Close();
                    }
                }
                catch
                {
                    // do nothing
                }
            }
        }

        public async Task<Session> GetSessionAsync(string sessionID, string endpointURL, string username = null, string password = null)
        {
            if (string.IsNullOrEmpty(sessionID) || string.IsNullOrEmpty(endpointURL))
            {
                return null;
            }

            OpcSessionCacheData entry;
            if (OpcSessionCache.TryGetValue(sessionID, out entry))
            {
                if (entry.OPCSession != null)
                {
                    if (entry.OPCSession.Connected)
                    {
                        return entry.OPCSession;
                    }

                    try
                    {
                        entry.OPCSession.Close(500);
                    }
                    catch
                    {
                        // do nothing
                    }

                    entry.OPCSession = null;
                }
            }
            else
            {
                // create a new entry
                OpcSessionCacheData newEntry = new OpcSessionCacheData { EndpointURL = endpointURL };
                OpcSessionCache.TryAdd(sessionID, newEntry);
            }

            EndpointDescription selectedEndpoint = CoreClientUtils.SelectEndpoint(_app.ApplicationConfiguration, endpointURL, true);
            ConfiguredEndpoint configuredEndpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(_app.ApplicationConfiguration));
            uint timeout = (uint)_app.ApplicationConfiguration.ClientConfiguration.DefaultSessionTimeout;

            UserIdentity userIdentity = null;
            if (username == null)
            {
                userIdentity = new UserIdentity(new AnonymousIdentityToken());
            }
            else
            {
                userIdentity = new UserIdentity(username, password);
            }
            Session session = await Session.Create(
                _app.ApplicationConfiguration,
                configuredEndpoint,
                true,
                false,
                _app.ApplicationConfiguration.ApplicationName,
                (uint)_app.ApplicationConfiguration.ClientConfiguration.DefaultSessionTimeout,
                userIdentity,
                null
            ).ConfigureAwait(false);

            if (session != null)
            {
                // enable diagnostics
                session.ReturnDiagnostics = DiagnosticsMasks.All;

                // Update our cache data
                if (OpcSessionCache.TryGetValue(sessionID, out entry))
                {
                    if (string.Equals(entry.EndpointURL, endpointURL, StringComparison.InvariantCultureIgnoreCase))
                    {
                        OpcSessionCacheData newValue = new OpcSessionCacheData
                        {
                            CertThumbprint = entry.CertThumbprint,
                            EndpointURL = entry.EndpointURL,
                            Trusted = entry.Trusted,
                            OPCSession = session
                        };
                        OpcSessionCache.TryUpdate(sessionID, newValue, entry);
                    }
                }
            }

            return session;
        }
    }
}