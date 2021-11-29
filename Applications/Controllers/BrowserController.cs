
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua.Export;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using UACloudLibrary;
using UANodesetWebViewer.Models;

namespace UANodesetWebViewer.Controllers
{
    public class StatusHub : Hub
    {
    }

    public class BrowserController : Controller
    {
        public static List<string> _nodeSetFilenames = new List<string>();

        private IHubContext<StatusHub> _hubContext;

        private WebClient _client;
        
        private static ApplicationInstance _application = new ApplicationInstance();

        public BrowserController(IHubContext<StatusHub> hubContext)
        {
             _hubContext = hubContext;
            _client = new WebClient();
        }


        private class MethodCallParameterData
        {
            public string Name { get; set; }

            public string Value { get; set; }

            public string ValueRank { get; set; }

            public string ArrayDimensions { get; set; }

            public string Description { get; set; }

            public string Datatype { get; set; }

            public string TypeName { get; set; }
        }

        public ActionResult Index()
        {
            OpcSessionModel sessionModel = new OpcSessionModel();
            sessionModel.SessionId = HttpContext.Session.Id;

            OpcSessionCacheData entry = null;
            if (OpcSessionHelper.Instance.OpcSessionCache.TryGetValue(HttpContext.Session.Id, out entry))
            {
                sessionModel.ServerIP = entry.EndpointURL.Host;
                sessionModel.ServerPort = entry.EndpointURL.Port.ToString();

                HttpContext.Session.SetString("EndpointUrl", entry.EndpointURL.AbsoluteUri);

                return View("Browse", sessionModel);
            }

            ViewBag.Nodesetids = new SelectList(new List<string>());

            UpdateStatus("Additional Information Required");
            return View("Index", sessionModel);
        }

