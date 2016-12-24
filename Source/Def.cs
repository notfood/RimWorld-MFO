using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Xml;

using Verse;

namespace Override
{
	public class Def : Verse.Def
	{
		public const string ATTRIBUTE_DEBUG = "Debug";
		public const string ATTRIBUTE_FILTER = "Filter";
		public const string ATTRIBUTE_MODE = "Mode";
		public const string ATTRIBUTE_DECORATOR = "Transform";
		public const string ATTRIBUTE_PARSER = "Parser";
		public const string ATTRIBUTE_COMPARE = "Compare";
		public const string ATTRIBUTE_INDEX = "Index";


		private static Dictionary<string, Mode> modes = new Dictionary<string, Mode>() {
			{ "clear", Mode.Clear },
			{ "append", Mode.Append },
			{ "replace", Mode.Replace },
			{ "insert", Mode.Insert },
			{ "default", Mode.Default },
		};

		private static Dictionary<string, Filter> filters = new Dictionary<string, Filter> () {
			{ "Name",     new NameFilter() },
			{ "Category", new CategoryFilter() },
			{ "Match",    new MatchFilter() },
			{ "Linq",    new LinqFilter() },
			{ "Race",     new RaceFilter() },
			{ "Trader",   new TraderFilter() },
			{ "Class",   new ClassFilter() },
		};

		private static FieldParser DefaultParser = new RimWorldDefaultParser();
		private static Dictionary<string, FieldParser> parsers = new Dictionary<string, FieldParser> () {
			{ "Default", DefaultParser },
			{ "ITab", new CustomITabParser() },
		};


		private static Dictionary<string, FieldComparer> comparers = new Dictionary<string, FieldComparer> () {
			{ "StatModifier", new StatModifierComparer() },
			{ "VerbProperties", new VerbPropertiesComparer() },
			{ "CompProperties", new CompComparer() }
		};

		// We need it for generics
		private static MethodInfo crossRefLoader_RegisterListWantsCrossRef = typeof(CrossRefLoader).GetMethod ("RegisterListWantsCrossRef");

		// actions to be run after resolve references. It'll only run once because we share defName
		private static Queue<Action> resolveReferencesActionQueue = new Queue<Action>();

		private void LoadDataFromXmlCustom(XmlNode xmlRoot) {
			// set defaults to avoid complains
			defName = "OverrideDef";

			bool debug;

			XmlAttribute debugAttribute = xmlRoot.Attributes [ATTRIBUTE_DEBUG];
			debug = debugAttribute != null && debugAttribute.Value == "True";

			Filter filter;

			XmlAttribute filterAttribute = xmlRoot.Attributes [ATTRIBUTE_FILTER];
			if (filterAttribute != null) {
				filters.TryGetValue (filterAttribute.Value, out filter);

				if (filter == null) {
					Type typeInAnyAssembly = GenTypes.GetTypeInAnyAssembly (filterAttribute.Value);
					if (typeInAnyAssembly != null && typeof(Filter).IsAssignableFrom (typeInAnyAssembly)) {
						filter = (Filter)Activator.CreateInstance (typeInAnyAssembly);
					} else {
						Log.Warning ("Override :: Unknown Filter " + filterAttribute.Value);

						return;
					}
				}
			} else {
				filter = filters["Name"];
			}

			if (debug) Log.Message ("Override :: Filter is " + filter);

			Func<Verse.Def, bool> predicate;

			try {
				predicate = filter.Predicate (xmlRoot.Attributes);
			} catch (ArgumentException e) {
				Log.Warning ("Override :: " + e);

				return;
			}

			// At this time, Def aren't loaded per se, they only have their primitives set and can only be found here.
			int count = 0;
			var defs = (
				from pack in LoadedModManager.RunningMods
				from def in pack.AllDefs.Where(predicate)
				select def
			);
			foreach(var def in defs) {
				OverrideDataFromXml (xmlRoot, def, debug);

				count++;
			}

			if (debug) {
				if (count > 0) {
					Log.Message ("Override :: Found " + count + " Defs");
				} else {
					Log.Warning ("Override :: No Defs Found");
				}
			}
		}

