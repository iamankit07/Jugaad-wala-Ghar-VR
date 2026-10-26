#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Adds the in-game pause menu (opened with the left controller's MENU button) (RESUME / CONTROLS / RESTART / MAIN MENU / QUIT) to the
/// level scene, plus UI-only controller rays that only exist while the menu is open (MenuRays).
/// Menu: Jugaad > Add Pause Menu To Game Scene
/// </summary>
public static class PauseMenuBuilder
{
    const string GameScenePath = "Assets/Scenes/SampleScene.unity";
    const string RigPath = "Assets/Samples/XR Interaction Toolkit/3.0.11/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string IconPath = "Assets/settings_932dp_FFFFFF_FILL0_wght400_GRAD0_opsz48.png";
    const string RayMatPath = "Assets/_Jugaad/Materials/UI_Ray.mat";

    static readonly Color Orange = new Color(0.95f, 0.64f, 0.23f);
    static readonly Color OrangeHi = new Color(1f, 0.76f, 0.40f);
    static readonly Color OrangeDown = new Color(0.78f, 0.50f, 0.16f);
    static readonly Color Btn = new Color(0.20f, 0.22f, 0.29f);
    static readonly Color BtnHi = new Color(0.30f, 0.33f, 0.42f);
    static readonly Color Red = new Color(0.75f, 0.25f, 0.22f);
    static readonly Color RedHi = new Color(0.88f, 0.34f, 0.30f);
    static readonly Color Dark = new Color(0.09f, 0.10f, 0.14f, 0.95f);

    static TMP_FontAsset _font;
    static Sprite _round, _circle;

