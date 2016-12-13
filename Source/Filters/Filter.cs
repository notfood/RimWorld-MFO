using System;
using System.Xml;

namespace Override {
	
	public interface Filter {
		Func<Verse.Def, bool> Predicate (XmlAttributeCollection attributes);
	}

}

