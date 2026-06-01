using UnityEngine;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// プロシージャル Skybox を RenderSettings.skybox に差し込む。
    /// シェーダー `Sandbox/ProceduralGradientSky` が見つかれば適用、無ければ静かに何もしない（fallback: 既存 skybox を尊重）。
    /// マテリアル本体は runtime 生成。色/太陽は AtmosphericProfileController が Shader Global で動的供給するので
    /// このコンポーネントは「材質を一度差し込むだけ」のシンプル責務。
    /// </summary>
    [DefaultExecutionOrder(-26)] // AtmosphericProfileController(-25) より早く差し込んでおく
    public sealed class ProceduralSky : MonoBehaviour
    {
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private string shaderName = "Sandbox/ProceduralGradientSky";

        private Material _mat;
        private Material _previousSkybox;
        private bool _applied;

        private void OnEnable()
        {
            if (applyOnEnable) Apply();
        }

        public void Apply()
        {
            if (_applied) return;
            var sh = Shader.Find(shaderName);
            if (sh == null)
            {
                Debug.LogWarning($"[ProceduralSky] shader '{shaderName}' not found. skipping.");
                return;
            }
            _mat = new Material(sh) { name = "ProceduralGradientSkyMat" };
            _previousSkybox = RenderSettings.skybox;
            RenderSettings.skybox = _mat;
            DynamicGI.UpdateEnvironment();
            _applied = true;
        }

        private void OnDisable()
        {
            if (!_applied) return;
            // 元に戻す（シーン編集中に Disable した時に skybox が消えないように）
            if (RenderSettings.skybox == _mat) RenderSettings.skybox = _previousSkybox;
            if (_mat != null) { Object.Destroy(_mat); _mat = null; }
            _applied = false;
        }
    }
}