        [HttpPost]
        public ActionResult Login(string instanceUrl, string clientId, string secret)
        {
            _client.Headers.Add("Authorization", "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secret)));
            _client.Headers.Add("Content-Type", "application/json");
            
            if (!instanceUrl.EndsWith('/'))
            {
                instanceUrl += '/';
            }
            _client.BaseAddress = instanceUrl;

            string[] keywords = { "*" }; // return everything
            string address = instanceUrl + "infomodel/find";
            string response = _client.UploadString(address, "PUT", JsonConvert.SerializeObject(keywords));
            List<string> identifiers = new List<string>(JsonConvert.DeserializeObject<string[]>(response));


            address = instanceUrl + "infomodel/namespaces";
            response = _client.DownloadString(address);
            string[] identifiers2 = JsonConvert.DeserializeObject<string[]>(response);

            for (int i = 0; i < identifiers.Count; i++)
            {
                for (int j = 0; j < identifiers2.Length; j++)
                {
                    if (identifiers2[j].Contains(identifiers[i]))
                    {
                        identifiers.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }

            List<string> nodesetnames = new List<string>();
            foreach (string identifier in identifiers)
            {
                address = instanceUrl + "infomodel/download/" + Uri.EscapeDataString(identifier);
                response = _client.DownloadString(address);
                AddressSpace addressSpace = JsonConvert.DeserializeObject<AddressSpace>(response);
                nodesetnames.Add(addressSpace.Title);
            }

            nodesetnames.Sort();
            ViewBag.Nodesetids = new SelectList(nodesetnames);

            OpcSessionModel sessionModel = new OpcSessionModel();
            return View("Index", sessionModel);
        }

        public ActionResult Privacy()
        {
            return View("Privacy");
        }

        public ActionResult GenerateAAS()
        {
            try
            {
                string packagePath = Path.Combine(Directory.GetCurrentDirectory(), "UANodeSet.aasx");
                using (Package package = Package.Open(packagePath, FileMode.Create))
                {
                    // add package origin part
                    PackagePart origin = package.CreatePart(new Uri("/aasx/aasx-origin", UriKind.Relative), MediaTypeNames.Text.Plain, CompressionOption.Maximum);
                    using (Stream fileStream = origin.GetStream(FileMode.Create))
                    {
                        var bytes = Encoding.ASCII.GetBytes("Intentionally empty.");
                        fileStream.Write(bytes, 0, bytes.Length);
                    }
                    package.CreateRelationship(origin.Uri, TargetMode.Internal, "http://www.admin-shell.io/aasx/relationships/aasx-origin");

                    // create package spec part
                    string packageSpecPath = Path.Combine(Directory.GetCurrentDirectory(), "aasenv-with-no-id.aas.xml");
                    using (StringReader reader = new StringReader(System.IO.File.ReadAllText(packageSpecPath)))
                    {
                        XmlSerializer aasSerializer = new XmlSerializer(typeof(AasEnv));
                        AasEnv aasEnv = (AasEnv)aasSerializer.Deserialize(reader);

                        aasEnv.AssetAdministrationShells.AssetAdministrationShell.SubmodelRefs.Clear();
                        aasEnv.Submodels.Clear();

                        foreach(string filename in _nodeSetFilenames)
                        {
                            string submodelPath = Path.Combine(Directory.GetCurrentDirectory(), "submodel.aas.xml");
                            using (StringReader reader2 = new StringReader(System.IO.File.ReadAllText(submodelPath)))
                            {
                                XmlSerializer aasSubModelSerializer = new XmlSerializer(typeof(AASSubModel));
                                AASSubModel aasSubModel = (AASSubModel)aasSubModelSerializer.Deserialize(reader2);

                                SubmodelRef nodesetReference = new SubmodelRef();
                                nodesetReference.Keys = new Keys();
                                nodesetReference.Keys.Key = new Key
                                {
                                    IdType = "URI",
                                    Local = true,
                                    Type = "Submodel",
                                    Text = "http://www.opcfoundation.org/type/opcua/" + filename.Replace(".", "").ToLower()
                            };

                                aasEnv.AssetAdministrationShells.AssetAdministrationShell.SubmodelRefs.Add(nodesetReference);

                                aasSubModel.Identification.Text += filename.Replace(".", "").ToLower();
                                aasSubModel.SubmodelElements.SubmodelElement.SubmodelElementCollection.Value.SubmodelElement.File.Value =
                                    aasSubModel.SubmodelElements.SubmodelElement.SubmodelElementCollection.Value.SubmodelElement.File.Value.Replace("TOBEREPLACED", filename);
                                aasEnv.Submodels.Add(aasSubModel);
                            }
                        }

                        XmlTextWriter aasWriter = new XmlTextWriter(packageSpecPath, Encoding.UTF8);
                        aasSerializer.Serialize(aasWriter, aasEnv);
                        aasWriter.Close();
                    }

                    // add package spec part
                    PackagePart spec = package.CreatePart(new Uri("/aasx/aasenv-with-no-id/aasenv-with-no-id.aas.xml", UriKind.Relative), MediaTypeNames.Text.Xml);
                    using (FileStream fileStream = new FileStream(packageSpecPath, FileMode.Open, FileAccess.Read))
                    {
                        CopyStream(fileStream, spec.GetStream());
                    }
                    origin.CreateRelationship(spec.Uri, TargetMode.Internal, "http://www.admin-shell.io/aasx/relationships/aas-spec");

                    // add nodeset files
                    for(int i = 0; i < _nodeSetFilenames.Count; i++)
                    {
                        PackagePart supplementalDoc = package.CreatePart(new Uri("/aasx/" + Path.GetFileNameWithoutExtension(_nodeSetFilenames[i]), UriKind.Relative), MediaTypeNames.Text.Xml);
                        string documentPath = Path.Combine(Directory.GetCurrentDirectory(), _nodeSetFilenames[i]);
                        using (FileStream fileStream = new FileStream(documentPath, FileMode.Open, FileAccess.Read))
                        {
                            CopyStream(fileStream, supplementalDoc.GetStream());
                        }
                        package.CreateRelationship(supplementalDoc.Uri, TargetMode.Internal, "http://www.admin-shell.io/aasx/relationships/aas-suppl");
                    }
                }

                return File(new FileStream(Path.Combine(Directory.GetCurrentDirectory(), "UANodeSet.aasx"), FileMode.Open, FileAccess.Read), "APPLICATION/octet-stream", "UANodeSet.aasx");
            }
            catch (Exception ex)
            {
                OpcSessionModel sessionModel = new OpcSessionModel
                {
                    ErrorMessage = HttpUtility.HtmlDecode(ex.Message)
                };

                return View("Error", sessionModel);
            }
        }

        private void CopyStream(Stream source, Stream target)
        {
            const int bufSize = 0x1000;
            byte[] buf = new byte[bufSize];
            int bytesRead = 0;
            while ((bytesRead = source.Read(buf, 0, bufSize)) > 0)
            {
                target.Write(buf, 0, bytesRead);
            }
        }

        [HttpPost]
        public ActionResult Error(string errorMessage)
        {
            OpcSessionModel sessionModel = new OpcSessionModel
            {
                ErrorMessage = HttpUtility.HtmlDecode(errorMessage)
            };

            UpdateStatus($"Error Occured: {sessionModel.ErrorMessage}");

            return View("Error", sessionModel);
        }

        public ActionResult CloudLibrayFileOpen(string nodesetfile)
        {
            OpcSessionModel sessionModel = new OpcSessionModel
            {
                ServerIP = "localhost",
                ServerPort = "4840",
            };

            string address = _client.BaseAddress + "infomodel/download/" + Uri.EscapeDataString(nodesetfile);
            string response = _client.DownloadString(address);
            AddressSpace addressSpace = JsonConvert.DeserializeObject<AddressSpace>(response);

            // store the file on the webserver
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "NodeSets", "nodeset2.xml");
            System.IO.File.WriteAllText(filePath, addressSpace.Nodeset.NodesetXml);
            _nodeSetFilenames.Add(filePath);

            return View("Browse", sessionModel);
        }

        [HttpPost]
        public async Task<ActionResult> LocalFileOpen(IFormFile[] files)
        {
            OpcSessionModel sessionModel = new OpcSessionModel
            {
                ServerIP = "localhost",
                ServerPort = "4840",
            };

            try
            {
                if ((files == null) || (files.Length == 0))
                {
                    throw new ArgumentException("No files specified!");
                }

                _nodeSetFilenames.Clear();
                foreach (IFormFile file in files)
                {
                    if ((file.Length == 0) || (file.ContentType != "text/xml"))
                    {
                        throw new ArgumentException("Invalid file specified!");
                    }

                    // file name validation
                    new FileInfo(file.FileName);

                    // store the file on the webserver
                    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "NodeSets", file.FileName);
                    using (FileStream stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream).ConfigureAwait(false);
                    }

                    _nodeSetFilenames.Add(filePath);
                }

                // Validate namespaces listed in each file and make sure all referenced nodeset files are present and loaded in the right order
                List<string> dependencies = new List<string>();
                foreach (string nodesetFile in _nodeSetFilenames)
                {
                    using (Stream stream = new FileStream(nodesetFile, FileMode.Open))
                    {
                        UANodeSet nodeSet = UANodeSet.Read(stream);
                        if ((nodeSet.NamespaceUris != null ) && (nodeSet.NamespaceUris.Length > 0))
                        {
                            foreach (string ns in nodeSet.NamespaceUris)
                            {
                                string dependency = ns.Substring(7).TrimEnd('/').ToLower();
                                dependency = dependency.Substring(dependency.IndexOf('/'));

                                if (dependency.StartsWith("/"))
                                {
                                    dependency = dependency.Substring(1);
                                }

                                if (dependency.StartsWith("ua"))
                                {
                                    dependency = dependency.Substring(dependency.IndexOf('/') + 1);
                                }

                                dependency = dependency.Replace("/", "");

                                if (!dependencies.Contains(dependency))
                                {
                                    dependencies.Add(dependency);
                                }
                            }
                        }
                    }
                }

                // try to deduct the UA namespace name from the nodeset filename
                // TODO: This does not work for "nonstandard" filenames (i.e. other than something.ua.something.nodest2.xml) and therefore needs improvement!
                List<string> loadedNodesets = new List<string>();
                foreach (string filename in _nodeSetFilenames)
                {
                    loadedNodesets.Add(filename);
                }

                // trim the additional nodeset filename formatting
                for (int i = 0; i < loadedNodesets.Count; i++)
                {
                    loadedNodesets[i] = Path.GetFileNameWithoutExtension(loadedNodesets[i]).ToLower();

                    if (loadedNodesets[i].StartsWith("opc."))
                    {
                        loadedNodesets[i] = loadedNodesets[i].Substring(4);
                    }

                    if (loadedNodesets[i].StartsWith("ua."))
                    {
                        loadedNodesets[i] = loadedNodesets[i].Substring(3);
                    }

                    if (loadedNodesets[i].EndsWith(".nodeset2"))
                    {
                        loadedNodesets[i] = loadedNodesets[i].Substring(0, loadedNodesets[i].IndexOf(".nodeset2"));
                    }
                }

                for (int i = 0; i < dependencies.Count; i++)
                {
                    if (!loadedNodesets.Contains(dependencies[i]))
                    {
                        sessionModel.ErrorMessage = "Referenced nodeset file '" + dependencies[i] + "' missing in loaded nodsets, please add it!";
                        return View("Error", sessionModel);
                    }
                }

                // (re-)start the UA server
                if (_application.Server != null)
                {
                    _application.Stop();
                }

                await StartServerAsync().ConfigureAwait(false);

                // start the UA client
                Session session = null;
                string endpointURL = "opc.tcp://" + sessionModel.ServerIP + ":" + sessionModel.ServerPort + "/";
                session = await OpcSessionHelper.Instance.GetSessionAsync(_application.ApplicationConfiguration, HttpContext.Session.Id, endpointURL, true).ConfigureAwait(false);
                UpdateStatus("Connected");

                HttpContext.Session.SetString("EndpointUrl", endpointURL);

                return View("Browse", sessionModel);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);

                sessionModel.ErrorMessage = ex.Message;
                UpdateStatus($"Error Occured: {sessionModel.ErrorMessage}");

                return View("Error", sessionModel);
            }
        }

