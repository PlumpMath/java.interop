﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using NUnit.Framework;

using Android.Runtime;

using Java.Interop;

using JValue    = Java.Interop.JValue;

namespace Android.InteropTests {

	delegate    IntPtr              IntPtrDelegate_CallObjectMethodA        (IntPtr                     env,    IntPtr                  instance,   IntPtr              method, JValue[]    args);
	delegate    void                IntPtrDelegate_DeleteLocalRef           (IntPtr                     env,    IntPtr handle);

	[TestFixture]
	public class TestsSample : Java.InteropTests.JavaVMFixture {

		const   string  JavaInteropLib                  = "JavaInterop";

		const   int     Unified_ToString_Iterations     = 10000;
		const   int     MaxLocalRefs                    = 500;

		// offsetof (JNIEnv, CallObjectMethodA)/sizeof(void*)
		const   int     JNIEnvIndex_CallObjectMethodA   = 36;

		// offsetof (JNIEnv, DeleteLocalRef)/sizeof(void*)
		const   int     JNIEnvIndex_DeleteLocalRef      = 23;

		static  readonly    int     JNIEnvOffset_CallObjectMethodA      = JNIEnvIndex_CallObjectMethodA * IntPtr.Size;
		static  readonly    int     JNIEnvOffset_DeleteLocalRef         = JNIEnvIndex_DeleteLocalRef    * IntPtr.Size;

		[DllImport (JavaInteropLib, CallingConvention = CallingConvention.Cdecl)]
		static extern JniLocalReference JavaInterop_CallObjectMethod (JniEnvironmentSafeHandle env, JniReferenceSafeHandle obj, JniInstanceMethodID method);

		[DllImport (JavaInteropLib, CallingConvention = CallingConvention.Cdecl)]
		static extern void JavaInterop_DeleteLocalRef (JniEnvironmentSafeHandle env, IntPtr handle);


		TimeSpan GetXAMethodCallTiming ()
		{
			var k = JNIEnv.FindClass ("java/lang/Object");
			var c = JNIEnv.GetMethodID (k, "<init>", "()V");
			var o = JNIEnv.NewObject (k, c);
			var t = JNIEnv.GetMethodID (k, "toString", "()Ljava/lang/String;");

			var sw = Stopwatch.StartNew ();
			for (int i = 0; i < Unified_ToString_Iterations; ++i) {
				var r = JNIEnv.CallObjectMethod (o, t);
				JNIEnv.DeleteLocalRef (r);
			}
			sw.Stop ();
			return sw.Elapsed;
		}

		TimeSpan GetJIMethodCallTiming ()
		{
			using (var k = new JniType ("java/lang/Object"))
			using (var c = k.GetConstructor ("()V"))
			using (var o = k.NewObject (c))
			using (var t = k.GetInstanceMethod ("toString", "()Ljava/lang/String;")) {

				var sw = Stopwatch.StartNew ();
				for (int i = 0; i < Unified_ToString_Iterations; ++i) {
					using (var r = t.CallVirtualObjectMethod (o)) {
					}
				}
				sw.Stop ();
				return sw.Elapsed;
			}
		}

		void GetXACallObjectMethodAndDeleteLocalRefTimings (out TimeSpan callObjectMethodTiming, out TimeSpan deleteLocalRefTiming)
		{
			var k = JNIEnv.FindClass ("java/lang/Object");
			var c = JNIEnv.GetMethodID (k, "<init>", "()V");
			var o = JNIEnv.NewObject (k, c);
			var t = JNIEnv.GetMethodID (k, "toString", "()Ljava/lang/String;");

			do {
				var r = JNIEnv.CallObjectMethod (o, t);
				JNIEnv.DeleteLocalRef (r);
			} while (false);

			var rs = new IntPtr [MaxLocalRefs];

			var sw = Stopwatch.StartNew ();
			for (int i = 0; i < MaxLocalRefs; ++i) {
				rs [i] = JNIEnv.CallObjectMethod (o, t);
			}
			sw.Stop ();
			callObjectMethodTiming  = sw.Elapsed;

			sw.Restart ();
			for (int i = 0; i < MaxLocalRefs; ++i) {
				JNIEnv.DeleteLocalRef (rs [i]);
			}
			sw.Stop ();
			deleteLocalRefTiming    = sw.Elapsed;
		}

