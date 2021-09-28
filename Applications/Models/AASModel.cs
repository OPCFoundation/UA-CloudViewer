
using System.Collections.Generic;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UANodesetWebViewer.Models
{
	[XmlRoot(ElementName = "aasenv", Namespace = "http://www.admin-shell.io/aas/1/0")]
	public class AasEnv
	{
		[XmlElement(ElementName = "assetAdministrationShells")]
		public AssetAdministrationShells AssetAdministrationShells { get; set; }

		[XmlElement(ElementName = "assets")]
		public Assets Assets { get; set; }

		[XmlArray(ElementName = "submodels")]
		[XmlArrayItem("submodel", Type = typeof(AASSubModel))]
		public List<AASSubModel> Submodels { get; set; }

		[XmlElement(ElementName = "conceptDescriptions")]
		public object ConceptDescriptions { get; set; }

		[XmlAttribute(Namespace = XmlSchema.InstanceNamespace)]
		public string schemaLocation = "http://www.admin-shell.io/aas/1/0 AAS.xsd http://www.admin-shell.io/IEC61360/1/0 IEC61360.xsd";
	}

	[XmlRoot(ElementName = "assetAdministrationShells")]
	public class AssetAdministrationShells
	{
		[XmlElement(ElementName = "assetAdministrationShell")]
		public AssetAdministrationShell AssetAdministrationShell { get; set; }
	}

	[XmlRoot(ElementName = "assetAdministrationShell")]
	public class AssetAdministrationShell
	{
		[XmlElement(ElementName = "idShort")]
		public string IdShort { get; set; }

		[XmlElement(ElementName = "category")]
		public string Category { get; set; }

		[XmlElement(ElementName = "identification")]
		public Identification Identification { get; set; }

		[XmlElement(ElementName = "assetRef")]
		public AssetRef AssetRef { get; set; }

		[XmlArray(ElementName = "submodelRefs")]
		[XmlArrayItem("submodelRef", Type = typeof(SubmodelRef))]
		public List<SubmodelRef> SubmodelRefs { get; set; }

		[XmlElement(ElementName = "conceptDictionaries")]
		public object ConceptDictionaries { get; set; }
	}

	[XmlRoot(ElementName = "identification")]
	public class Identification
	{
		[XmlAttribute(AttributeName = "idType")]
		public string IdType { get; set; }

		[XmlText]
		public string Text { get; set; }
	}

	[XmlRoot(ElementName = "assetRef")]
	public class AssetRef
	{
		[XmlElement(ElementName = "keys")]
		public Keys Keys { get; set; }
	}

	[XmlRoot(ElementName = "submodelRef")]
	public class SubmodelRef
	{
		[XmlElement(ElementName = "keys")]
		public Keys Keys { get; set; }
	}

	[XmlRoot(ElementName = "assets")]
	public class Assets
	{
		[XmlElement(ElementName = "asset")]
		public Asset Asset { get; set; }
	}

	[XmlRoot(ElementName = "asset")]
	public class Asset
	{
		[XmlElement(ElementName = "idShort")]
		public string IdShort { get; set; }

		[XmlElement(ElementName = "category")]
		public object Category { get; set; }

		[XmlElement(ElementName = "identification")]
		public Identification Identification { get; set; }

		[XmlElement(ElementName = "kind")]
		public string Kind { get; set; }

		[XmlElement(ElementName = "assetIdentificationModelRef")]
		public AssetIdentificationModelRef AssetIdentificationModelRef { get; set; }
	}

	[XmlRoot(ElementName = "assetIdentificationModelRef")]
	public class AssetIdentificationModelRef
	{
		[XmlElement(ElementName = "keys")]
		public object Keys { get; set; }
	}

	[XmlRoot(ElementName = "keys")]
	public class Keys
	{
		[XmlElement(ElementName = "key")]
		public Key Key { get; set; }
	}

	[XmlRoot(ElementName = "key")]
	public class Key
	{
		[XmlAttribute(AttributeName = "type")]
		public string Type { get; set; }

		[XmlAttribute(AttributeName = "local")]
		public bool Local { get; set; }

		[XmlAttribute(AttributeName = "idType")]
		public string IdType { get; set; }

		[XmlText]
		public string Text { get; set; }
	}
}
