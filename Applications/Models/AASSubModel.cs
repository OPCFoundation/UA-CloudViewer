
using System.Xml.Serialization;

namespace UANodesetWebViewer.Models
{
	[XmlRoot(  ElementName = "submodel")]
	public class AASSubModel
	{
		[XmlElement(ElementName = "idShort")]
		public string IdShort { get; set; }

		[XmlElement(ElementName = "category")]
		public string Category { get; set; }

		[XmlElement(ElementName = "identification")]
		public Identification Identification { get; set; }

		[XmlElement(ElementName = "semanticId")]
		public SemanticId SemanticId { get; set; }

		[XmlElement(ElementName = "kind")]
		public string Kind { get; set; }

		[XmlElement(ElementName = "qualifier")]
		public object Qualifier { get; set; }

		[XmlElement(ElementName = "submodelElements")]
		public SubmodelElements SubmodelElements { get; set; }
	}

	[XmlRoot(ElementName = "semanticId")]
	public class SemanticId
	{
		[XmlElement(ElementName = "keys")]
		public object Keys { get; set; }
	}

	[XmlRoot(ElementName = "submodelElements")]
	public class SubmodelElements
	{
		[XmlElement(ElementName = "submodelElement")]
		public SubmodelElement SubmodelElement { get; set; }
	}

	[XmlRoot(ElementName = "submodelElement")]
	public class SubmodelElement
	{
		[XmlElement(ElementName = "file")]
		public File File { get; set; }

		[XmlElement(ElementName = "submodelElementCollection")]
		public SubmodelElementCollection SubmodelElementCollection { get; set; }
	}

	[XmlRoot(ElementName = "submodelElementCollection")]
	public class SubmodelElementCollection
	{
		[XmlElement(ElementName = "idShort")]
		public string IdShort { get; set; }

		[XmlElement(ElementName = "category")]
		public object Category { get; set; }

		[XmlElement(ElementName = "semanticId")]
		public SemanticId SemanticId { get; set; }

		[XmlElement(ElementName = "kind")]
		public string Kind { get; set; }

		[XmlElement(ElementName = "qualifier")]
		public object Qualifier { get; set; }

		[XmlElement(ElementName = "value")]
		public Value Value { get; set; }

		[XmlElement(ElementName = "ordered")]
		public bool Ordered { get; set; }

		[XmlElement(ElementName = "allowDuplicates")]
		public bool AllowDuplicates { get; set; }
	}

	[XmlRoot(ElementName = "value")]
	public class Value
	{
		[XmlElement(ElementName = "submodelElement")]
		public SubmodelElement SubmodelElement { get; set; }
	}

	[XmlRoot(ElementName = "file")]
	public class File
	{
		[XmlElement(ElementName = "idShort")]
		public string IdShort { get; set; }

		[XmlElement(ElementName = "category")]
		public object Category { get; set; }

		[XmlElement(ElementName = "semanticId")]
		public SemanticId SemanticId { get; set; }

		[XmlElement(ElementName = "kind")]
		public string Kind { get; set; }

		[XmlElement(ElementName = "qualifier")]
		public object Qualifier { get; set; }

		[XmlElement(ElementName = "mimeType")]
		public string MimeType { get; set; }

		[XmlElement(ElementName = "value")]
		public string Value { get; set; }
	}
}
