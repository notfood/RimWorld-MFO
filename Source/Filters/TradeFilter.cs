using System;
using System.Text.RegularExpressions;
using System.Xml;

namespace Override
{
	public class TraderFilter : Filter {
		public Func<Verse.Def, bool> Predicate (XmlAttributeCollection attributes) {
			XmlAttribute traderClassAttribute = attributes ["Category"];
			XmlAttribute techLevelAttribute = attributes ["Tech"];
			XmlAttribute roleAttribute = attributes ["Role"];

			string techLevel = techLevelAttribute != null ? techLevelAttribute.Value.ToLowerInvariant() : null;
			string traderClass = traderClassAttribute != null ? traderClassAttribute.Value.ToLowerInvariant() : null;
			string role = roleAttribute != null ? roleAttribute.Value.ToLowerInvariant() : null;

			return delegate(Verse.Def def) {
				RimWorld.TraderKindDef traderKindDef = def as RimWorld.TraderKindDef;
				if (traderKindDef == null) {
					return false;
				}

				var Groups = traderKindDef.defName.ToLowerInvariant().Split('_');

				if (Groups.Length == 2) {
					return (traderClass == null || traderClass == Groups[0])
						&& (role == null || role == Groups[1]);
				} else if (Groups.Length == 3){
					return (traderClass == null || traderClass == Groups[0])
						&& (techLevel == null || techLevel == Groups[1])
						&& (role == null || role == Groups[2]);
				} else {
					return false;
				}
			};
		}
	}
}

