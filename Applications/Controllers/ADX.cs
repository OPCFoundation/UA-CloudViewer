using Kusto.Cloud.Platform.Data;
using Kusto.Cloud.Platform.Utils;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Ingestion;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UANodesetWebViewer.Models;

namespace UANodesetWebViewer.Controllers
{
    public class ADX : Controller
    {
        public ActionResult Index()
        {
            ADXModel model = new ADXModel
            {
                StatusMessage = ""
            };

            return View("Index", model);
        }

        [HttpPost]
        public ActionResult Upload(string instanceUrl, string databaseName, string tenantId, string clientId, string secret)
        {
            ADXModel model = new ADXModel
            {
                InstanceUrl = instanceUrl,
                DatabaseName = databaseName,
                TenantId = tenantId,
                ClientId = clientId,
                Secret = secret,
            };

            try
            {
                var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(instanceUrl).WithAadApplicationKeyAuthentication(clientId, secret, tenantId);
                string tableName = DTDL._nodesetNamespaceURI.Replace("http://", "").Replace('/','_').Replace('.','_').TrimEnd('_');
                
                using (var kustoClient = KustoClientFactory.CreateCslAdminProvider(kustoConnectionStringBuilder))
                {
                    kustoClient.ExecuteControlCommand(databaseName, ".create table " + tableName + " (ExpandedNodeID: string, DisplayName: string, Type: string, ParentNodeID: string)");

                    string command = ".create table " + tableName + " ingestion json mapping 'Nodeset_Mapping' '["
                        + "{ \"properties\": { \"path\": \"$.Key\" }, \"column\": \"ExpandedNodeID\", \"datatype\": \"string\" },"
                        + "{ \"properties\": { \"path\": \"$.Value.Item1\" }, \"column\": \"DisplayName\", \"datatype\": \"string\" } ,"
                        + "{ \"properties\": { \"path\": \"$.Value.Item2\" }, \"column\": \"Type\", \"datatype\": \"string\" },"
                        + "{ \"properties\": { \"path\": \"$.Value.Item3\" }, \"column\": \"ParentNodeID\", \"datatype\": \"string\" } ]'";
                    kustoClient.ExecuteControlCommand(databaseName, command);

                    command = ".alter table " + tableName + " policy ingestionbatching @'{"
                        + "\"MaximumBatchingTimeSpan\": \"00:00:05\","
                        + "\"MaximumNumberOfItems\": 100,"
                        + "\"MaximumRawDataSizeMB\": 1024 }'";
                    kustoClient.ExecuteControlCommand(databaseName, command);
                }

                using (IKustoIngestClient client = KustoIngestFactory.CreateDirectIngestClient(kustoConnectionStringBuilder))
                {
                    var kustoIngestionProperties = new KustoIngestionProperties(databaseName, tableName);
                    kustoIngestionProperties.Format = DataSourceFormat.multijson;
                    kustoIngestionProperties.IngestionMapping = new IngestionMapping()
                    {
                        IngestionMappingReference = "Nodeset_Mapping",
                        IngestionMappingKind = IngestionMappingKind.Json
                    };

                    foreach (KeyValuePair<string, Tuple<string, string, string>> entry in DTDL._nodeList.ToArray(100))
                    {
                        string content = JsonConvert.SerializeObject(entry, Formatting.Indented);
                        MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                        client.IngestFromStream(stream, kustoIngestionProperties);
                        stream.Dispose();
                    }
                }

                using (var cslQueryProvider = KustoClientFactory.CreateCslQueryProvider(kustoConnectionStringBuilder))
                {
                    string query = $"{tableName} | count";
                    var results = cslQueryProvider.ExecuteQuery<long>(databaseName, query);
                    foreach (long result in results)
                    {
                        model.StatusMessage = "ADX Ingestion succeeded.";
                        return View("Index", model);
                    }
                }

                model.StatusMessage = "ADX Ingestion failed!";
                return View("Error", model);
            }
            catch (Exception ex)
            {
                model.StatusMessage = ex.Message;
                return View("Index", model);
            }
        }
    }
}
