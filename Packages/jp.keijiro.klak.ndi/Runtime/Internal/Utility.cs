using Klak.Ndi.Interop;
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using BindingFlags = System.Reflection.BindingFlags;
using Delegate = System.Delegate;
using IntPtr = System.IntPtr;

namespace Klak.Ndi
{
    static class Util
    {
        public static Vector2 FrameRateND(FrameRate frameRate )
        {
            Debug.Log("ge nd value;");
            switch (frameRate)
            {
                case FrameRate.NTSC_2398:
                    return new Vector2(24000, 1001);
                case FrameRate.NTSC_2997:
                    return new Vector2(30000, 1001);
                case FrameRate.NTSC_5994:
                    return new Vector2(60000, 1001);
                case FrameRate.PAL_25:
                    return new Vector2(30000, 1200);
                case FrameRate.PAL_50:
                    return new Vector2(60000, 1200);
                default:
                    return new Vector2(60000, 1001);
            }
        }

        public static int FrameDataCount(int width, int height, bool alpha)
          => width * height * (alpha ? 3 : 2) / 4;

        public static bool CheckAlpha(Interop.FourCC fourCC)
          => fourCC == Interop.FourCC.UYVA;

        public static float[] ConvertByteArrayToFloat(byte[] bytes)
        {
            if (bytes.Length % 4 != 0) throw new ArgumentException();

            float[] floats = new float[bytes.Length / 4];
            for (int i = 0; i < floats.Length; i++)
            {
                floats[i] = BitConverter.ToSingle(bytes, i * 4);
            }

            return floats;
        }

        // This REQUIRES you to use Marshal.FreeHGlobal() on the returned pointer!
        public static IntPtr StringToUtf8(String managedString)
        {
            int len = Encoding.UTF8.GetByteCount(managedString);

            byte[] buffer = new byte[len + 1];

            Encoding.UTF8.GetBytes(managedString, 0, managedString.Length, buffer, 0);

            IntPtr nativeUtf8 = Marshal.AllocHGlobal(buffer.Length);

            Marshal.Copy(buffer, 0, nativeUtf8, buffer.Length);

            return nativeUtf8;
        }

        // this version will also return the length of the utf8 string
        // This REQUIRES you to use Marshal.FreeHGlobal() on the returned pointer!
        public static IntPtr StringToUtf8(String managedString, out int utf8Length)
        {
            utf8Length = Encoding.UTF8.GetByteCount(managedString);

            byte[] buffer = new byte[utf8Length + 1];

            Encoding.UTF8.GetBytes(managedString, 0, managedString.Length, buffer, 0);

            IntPtr nativeUtf8 = Marshal.AllocHGlobal(buffer.Length);

            Marshal.Copy(buffer, 0, nativeUtf8, buffer.Length);

            return nativeUtf8;
        }

        // Length is optional, but recommended
        // This is all potentially dangerous
        public static string Utf8ToString(IntPtr nativeUtf8, uint? length = null)
        {
            if (nativeUtf8 == IntPtr.Zero)
                return String.Empty;

            uint len = 0;

            if (length.HasValue)
            {
                len = length.Value;
            }
            else
            {
                // try to find the terminator
                while (Marshal.ReadByte(nativeUtf8, (int)len) != 0)
                {
                    ++len;
                }
            }

            byte[] buffer = new byte[len];

            Marshal.Copy(nativeUtf8, buffer, 0, buffer.Length);

            return Encoding.UTF8.GetString(buffer);
        }
    }

    //
    // Directly load an unmanaged data array to a compute buffer via an
    // Intptr. This is not a public interface so will be broken one day.
    // DO NOT TRY AT HOME.
    //
    class ComputeDataSetter
    {
        delegate void SetDataDelegate
          (IntPtr pointer, int s_offs, int d_offs, int count, int stride);

        SetDataDelegate _setData;

        public ComputeDataSetter(ComputeBuffer buffer)
        {
            var method = typeof(ComputeBuffer).GetMethod
              ("InternalSetNativeData",
               BindingFlags.InvokeMethod |
               BindingFlags.NonPublic |
               BindingFlags.Instance);

            _setData = (SetDataDelegate)Delegate.CreateDelegate
              (typeof(SetDataDelegate), buffer, method);
        }

        public void SetData(IntPtr pointer, int count, int stride)
          => _setData(pointer, 0, 0, count, stride);
    }
}
