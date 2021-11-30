using System.Runtime.InteropServices;

namespace CignalRP {
    public static class IntFloatConverter {
        [StructLayout(LayoutKind.Explicit)]
        public struct IntFloat {
            [FieldOffset(0)] public int intValue;
            [FieldOffset(0)] public float floatValue;
        }

        public static float ToFloat(this int value) {
            IntFloat v = default;
            v.intValue = value;
            return v.floatValue;
        }
    }
}