        private async Task StartServerAsync()
        {
            // load the application configuration.

            ApplicationConfiguration config = await _application.LoadApplicationConfiguration(Path.Combine(Directory.GetCurrentDirectory(), "Application.Config.xml"), false).ConfigureAwait(false);

            // check the application certificate.
            await _application.CheckApplicationInstanceCertificate(false, 0).ConfigureAwait(false);

            // create cert validator
            config.CertificateValidator = new CertificateValidator();
            config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);

            // start the server.
            await _application.Start(new SimpleServer()).ConfigureAwait(false);
        }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted)
            {
                // accept all OPC UA client certificates
                Console.WriteLine("Automatically trusting client certificate " + e.Certificate.Subject);
                e.Accept = true;
            }
        }

        [HttpPost]
        public ActionResult Disconnect()
        {
            try
            {
                OpcSessionHelper.Instance.Disconnect(HttpContext.Session.Id);
                HttpContext.Session.SetString("EndpointUrl", string.Empty);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
            }

            OpcSessionModel sessionModel = new OpcSessionModel();
            sessionModel.SessionId = HttpContext.Session.Id;

            if (_application.Server != null)
            {
                _application.Stop();
            }

            UpdateStatus("Disconnected");

            return View("Index", sessionModel);
        }

        [HttpPost]
        public async Task<ActionResult> GetRootNode()
        {
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;
            var jsonTree = new List<object>();

            bool lastRetry = false;
            while (true)
            {
                try
                {
                    Session session = await OpcSessionHelper.Instance.GetSessionAsync(_application.ApplicationConfiguration, HttpContext.Session.Id, HttpContext.Session.GetString("EndpointUrl")).ConfigureAwait(false);

                    session.Browse(
                        null,
                        null,
                        ObjectIds.RootFolder,
                        0u,
                        BrowseDirection.Forward,
                        ReferenceTypeIds.HierarchicalReferences,
                        true,
                        0,
                        out continuationPoint,
                        out references);
                    jsonTree.Add(new { id = ObjectIds.RootFolder.ToString(), text = "Root", children = (references?.Count != 0) });

                    return Json(jsonTree);
                }
                catch (Exception ex)
                {
                    OpcSessionHelper.Instance.Disconnect(HttpContext.Session.Id);
                    if (lastRetry)
                    {
                        return Content(CreateOpcExceptionActionString(ex));
                    }
                    lastRetry = true;
                }
            }
        }

        [HttpPost]
        public async Task<ActionResult> GetChildren(string jstreeNode)
        {
            // This delimiter is used to allow the storing of the OPC UA parent node ID together with the OPC UA child node ID in jstree data structures and provide it as parameter to
            // Ajax calls.
            var node = OpcSessionHelper.GetNodeIdFromJsTreeNode(jstreeNode);

            ReferenceDescriptionCollection references = null;
            Byte[] continuationPoint;
            var jsonTree = new List<object>();

            // read the currently published nodes
            Session session = null;
            string endpointUrl = null;
            try
            {
                UpdateStatus("Connecting to OPC Server");
                session = await OpcSessionHelper.Instance.GetSessionAsync(_application.ApplicationConfiguration, HttpContext.Session.Id, HttpContext.Session.GetString("EndpointUrl")).ConfigureAwait(false);
                endpointUrl = session.ConfiguredEndpoint.EndpointUrl.AbsoluteUri;
            }
            catch (Exception ex)
            {
                // do nothing, since we still want to show the tree
                Trace.TraceError("Can not read published nodes for endpoint '{0}'.", endpointUrl);
                Trace.TraceError(ex.Message);
            }

            bool lastRetry = false;
            while (true)
            {
                try
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    try
                    {
                        if (session.Disposed)
                        {
                            session.Reconnect();
                        }

                        UpdateStatus($"Browse OPC UA node {node}");
                        session.Browse(
                            null,
                            null,
                            node,
                            0u,
                            BrowseDirection.Forward,
                            ReferenceTypeIds.HierarchicalReferences,
                            true,
                            0,
                            out continuationPoint,
                            out references);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Can not browse node '{0}'", node);
                        Trace.TraceError(ex.Message);
                    }

                    Trace.TraceInformation("Browsing node '{0}' data took {0} ms", node.ToString(), stopwatch.ElapsedMilliseconds);

                    if (references != null)
                    {
                        var idList = new List<string>();
                        foreach (var nodeReference in references)
                        {
                            bool idFound = false;
                            foreach (var id in idList)
                            {
                                if (id == nodeReference.NodeId.ToString())
                                {
                                    idFound = true;
                                }
                            }
                            if (idFound == true)
                            {
                                continue;
                            }

                            ReferenceDescriptionCollection childReferences = null;
                            Byte[] childContinuationPoint;

                            Trace.TraceInformation("Browse '{0}' count: {1}", nodeReference.NodeId, jsonTree.Count);

                            INode currentNode = null;
                            try
                            {
                                if (session.Disposed)
                                {
                                    session.Reconnect();
                                }

                                UpdateStatus($"Browse OPC UA node {ExpandedNodeId.ToNodeId(nodeReference.NodeId, session.NamespaceUris)}");
                                session.Browse(
                                    null,
                                    null,
                                    ExpandedNodeId.ToNodeId(nodeReference.NodeId, session.NamespaceUris),
                                    0u,
                                    BrowseDirection.Forward,
                                    ReferenceTypeIds.HierarchicalReferences,
                                    true,
                                    0,
                                    out childContinuationPoint,
                                    out childReferences);

                                UpdateStatus($"Read OPC UA node {ExpandedNodeId.ToNodeId(nodeReference.NodeId, session.NamespaceUris)}");
                                currentNode = session.ReadNode(ExpandedNodeId.ToNodeId(nodeReference.NodeId, session.NamespaceUris));
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceError("Can not browse or read node '{0}'", nodeReference.NodeId);
                                Trace.TraceError(ex.Message);

                                // skip this node
                                continue;
                            }

                            byte currentNodeAccessLevel = 0;
                            byte currentNodeEventNotifier = 0;
                            bool currentNodeExecutable = false;

                            VariableNode variableNode = currentNode as VariableNode;
                            if (variableNode != null)
                            {
                                currentNodeAccessLevel = variableNode.UserAccessLevel;
                            }

                            ObjectNode objectNode = currentNode as ObjectNode;
                            if (objectNode != null)
                            {
                                currentNodeEventNotifier = objectNode.EventNotifier;
                            }

                            ViewNode viewNode = currentNode as ViewNode;
                            if (viewNode != null)
                            {
                                currentNodeEventNotifier = viewNode.EventNotifier;
                            }

                            MethodNode methodNode = currentNode as MethodNode;
                            if (methodNode != null)
                            {
                                currentNodeExecutable = methodNode.UserExecutable;
                            }

                            jsonTree.Add(new
                            {
                                id = ("__" + node + OpcSessionHelper.Delimiter + nodeReference.NodeId.ToString()),
                                text = nodeReference.DisplayName.ToString() + " (ns=" + session.NamespaceUris.ToArray()[nodeReference.NodeId.NamespaceIndex] + ";" + nodeReference.NodeId.ToString() + ")",
                                nodeClass = nodeReference.NodeClass.ToString(),
                                accessLevel = currentNodeAccessLevel.ToString(),
                                eventNotifier = currentNodeEventNotifier.ToString(),
                                executable = currentNodeExecutable.ToString(),
                                children = (childReferences.Count == 0) ? false : true,
                                publishedNode = false
                            });
                            idList.Add(nodeReference.NodeId.ToString());
                        }

                        // If there are no children, then this is a call to read the properties of the node itself.
                        if (jsonTree.Count == 0)
                        {
                            INode currentNode = null;

                            try
                            {
                                currentNode = session.ReadNode(new NodeId(node));
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceError("Can not read node '{0}'", new NodeId(node));
                                Trace.TraceError(ex.Message);
                            }

                            if (currentNode == null)
                            {
                                byte currentNodeAccessLevel = 0;
                                byte currentNodeEventNotifier = 0;
                                bool currentNodeExecutable = false;

                                VariableNode variableNode = currentNode as VariableNode;

                                if (variableNode != null)
                                {
                                    currentNodeAccessLevel = variableNode.UserAccessLevel;
                                }

                                ObjectNode objectNode = currentNode as ObjectNode;

                                if (objectNode != null)
                                {
                                    currentNodeEventNotifier = objectNode.EventNotifier;
                                }

                                ViewNode viewNode = currentNode as ViewNode;

                                if (viewNode != null)
                                {
                                    currentNodeEventNotifier = viewNode.EventNotifier;
                                }

                                MethodNode methodNode = currentNode as MethodNode;

                                if (methodNode != null)
                                {
                                    currentNodeExecutable = methodNode.UserExecutable;
                                }

                                jsonTree.Add(new
                                {
                                    id = jstreeNode,
                                    text = currentNode.DisplayName.ToString() + " (ns=" + session.NamespaceUris.ToArray()[currentNode.NodeId.NamespaceIndex] + ";" + currentNode.NodeId.ToString() + ")",
                                    nodeClass = currentNode.NodeClass.ToString(),
                                    accessLevel = currentNodeAccessLevel.ToString(),
                                    eventNotifier = currentNodeEventNotifier.ToString(),
                                    executable = currentNodeExecutable.ToString(),
                                    children = false
                                });
                            }
                        }
                    }

                    stopwatch.Stop();
                    Trace.TraceInformation("Browsing all child infos of node '{0}' took {0} ms", node, stopwatch.ElapsedMilliseconds);

                    return Json(jsonTree);
                }
                catch (Exception ex)
                {
                    OpcSessionHelper.Instance.Disconnect(HttpContext.Session.Id);
                    if (lastRetry)
                    {
                        return Content(CreateOpcExceptionActionString(ex));
                    }
                    lastRetry = true;
                }
            }
        }

        [HttpPost]
        public async Task<ActionResult> VariableRead(string jstreeNode)
        {
            var node = OpcSessionHelper.GetNodeIdFromJsTreeNode(jstreeNode);
            bool lastRetry = false;
            while (true)
            {
                try
                {
                    DataValueCollection values = null;
                    DiagnosticInfoCollection diagnosticInfos = null;
                    ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
                    ReadValueId valueId = new ReadValueId();
                    valueId.NodeId = new NodeId(node);
                    valueId.AttributeId = Attributes.Value;
                    valueId.IndexRange = null;
                    valueId.DataEncoding = null;
                    nodesToRead.Add(valueId);

                    UpdateStatus($"Read OPC UA node: {valueId.NodeId}");

                    Session session = await OpcSessionHelper.Instance.GetSessionAsync(_application.ApplicationConfiguration, HttpContext.Session.Id, HttpContext.Session.GetString("EndpointUrl")).ConfigureAwait(false);
                    ResponseHeader responseHeader = session.Read(null, 0, TimestampsToReturn.Both, nodesToRead, out values, out diagnosticInfos);
                    string value = "";
                    string actionResult;
                    if (values.Count > 0 && values[0].Value != null)
                    {
                        if (values[0].WrappedValue.ToString().Length > 40)
                        {
                            value = values[0].WrappedValue.ToString().Substring(0, 40);
                            value += "...";
                        }
                        else
                        {
                            value = values[0].WrappedValue.ToString();
                        }

                        actionResult = $"{{ \"value\": \"{value}\", \"status\": \"{values[0].StatusCode}\", \"sourceTimestamp\": \"{values[0].SourceTimestamp}\", \"serverTimestamp\": \"{values[0].ServerTimestamp}\" }}";
                    }
                    else
                    {
                        actionResult = string.Empty;
                    }


                    return Content(actionResult);
                }
                catch (Exception ex)
                {
                    OpcSessionHelper.Instance.Disconnect(HttpContext.Session.Id);
                    if (lastRetry)
                    {
                        return Content(CreateOpcExceptionActionString(ex));
                    }
                    lastRetry = true;
                }
            }
        }

        [HttpPost]
        public async Task<ActionResult> VariableWrite(string jstreeNode, string newValue)
        {
            Session session = await OpcSessionHelper.Instance.GetSessionAsync(_application.ApplicationConfiguration, HttpContext.Session.Id, HttpContext.Session.GetString("EndpointUrl")).ConfigureAwait(false);

            bool lastRetry = false;
            while (true)
            {
                try
                {
                    string actionResult = string.Empty;
                    var callResult = OpcSessionHelper.Instance.WriteOpcNode(jstreeNode, newValue, session);

                    if (callResult.result != null && callResult.result.Count > 0 && Opc.Ua.StatusCode.IsGood(callResult.result[0]))
                    {
                        actionResult = $"{{ \"id\": \"{jstreeNode}\", \"status\": \"ok\"}}";
                        UpdateStatus($"Updated value to {newValue}");
                    }
                    else
                    {
                        actionResult = $"{{ \"id\": \"{jstreeNode}\", \"status\": \"error\", \"StatusCode\": \"{callResult.result[0]}\", \"diagnosticInfo\": \"{callResult.diagInfo}\" }}";

                        string errorMessage = $"Error writing value of opc variable";
                        if (callResult.result != null && callResult.result.Count > 0)
                        {
                            errorMessage += $", StatusCode: {callResult.result[0]} ";
                        }
                        if(callResult.diagInfo != null)
                        {
                            foreach (var info in callResult.diagInfo)
                            {
                                errorMessage += " " + info.ToString();
                            }
                        }
                        UpdateStatus(errorMessage);
                    }

                    return Content(actionResult);
                }
                catch (Exception ex)
                {
                    OpcSessionHelper.Instance.Disconnect(HttpContext.Session.Id);
                    if (lastRetry)
                    {
                        return Content(CreateOpcExceptionActionString(ex));
                    }
                    lastRetry = true;
                }
            }
        }

        [HttpPost]
        public async Task<ActionResult> MethodCallGetParameter(string jstreeNode)
        {
            var node = OpcSessionHelper.GetNodeIdFromJsTreeNode(jstreeNode);

            var jsonParameter = new List<object>();
            int parameterCount = 0;
            bool lastRetry = false;
            while (true)
            {
                try
                {
                    QualifiedName browseName = null;
                    browseName = Opc.Ua.BrowseNames.InputArguments;

                    ReferenceDescriptionCollection references = null;
                    Byte[] continuationPoint;

                    Session session = await OpcSessionHelper.Instance.GetSessionAsync(_application.ApplicationConfiguration, HttpContext.Session.Id, HttpContext.Session.GetString("EndpointUrl")).ConfigureAwait(false);
                    session.Browse(
                            null,
                            null,
                            node,
                            1u,
                            BrowseDirection.Forward,
                            ReferenceTypeIds.HasProperty,
                            true,
                            0,
                            out continuationPoint,
                            out references);
                    if (references.Count == 1)
                    {
                        var nodeReference = references[0];
                        VariableNode argumentsNode = session.ReadNode(ExpandedNodeId.ToNodeId(nodeReference.NodeId, session.NamespaceUris)) as VariableNode;
                        DataValue value = session.ReadValue(argumentsNode.NodeId);

                        ExtensionObject[] argumentsList = value.Value as ExtensionObject[];
                        for (int ii = 0; ii < argumentsList.Length; ii++)
                        {
                            Argument argument = (Argument)argumentsList[ii].Body;
                            NodeId nodeId = new NodeId(argument.DataType);
                            Node dataTypeIdNode = session.ReadNode(nodeId);
                            jsonParameter.Add(new { name = argument.Name, value = argument.Value, valuerank = argument.ValueRank, arraydimentions = argument.ArrayDimensions, description = argument.Description.Text, datatype = nodeId.Identifier, typename = dataTypeIdNode.DisplayName.Text });
                        }
                        parameterCount = argumentsList.Length;
                    }
                    else
                    {
                        parameterCount = 0;

                    }

                    UpdateStatus($"Loaded input arguments descrption, count: {parameterCount}");

                    return Json(new { count = parameterCount, parameter = jsonParameter });
                }
                catch (Exception ex)
                {
                    OpcSessionHelper.Instance.Disconnect(HttpContext.Session.Id);
                    if (lastRetry)
                    {
                        return Content(CreateOpcExceptionActionString(ex));
                    }
                    lastRetry = true;
                }
            }
        }

        [HttpPost]
        public async Task<ActionResult> MethodCall(string jstreeNode, string parameterData, string parameterValues)
        {
            string[] delimiter = { "__$__" };
            string[] jstreeNodeSplit = jstreeNode.Split(delimiter, 3, StringSplitOptions.None);
            string node;
            string parentNode = null;

            if (jstreeNodeSplit.Length == 1)
            {
                node = jstreeNodeSplit[0];
                parentNode = null;
            }
            else
            {
                node = jstreeNodeSplit[1];
                parentNode = (jstreeNodeSplit[0].Replace(delimiter[0], "")).Replace("__", "");
            }

            List<MethodCallParameterData> originalData = string.IsNullOrWhiteSpace(parameterData)
                ? new List<MethodCallParameterData>()
                : JsonConvert.DeserializeObject<List<MethodCallParameterData>>(parameterData);

            List<Variant> values = JsonConvert.DeserializeObject<List<Variant>>(parameterValues);
            int count = values.Count;
            VariantCollection inputArguments = new VariantCollection();

            bool lastRetry = false;
            while (true)
            {
                try
                {
                    var session = await OpcSessionHelper.Instance.GetSessionAsync(_application.ApplicationConfiguration, HttpContext.Session.Id, HttpContext.Session.GetString("EndpointUrl")).ConfigureAwait(false);


                    for (int i = 0; i < count; i++)
                    {
                        Variant value = new Variant();
                        NodeId dataTypeNodeId = "i=" + originalData[i].Datatype;
                        string dataTypeName = originalData[i].TypeName;
                        Int32 valueRank = Convert.ToInt32(originalData[i].ValueRank, CultureInfo.InvariantCulture);
                        string newValue = values[i].Value.ToString();

                        try
                        {
                            OpcSessionHelper.Instance.BuildDataValue(ref session, ref value, dataTypeNodeId, valueRank, newValue);
                            inputArguments.Add(value);
                        }
                        catch (Exception ex)
                        {
                            UpdateStatus("Error convering input arguments of method: " + ex.Message);

                            var diagCollection = new DiagnosticInfoCollection();
                            diagCollection.Add(new DiagnosticInfo(ex, DiagnosticsMasks.All, false, new StringTable()));
                            var errorResult = new
                            {
                                status = "error",
                                statusCode = Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError,
                                numberOfDiagnosticInfo = diagCollection.Count,
                                diagnosticInfo = diagCollection
                            };
                            return Json(errorResult);
                        }
                    }

                    CallMethodRequestCollection requests = new CallMethodRequestCollection();
                    CallMethodResultCollection results;
                    DiagnosticInfoCollection diagnosticInfos = null;
                    CallMethodRequest request = new CallMethodRequest();
                    request.ObjectId = new NodeId(parentNode);
                    request.MethodId = new NodeId(node);
                    request.InputArguments = inputArguments;
                    requests.Add(request);
                    ResponseHeader responseHeader = session.Call(null, requests, out results, out diagnosticInfos);
                    if (Opc.Ua.StatusCode.IsBad(results[0].StatusCode))
                    {
                        UpdateStatus($"Error calling method, StatusCode: {results[0].StatusCode}");

                        var errorResult = new
                        {
                            status = "error",
                            statusCode = results[0].StatusCode,
                            numberOfDiagnosticInfo = diagnosticInfos.Count,
                            diagnosticInfo = diagnosticInfos
                        };
                        return Json(errorResult);
                    }
                    else
                    {
                        UpdateStatus("Method Call Succeeded");

                        var successResult = new {
                            status = "ok",
                            numberOfOutputArguments = results[0].OutputArguments.Count,
                            outputArguments = results[0].OutputArguments
                        };

                        return Json(successResult);
                    }
                }
                catch (Exception ex)
                {
                    OpcSessionHelper.Instance.Disconnect(HttpContext.Session.Id);
                    if (lastRetry)
                    {
                        return Content(CreateOpcExceptionActionString(ex));
                    }
                    lastRetry = true;
                }
            }
        }

        /// <summary>
        /// Writes an error message to the trace and generates an HTML encoded string to be sent to the client in case of an error.
        /// </summary>
        private string CreateOpcExceptionActionString(Exception ex)
        {
            Trace.TraceError(ex.Message);

            string actionResult = HttpUtility.HtmlEncode(ex.Message);
            Response.StatusCode = 1;
            return actionResult;
        }

        /// <summary>
        /// Sends the message to all connected clients as status indication
        /// </summary>
        /// <param name="message">Text to show on web page</param>
        private void UpdateStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(nameof(message));
            }

            _hubContext.Clients.All.SendAsync("addNewMessageToPage", HttpContext?.Session.Id, message).Wait();
        }
    }
}
