using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Text;

using Mono.Linq.Expressions;

using Java.Interop;

namespace Java.Interop.Dynamic {

	class JavaMethodInfo : JavaMethodBase {

		public  JniType         ReturnType;

		string  name;
		bool    isStatic;

		public JavaMethodInfo (JniPeerMembers members, JniObjectReference method, string name, bool isStatic)
			: base (members, method)
		{
			this.name       = name;
			this.isStatic   = isStatic;
		}

		public  override    string  Name {
			get {return name;}
		}

		public  override    bool    IsStatic {
			get {return isStatic;}
		}

		public override     bool    IsConstructor {
			get {return false;}
		}

		protected override void Dispose (bool disposing)
		{
			if (!disposing || ReturnType == null)
				return;

			ReturnType.Dispose ();
			ReturnType  = null;
		}

		protected override string JniReturnType {
			get {
				if (ReturnType == null)
					return "V";
				return JniEnvironment.Current.JavaVM.GetJniTypeInfoForJniTypeReference (ReturnType.Name).JniTypeReference;
			}
		}

		public override unsafe object Invoke (IJavaPeerable self, JValue* arguments)
		{
			AssertSelf (self);

			if (IsStatic)
				return InvokeStaticMethod (arguments);
			return InvokeInstanceMethod (self, arguments);
		}

		void AssertSelf (IJavaPeerable self)
		{
			if (IsStatic && self != null)
				throw new ArgumentException (
						string.Format ("Field '{0}' is static but an instance was provided.", JniSignature),
						"self");
			if (!IsStatic && self == null)
				throw new ArgumentException (
						string.Format ("Field '{0}' is an instance field but no instance was provided.", JniSignature),
						"self");
		}

		unsafe object InvokeInstanceMethod (IJavaPeerable self, JValue* arguments)
		{
			var e   = GetSignatureReturnTypeStartIndex ();
			switch (JniSignature [e + 1]) {
			case 'Z':   return members.InstanceMethods.InvokeVirtualBooleanMethod (JniSignature, self, arguments);
			case 'B':   return members.InstanceMethods.InvokeVirtualSByteMethod (JniSignature, self, arguments);
			case 'C':   return members.InstanceMethods.InvokeVirtualCharacterMethod (JniSignature, self, arguments);
			case 'S':   return members.InstanceMethods.InvokeVirtualInt16Method (JniSignature, self, arguments);
			case 'I':   return members.InstanceMethods.InvokeVirtualInt32Method (JniSignature, self, arguments);
			case 'J':   return members.InstanceMethods.InvokeVirtualInt64Method (JniSignature, self, arguments);
			case 'F':   return members.InstanceMethods.InvokeVirtualSingleMethod (JniSignature, self, arguments);
			case 'D':   return members.InstanceMethods.InvokeVirtualDoubleMethod (JniSignature, self, arguments);
			case 'L':
			case '[':
				var lref = members.InstanceMethods.InvokeVirtualObjectMethod (JniSignature, self, arguments);
				return ToReturnValue (ref lref, JniSignature, e + 1);
			case 'V':
				members.InstanceMethods.InvokeVirtualVoidMethod (JniSignature, self, arguments);
				return null;
			default:
				throw new NotSupportedException ("Unsupported argument type: " + JniSignature.Substring (e + 1));
			}
		}

		unsafe object InvokeStaticMethod (JValue* arguments)
		{
			var e   = GetSignatureReturnTypeStartIndex ();
			switch (JniSignature [e + 1]) {
			case 'Z':   return members.StaticMethods.InvokeBooleanMethod (JniSignature, arguments);
			case 'B':   return members.StaticMethods.InvokeSByteMethod (JniSignature, arguments);
			case 'C':   return members.StaticMethods.InvokeCharacterMethod (JniSignature, arguments);
			case 'S':   return members.StaticMethods.InvokeInt16Method (JniSignature, arguments);
			case 'I':   return members.StaticMethods.InvokeInt32Method (JniSignature, arguments);
			case 'J':   return members.StaticMethods.InvokeInt64Method (JniSignature, arguments);
			case 'F':   return members.StaticMethods.InvokeSingleMethod (JniSignature, arguments);
			case 'D':   return members.StaticMethods.InvokeDoubleMethod (JniSignature, arguments);
			case 'L':
			case '[':
				var lref = members.StaticMethods.InvokeObjectMethod (JniSignature, arguments);
				return ToReturnValue (ref lref, JniSignature, e + 1);
			case 'V':
				members.StaticMethods.InvokeVoidMethod (JniSignature, arguments);
				return null;
			default:
				throw new NotSupportedException ("Unsupported argument type: " + JniSignature.Substring (e + 1));
			}
		}

		protected int GetSignatureReturnTypeStartIndex ()
		{
			int n = JniSignature.IndexOf (')');
			if (n == JniSignature.Length - 1)
				throw new NotSupportedException (
					string.Format ("Could not determine method return type from signature '{0}'.", JniSignature));
			return n;
		}
	}

}
