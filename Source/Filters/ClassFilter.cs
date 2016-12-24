using System;
using System.Xml;

using Verse;

namespace Override
{
	public class ClassFilter : Filter
	{
		public Func<Verse.Def, bool> Predicate (XmlAttributeCollection attributes) {
			XmlAttribute relevant = attributes ["Target"];
			if (relevant == null) {
				throw new ArgumentException ("No Target attribute");
			}

			if (relevant.Value.Length == 0) {
				throw new ArgumentException ("No target");
			}

			object result = ParseHelper.FromString (relevant.Value, typeof(Type));

			return delegate(Verse.Def def) {
				ThingDef thingDef = def as ThingDef;
				return thingDef != null && thingDef.thingClass != null && thingDef.thingClass == result;
			};
		}
	}
}