		void GetJICallObjectMethodAndDeleteLocalRefTimings (
				out TimeSpan callVirtualObjectMethodTime, out TimeSpan disposeTime,
				out TimeSpan safeCallObjectMethodTime, out TimeSpan safeDeleteLocalRefTime,
				out TimeSpan unsafeCallObjectMethodTime, out TimeSpan unsafeDeleteLocalRefTime,
				out TimeSpan pinvokeCallObjectMethodTime, out TimeSpan pinvokeDeleteLocalRefTime)
		{

			using (var k = new JniType ("java/lang/Object"))
			using (var c = k.GetConstructor ("()V"))
			using (var o = k.NewObject (c))
			using (var t = k.GetInstanceMethod ("toString", "()Ljava/lang/String;")) {

				using (var r = t.CallVirtualObjectMethod (o)) {
				}

				var rs = new JniLocalReference [MaxLocalRefs];

				var sw = Stopwatch.StartNew ();
				for (int i = 0; i < rs.Length; ++i) {
					rs [i] = t.CallVirtualObjectMethod (o);
				}
				sw.Stop ();
				callVirtualObjectMethodTime   = sw.Elapsed;

				sw.Restart ();
				for (int i = 0; i < rs.Length; ++i) {
					rs [i].Dispose ();
				}
				sw.Stop ();
				disposeTime   = sw.Elapsed;

				IntPtr JNIEnv_CallObjectMethodA = Marshal.ReadIntPtr (Marshal.ReadIntPtr (JNIEnv.Handle), JNIEnvOffset_CallObjectMethodA);
				IntPtr JNIEnv_DeleteLocalRef    = Marshal.ReadIntPtr (Marshal.ReadIntPtr (JNIEnv.Handle), JNIEnvOffset_DeleteLocalRef);

				var safeCall = (SafeHandleDelegate_CallObjectMethodA)
						Marshal.GetDelegateForFunctionPointer (JNIEnv_CallObjectMethodA, typeof (SafeHandleDelegate_CallObjectMethodA));
				var safeDel = (SafeHandleDelegate_DeleteLocalRef)
						Marshal.GetDelegateForFunctionPointer (JNIEnv_DeleteLocalRef, typeof (SafeHandleDelegate_DeleteLocalRef));
				var usafeCall = (IntPtrDelegate_CallObjectMethodA)
						Marshal.GetDelegateForFunctionPointer (JNIEnv_CallObjectMethodA, typeof (IntPtrDelegate_CallObjectMethodA));
				var usafeDel = (IntPtrDelegate_DeleteLocalRef)
						Marshal.GetDelegateForFunctionPointer (JNIEnv_DeleteLocalRef, typeof (IntPtrDelegate_DeleteLocalRef));

				var sh = JniEnvironment.Current.SafeHandle;
				var uh = sh.DangerousGetHandle ();
				var args = new JValue [0];

				sw.Restart ();
				for (int i = 0; i < rs.Length; ++i) {
					rs [i] = safeCall (sh, o, t, args);
				}
				sw.Stop ();
				safeCallObjectMethodTime  = sw.Elapsed;

				sw.Restart ();
				for (int i = 0; i < rs.Length; ++i) {
					safeDel (sh, rs [i].DangerousGetHandle ());
					rs [i].SetHandleAsInvalid ();
				}
				sw.Stop ();
				safeDeleteLocalRefTime    = sw.Elapsed;

				var urs = new IntPtr [MaxLocalRefs];
				var ut = t.DangerousGetHandle ();
				var uo = o.DangerousGetHandle ();

				sw.Restart ();
				for (int i = 0; i < urs.Length; ++i) {
					urs [i] = usafeCall (uh, uo, ut, args);
				}
				sw.Stop ();
				unsafeCallObjectMethodTime  = sw.Elapsed;

				sw.Restart ();
				for (int i = 0; i < urs.Length; ++i) {
					usafeDel (uh, urs [i]);
				}
				sw.Stop ();
				unsafeDeleteLocalRefTime  = sw.Elapsed;

				sw.Restart ();
				for (int i = 0; i < urs.Length; ++i) {
					rs [i] = JavaInterop_CallObjectMethod (sh, o, t);
				}
				sw.Stop ();
				pinvokeCallObjectMethodTime = sw.Elapsed;

				sw.Restart ();
				for (int i = 0; i < rs.Length; ++i) {
					JavaInterop_DeleteLocalRef (sh, rs [i].DangerousGetHandle ());
					rs [i].SetHandleAsInvalid ();
				}
				sw.Stop ();
				pinvokeDeleteLocalRefTime   = sw.Elapsed;
			}
		}

