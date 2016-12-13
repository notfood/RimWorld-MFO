using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Xml;


using Verse;

namespace Override
{
	public interface FieldParser {
		Func<XmlNode, object> makeParser(params Type[] arguments);
	}

	public class RimWorldDefaultParser : FieldParser {
		private static MethodInfo xmlToObject_ObjectFromXmlMethod = typeof(XmlToObject).GetMethod (XmlToObject.ObjectFromXmlMethodName);

		public Func<XmlNode, object> makeParser(params Type[] arguments) {
			MethodInfo parser = xmlToObject_ObjectFromXmlMethod.MakeGenericMethod (arguments);
			return delegate(XmlNode node) {
				return parser.Invoke (null, new object[] { node, true });
			};
		}
	}

	public class CustomITabParser : FieldParser {
		public Func<XmlNode, object> makeParser(params Type[] arguments) {
			return delegate(XmlNode node) {
				Type typeInAnyAssembly = GenTypes.GetTypeInAnyAssembly (node.InnerText);
				if (typeInAnyAssembly != null && typeof(Type).IsAssignableFrom (typeInAnyAssembly)) {
					Type iTabType = (Type) Activator.CreateInstance (typeInAnyAssembly);

					return InspectTabManager.GetSharedInstance(iTabType);
				} else {
					return null;
				}
			};
		}
	}
}