    [MenuItem("Jugaad/Add Pause Menu To Game Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();     // works on whichever level is open (Level 1, Garage...)
        if (scene.path.EndsWith("MainMenu.unity")) { Debug.LogError("[PauseMenuBuilder] Open a level scene first."); return; }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // remove an older copy so the builder can be re-run safely
        foreach (var n in new[] { "PauseMenu", "PauseMenuCanvas" })
        {
            var old = GameObject.Find(n);
            if (old) Object.DestroyImmediate(old);
        }
        var hud = GameObject.Find("TaskHUD");
        if (hud == null) { Debug.LogError("[PauseMenuBuilder] TaskHUD not found."); return; }
        var oldIcon = hud.transform.Find("SettingsButton");
        if (oldIcon) Object.DestroyImmediate(oldIcon.gameObject);

        EnsureEventSystem();
        AddUIRays();

        // ---------- pause panel ----------
        var pause = WorldCanvas("PauseMenuCanvas", null, new Vector2(700, 800));
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(pause, false);
        var brt = (RectTransform)bg.transform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
        var bimg = bg.GetComponent<Image>();
        bimg.sprite = _round; bimg.type = Image.Type.Sliced; bimg.pixelsPerUnitMultiplier = 0.25f; bimg.color = Dark;

        // main page: the buttons
        var main = Page(pause, "MainPage");
        MakeText(main, "Title", "SETTINGS", 64, Orange, new Vector2(0, 320), new Vector2(640, 90), FontStyles.Bold);
        var resume = MakeButton(main, "ResumeButton", "RESUME", new Vector2(0, 205), new Vector2(480, 90), 42, Orange, OrangeHi, OrangeDown, _round, new Color(0.12f, 0.08f, 0.04f));
        var controls = MakeButton(main, "ControlsButton", "CONTROLS", new Vector2(0, 100), new Vector2(480, 90), 42, Btn, BtnHi, new Color(0.14f, 0.15f, 0.2f), _round);
        var restart = MakeButton(main, "RestartButton", "RESTART", new Vector2(0, -5), new Vector2(480, 90), 42, Btn, BtnHi, new Color(0.14f, 0.15f, 0.2f), _round);
        var menu = MakeButton(main, "MainMenuButton", "MAIN MENU", new Vector2(0, -110), new Vector2(480, 90), 42, Btn, BtnHi, new Color(0.14f, 0.15f, 0.2f), _round);
        var quit = MakeButton(main, "QuitButton", "QUIT", new Vector2(0, -215), new Vector2(480, 90), 42, Red, RedHi, new Color(0.6f, 0.18f, 0.16f), _round);
        var hint = MakeText(main, "Hint", "Left controller ka MENU button se bhi khulta / band hota hai", 24,
                 new Color(0.65f, 0.67f, 0.73f), new Vector2(0, -320), new Vector2(640, 60), FontStyles.Normal);
        var loc = hint.gameObject.AddComponent<LocalizedText>();
        loc.english = "The MENU button on the left controller also opens / closes this";
        loc.hinglish = "Left controller ka MENU button se bhi khulta / band hota hai";

        // controls page: what every button does (rows are filled in by ControlsInfo at runtime)
        var ctl = Page(pause, "ControlsPage");
        MakeText(ctl, "Title", "CONTROLS", 50, Orange, new Vector2(0, 350), new Vector2(640, 70), FontStyles.Bold);
        var basicsTab = MakeButton(ctl, "BasicsTab", "BASICS", new Vector2(-140, 282), new Vector2(270, 54), 28, Orange, OrangeHi, OrangeDown, _round, new Color(0.12f, 0.08f, 0.04f));
        var carTab = MakeButton(ctl, "CarTab", "CAR & TOOLS", new Vector2(140, 282), new Vector2(270, 54), 28, Btn, BtnHi, new Color(0.14f, 0.15f, 0.2f), _round);
        var rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(ctl, false);
        ((RectTransform)rows.transform).sizeDelta = Vector2.zero;
        var back = MakeButton(ctl, "BackButton", "BACK", new Vector2(0, -330), new Vector2(300, 70), 36, Orange, OrangeHi, OrangeDown, _round, new Color(0.12f, 0.08f, 0.04f));
        var info = ctl.gameObject.AddComponent<ControlsInfo>();
        var iso = new SerializedObject(info);
        iso.FindProperty("rows").objectReferenceValue = rows.transform;
        iso.FindProperty("font").objectReferenceValue = _font;
        iso.FindProperty("basicsTab").objectReferenceValue = basicsTab;
        iso.FindProperty("carTab").objectReferenceValue = carTab;
        iso.ApplyModifiedPropertiesWithoutUndo();
        ctl.gameObject.SetActive(false);
        pause.gameObject.SetActive(false);

        // ---------- logic ----------
        var mgr = new GameObject("PauseMenu");
        var pm = mgr.AddComponent<PauseMenu>();
        var so = new SerializedObject(pm);
        so.FindProperty("panel").objectReferenceValue = pause.gameObject;
        so.FindProperty("mainPage").objectReferenceValue = main.gameObject;
        so.FindProperty("controlsPage").objectReferenceValue = ctl.gameObject;
        so.FindProperty("hideWhileOpen").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        // hand rays only while the menu is open (it opens with the left controller's MENU button only)
        var mr = mgr.AddComponent<MenuRays>();
        var mso = new SerializedObject(mr);
        mso.FindProperty("menu").objectReferenceValue = pm;
        mso.ApplyModifiedPropertiesWithoutUndo();

        // the rays only hit the UI layer (raycastMask), so both canvases must live on it
        SetLayer(pause.gameObject, 5);
        pause.GetComponent<Canvas>().sortingOrder = 50;   // drawn over the task panel / phone UI

        UnityEventTools.AddPersistentListener(resume.onClick, new UnityAction(pm.Resume));
        UnityEventTools.AddPersistentListener(controls.onClick, new UnityAction(pm.ShowControls));
        UnityEventTools.AddPersistentListener(restart.onClick, new UnityAction(pm.Restart));
        UnityEventTools.AddPersistentListener(menu.onClick, new UnityAction(pm.GoToMainMenu));
        UnityEventTools.AddPersistentListener(quit.onClick, new UnityAction(pm.Quit));
        UnityEventTools.AddPersistentListener(back.onClick, new UnityAction(pm.ShowMain));
        UnityEventTools.AddPersistentListener(basicsTab.onClick, new UnityAction(info.ShowBasics));
        UnityEventTools.AddPersistentListener(carTab.onClick, new UnityAction(info.ShowCar));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PauseMenuBuilder] Settings button + pause menu added to " + scene.path);
    }

