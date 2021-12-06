using System.Runtime.CompilerServices;
using UnityEngine.Rendering;

namespace CignalRP {
    public enum EProfileStep {
        Begin,
        End,
    }

    public class CmdBufferExt {
        // 内联优化
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ProfileSample(ref ScriptableRenderContext context, CommandBuffer cmdBuffer, EProfileStep step, string sampleName, bool toExec = true) {
            if (step == EProfileStep.Begin) {
                cmdBuffer.BeginSample(sampleName);
            }
            else if (step == EProfileStep.End) {
                cmdBuffer.EndSample(sampleName);
            }

            if (toExec) {
                Execute(ref context, cmdBuffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref ScriptableRenderContext context, CommandBuffer cmdBuffer) {
            context.ExecuteCommandBuffer(cmdBuffer);
            cmdBuffer.Clear();
        }
    }
}
