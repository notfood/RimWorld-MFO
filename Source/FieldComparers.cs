using System;
using System.Collections;

using Verse;
using RimWorld;

namespace Override
{
	public abstract class FieldComparer {
		public bool delay;

		public FieldComparer(bool delay) {
			this.delay = delay;
		}

		public abstract bool Compare (object x, object y);
	}

	public class StatModifierComparer : FieldComparer {
		
		public StatModifierComparer() : base(true) {}

		public override bool Compare (object x, object y) {
			StatModifier statX = x as StatModifier;
			StatModifier statY = y as StatModifier;

			return statX != null && statY != null
				&& statX.stat == statY.stat;
		}
	}

	public class VerbPropertiesComparer : FieldComparer {
		public VerbPropertiesComparer() : base(true) {}

		public override bool Compare (Object x, Object y) {
			VerbProperties verbX = x as VerbProperties;
			VerbProperties verbY = y as VerbProperties;

			return verbX != null && verbY != null
				&& verbX.linkedBodyPartsGroup == verbY.linkedBodyPartsGroup;
		}
	}

	public class CompComparer : FieldComparer {
		public CompComparer() : base(false) {}

		public override bool Compare (Object x, Object y) {
			CompProperties compX = x as CompProperties;
			CompProperties compY = y as CompProperties;

			return compX != null && compY != null
				&& compX.compClass == compY.compClass;
		}


	}
}

