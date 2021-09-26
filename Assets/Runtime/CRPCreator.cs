using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    [CreateAssetMenu(menuName = "CRP/CreateCRP")]
    public partial class CRPCreator : RenderPipelineAsset {
        protected override RenderPipeline CreatePipeline() {
            return new CRP();
        }
    }
}