using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// VR-safe scene change: a black sphere around the head fades in (no camera motion), an optional
/// line like 'The next day...' shows on black, the next scene loads, then the black fades away.
/// Survives the scene load and always sits on whichever camera is rendering.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static bool Busy { get; private set; }

    private Material _fade;
    private TextMeshPro _text;
    private Transform _cam;
    private string _scene;
    private string _line;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static void Begin(string scene, string line, Material fadeMaterial, TMP_FontAsset font, Material textMaterial)
    {
        if (Busy) return;
        Busy = true;
        Time.timeScale = 1f;
        var go = new GameObject("SceneTransition");
        DontDestroyOnLoad(go);
        var t = go.AddComponent<SceneTransition>();
        t._scene = scene;
        t._line = line;
        t.Build(fadeMaterial, font, textMaterial);
        t.StartCoroutine(t.Run());
    }

    private void Build(Material fadeMaterial, TMP_FontAsset font, Material textMaterial)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.name = "Fade";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * 0.6f;
        var r = sphere.GetComponent<MeshRenderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingOrder = 32000;   // world-space UI with a sorting order (score card, menus) must not show through
        _fade = new Material(fadeMaterial);
        _fade.SetColor(ColorId, new Color(0f, 0f, 0f, 0f));
        r.sharedMaterial = _fade;

        if (!string.IsNullOrEmpty(_line) && font != null)
        {
            var tgo = new GameObject("Line");
            tgo.transform.SetParent(transform, false);
            tgo.transform.localPosition = new Vector3(0f, 0f, 1.4f);
            _text = tgo.AddComponent<TextMeshPro>();
            _text.font = font;
            if (textMaterial) _text.fontSharedMaterial = textMaterial;
            _text.text = _line;
            _text.fontSize = 0.6f;
            _text.alignment = TextAlignmentOptions.Center;
            _text.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
            _text.color = new Color(1f, 0.93f, 0.8f, 0f);
            _text.GetComponent<MeshRenderer>().sortingOrder = 32001;   // on top of the black
        }
    }

    private void OnEnable()  { RenderPipelineManager.beginCameraRendering += Follow; }
    private void OnDisable() { RenderPipelineManager.beginCameraRendering -= Follow; }

    private void Follow(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam.cameraType != CameraType.Game) return;
        transform.SetPositionAndRotation(cam.transform.position, Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f));
    }

    private IEnumerator Run()
    {
        float vol = AudioListener.volume;
        yield return Fade(0f, 1f, 0.9f, vol, 0f);

        AsyncOperation op = SceneManager.LoadSceneAsync(_scene, LoadSceneMode.Single);
        if (op == null)
        {
            Debug.LogError("[SceneTransition] scene '" + _scene + "' is not in Build Settings");
            AudioListener.volume = vol;
            yield return Fade(1f, 0f, 0.6f, 0f, vol);
            Finish();
            yield break;
        }
        op.allowSceneActivation = false;

        if (_text)
        {
            yield return TextAlpha(0f, 1f, 0.5f);
            yield return new WaitForSecondsRealtime(1.6f);
        }
        while (op.progress < 0.9f) yield return null;
        if (_text) yield return TextAlpha(1f, 0f, 0.4f);

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.4f);   // let the new scene settle (XR rig, physics)
        yield return Fade(1f, 0f, 1.1f, 0f, vol);
        Finish();
    }

    private void Finish()
    {
        Busy = false;
        Destroy(gameObject);
    }

    private IEnumerator Fade(float a, float b, float time, float volA, float volB)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            _fade.SetColor(ColorId, new Color(0f, 0f, 0f, Mathf.Lerp(a, b, k)));
            AudioListener.volume = Mathf.Lerp(volA, volB, k);
            yield return null;
        }
        _fade.SetColor(ColorId, new Color(0f, 0f, 0f, b));
        AudioListener.volume = volB;
    }

    private IEnumerator TextAlpha(float a, float b, float time)
    {
        var c = _text.color;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            c.a = Mathf.Lerp(a, b, t / time);
            _text.color = c;
            yield return null;
        }
        c.a = b;
        _text.color = c;
    }
}
