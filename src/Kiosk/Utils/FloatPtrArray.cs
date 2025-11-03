using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Kiosk.Utils
{
    /// <summary>
    /// float[] 목록을 네이티브에 넘길 float** 로 구성/해제하는 유틸.
    /// - 각 float[]는 GCHandle로 Pin
    /// - IntPtr[]를 unmanaged 메모리에 복사해서 최종 float** 제공
    /// - Dispose()에서 반드시 모든 핀/할당 해제
    /// </summary>
    public sealed class FloatPtrArray : IDisposable
    {
        public IntPtr Pointer { get; private set; } = IntPtr.Zero; // float** unmanaged
        private readonly List<GCHandle> _pins = new();
        private bool _disposed;

        private FloatPtrArray()
        {
        }

        public static FloatPtrArray Build(IReadOnlyList<float[]> arrays)
        {
            var holder = new FloatPtrArray();
            try
            {
                int n = arrays?.Count ?? 0;
                if (n == 0) return holder;

                var ptrs = new IntPtr[n];
                for (int i = 0; i < n; i++)
                {
                    var arr = arrays[i] ?? throw new ArgumentException($"arrays[{i}] is null");
                    if (arr.Length == 0) throw new ArgumentException($"arrays[{i}] is empty");

                    var h = GCHandle.Alloc(arr, GCHandleType.Pinned);
                    holder._pins.Add(h);
                    ptrs[i] = h.AddrOfPinnedObject();
                }

                holder.Pointer = Marshal.AllocHGlobal(IntPtr.Size * n);
                for (int i = 0; i < n; i++)
                    Marshal.WriteIntPtr(holder.Pointer, i * IntPtr.Size, ptrs[i]);

                return holder;
            }
            catch
            {
                holder.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (Pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Pointer);
                Pointer = IntPtr.Zero;
            }

            foreach (var pin in _pins)
                if (pin.IsAllocated)
                    pin.Free();
            _pins.Clear();
        }
    }
}