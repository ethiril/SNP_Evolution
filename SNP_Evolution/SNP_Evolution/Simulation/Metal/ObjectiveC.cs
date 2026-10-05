using System;
using System.Runtime.InteropServices;

namespace SnpEvolution.Simulation.Metal
{
    // Just enough of the Objective-C runtime to drive Metal from C# without a native helper library. Each objc_msgSend
    // overload must match the Objective-C method's signature exactly, which arm64 relies on to place the arguments.
    internal static class ObjectiveC
    {
        private const string Runtime = "/usr/lib/libobjc.A.dylib";
        private const string MetalFramework = "/System/Library/Frameworks/Metal.framework/Metal";
        private const string FoundationFramework = "/System/Library/Frameworks/Foundation.framework/Foundation";

        [StructLayout(LayoutKind.Sequential)]
        public readonly record struct Size(nuint Width, nuint Height, nuint Depth);

        // Null when there is no Metal device, as on any system other than macOS.
        public static IntPtr CreateDefaultDevice()
        {
            if (!OperatingSystem.IsMacOS())
            {
                return IntPtr.Zero;
            }
            NativeLibrary.Load(FoundationFramework);
            return MTLCreateSystemDefaultDevice();
        }

        public static IntPtr Selector(string name) => sel_registerName(name);

        public static IntPtr NSString(string value)
        {
            IntPtr utf8 = Marshal.StringToCoTaskMemUTF8(value);
            try
            {
                return Send(objc_getClass("NSString"), Selector("stringWithUTF8String:"), utf8);
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8);
            }
        }

        public static string? Describe(IntPtr error) =>
            error == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send(Send(error, Selector("localizedDescription")), Selector("UTF8String")));

        public static void Release(IntPtr instance)
        {
            if (instance != IntPtr.Zero)
            {
                Send(instance, Selector("release"));
            }
        }

        [DllImport(MetalFramework)]
        private static extern IntPtr MTLCreateSystemDefaultDevice();

        [DllImport(Runtime)]
        private static extern IntPtr objc_getClass(string name);

        [DllImport(Runtime)]
        private static extern IntPtr sel_registerName(string name);

        [DllImport(Runtime, EntryPoint = "objc_autoreleasePoolPush")]
        public static extern IntPtr PushAutoreleasePool();

        [DllImport(Runtime, EntryPoint = "objc_autoreleasePoolPop")]
        public static extern void PopAutoreleasePool(IntPtr pool);

        [DllImport(Runtime, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector);

        [DllImport(Runtime, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

        [DllImport(Runtime, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument, out IntPtr error);

        [DllImport(Runtime, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr first, IntPtr second, out IntPtr error);

        [DllImport(Runtime, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, nuint first, nuint second);

        [DllImport(Runtime, EntryPoint = "objc_msgSend")]
        public static extern void Send(IntPtr receiver, IntPtr selector, IntPtr argument, nuint first, nuint second);

        [DllImport(Runtime, EntryPoint = "objc_msgSend")]
        public static extern void Send(IntPtr receiver, IntPtr selector, Size first, Size second);

        [DllImport(Runtime, EntryPoint = "objc_msgSend")]
        public static extern nuint SendForUnsigned(IntPtr receiver, IntPtr selector);

        [DllImport(Runtime, EntryPoint = "objc_msgSend")]
        public static extern double SendForDouble(IntPtr receiver, IntPtr selector);
    }
}
