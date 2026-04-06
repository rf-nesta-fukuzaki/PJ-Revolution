using UnityEngine;

/// <summary>
/// LineRenderer の外観設定と接続時のパーティクル演出を担当する。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class RopeRenderer : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private float startWidth = 0.05f;
    [SerializeField] private float endWidth = 0.015f;
    [SerializeField] private Color ropeColor = new Color(0.65f, 0.42f, 0.18f);
    [SerializeField] private Color ropeTipColor = new Color(0.85f, 0.72f, 0.48f);

    [Header("Speed-based Width")]
    [SerializeField] private float minWidthMultiplier = 0.8f;
    [SerializeField] private float maxWidthMultiplier = 1.5f;
    [SerializeField] private float maxSpeedForWidth = 15f;

    private LineRenderer _lr;
    private RopeSystem _ropeSystem;
    private Rigidbody _playerRb;

    private void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _ropeSystem = GetComponent<RopeSystem>();
        _playerRb = GetComponentInParent<Rigidbody>();
        SetupMaterial();
    }

    private void SetupMaterial()
    {
        // URP/Lit マテリアルを設定
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");
        if (urpLit == null) return;

        var mat = new Material(urpLit);
        _lr.material = mat;

        // グラデーション（根元から先端）
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(ropeColor, 0f),
                new GradientColorKey(ropeTipColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 1f)
            }
        );
        _lr.colorGradient = gradient;

        _lr.startWidth = startWidth;
        _lr.endWidth = endWidth;
        _lr.numCapVertices = 4;
        _lr.numCornerVertices = 4;
    }

    private void LateUpdate()
    {
        if (_ropeSystem == null || !_ropeSystem.IsAttached) return;

        // 速度に応じて太さを変える
        if (_playerRb != null)
        {
            float speed = _playerRb.linearVelocity.magnitude;
            float t = Mathf.Clamp01(speed / maxSpeedForWidth);
            float mult = Mathf.Lerp(minWidthMultiplier, maxWidthMultiplier, t);
            _lr.startWidth = startWidth * mult;
            _lr.endWidth = endWidth * mult;
        }
    }

    // ロープ接続時の砂埃パーティクルを指定位置に生成
    public void SpawnAttachEffect(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.3f;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.8f, 0.7f, 0.5f, 0.8f);
            mr.material = mat;
        }

        Destroy(go.GetComponent<Collider>());
        Destroy(go, 0.3f);
    }
}
