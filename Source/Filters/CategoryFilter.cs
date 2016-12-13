using System;
using System.Xml;

using Verse;

namespace Override {
	
	public class CategoryFilter : Filter {
		public const string RELEVANT_ATTRIBUTE = "Category";

		public Func<Verse.Def, bool> Predicate (XmlAttributeCollection attributes) {
			XmlAttribute relevant = attributes [RELEVANT_ATTRIBUTE];
			if (relevant == null) {
				throw new ArgumentException ("No Value attribute");
			}

			ThingCategory category;

			try {
				category = (ThingCategory) Enum.Parse (typeof(ThingCategory), relevant.Value);
			} catch (ArgumentException innerException) {
				string text = "'" + relevant.Value + "' is not a valid category. Valid categories are: ";
				text += GenText.StringFromEnumerable (Enum.GetValues ((typeof(ThingCategory))));
				throw new ArgumentException (text, innerException);
			}

			return delegate(Verse.Def def) {
				ThingDef thingDef = def as ThingDef;
				return thingDef != null && thingDef.category == category;
			};
		}

	}

}

