using System;
using System.Xml;

using Verse;

namespace Override {
	public class RaceFilter : Filter {
		public const string RELEVANT_ATTRIBUTE = "Race";

		public Func<Verse.Def, bool> Predicate (XmlAttributeCollection attributes) {
			XmlAttribute relevant = attributes [RELEVANT_ATTRIBUTE];
			if (relevant == null) {
				throw new ArgumentException ("No Race attribute");
			}

			Func<ThingDef, bool> which;

			switch(relevant.Value.ToLowerInvariant()) {
			case "human":
			case "humanlike":
				which = delegate(ThingDef thingDef) {
					return thingDef.race.Humanlike;
				};
				break;
			case "animal":
				which = delegate(ThingDef thingDef) {
					return thingDef.race.Animal;
				};
				break;
			case "mechanoid":
				which = delegate(ThingDef thingDef) {
					return thingDef.race.IsMechanoid;
				};
				break;
			default:
				throw new ArgumentException ("Unknown race");
			}

			return delegate(Verse.Def def) {
				ThingDef thingDef = def as ThingDef;
				return thingDef != null && thingDef.race != null && which(thingDef);
			};

		}
	}

}