		[Test]
		public void CompareJniInvocationTiming ()
		{
			var jiFullMethodCallTiming  = GetJIMethodCallTiming ();
			var xaFullMethodCallTiming  = GetXAMethodCallTiming ();

			TimeSpan xaCallObjectMethodTime, xaDeleteLocalRefTime;
			GetXACallObjectMethodAndDeleteLocalRefTimings (out xaCallObjectMethodTime, out xaDeleteLocalRefTime);

			TimeSpan callVirtualObjectMethodTiming, disposeTiming, safeCallTime, safeDelTime, unsafeCallTime, unsafeDelTime;
			TimeSpan pinvokeCallTime, pinvokeDelTime;
			GetJICallObjectMethodAndDeleteLocalRefTimings (
					out callVirtualObjectMethodTiming, out disposeTiming,
					out safeCallTime, out safeDelTime,
					out unsafeCallTime, out unsafeDelTime,
					out pinvokeCallTime, out pinvokeDelTime);

			Console.WriteLine ("# \"Full\" Invocations: JNIEnv::CallObjectMethod() + JNIEnv::DeleteLocalRef() for {0} iterations", Unified_ToString_Iterations);
			Console.WriteLine ("           Java.Interop Object.toString() Timing: {0}; {1,12} ms/iteration                                -- ~{2}x",
					jiFullMethodCallTiming,
					jiFullMethodCallTiming.TotalMilliseconds / Unified_ToString_Iterations,
					jiFullMethodCallTiming.TotalMilliseconds / xaFullMethodCallTiming.TotalMilliseconds);
			Console.WriteLine ("        Xamarin.Android Object.toString() Timing: {0}; {1,12} ms/iteration",
					xaFullMethodCallTiming,
					xaFullMethodCallTiming.TotalMilliseconds / Unified_ToString_Iterations);

			Console.WriteLine ("# JNIEnv::CallObjectMethod() for {0} iterations", MaxLocalRefs);
			Console.WriteLine ("           Java.Interop Object.toString() Timing: {0}; {1,12} ms/CallVirtualObjectMethod()                -- ~{2}x",
					callVirtualObjectMethodTiming,
					callVirtualObjectMethodTiming.TotalMilliseconds / MaxLocalRefs,
					callVirtualObjectMethodTiming.TotalMilliseconds / xaCallObjectMethodTime.TotalMilliseconds);
			Console.WriteLine ("       Xamarin.Android CallObjectMethod() Timing: {0}; {1,12} ms/CallObjectMethod()",
					xaCallObjectMethodTime,
					xaCallObjectMethodTime.TotalMilliseconds / MaxLocalRefs);

			Console.WriteLine ("# JNIEnv::DeleteLocalRef() for {0} iterations", MaxLocalRefs);
			Console.WriteLine (" Java.Interop JniLocalReference.Dispose() Timing: {0}; {1,12} ms/Dispose()                                -- ~{2}x",
					disposeTiming,
					disposeTiming.TotalMilliseconds / MaxLocalRefs,
					disposeTiming.TotalMilliseconds / xaDeleteLocalRefTime.TotalMilliseconds);
			Console.WriteLine ("         Xamarin.Android DeleteLocalRef() Timing: {0}; {1,12} ms/DeleteLocalRef()",
					xaDeleteLocalRefTime,
					xaDeleteLocalRefTime.TotalMilliseconds / MaxLocalRefs);

			Console.WriteLine ("## Breaking down the above Object.toString() + JniLocalReference.Dispose() timings, the JNI calls:");
			Console.WriteLine ("# JNIEnv::CallObjectMethod: SafeHandle vs. IntPtr");
			Console.WriteLine ("                  Java.Interop safeCall() Timing: {0}; {1,12} ms/SafeHandle JNIEnv::CallObjectMethodA()   -- ~{2}x",
					safeCallTime,
					safeCallTime.TotalMilliseconds / MaxLocalRefs,
					safeCallTime.TotalMilliseconds / unsafeCallTime.TotalMilliseconds);
			Console.WriteLine ("         Java.Interop P/Invoke safeCall() Timing: {0}; {1,12} ms/SafeHandle JNIEnv::CallObjectMethodA()   -- ~{2}x",
					pinvokeCallTime,
					pinvokeCallTime.TotalMilliseconds / MaxLocalRefs,
					pinvokeCallTime.TotalMilliseconds / unsafeCallTime.TotalMilliseconds);
			Console.WriteLine ("                Java.Interop unsafeCall() Timing: {0}; {1,12} ms/IntPtr JNIEnv::CallObjectMethodA()",
					unsafeCallTime,
					unsafeCallTime.TotalMilliseconds / MaxLocalRefs);
			Console.WriteLine ("# JNIEnv::DeleteLocalRef: SafeHandle vs. IntPtr");
			Console.WriteLine ("                   Java.Interop safeDel() Timing: {0}; {1,12} ms/SafeHandle JNIEnv::DeleteLocalRef()      -- ~{2}x",
					safeDelTime,
					safeDelTime.TotalMilliseconds / MaxLocalRefs,
					safeDelTime.TotalMilliseconds / unsafeDelTime.TotalMilliseconds);
			Console.WriteLine ("          Java.Interop P/Invoke safeDel() Timing: {0}; {1,12} ms/SafeHandle JNIEnv::DeleteLocalRef()      -- ~{2}x",
					pinvokeDelTime,
					pinvokeDelTime.TotalMilliseconds / MaxLocalRefs,
					pinvokeDelTime.TotalMilliseconds / unsafeDelTime.TotalMilliseconds);
			Console.WriteLine ("                 Java.Interop unsafeDel() Timing: {0}; {1,12} ms/IntPtr JNIEnv::DeleteLocalRef",
					unsafeDelTime,
					unsafeDelTime.TotalMilliseconds / MaxLocalRefs);
		}
	}
}
