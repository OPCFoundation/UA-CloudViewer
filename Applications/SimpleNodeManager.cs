
using Opc.Ua;
using Opc.Ua.Export;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UANodesetWebViewer.Controllers;

namespace UANodesetWebViewer
{
    public class SimpleNodeManager : CustomNodeManager2
    {
        public SimpleNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        : base(server, configuration)
        {
            SystemContext.NodeIdFactory = this;

            List<string> namespaces = new List<string>();
            foreach (string nodesetFile in BrowserController._nodeSetFilenames)
            {
                using (Stream stream = new FileStream(nodesetFile, FileMode.Open))
                {
                    UANodeSet nodeSet = UANodeSet.Read(stream);
                    if ((nodeSet.NamespaceUris != null) && (nodeSet.NamespaceUris.Length > 0))
                    {
                        foreach (string ns in nodeSet.NamespaceUris)
                        {
                            if (!namespaces.Contains(ns))
                            {
                                namespaces.Add(ns);
                            }
                        }
                    }

                    DTDL.Generate(nodeSet);
                }
            }

            NamespaceUris = namespaces.ToArray();
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                IList<IReference> references = null;
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                }

                if (BrowserController._nodeSetFilenames.Count > 0)
                {
                    // we need as many passes as we have nodesetfiles to make sure all references can be resolved
                    for (int i = 0; i < BrowserController._nodeSetFilenames.Count; i++)
                    {
                        foreach (string nodesetFile in BrowserController._nodeSetFilenames)
                        {
                            ImportNodeset2Xml(externalReferences, nodesetFile, i);
                        }

                        Console.WriteLine("Import nodes pass " + i.ToString() + " completed!");
                    }
                }

                AddReverseReferences(externalReferences);
            }
        }

        private void ImportNodeset2Xml(IDictionary<NodeId, IList<IReference>> externalReferences, string resourcepath, int pass)
        {
            using (Stream stream = new FileStream(resourcepath, FileMode.Open))
            {
                UANodeSet nodeSet = UANodeSet.Read(stream);

                NodeStateCollection predefinedNodes = new NodeStateCollection();
                nodeSet.Import(SystemContext, predefinedNodes);
# if DEBUG
                DebugOutput(nodeSet, predefinedNodes);
#endif
                for (int i = 0; i < predefinedNodes.Count; i++)
                {
                    try
                    {
                        AddPredefinedNode(SystemContext, predefinedNodes[i]);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Pass " + pass.ToString() + ": Importing node ns=" + predefinedNodes[i].NodeId.NamespaceIndex + ";i=" + predefinedNodes[i].NodeId.Identifier + " (" + predefinedNodes[i].DisplayName + ") failed with error: " + ex.Message);
                    }
                }
            }
        }

        void DebugOutput(UANodeSet nodeSet, NodeStateCollection predefinedNodes)
        {
            Debug.WriteLine("");
            Debug.WriteLine("Server Namespaces:");
            for (int i = 0; i < Server.NamespaceUris.Count; i++)
            {
                Debug.WriteLine(i + ": " + Server.NamespaceUris.GetString((uint)i));
            }

            Debug.WriteLine("");
            Debug.WriteLine("Nodeset Namespaces:");
            if ((nodeSet.NamespaceUris != null) && (nodeSet.NamespaceUris.Length > 0))
            {
                for (int i = 0; i < nodeSet.NamespaceUris.Length; i++)
                {
                    Debug.WriteLine(i + ": " + nodeSet.NamespaceUris[i]);
                }
            }

            Debug.WriteLine("");
        }
    }
}
