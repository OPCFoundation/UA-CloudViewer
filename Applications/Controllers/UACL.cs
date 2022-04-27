using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using UACloudLibrary.Models;
using UANodesetWebViewer.Models;

namespace UANodesetWebViewer.Controllers
{
    public class UACL : Controller
    {
        public ActionResult Index()
        {
            UACLModel uaclModel = new UACLModel
            {
                StatusMessage = "",
                InstanceUrl = "https://uacloudlibrary.opcfoundation.org"
            };

            if (BrowserController._nodeSetFilenames.Count > 0)
            {
                ViewBag.Nodesetfile = new SelectList(BrowserController._nodeSetFilenames, BrowserController._nodeSetFilenames[0]);
            }
            else
            {
                ViewBag.Nodesetfile = new SelectList(BrowserController._nodeSetFilenames);
            }

            return View("Index", uaclModel);
        }

        [HttpPost]
        public ActionResult Upload(
            string instanceUrl,
            string clientId,
            string secret,
            string nodesettitle,
            string nodesetfile,
            string license,
            string copyright,
            string description,
            string namespacename,
            string namespacedescription,
            string namespaceiconurl,
            string documentationurl,
            string iconurl,
            string licenseurl,
            string keywords,
            string purchasinginfo,
            string releasenotes,
            string testspecification,
            string locales,
            string orgname,
            string orgdescription,
            string orglogo,
            string orgcontact,
            string orgwebsite,
            bool overwrite)
        {
            UACLModel uaclModel = new UACLModel
            {
                InstanceUrl = instanceUrl,
                ClientId = clientId,
                Secret = secret,
            };

            try
            {
                if (BrowserController._nodeSetFilenames.Count < 1)
                {
                    throw new Exception("No nodeset file is currently loaded!");
                }

                // call the UA Cloud Library REST endpoint for info model upload
                if (string.IsNullOrWhiteSpace(instanceUrl) || !Uri.IsWellFormedUriString(instanceUrl.Trim(), UriKind.Absolute))
                {
                    throw new ArgumentException("Invalid UA Cloud Library instance Url entered!");
                }

                if (string.IsNullOrWhiteSpace(clientId))
                {
                    throw new ArgumentException("Invalid UA Cloud Library username entered!");
                }

                if (string.IsNullOrWhiteSpace(secret))
                {
                    throw new ArgumentException("Invalid UA Cloud Library password entered!");
                }

                UANameSpace nameSpace = new UANameSpace();

                switch (license)
                {
                    case "MIT": nameSpace.License = License.MIT;
                    break;
                    case "ApacheLicense20": nameSpace.License = License.ApacheLicense20;
                    break;
                    case "Custom": nameSpace.License = License.Custom;
                    break;
                    default: throw new ArgumentException("Invalid license entered!");
                }

                if (!string.IsNullOrWhiteSpace(nodesettitle))
                {
                    nameSpace.Title = nodesettitle;
                }
                else
                {
                    throw new ArgumentException("Invalid nodeset title entered!");
                }

                if (!string.IsNullOrWhiteSpace(copyright))
                {
                    nameSpace.CopyrightText = copyright;
                }
                else
                {
                    throw new ArgumentException("Invalid copyright text entered!");
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    nameSpace.Description = description;
                }
                else
                {
                    throw new ArgumentException("Invalid description entered!");
                }

                if (!string.IsNullOrWhiteSpace(namespacename))
                {
                    nameSpace.Category.Name = namespacename;
                }
                else
                {
                    throw new ArgumentException("Invalid address space name entered!");
                }

                if (!string.IsNullOrWhiteSpace(namespacedescription))
                {
                    nameSpace.Category.Description = namespacedescription;
                }

                if (!string.IsNullOrWhiteSpace(namespaceiconurl))
                {
                    nameSpace.Category.IconUrl = new Uri(namespaceiconurl);
                }

                if (!string.IsNullOrWhiteSpace(documentationurl))
                {
                    nameSpace.DocumentationUrl = new Uri(documentationurl);
                }

                if (!string.IsNullOrWhiteSpace(iconurl))
                {
                    nameSpace.IconUrl = new Uri(iconurl);
                }

                if (!string.IsNullOrWhiteSpace(licenseurl))
                {
                    nameSpace.LicenseUrl = new Uri(licenseurl);
                }

                if (!string.IsNullOrWhiteSpace(keywords))
                {
                    nameSpace.Keywords = keywords.Split(',');
                }

                if (!string.IsNullOrWhiteSpace(purchasinginfo))
                {
                    nameSpace.PurchasingInformationUrl = new Uri(purchasinginfo);
                }

                if (!string.IsNullOrWhiteSpace(releasenotes))
                {
                    nameSpace.ReleaseNotesUrl = new Uri(releasenotes);
                }

                if (!string.IsNullOrWhiteSpace(testspecification))
                {
                    nameSpace.TestSpecificationUrl = new Uri(testspecification);
                }

                if (!string.IsNullOrWhiteSpace(locales))
                {
                    nameSpace.SupportedLocales = locales.Split(',');
                }

                if (!string.IsNullOrWhiteSpace(orgname))
                {
                    nameSpace.Contributor.Name = orgname;
                }
                else
                {
                    throw new ArgumentException("Invalid organisation name entered!");
                }

                if (!string.IsNullOrWhiteSpace(orgdescription))
                {
                    nameSpace.Contributor.Description = orgdescription;
                }

                if (!string.IsNullOrWhiteSpace(orglogo))
                {
                    nameSpace.Contributor.LogoUrl = new Uri(orglogo);
                }

                if (!string.IsNullOrWhiteSpace(orgcontact))
                {
                    nameSpace.Contributor.ContactEmail = new MailAddress(orgcontact).Address;
                }

                if (!string.IsNullOrWhiteSpace(orgwebsite))
                {
                    nameSpace.Contributor.Website = new Uri(orgwebsite);
                }

                nameSpace.Nodeset.NodesetXml = System.IO.File.ReadAllText(nodesetfile);

                instanceUrl = instanceUrl.Trim();
                if (!instanceUrl.EndsWith('/'))
                {
                    instanceUrl += '/';
                }

                HttpClient webClient = new HttpClient
                {
                    BaseAddress = new Uri(instanceUrl)
                };

                webClient.DefaultRequestHeaders.Add("Authorization", "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secret)));

                string address = webClient.BaseAddress + "InfoModel/upload";
                if (overwrite)
                {
                    address += "?overwrite=true";
                }

                string body = JsonConvert.SerializeObject(nameSpace);
                HttpResponseMessage response = webClient.Send(new HttpRequestMessage(HttpMethod.Put, address) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
                webClient.Dispose();

                uaclModel.StatusMessage = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (BrowserController._nodeSetFilenames.Count > 0)
                {
                    ViewBag.Nodesetfile = new SelectList(BrowserController._nodeSetFilenames, BrowserController._nodeSetFilenames[0]);
                }
                else
                {
                    ViewBag.Nodesetfile = new SelectList(BrowserController._nodeSetFilenames);
                }
                return View("Index", uaclModel);
            }
            catch (Exception ex)
            {
                if ((ex is WebException) && (((WebException)ex).Response != null))
                {
                    uaclModel.StatusMessage = new StreamReader(((WebException)ex).Response.GetResponseStream()).ReadToEnd();
                }
                else
                {
                    uaclModel.StatusMessage = ex.Message;
                }

                if (BrowserController._nodeSetFilenames.Count > 0)
                {
                    ViewBag.Nodesetfile = new SelectList(BrowserController._nodeSetFilenames, BrowserController._nodeSetFilenames[0]);
                }
                else
                {
                    ViewBag.Nodesetfile = new SelectList(BrowserController._nodeSetFilenames);
                }
                return View("Index", uaclModel);
            }
        }
    }
}