    static void EnsureEventSystem()
    {
        var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<UnityEngine.EventSystems.EventSystem>();
        foreach (var m in es.GetComponents<UnityEngine.EventSystems.BaseInputModule>())
            if (!(m is XRUIInputModule)) Object.DestroyImmediate(m);
        if (es.GetComponent<XRUIInputModule>() == null) es.gameObject.AddComponent<XRUIInputModule>();
    }

    /// UI-only rays on each controller: can't grab anything, and are invisible unless pointing at UI.
    static void AddUIRays()
    {
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
        var starterNearFar = rigPrefab ? rigPrefab.GetComponentsInChildren<NearFarInteractor>(true) : new NearFarInteractor[0];
        var mat = RayMaterial();

        foreach (var direct in Object.FindObjectsByType<XRDirectInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var parent = direct.transform.parent;
            if (parent == null) continue;
            var existing = parent.Find("UI Ray");
            if (existing) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject("UI Ray");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.widthMultiplier = 1f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.useWorldSpace = true;

            var ray = go.AddComponent<XRRayInteractor>();
            ray.handedness = direct.handedness;
            ray.interactionLayers = 0;                  // UI only - never grabs 3D objects
            ray.enableUIInteraction = true;
            ray.maxRaycastDistance = 6f;
            ray.raycastMask = LayerMask.GetMask("UI"); // skip physics hits (nothing lives on UI)

            foreach (var nf in starterNearFar)
            {
                if (nf.handedness != direct.handedness) continue;
                var reader = new XRInputButtonReader("UI Press");
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(nf.uiPressInput), reader);
                ray.uiPressInput = reader;              // trigger clicks buttons
                break;
            }

            var vis = go.AddComponent<XRInteractorLineVisual>();
            vis.lineWidth = 0.004f;
            vis.stopLineAtFirstRaycastHit = true;
            var valid = new Gradient();
            valid.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.4f, 1f) });
            // the ray only exists while the menu is open (MenuRays), so show a faint line even off the panel
            var invisible = new Gradient();
            invisible.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                              new[] { new GradientAlphaKey(0.45f, 0f), new GradientAlphaKey(0f, 1f) });
            vis.validColorGradient = valid;
            vis.invalidColorGradient = invisible;
            EditorUtility.SetDirty(go);
        }
    }

    static Material RayMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(RayMatPath);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(m, RayMatPath);
        }
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        m.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Sprite ImportIcon()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 256;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    static void SetLayer(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    static RectTransform Page(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform WorldCanvas(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent) go.transform.SetParent(parent, false);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        go.AddComponent<TrackedDeviceGraphicRaycaster>();
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one * 0.001f;
        return rt;
    }

    static TextMeshProUGUI MakeText(RectTransform parent, string name, string text, float size, Color color,
                                    Vector2 pos, Vector2 box, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font) t.font = _font;
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = box;
        return t;
    }

    static Button MakeButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, float fontSize,
                             Color normal, Color hi, Color down, Sprite sprite, Color? labelColor = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite; img.type = sprite == _round ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = 0.35f;
        img.color = Color.white;
        var b = go.GetComponent<Button>();
        var cb = b.colors;
        cb.normalColor = normal; cb.highlightedColor = hi; cb.selectedColor = normal; cb.pressedColor = down;
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
        b.colors = cb;
        if (!string.IsNullOrEmpty(label))
        {
            var t = MakeText(rt, "Label", label, fontSize, labelColor ?? new Color(0.94f, 0.94f, 0.96f), Vector2.zero, size, FontStyles.Bold);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        }
        return b;
    }
}
#endif
