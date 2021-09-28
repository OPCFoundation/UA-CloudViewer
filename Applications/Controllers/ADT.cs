
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Net.Mime;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using UANodesetWebViewer.Models;

namespace UANodesetWebViewer.Controllers
{
    public class ADT : Controller
    {
        public ActionResult Index()
        {
            ADTModel adtModel = new ADTModel
            {
                StatusMessage = ""
            };

            return View("Index", adtModel);
        }

        [HttpPost]
        public ActionResult Upload(string instanceUrl, string tenantId, string clientId, string secret)
        {
            ADTModel adtModel = new ADTModel
            {
                InstanceUrl = instanceUrl,
                TenantId = tenantId,
                ClientId = clientId,
                Secret = secret
            };

            try
            {
                // login to Azure using the Environment variables mechanism of DefaultAzureCredential
                Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", clientId);
                Environment.SetEnvironmentVariable("AZURE_TENANT_ID", tenantId);
                Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", secret);

                // create ADT client instance
                DigitalTwinsClient client = new DigitalTwinsClient(new Uri(instanceUrl), new DefaultAzureCredential(new DefaultAzureCredentialOptions{ ExcludeVisualStudioCodeCredential = true }));

                // read generated DTDL models
                List<string> dtdlModels = new List<string>();
                foreach (string dtdlFilePath in Directory.EnumerateFiles(Path.Combine(Directory.GetCurrentDirectory(),"JSON"), "*.dtdl.json"))
                {
                    dtdlModels.Add(System.IO.File.ReadAllText(dtdlFilePath));
                }

                // upload
                Azure.Response<DigitalTwinsModelData[]> response = client.CreateModelsAsync(dtdlModels).GetAwaiter().GetResult();

                // clear secret
                Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", "");

                if (response.GetRawResponse().Status == 201)
                {
                    adtModel.StatusMessage = "Upload successful!";
                    return View("Index", adtModel);
                }
                else
                {
                    adtModel.StatusMessage = response.GetRawResponse().ReasonPhrase;
                    return View("Error", adtModel);
                }
            }
            catch (Exception ex)
            {
                // clear secret
                Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", "");

                adtModel.StatusMessage = ex.Message;
                return View("Error", adtModel);
            }
        }

        public ActionResult GenerateAAS()
        {
            try
            {
                string packagePath = Path.Combine(Directory.GetCurrentDirectory(), "DTDL.aasx");
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

                        foreach (string filenamePath in Directory.EnumerateFiles(Path.Combine(Directory.GetCurrentDirectory(), "JSON"), "*.dtdl.json"))
                        {
                            string submodelPath = Path.Combine(Directory.GetCurrentDirectory(), "submodel.adt.xml");
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
                                    Text = "http://www.microsoft.com/type/dtdl/" + Path.GetFileNameWithoutExtension(filenamePath).Replace(".", "").ToLower()
                                };

                                aasEnv.AssetAdministrationShells.AssetAdministrationShell.SubmodelRefs.Add(nodesetReference);

                                aasSubModel.Identification.Text += Path.GetFileNameWithoutExtension(filenamePath).Replace(".", "").ToLower();
                                aasSubModel.SubmodelElements.SubmodelElement.SubmodelElementCollection.Value.SubmodelElement.File.Value =
                                    aasSubModel.SubmodelElements.SubmodelElement.SubmodelElementCollection.Value.SubmodelElement.File.Value.Replace("TOBEREPLACED", Path.GetFileNameWithoutExtension(filenamePath));
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

                    // add DTDL files
                    foreach (string filenamePath in Directory.EnumerateFiles(Path.Combine(Directory.GetCurrentDirectory(), "JSON"), "*.dtdl.json"))
                    {
                        PackagePart supplementalDoc = package.CreatePart(new Uri("/aasx/" + Path.GetFileNameWithoutExtension(filenamePath), UriKind.Relative), MediaTypeNames.Application.Json);
                        using (FileStream fileStream = new FileStream(filenamePath, FileMode.Open, FileAccess.Read))
                        {
                            CopyStream(fileStream, supplementalDoc.GetStream());
                        }
                        package.CreateRelationship(supplementalDoc.Uri, TargetMode.Internal, "http://www.admin-shell.io/aasx/relationships/aas-suppl");
                    }
                }

                return File(new FileStream(Path.Combine(Directory.GetCurrentDirectory(), "DTDL.aasx"), FileMode.Open, FileAccess.Read), "APPLICATION/octet-stream", "DTDL.aasx");
            }
            catch (Exception ex)
            {
                ADTModel model = new ADTModel
                {
                    StatusMessage = HttpUtility.HtmlDecode(ex.Message)
                };

                return View("Error", model);
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
    }
}
