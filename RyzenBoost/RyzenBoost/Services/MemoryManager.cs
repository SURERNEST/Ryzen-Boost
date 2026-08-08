using System;
using System.Runtime.InteropServices;

namespace RyzenBoost.Services
{
    public static class MemoryManager
    {
        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        /// <summary>
        /// Intenta liberar memoria de trabajo del proceso actual.
        /// Esto no es una "defragmentación" de todo Windows, pero reduce la RAM
        /// usada por esta aplicación sin pausar el sistema.
        /// </summary>
        public static bool TrimCurrentProcessWorkingSet()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                return EmptyWorkingSet(GetCurrentProcess());
            }
            catch
            {
                return false;
            }
        }
    }
}
