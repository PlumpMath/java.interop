using System;

namespace Java.Interop
{
	static class JniSystem {
		static JniType _typeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _typeRef, "java/lang/System");}
		}

		static JniStaticMethodInfo _identityHashCode;
		internal static unsafe int IdentityHashCode (JniObjectReference value)
		{
			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (value);
			return TypeRef.GetCachedStaticMethod (ref _identityHashCode, "identityHashCode", "(Ljava/lang/Object;)I")
				.CallInt32Method (TypeRef.PeerReference, args);
		}
	}
}

