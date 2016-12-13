using System;
using System.Xml;

namespace Override {
	
	public class NameFilter : Filter {
		public Func<Verse.Def, bool> Predicate (XmlAttributeCollection attributes) {

			XmlAttribute relevant = attributes ["Target"];
			if (relevant == null) {
				throw new ArgumentException ("No Target attribute");
			}

			string[] targets = Array.ConvertAll(relevant.Value.Split(','), p => p.Trim());

			if (relevant.Value.Length == 0 || targets.Length == 0) {
				throw new ArgumentException ("No targets");
			}

			string type = null;
			relevant = attributes ["Type"];
			if (relevant != null) {
				type = relevant.Value;
			}

			return delegate(Verse.Def def) {
				return Array.IndexOf (targets, def.defName) >= 0 && (type == null || def.GetType().Name == type);
			};
		}
	}

}

