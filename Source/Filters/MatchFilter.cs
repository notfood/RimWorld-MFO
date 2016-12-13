using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

using Verse;

namespace Override {
	
	public class MatchFilter : Filter {
		public const string RELEVANT_ATTRIBUTE = "Match";

		// match: or not type@field(arg1,arg2)=value
		private static Regex filter = new Regex(@"((?<operation>(or|and))\s+)?(?<negation>not(\s+))?(?<field>\w+)\s*=\s*(?<value>\w+)?");

		public Func<Verse.Def, bool> Predicate (XmlAttributeCollection attributes) {
			XmlAttribute relevant;

			relevant = attributes ["Match"];
			if (relevant == null) {
				throw new ArgumentException ("No Match attribute given");
			}

			string expression = relevant.Value;

			relevant = attributes ["Type"];
			if (relevant == null) {
				throw new ArgumentException ("No Type attribute given");
			}

			Type itType = GenTypes.GetTypeInAnyAssembly (relevant.Value);

			if (!typeof(Verse.Def).IsAssignableFrom (itType)) {
				throw new ArgumentException (string.Format("'{0}' is not a Def Type", itType));
			}
				
			ParameterExpression it = Expression.Parameter(itType, "");
			Expression left = null, right = null;

			foreach(Match match in filter.Matches(expression)) {
				string operation = match.Groups["operation"].Value;
				string negation = match.Groups["negation"].Value;
				string field = match.Groups["field"].Value;
				string value = match.Groups["value"].Value;

				if (field.Length > 0) {
					var itField = itType.GetField (field);
					if (itField == null) {
						throw new ArgumentException (string.Format("'{0}' is not a Field for '{1}' Type", field, itType));
					}

					if (value.Length > 0) {
						if (!ParseHelper.HandlesType (itField.FieldType)) {
							throw new ArgumentException (
								string.Format("ParseHelper can't handle '{0}' for '{1}' Field in '{2}' Type", itField.FieldType, field, itType)
							);
						}
						var tefld = Expression.Field (it, itField);
						var teval = Expression.Constant(ParseHelper.FromString (value, itField.FieldType));

						//right = Expression.Call (typeof(System.Object).GetMethod ("Equals", new Type[] { typeof(object), typeof(object) }), tefld, teval);
						right = Expression.Equal(tefld, teval);

						if (negation.Length > 0) {
							right = Expression.Not (right);
						}
							
						if (left != null) {
							if (operation == "or") {
								left = Expression.OrElse (left, right);
							} else {
								left = Expression.AndAlso (left, right);
							}
						} else {
							left = right;
						}

						Log.Message (field + " = " + value);
					}
				}
			}

			if (left != null) {
				Log.Message ("Override :: Filter is \"" + left.ToString() + "\"");

				return Expression.Lambda<Func<Verse.Def, bool>> (left, it).Compile();
			} else {
				throw new ArgumentException ("Filter is Empty");
			}
		}
	}

}

