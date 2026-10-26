using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Meta store manifest rules that Unity doesn't set on its own:
///  - the launch activity must have android:excludeFromRecents="true"
///  - android:installLocation must be "auto" (also set in Player Settings; enforced here too)
/// Runs on every Android build, after Unity generates the Gradle project.
/// </summary>
public class QuestManifestFix : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 999;

    private const string AndroidNs = "http://schemas.android.com/apk/res/android";

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // 'path' is the unityLibrary module; the launcher module sits next to it.
        string root = Path.GetDirectoryName(path);
        Patch(Path.Combine(path, "src/main/AndroidManifest.xml"));
        Patch(Path.Combine(root, "launcher/src/main/AndroidManifest.xml"));
    }

    private static void Patch(string file)
    {
        if (!File.Exists(file)) return;
        var doc = new XmlDocument();
        doc.Load(file);
        bool changed = false;

        var manifest = doc.DocumentElement;
        if (manifest != null && manifest.HasAttribute("installLocation", AndroidNs) &&
            manifest.GetAttribute("installLocation", AndroidNs) != "auto")
        {
            manifest.SetAttribute("installLocation", AndroidNs, "auto");
            changed = true;
        }

        foreach (XmlNode node in doc.GetElementsByTagName("activity"))
        {
            var activity = node as XmlElement;
            if (activity == null || !IsLaunchActivity(activity)) continue;
            if (activity.GetAttribute("excludeFromRecents", AndroidNs) != "true")
            {
                activity.SetAttribute("excludeFromRecents", AndroidNs, "true");
                changed = true;
            }
        }

        if (changed)
        {
            doc.Save(file);
            Debug.Log("[QuestManifestFix] Patched " + file);
        }
    }

    private static bool IsLaunchActivity(XmlElement activity)
    {
        foreach (XmlNode f in activity.GetElementsByTagName("intent-filter"))
            foreach (XmlNode c in f.ChildNodes)
            {
                var e = c as XmlElement;
                if (e != null && e.Name == "action" && e.GetAttribute("name", AndroidNs) == "android.intent.action.MAIN")
                    return true;
            }
        return false;
    }
}
