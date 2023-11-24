using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Cloud.Library.Models;
using Opc.Ua.Configuration;
using Opc.Ua.Export;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
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

        private static HttpClient _client = new HttpClient();

        private static Dictionary<string, string> _namespacesInCloudLibrary = new Dictionary<string, string>();

        private static Dictionary<string, string> _namesInCloudLibrary = new Dictionary<string, string>();

        private static ApplicationInstance _application = new ApplicationInstance();

        public BrowserController(IHubContext<StatusHub> hubContext)
        {
             _hubContext = hubContext;
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
            OpcSessionModel sessionModel = new OpcSessionModel
            {
                SessionId = HttpContext.Session.Id,
                NodesetIDs = new SelectList(new List<string>())
            };

            OpcSessionCacheData entry = null;
            if (OpcSessionHelper.Instance.OpcSessionCache.TryGetValue(HttpContext.Session.Id, out entry))
            {
                sessionModel.ServerIP = entry.EndpointURL.Host;
                sessionModel.ServerPort = entry.EndpointURL.Port.ToString();

                HttpContext.Session.SetString("EndpointUrl", entry.EndpointURL.AbsoluteUri);

                return View("Browse", sessionModel);
            }

            UpdateStatus("Additional Information Required");
            return View("Index", sessionModel);
        }

        [HttpPost]
        public ActionResult Login(string instanceUrl, string clientId, string secret)
        {
            OpcSessionModel sessionModel = new OpcSessionModel
            {
                SessionId = HttpContext.Session.Id,
                NodesetIDs = new SelectList(new List<string>())
            };

            _client.DefaultRequestHeaders.Remove("Authorization");
            _client.DefaultRequestHeaders.Add("Authorization", "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secret)));

            if (!instanceUrl.EndsWith('/'))
            {
                instanceUrl += '/';
            }
            _client.BaseAddress = new Uri(instanceUrl);

            // get namespaces
            string address = instanceUrl + "infomodel/namespaces";
            HttpResponseMessage response = _client.Send(new HttpRequestMessage(HttpMethod.Get, address));
            string[] identifiers = JsonConvert.DeserializeObject<string[]>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

            _namespacesInCloudLibrary.Clear();
            foreach (string nodeset in identifiers)
            {
                string[] tuple = nodeset.Split(",");
                _namespacesInCloudLibrary.Add(tuple[1], tuple[0]);
            }

            // get names
            address = instanceUrl + "infomodel/names";
            response = _client.Send(new HttpRequestMessage(HttpMethod.Get, address));
            string[] names = JsonConvert.DeserializeObject<string[]>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

            List<string> sortedNames = new List<string>(names);
            sortedNames.Sort();

            _namesInCloudLibrary.Clear();
            foreach (string name in sortedNames)
            {
                string[] tuple = name.Split(",");
                _namesInCloudLibrary.Add(tuple[1], tuple[0]);
            }

            sessionModel.NodesetIDs = new SelectList(_namesInCloudLibrary.Values);

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
                                    Text = "http://www.opcfoundation.org/type/opcua/" + Path.GetFileName(filename).Replace(".", "").ToLower()
                            };

                                aasEnv.AssetAdministrationShells.AssetAdministrationShell.SubmodelRefs.Add(nodesetReference);

                                aasSubModel.Identification.Text += Path.GetFileName(filename).Replace(".", "").ToLower();
                                aasSubModel.SubmodelElements.SubmodelElement.SubmodelElementCollection.Value.SubmodelElement.File.Value =
                                    aasSubModel.SubmodelElements.SubmodelElement.SubmodelElementCollection.Value.SubmodelElement.File.Value.Replace("TOBEREPLACED", Path.GetFileName(filename));
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
                        PackagePart supplementalDoc = package.CreatePart(new Uri("/aasx/" + Path.GetFileName(_nodeSetFilenames[i]), UriKind.Relative), MediaTypeNames.Text.Xml);
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
                ErrorMessage = HttpUtility.HtmlDecode(errorMessage),
                NodesetIDs = new SelectList(new List<string>())
            };

            UpdateStatus($"Error Occured: {sessionModel.ErrorMessage}");

            return View("Error", sessionModel);
        }

        public async Task<ActionResult> CloudLibrayFileOpen(string nodesetfile)
        {
            OpcSessionModel sessionModel = new OpcSessionModel
            {
                ServerIP = "localhost",
                ServerPort = "4840",
            };

            string address = _client.BaseAddress + "infomodel/download/";
            foreach (KeyValuePair<string, string> ns in _namesInCloudLibrary)
            {
                if (ns.Value == nodesetfile)
                {
                    address += Uri.EscapeDataString(ns.Key);
                    break;
                }
            }

            HttpResponseMessage response = _client.Send(new HttpRequestMessage(HttpMethod.Get, address));
            UANameSpace nameSpace = JsonConvert.DeserializeObject<UANameSpace>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

            // store the file on the webserver
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "NodeSets", "nodeset2.xml");
            System.IO.File.WriteAllText(filePath, nameSpace.Nodeset.NodesetXml);
            _nodeSetFilenames.Add(filePath);

            string error = ValidateNamespacesAndModels(true);
            if (!string.IsNullOrEmpty(error))
            {
                sessionModel.ErrorMessage = error;
                return View("Error", sessionModel);
            }

            await StartClientAndServer(sessionModel).ConfigureAwait(false);

            return View("Browse", sessionModel);
        }

        [HttpPost]
        public async Task<ActionResult> LocalFileOpen(IFormFile[] files, bool autodownloadreferences)
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

                string error = ValidateNamespacesAndModels(autodownloadreferences);
                if (!string.IsNullOrEmpty(error))
                {
                    sessionModel.ErrorMessage = error;
                    return View("Error", sessionModel);
                }

                await StartClientAndServer(sessionModel).ConfigureAwait(false);

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

        private async Task StartClientAndServer(OpcSessionModel sessionModel)
        {
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
        }

        private string ValidateNamespacesAndModels(bool autodownloadreferences)
        {
            // Collect all models as well as all required/referenced model namespace URIs listed in each file
            List<string> models = new List<string>();
            List<string> modelreferences = new List<string>();
            foreach (string nodesetFile in _nodeSetFilenames)
            {
                // workaround for bug https://github.com/dotnet/runtime/issues/67622
                System.IO.File.WriteAllText(nodesetFile, System.IO.File.ReadAllText(nodesetFile).Replace("<Value/>", "<Value xsi:nil='true' />"));

                using (Stream stream = new FileStream(nodesetFile, FileMode.Open))
                {
                    UANodeSet nodeSet = UANodeSet.Read(stream);

                    // validate namespace URIs
                    if ((nodeSet.NamespaceUris != null) && (nodeSet.NamespaceUris.Length > 0))
                    {
                        foreach (string ns in nodeSet.NamespaceUris)
                        {
                            if (string.IsNullOrEmpty(ns) || !Uri.IsWellFormedUriString(ns, UriKind.Absolute))
                            {
                                return "Nodeset file " + nodesetFile + " contains an invalid Namespace URI: \"" + ns + "\"";
                            }
                        }
                    }
                    else
                    {
                        return "'NamespaceUris' entry missing in " + nodesetFile + ". Please add it!";
                    }

                    // validate model URIs
                    if ((nodeSet.Models != null) && (nodeSet.Models.Length > 0))
                    {
                        foreach (ModelTableEntry model in nodeSet.Models)
                        {
                            if (model != null)
                            {
                                if (Uri.IsWellFormedUriString(model.ModelUri, UriKind.Absolute))
                                {
                                    // ignore the default namespace which is always present and don't add duplicates
                                    if ((model.ModelUri != "http://opcfoundation.org/UA/") && !models.Contains(model.ModelUri))
                                    {
                                        models.Add(model.ModelUri);
                                    }
                                }
                                else
                                {
                                    return "Nodeset file " + nodesetFile + " contains an invalid Model Namespace URI: \"" + model.ModelUri + "\"";
                                }

                                if ((model.RequiredModel != null) && (model.RequiredModel.Length > 0))
                                {
                                    foreach (ModelTableEntry requiredModel in model.RequiredModel)
                                    {
                                        if (requiredModel != null)
                                        {
                                            if (Uri.IsWellFormedUriString(requiredModel.ModelUri, UriKind.Absolute))
                                            {
                                                // ignore the default namespace which is always required and don't add duplicates
                                                if ((requiredModel.ModelUri != "http://opcfoundation.org/UA/") && !modelreferences.Contains(requiredModel.ModelUri))
                                                {
                                                    modelreferences.Add(requiredModel.ModelUri);
                                                }
                                            }
                                            else
                                            {
                                                return "Nodeset file " + nodesetFile + " contains an invalid referenced Model Namespace URI: \"" + requiredModel.ModelUri + "\"";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        return "'Model' entry missing in " + nodesetFile + ". Please add it!";
                    }
                }
            }

            // now check if we have all references for each model we want to load
            foreach (string modelreference in modelreferences)
            {
                if (!models.Contains(modelreference))
                {
                    if (!autodownloadreferences)
                    {
                        return "Referenced OPC UA model " + modelreference + " is missing from selected list of nodeset files, please add the corresponding nodeset file to the list of loaded files!";
                    }
                    else
                    {
                        try
                        {
                            // try to auto-download the missing references from the UA Cloud Library
                            string address = _client.BaseAddress + "infomodel/download/";
                            foreach(KeyValuePair<string, string> ns in _namespacesInCloudLibrary)
                            {
                                if (ns.Value == modelreference)
                                {
                                    address += Uri.EscapeDataString(ns.Key);
                                    break;
                                }
                            }

                            HttpResponseMessage response = _client.Send(new HttpRequestMessage(HttpMethod.Get, address));
                            UANameSpace nameSpace = JsonConvert.DeserializeObject<UANameSpace>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

                            // store the file on the webserver
                            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "NodeSets", nameSpace.Category.Name + ".nodeset2.xml");
                            System.IO.File.WriteAllText(filePath, nameSpace.Nodeset.NodesetXml);
                            _nodeSetFilenames.Add(filePath);
                        }
                        catch (Exception ex)
                        {
                            return "Could not download referenced nodeset " + modelreference + ": " + ex.Message;
                        }
                    }
                }
            }

            return string.Empty; // no error
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
            config.CertificateValidator.Update(config.SecurityConfiguration).GetAwaiter().GetResult();

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

            if (_application.Server != null)
            {
                _application.Stop();
            }

            UpdateStatus("Disconnected");

            OpcSessionModel sessionModel = new OpcSessionModel
            {
                SessionId = HttpContext.Session.Id,
                NodesetIDs = new SelectList(new List<string>())
            };

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

                    string actionResult;
                    if (values.Count > 0)
                    {
                        actionResult = $"{{ \"value\": \"{values[0]}\", \"status\": \"{values[0].StatusCode}\", \"sourceTimestamp\": \"{values[0].SourceTimestamp}\", \"serverTimestamp\": \"{values[0].ServerTimestamp}\" }}";
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
