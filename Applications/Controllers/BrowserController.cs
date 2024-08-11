using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
using UANodesetWebViewer.Models;

namespace UANodesetWebViewer.Controllers
{
    public class BrowserController : Controller
    {
        public static List<string> _nodeSetFilenames = new List<string>();

        private static HttpClient _client = new HttpClient();

        private static Dictionary<string, string> _namespacesInCloudLibrary = new Dictionary<string, string>();

        private static Dictionary<string, string> _namesInCloudLibrary = new Dictionary<string, string>();

        private readonly OpcSessionHelper _helper;
        private readonly ApplicationInstance _application;

        public BrowserController(OpcSessionHelper helper, ApplicationInstance app)
        {
            _helper = helper;
            _application = app;
        }

        public ActionResult Index()
        {
            OpcSessionModel sessionModel = new OpcSessionModel
            {
                SessionId = HttpContext.Session.Id,
                NodesetIDs = new SelectList(new List<string>())
            };

            OpcSessionCacheData entry = null;
            if (_helper.OpcSessionCache.TryGetValue(HttpContext.Session.Id, out entry))
            {
                sessionModel.EndpointUrl = "opc.tcp://localhost";

                HttpContext.Session.SetString("EndpointUrl", entry.EndpointURL);

                return View("Browse", sessionModel);
            }

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

                    // add package spec part
                    PackagePart spec = package.CreatePart(new Uri("/aasx/" + _nodeSetFilenames[0], UriKind.Relative), MediaTypeNames.Text.Xml);
                    string submodelPath = Path.Combine(Directory.GetCurrentDirectory(), _nodeSetFilenames[0]);
                    using (FileStream reader2 = new(submodelPath,FileMode.Open))
                    {
                        CopyStream(reader2, spec.GetStream());
                    }

                    origin.CreateRelationship(spec.Uri, TargetMode.Internal, "http://www.admin-shell.io/aasx/relationships/aas-spec");
                }

                return File(new FileStream(Path.Combine(Directory.GetCurrentDirectory(), "UANodeSet.aasx"), FileMode.Open, FileAccess.Read), "APPLICATION/octet-stream", "UANodeSet.aasx");
            }
            catch (Exception ex)
            {
                OpcSessionModel sessionModel = new OpcSessionModel
                {
                    StatusMessage = HttpUtility.HtmlDecode(ex.Message)
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
                StatusMessage = HttpUtility.HtmlDecode(errorMessage),
                NodesetIDs = new SelectList(new List<string>())
            };

            return View("Error", sessionModel);
        }

        public async Task<ActionResult> CloudLibrayFileOpen(string nodesetfile)
        {
            OpcSessionModel sessionModel = new OpcSessionModel
            {
                EndpointUrl = "opc.tcp://localhost"
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
                sessionModel.StatusMessage = error;
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
                EndpointUrl = "opc.tcp://localhost"
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
                    sessionModel.StatusMessage = error;
                    return View("Error", sessionModel);
                }

                await StartClientAndServer(sessionModel).ConfigureAwait(false);

                return View("Browse", sessionModel);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);

                sessionModel.StatusMessage = ex.Message;

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
            string endpointURL = "opc.tcp://localhost";

            session = await _helper.GetSessionAsync(HttpContext.Session.Id, endpointURL).ConfigureAwait(false);

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
                _helper.Disconnect(HttpContext.Session.Id);
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

            OpcSessionModel sessionModel = new OpcSessionModel
            {
                SessionId = HttpContext.Session.Id,
                NodesetIDs = new SelectList(new List<string>())
            };

            return View("Index", sessionModel);
        }
    }
}
