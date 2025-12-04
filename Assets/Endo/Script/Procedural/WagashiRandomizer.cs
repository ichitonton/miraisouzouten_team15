using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WagashiRandomizer : MonoBehaviour
{
    private Renderer[] _renderers;
    private Rigidbody _rb;
    private Collider _col;
    private WagashiRuntime _runtime;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _runtime = GetComponent<WagashiRuntime>();
        if (_runtime == null)
        {
            _runtime = gameObject.AddComponent<WagashiRuntime>();
        }
    }

    /// <summary>
    /// 外部から渡されたPresetに従ってランダム化
    /// </summary>
    public void ApplyRandomParameters(WagashiPreset preset)
    {
        if (preset == null)
        {
            Debug.LogError("WagashiRandomizer: preset is null");
            return;
        }

        _runtime.preset = preset;

        // ① カラー決定
        /*Color color = Color.white;
        if (preset.possibleColors != null && preset.possibleColors.Length > 0)
        {
            color = preset.possibleColors[Random.Range(0, preset.possibleColors.Length)];
        }

        // Rendererたちのマテリアルをインスタンス化して色を変更
        if (_renderers != null && _renderers.Length > 0)
        {
            if (preset.useSingleMaterialInstance)
            {
                // 1つだけMaterialインスタンスを作って共通で使う
                Material matInstance = Instantiate(_renderers[0].sharedMaterial);
                SetColor(matInstance, color);
                foreach (var r in _renderers)
                {
                    r.sharedMaterial = matInstance;
                }
            }
            else
            {
                // Rendererごとに別インスタンス
                foreach (var r in _renderers)
                {
                    var matInstance = Instantiate(r.sharedMaterial);
                    SetColor(matInstance, color);
                    r.sharedMaterial = matInstance;
                }
            }
        }
        */
        // ② スケールランダム(いらなそう)
        /*Vector3 scale = new Vector3(
            Random.Range(preset.minScale.x, preset.maxScale.x),
            Random.Range(preset.minScale.y, preset.maxScale.y),
            Random.Range(preset.minScale.z, preset.maxScale.z)
        );
        transform.localScale = scale;
        */

        // 質量ランダム
        float mass = Random.Range(preset.minMass, preset.maxMass);
        _rb.mass = mass;

        // 弾力ランダム（PhysicMaterial）
        if (_col != null && _col.sharedMaterial != null)
        {
            PhysicsMaterial pmInstance = Instantiate(_col.sharedMaterial);
            float bounciness = Random.Range(preset.minBounciness, preset.maxBounciness);
            pmInstance.bounciness = bounciness;
            pmInstance.bounceCombine = PhysicsMaterialCombine.Maximum;
            _col.sharedMaterial = pmInstance;

            _runtime.currentBounciness = bounciness;
        }

        // ⑤ 伸び率パラメータ（後でスクイッシュに使う）
        float stretchFactor = Random.Range(preset.minStretchFactor, preset.maxStretchFactor);

        // ⑥ WagashiRuntime に保存
        //_runtime.currentColor = color;
        _runtime.currentMass = mass;
        _runtime.currentStretchFactor = stretchFactor;
    }

    private void SetColor(Material mat, Color color)
    {
        if (mat == null) return;

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
    }
}
