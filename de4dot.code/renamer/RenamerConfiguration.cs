/*
    Copyright (C) 2011-2015 de4dot@gmail.com, 2024 kant2002@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using System.Xml.Serialization;

namespace de4dot.code.renamer {
	[XmlRoot(ElementName="Parameter")]
	public class ParameterConfiguration { 

		[XmlAttribute(AttributeName="index")] 
		public int Index { get; set; } 

		[XmlAttribute(AttributeName="newName")] 
		public string NewName { get; set; } 
	}

	[XmlRoot(ElementName="Method")]
	public class MethodConfiguration { 

		[XmlElement(ElementName="Parameter")] 
		public List<ParameterConfiguration> Parameter { get; set; } 

		[XmlAttribute(AttributeName="token")] 
		public string Token { get; set; } 

		[XmlAttribute(AttributeName="newName")] 
		public string NewName { get; set; } 
	}

	[XmlRoot(ElementName="Field")]
	public class FieldConfiguration {
		[XmlAttribute(AttributeName="token")] 
		public string Token { get; set; } 

		[XmlAttribute(AttributeName="newName")] 
		public string NewName { get; set; } 
	}

	[XmlRoot(ElementName="Property")]
	public class PropertyConfiguration {
		[XmlAttribute(AttributeName="token")] 
		public string Token { get; set; } 

		[XmlAttribute(AttributeName="newName")] 
		public string NewName { get; set; } 
	}

	[XmlRoot(ElementName="Type")]
	public class TypeConfiguration { 

		[XmlElement(ElementName="Method")]
		public List<MethodConfiguration> Methods { get; set; } 

		[XmlElement(ElementName="Type")] 
		public List<TypeConfiguration> NestedTypes { get; set; } 

		[XmlElement(ElementName="Field")] 
		public List<FieldConfiguration> Fields { get; set; } 

		[XmlElement(ElementName="Property")]
		public List<PropertyConfiguration> Properties { get; set; } 

		[XmlAttribute(AttributeName="token")]
		public string Token { get; set; }

		[XmlAttribute(AttributeName="newName")]
		public string NewName { get; set; } 
	}

	[XmlRoot(ElementName="Types")]
	public class TypesConfigration { 

		[XmlElement(ElementName="Type")] 
		public List<TypeConfiguration> Types { get; set; } 
	}

	[XmlRoot(ElementName="config")]
	public class RenamerConfiguration { 

		[XmlElement(ElementName="Types")] 
		public TypesConfigration Types { get; set; } 
	}
}
