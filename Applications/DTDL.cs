
using Newtonsoft.Json;
using Opc.Ua.Export;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UANodesetWebViewer.Models;

namespace UANodesetWebViewer
{
    class DTDL
    {
        private static Dictionary<string, string> _map = new Dictionary<string, string>();
        private static List<Tuple<DtdlInterface, string, string>> _interfaceList = new List<Tuple<DtdlInterface, string, string>>();
        private static List<Tuple<DtdlContents, string>> _contentsList = new List<Tuple<DtdlContents, string>>();

        private static void CreateSchemaMap()
        {
            // OPC UA built-in types: https://reference.opcfoundation.org/v104/Core/docs/Part6/5.1.2/
            // DTDL types: https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v2/dtdlv2.md#primitive-schemas

            _map.Clear();
            _map.Add("Boolean", "boolean");
            _map.Add("SByte", "integer");
            _map.Add("Byte", "integer");
            _map.Add("Int16", "integer");
            _map.Add("UInt16", "integer");
            _map.Add("Int32", "integer");
            _map.Add("UInt32", "integer");
            _map.Add("Int64", "long");
            _map.Add("UInt64", "long");
            _map.Add("Float", "float");
            _map.Add("Double", "double");
            _map.Add("String", "string");
            _map.Add("LocalizedText", "string");
            _map.Add("DateTime", "dateTime");
            _map.Add("StatusCode", "integer");
            _map.Add("Integer", "integer");
        }

        private static string GetDtdlDataType(string id)
        {
            try
            {
                return _map[id];
            }
            catch
            {
                return "string"; // default to string
            }
        }



        // OPC UA defines variables, views and objects, as well as associated variabletypes, datatypes, referencetypes and objecttypes
        // In addition, OPC UA defines methods and properties
        public static void Generate(UANodeSet nodeSet)
        {
            // clear previously generated DTDL
            _map.Clear();
            _interfaceList.Clear();
            _contentsList.Clear();

            CreateSchemaMap();

            // create DTDL interfaces and their contents
            foreach (UANode uaNode in nodeSet.Items)
            {
                UAVariable variable = uaNode as UAVariable;
                if (variable != null)
                {
                    if (uaNode.BrowseName.ToString() == "InputArguments")
                    {
                        // TODO: Support input arguments nodes (for commands)
                        continue;
                    }

                    DtdlContents dtdlTelemetry = new DtdlContents
                    {
                        Type = "Telemetry",
                        Name = Regex.Replace(uaNode.BrowseName.ToString().Trim(), "[^A-Za-z]+", ""),
                        Schema = GetDtdlDataType(variable.DataType)
                    };

                    Tuple<DtdlContents, string> newTuple = new Tuple<DtdlContents, string>(dtdlTelemetry, variable.ParentNodeId);
                    if (!_contentsList.Contains(newTuple))
                    {
                        _contentsList.Add(newTuple);
                    }

                    continue;
                }

                UAMethod method = uaNode as UAMethod;
                if (method != null)
                {
                    DtdlContents dtdlCommand = new DtdlContents
                    {
                        Type = "Command",
                        Name = Regex.Replace(uaNode.BrowseName.ToString().Trim(), "[^A-Za-z]+", "")
                    };

                    Tuple<DtdlContents, string> newTuple = new Tuple<DtdlContents, string>(dtdlCommand, method.ParentNodeId);
                    if (!_contentsList.Contains(newTuple))
                    {
                        _contentsList.Add(newTuple);
                    }

                    continue;
                }

                UAObject uaObject = uaNode as UAObject;
                if (uaObject != null)
                {
                    DtdlInterface dtdlInterface = new DtdlInterface
                    {
                        Id = "dtmi:" + Regex.Replace(uaNode.BrowseName.ToString().Trim(), "[^A-Za-z]+", "") + ";1",
                        Type = "Interface",
                        DisplayName = Regex.Replace(uaNode.BrowseName.ToString().Trim(), "[^A-Za-z]+", ""),
                        Contents = new List<DtdlContents>()
                    };

                    Tuple<DtdlInterface, string, string> newTuple = new Tuple<DtdlInterface, string, string>(dtdlInterface, uaObject.NodeId, uaObject.ParentNodeId);
                    if (!_interfaceList.Contains(newTuple))
                    {
                        _interfaceList.Add(newTuple);
                    }

                    continue;
                }

                UAView view = uaNode as UAView;
                if (view != null)
                {
                    // we don't map views since DTDL has no such concept
                    continue;
                }

                UAVariableType variableType = uaNode as UAVariableType;
                if (variableType != null)
                {
                    // we don't map UA variable types, only instances. DTDL only has a limited set of built-in types.
                    continue;
                }

                UADataType dataType = uaNode as UADataType;
                if (dataType != null)
                {
                    // we don't map UA data types, only instances. DTDL only has a limited set of built-in types.
                    continue;
                }

                UAReferenceType referenceType = uaNode as UAReferenceType;
                if (referenceType != null)
                {
                    // we don't map UA reference types, only instances. DTDL only has a limited set of built-in types.
                    continue;
                }

                UAObjectType objectType = uaNode as UAObjectType;
                if (objectType != null)
                {
                    // we don't map UA object (custom) types, only instances. DTDL only has a limited set of built-in types.
                    continue;
                }

                throw new ArgumentException("Unknown UA node detected!");
            }

            AddComponentsToInterfaces();
            AddRelationshipsBetweenInterfaces();

            // generate JSON files
            foreach (Tuple<DtdlInterface, string, string> dtdlInterfaceTuple in _interfaceList)
            {
                string generatedDTDL = JsonConvert.SerializeObject(dtdlInterfaceTuple.Item1, Formatting.Indented);
                string dtdlPath = Path.Combine(Directory.GetCurrentDirectory(), "JSON", Path.GetFileNameWithoutExtension(dtdlInterfaceTuple.Item1.DisplayName) + ".dtdl.json");
                System.IO.File.WriteAllText(dtdlPath, generatedDTDL);
            }
        }

        private static void AddRelationshipsBetweenInterfaces()
        {
            foreach (Tuple<DtdlInterface, string, string> dtdlInterfaceTuple in _interfaceList)
            {
                // find the interface to add a relationship to
                foreach (Tuple<DtdlInterface, string, string> dtdlInterfaceTupleRelationship in _interfaceList)
                {
                    if (dtdlInterfaceTuple.Item3 == dtdlInterfaceTupleRelationship.Item2)
                    {
                        DtdlContents dtdlRelationship = new DtdlContents
                        {
                            Type = "Relationship",
                            Name = "Parent",
                            Target = "dtmi:" + dtdlInterfaceTupleRelationship.Item1.DisplayName + ";1",
                        };

                        if (!dtdlInterfaceTuple.Item1.Contents.Contains(dtdlRelationship))
                        {
                            dtdlInterfaceTuple.Item1.Contents.Add(dtdlRelationship);
                        }
                        break;
                    }
                }
            }
        }

        private static void AddComponentsToInterfaces()
        {
            foreach (Tuple<DtdlContents, string> dtdlComponentTuple in _contentsList)
            {
                // find the interface to add the component to
                foreach (Tuple<DtdlInterface, string, string> dtdlInterfaceTuple in _interfaceList)
                {
                    if (dtdlComponentTuple.Item2 == dtdlInterfaceTuple.Item2)
                    {
                        if (!dtdlInterfaceTuple.Item1.Contents.Contains(dtdlComponentTuple.Item1))
                        {
                            dtdlInterfaceTuple.Item1.Contents.Add(dtdlComponentTuple.Item1);
                        }
                        break;
                    }
                }
            }
        }
    }
}