		private void OverrideDataFromXml (XmlNode xmlRoot, Verse.Def destinationDef, bool debug) {

			Type destinationType = destinationDef.GetType ();

			string prefix = "Override :: " + destinationDef + " :: ";

			foreach (XmlNode node in xmlRoot.ChildNodes) {
				// field we are about to change
				string name = node.Name;

				if (name == null
					// may cause save issues if these change
					|| name == "shortHash" 
					|| name == "index" 
					|| name == "debugRandomId") {
					continue;
				}

				string text = node.InnerText;

				Mode mode = getMode (node);

				if (string.IsNullOrEmpty(text) && mode != Mode.Clear) {
					// removal must be explicit replace or it's ignored
					continue;
				}

				if (name == "defName") {
					// not allowed to change target defName
					// we use it for tracking
					if (debug) Log.Message (prefix + text);

					prefix = "Override :: " + text + " :: ";

					continue;
				}

				FieldInfo destinationField = destinationType.GetField (name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

				if (destinationField == null) {
					Log.Warning (prefix + "\"" + name + "\" not found in target");

					continue;
				}

				Type destinationFieldType = destinationField.FieldType;

				object destinationValue;
				if (mode == Mode.Clear && !destinationFieldType.IsPrimitive) {
					// we are clearing
					// primitives can't be null, set to 0 ?
					destinationValue = null;

					destinationField.SetValue (destinationDef, destinationValue);

					if (debug) Log.Message (prefix + "\"" + name + "\" has been set to null");
				} else {
					destinationValue = destinationField.GetValue (destinationDef);
				}

				if (destinationFieldType.HasGenericDefinition (typeof(List<>))) {
					// its a list, search the source and queue or insert

					Type[] genericArguments = destinationFieldType.GetGenericArguments ();
					// Is it a def or derivate?
					Type targetDefType = null;
					foreach (Type t in genericArguments) {
						if (typeof(Verse.Def).IsAssignableFrom (t)) {
							targetDefType = t;
							break;
						}
					}

					// sometimes, they don't exist. Don't worry, no one will interfere.
					if (destinationValue == null) {
						destinationValue = Activator.CreateInstance (destinationFieldType);
						destinationField.SetValue (destinationDef, destinationValue);

						if (debug) Log.Message (prefix + "\"" + name + "\" has been set to \"" + destinationValue + "\"");
					}

					FieldParser parserFactory = getParserFactory(node);
					Func<XmlNode, object> parser = parserFactory.makeParser (genericArguments);

					// compares destination and result. Only available for Replace mode
					FieldComparer comparer = mode == Mode.Replace ? getComparer (node, genericArguments) : null;

					if (targetDefType != null) {
						// Crossreferencing a List needs the generic method
						MethodInfo crossRefLoader_RegisterListWantsCrossRef_generic = crossRefLoader_RegisterListWantsCrossRef.MakeGenericMethod (targetDefType);

						foreach (XmlNode child in node.ChildNodes) {
							object[] parameters = new object[] {
								destinationValue, child.InnerText
							};
							crossRefLoader_RegisterListWantsCrossRef_generic.Invoke (null, parameters);

							if (debug) Log.Message (prefix + "Registered into \"" + name + "\" the value \"" + child.InnerText + "\" of type \"" + targetDefType + "\"");
						}

					} else {

						if (parser == null) {
							Log.Warning (prefix + "Parser is null");

							continue;
						}

						IList destinationList = (IList) destinationValue;

						foreach (XmlNode child in node.ChildNodes) {
							object result = parser(child);

							if (result == null) {
								// no nulls allowed, they are troublemakers
								Log.Warning (prefix + "Can't Add null into \"" + name + "\"");

								continue;
							}

							if (mode == Mode.Replace) {

								if (comparer == null) {
									Log.Warning (prefix + "No known comparer for \"" + name + "\"");

									break;
								}

								Action findAndReplace = delegate {

									bool found = false;

									int index;
									for (index = 0; index < destinationList.Count; index++) {

										if (comparer.Compare (result, destinationList [index])) {
											destinationList [index] = result;

											found = true;

											break;
										}

									}

									if (found) {
										if (debug) Log.Message (prefix + "Replaced into postion " + index + " at \"" + name + "\" the value \"" + result + "\"");
									} else {
										destinationList.Add (result);

										if (debug) Log.Message (prefix + "Added into \"" + name + "\" the value \"" + result + "\"");
									}

								};

								if (comparer.delay) {
									resolveReferencesActionQueue.Enqueue (findAndReplace);
									if (debug) Log.Message (prefix + "Delaying Replace of element in \"" + name + "\" list");
								} else {
									findAndReplace ();
								}

							} else if (mode == Mode.Append) { 

								destinationList.Add (result);

								if (debug) Log.Message (prefix + "Added into \"" + name + "\" the value \"" + result + "\"");

							} else {

								int index = mode == Mode.Insert ? getIndex (child, destinationList) : 0;

								destinationList.Insert (index, result);

								if (debug) Log.Message (prefix + "Inserted into position " + index + " at \"" + destinationField.Name + "\" the value \"" + result + "\"");

							}
						}
					}


				} else if (destinationFieldType.HasGenericDefinition (typeof(Dictionary<, >))) {
					// its a dict, what do we do?
					Log.Warning (prefix + "We don't know how to override Dictionary yet...");

				} else if (typeof(Verse.Def).IsAssignableFrom (destinationFieldType)) {
					// its a Def, queue
					CrossRefLoader.RegisterObjectWantsCrossRef (destinationDef, destinationField, text);

					if (debug) Log.Message (prefix + "Registered \"" + name + "\" with value \"" + destinationValue + "\" of type \"" + destinationFieldType.Name + "\" into \"" + text + "\"");
				} else if (ParseHelper.HandlesType (destinationFieldType)) {
					// it can be handled by ParserHelper

					object result = ParseHelper.FromString (text, destinationFieldType);

					destinationField.SetValue (destinationDef, result);

					if (debug) Log.Message (prefix + "Set \"" + name + "\" with value \"" + destinationValue + "\" of type \"" + destinationFieldType.Name + "\" into \"" + text + "\"");
				} else {
					// it's most likely an object, try XmlToObject.
					FieldParser parserFactory = getParserFactory(node);
					Func<XmlNode, object> parser = parserFactory.makeParser (destinationFieldType);

					object result = null;
					if (parser != null) {
						result = parser(node);
					}

					if (result != null) {
						// this may fail, try catch?
						destinationField.SetValue (destinationDef, result);

						if (debug) Log.Message (prefix + "Set \"" + name + "\" with value \"" + destinationValue + "\" of type \"" + destinationFieldType.Name + "\" into \"" + result + "\"");
					} else {
						// user entered null
						Log.Warning (prefix + "Can't Set \"" + name + "\"");
					}
				}
			}

		}

		private static Mode getMode(XmlNode node) {
			Mode mode = Mode.Default;

			XmlAttribute attribute = node.Attributes[ATTRIBUTE_MODE];
			if (attribute != null) {
				string value = attribute.Value.ToLowerInvariant ();

				modes.TryGetValue (value, out mode);
			}

			return mode;
		}

		private static FieldParser getParserFactory(XmlNode node) {
			FieldParser parserFactory = null;

			XmlAttribute attribute = node.Attributes [ATTRIBUTE_PARSER];
			if (attribute != null) {
				parsers.TryGetValue (attribute.Value, out parserFactory);
			}

			if (parserFactory == null) {
				parserFactory = DefaultParser;
			}

			return parserFactory;
		}

		private static FieldComparer getComparer(XmlNode node, Type[] genericArguments) {
			FieldComparer comparer = null;

			XmlAttribute attribute = node.Attributes [ATTRIBUTE_COMPARE];
			if (attribute != null) {
				// explicit comparer, get from comparers or load from assembly
				comparers.TryGetValue (attribute.Value, out comparer);

				if (comparer == null) {
					Type typeInAnyAssembly = GenTypes.GetTypeInAnyAssembly (attribute.Value);
					if (typeInAnyAssembly != null && typeof(FieldComparer).IsAssignableFrom (typeInAnyAssembly)) {
						comparer = (FieldComparer) Activator.CreateInstance (typeInAnyAssembly);
					}
				}
			}

			if (comparer == null) {
				// try to infer comparer
				foreach (Type t in genericArguments) {

					if (comparers.ContainsKey(t.Name)) {
						comparer = comparers [t.Name];

						break;
					}
				}
			}

			return comparer;
		}

		private static int getIndex(XmlNode node, IList destinationList) {
			int index = 0;

			XmlAttribute attribute = node.Attributes [ATTRIBUTE_INDEX];
			if (attribute != null) {
				int.TryParse (attribute.Value, out index);

				// prevent out of bounds
				if (index >= destinationList.Count) {
					index = destinationList.Count - 1;
				} else if (index < 0) {
					index = 0;
				}
			}

			return index;
		}

		// This will get called only once as our defName is unique.
		public override void ResolveReferences ()
		{
			if (resolveReferencesActionQueue.Count > 0) {
				while (resolveReferencesActionQueue.Count > 0)
				{
					resolveReferencesActionQueue.Dequeue().Invoke();
				}
			}
		}

		public static void RegisterFilter(string name, Filter filter) {
			filters.Add (name, filter);
		}

		public static void RegisterParserFactory(string name, FieldParser parserFactory) {
			parsers.Add (name, parserFactory);
		}

		public static void RegisterComparer(Type type, FieldComparer comparer) {
			comparers.Add (type.Name, comparer);
		}

		private enum Mode {
			Default, Clear, Insert, Append, Replace, Copy
		}

	}

}

