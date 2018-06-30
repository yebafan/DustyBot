﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public static class SecureStringExtensions
    {
        /// <summary>
        /// Performs an action on each character of the SecureString.
        /// While this does not create a managed string object, it obviously compromises security of the underlying string.
        /// </summary>
        public static async Task ForEach(this SecureString value, Func<short, Task> action)
        {
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = SecureStringMarshal.SecureStringToGlobalAllocUnicode(value);
                for (int i = 0; i < value.Length; i++)
                {
                    short unicodeChar = Marshal.ReadInt16(valuePtr, i * 2);
                    await action(unicodeChar);
                }
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        public static SecureString ToSecureString(this string unsecureString)
        {
            if (unsecureString == null) throw new ArgumentNullException("unsecureString");

            return unsecureString.Aggregate(new SecureString(), AppendChar, MakeReadOnly);
        }

        private static SecureString MakeReadOnly(SecureString ss)
        {
            ss.MakeReadOnly();
            return ss;
        }

        private static SecureString AppendChar(SecureString ss, char c)
        {
            ss.AppendChar(c);
            return ss;
        }
    }
}
