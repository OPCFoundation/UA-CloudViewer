using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Xml;
using UACloudLibrary;
using UANodesetWebViewer.Models;

namespace UANodesetWebViewer.Controllers
{
    public class UACL : Controller
    {
        public ActionResult Index()
        {
            UACLModel uaclModel = new UACLModel
            {
                StatusMessage = ""
            };

            return View("Index", uaclModel);
        }

        [HttpPost]
        public ActionResult Upload(
            string instanceUrl,
            string clientId,
            string secret,
            string nodesettitle,
            string version,
            string license,
            string copyright,
            string description,
            string addressspacename,
            string addressspacedescription,
            string addressspaceiconurl,
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
                Secret = secret
            };

            try
            {
                if (BrowserController._nodeSetFilename.Count < 1)
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

                AddressSpace uaAddressSpace = new AddressSpace();

                switch (license)
                {
                    case "MIT": uaAddressSpace.License = AddressSpaceLicense.MIT;
                    break;
                    case "ApacheLicense20": uaAddressSpace.License = AddressSpaceLicense.ApacheLicense20;
                    break;
                    case "Custom": uaAddressSpace.License = AddressSpaceLicense.Custom;
                    break;
                    default: throw new ArgumentException("Invalid license entered!");
                }

                if (!string.IsNullOrWhiteSpace(nodesettitle))
                {
                    uaAddressSpace.Title = nodesettitle;
                }
                else
                {
                    throw new ArgumentException("Invalid nodeset title entered!");
                }

                uaAddressSpace.Version = new Version(version).ToString();
                if (!string.IsNullOrWhiteSpace(copyright))
                {
                    uaAddressSpace.CopyrightText = copyright;
                }
                else
                {
                    throw new ArgumentException("Invalid copyright text entered!");
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    uaAddressSpace.Description = description;
                }
                else
                {
                    throw new ArgumentException("Invalid description entered!");
                }

                if (!string.IsNullOrWhiteSpace(addressspacename))
                {
                    uaAddressSpace.Category.Name = addressspacename;
                }
                else
                {
                    throw new ArgumentException("Invalid address space name entered!");
                }

                if (!string.IsNullOrWhiteSpace(addressspacedescription))
                {
                    uaAddressSpace.Category.Description = addressspacedescription;
                }

                if (!string.IsNullOrWhiteSpace(addressspaceiconurl))
                {
                    uaAddressSpace.Category.IconUrl = new Uri(addressspaceiconurl);
                }

                if (!string.IsNullOrWhiteSpace(documentationurl))
                {
                    uaAddressSpace.DocumentationUrl = new Uri(documentationurl);
                }

                if (!string.IsNullOrWhiteSpace(iconurl))
                {
                    uaAddressSpace.IconUrl = new Uri(iconurl);
                }

                if (!string.IsNullOrWhiteSpace(licenseurl))
                {
                    uaAddressSpace.LicenseUrl = new Uri(licenseurl);
                }

                if (!string.IsNullOrWhiteSpace(keywords))
                {
                    uaAddressSpace.Keywords = keywords.Split(',');
                }

                if (!string.IsNullOrWhiteSpace(purchasinginfo))
                {
                    uaAddressSpace.PurchasingInformationUrl = new Uri(purchasinginfo);
                }

                if (!string.IsNullOrWhiteSpace(releasenotes))
                {
                    uaAddressSpace.ReleaseNotesUrl = new Uri(releasenotes);
                }

                if (!string.IsNullOrWhiteSpace(testspecification))
                {
                    uaAddressSpace.TestSpecificationUrl = new Uri(testspecification);
                }

                if (!string.IsNullOrWhiteSpace(locales))
                {
                    uaAddressSpace.SupportedLocales = locales.Split(',');
                }

                if (!string.IsNullOrWhiteSpace(orgname))
                {
                    uaAddressSpace.Contributor.Name = orgname;
                }
                else
                {
                    throw new ArgumentException("Invalid organisation name entered!");
                }

                if (!string.IsNullOrWhiteSpace(orgdescription))
                {
                    uaAddressSpace.Contributor.Description = orgdescription;
                }

                if (!string.IsNullOrWhiteSpace(orglogo))
                {
                    uaAddressSpace.Contributor.LogoUrl = new Uri(orglogo);
                }

                if (!string.IsNullOrWhiteSpace(orgcontact))
                {
                    uaAddressSpace.Contributor.ContactEmail = new MailAddress(orgcontact).Address;
                }

                if (!string.IsNullOrWhiteSpace(orgwebsite))
                {
                    uaAddressSpace.Contributor.Website = new Uri(orgwebsite);
                }

                string nodesetFileName = BrowserController._nodeSetFilename[BrowserController._nodeSetFilename.Count - 1];
                uaAddressSpace.Nodeset.NodesetXml = System.IO.File.ReadAllText(nodesetFileName);

                instanceUrl = instanceUrl.Trim();
                if (!instanceUrl.EndsWith('/'))
                {
                    instanceUrl += '/';
                }
                WebClient webClient = new WebClient
                {
                    BaseAddress = instanceUrl
                };

                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secret)));

                string address = webClient.BaseAddress + "InfoModel/upload";
                if (overwrite)
                {
                    address += "?overwrite=true";
                }

                string body = JsonConvert.SerializeObject(uaAddressSpace);
                string response = webClient.UploadString(address, "PUT", body);
                webClient.Dispose();

                uaclModel.StatusMessage = response;
                return View("Index", uaclModel);
            }
            catch (Exception ex)
            {
                if ((ex is WebException) && (((WebException)ex).Response != null))
                {
                    uaclModel.StatusMessage = new StreamReader(((WebException)ex).Response.GetResponseStream()).ReadToEnd();
                    return View("Index", uaclModel);
                }
                uaclModel.StatusMessage = ex.Message;
                return View("Index", uaclModel);
            }
        }
    }
}
