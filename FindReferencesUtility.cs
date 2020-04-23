///  Author:    Ashley Seric contact@ashleyseric.com    |   ashleyseric.com

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AshleySeric.Utilities.Editor
{
    public class FindReferencesUtility : EditorWindow
    {
        private Dictionary<Object, List<Object>> references = new Dictionary<Object, List<Object>>();
        private Dictionary<Object, bool> foldoutStates = new Dictionary<Object, bool>();
        private Dictionary<Object, string> headingLabels = new Dictionary<Object, string>();
        private string unreferencedBtnLabel, referencedBtnLabel;
        private Color referencedCol = new Color(0.7f, 1f, 0.7f, 1f);
        private Color unreferencedCol = new Color(1f, 0.7f, 0.7f, 1f);
        private Vector2 scrollPosition = Vector2.zero;
        private GUIStyle buttonStyle = null;

        [MenuItem("Ashley Seric/Tools/Find References Utility", priority = 10)]
        public static void OpenWindow()
        {
            EditorWindow eWindow = EditorWindow.GetWindow(typeof(FindReferencesUtility));
            eWindow.titleContent = new GUIContent("Find References");
            // Get existing open window or if none, make a new one.
            FindReferencesUtility window = (FindReferencesUtility)eWindow;

            window.Show();
        }

        private void OnGUI()
        {
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle("Button");
                buttonStyle.alignment = TextAnchor.MiddleLeft;
            }

            using (new EditorGUILayout.HorizontalScope()) // For horizontal edge padding.
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(20);

                    using (new EditorGUILayout.HorizontalScope()) // For centering.
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("  Find References to Selected  ", GUILayout.Height(30)))
                        {
                            if (Selection.objects.Length > 0)
                            {
                                // Filter out selected directories.
                                var filteredSelection = Selection.objects.ToList();
                                for (int i = 0; i < filteredSelection.Count; i++)
                                {
                                    if (System.IO.Directory.Exists(AssetDatabase.GetAssetPath(filteredSelection[i])))
                                    {
                                        filteredSelection.RemoveAt(i);
                                    }
                                }

                                references = FindReferencesTo(filteredSelection.ToArray());
                                unreferencedBtnLabel = string.Format("  Select {0} Unreferenced  ", references.Count(x => x.Value.Count == 0));
                                referencedBtnLabel = string.Format("  Select {0} Referenced  ", references.Count(x => x.Value.Count > 0));
                                // Initialise foldout states and heading labels.
                                headingLabels = new Dictionary<Object, string>();
                                foldoutStates = new Dictionary<Object, bool>();
                                var unityEngineChars = ("UnityEngine.").ToCharArray();
                                foreach (var obj in references.Keys)
                                {
                                    foldoutStates.Add(obj, false);
                                    headingLabels.Add(obj, string.Format("[{0}]   {1}   ({2})", obj.GetType().ToString().TrimStart(unityEngineChars), obj.name, references[obj].Count));
                                }
                            }
                            else
                            {
                                references = new Dictionary<Object, List<Object>>();
                            }
                        }
                        if (references.Count > 0)
                        {
                            GUI.color = referencedCol;
                            if (GUILayout.Button(referencedBtnLabel, buttonStyle, GUILayout.Height(30)))
                            {
                                Selection.objects = references.Where(x => x.Value.Count > 0).Select(x => x.Key).ToArray();
                            }
                            GUI.color = unreferencedCol;
                            if (GUILayout.Button(unreferencedBtnLabel, buttonStyle, GUILayout.Height(30)))
                            {
                                Selection.objects = references.Where(x => x.Value.Count == 0).Select(x => x.Key).ToArray();
                            }
                            GUI.color = Color.white;
                        }
                        GUILayout.FlexibleSpace();
                    }

                    GUILayout.Space(20);

                    using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition))
                    {
                        scrollPosition = scroll.scrollPosition;

                        foreach (var keyValue in references)
                        {
                            bool hasReferences = keyValue.Value.Count > 0;

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                // Tint green if referenced, red if not.
                                GUI.color = keyValue.Value.Count > 0 ? referencedCol : unreferencedCol;

                                // Foldout arrow button
                                if (hasReferences && GUILayout.Button(foldoutStates[keyValue.Key] ? "▼" : "►", GUILayout.Height(25), GUILayout.Width(30)))
                                {
                                    foldoutStates[keyValue.Key] = !foldoutStates[keyValue.Key];
                                }

                                // Object that's referenced button (to select that object).
                                if (GUILayout.Button(headingLabels[keyValue.Key], buttonStyle, GUILayout.Height(25), GUILayout.ExpandWidth(true)))
                                {
                                    Selection.activeObject = keyValue.Key;
                                }
                                GUI.color = Color.white;
                            }

                            if (hasReferences && foldoutStates[keyValue.Key])
                            {
                                GUILayout.Space(5);
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    GUILayout.Space(50); // Padding.
                                    
                                    using (new EditorGUILayout.VerticalScope())
                                    {
                                        foreach (var obj in keyValue.Value)
                                        {
                                            // Button to select any of the scene objects referencing this object.
                                            if (GUILayout.Button(obj.name, buttonStyle, GUILayout.ExpandWidth(true)))
                                            {
                                                Selection.activeObject = obj;
                                            }
                                        }
                                    }
                                    GUILayout.Space(50); // Padding.
                                }
                                GUILayout.Space(20);
                            }
                        }
                    }
                    GUILayout.Space(10);
                }
            }
        }

        private static Dictionary<Object, List<Object>> FindReferencesTo(Object[] objects)
        {
            Dictionary<Object, List<Object>> references = new Dictionary<Object, List<Object>>();
            
            try
            {                
                // Collect all scene objects (even those that are disabled).
                List<GameObject> allSceneObjects = new List<GameObject>();
                
                foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    allSceneObjects.Add(item);
                    GetChildrenRecursive(ref allSceneObjects, item.transform);
                }

                references = FindReferences(objects, allSceneObjects, null);
                EditorUtility.ClearProgressBar();
            }
            catch (System.Exception e) // Ensure progress bar is cleared if we hit an error.
            {
                Debug.LogError(e + e.StackTrace);
                EditorUtility.ClearProgressBar();
            }

            return references;
        }

        private static void GetChildrenRecursive(ref List<GameObject> res, Transform t)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                res.Add(child.gameObject);
                GetChildrenRecursive(ref res, child);
            }
        }

        private static Dictionary<Object, List<Object>> FindReferences(Object[] referencees, List<GameObject> potentialReferencers, System.Action onCancelButtonClicked = null)
        {
            var res = new Dictionary<Object, List<Object>>();
            GameObject go = null;

            for (int j = 0; j < potentialReferencers.Count; j++)
            {
                // Only update progress bar every 50 objects for a bit of a speed boost (little bit of overhead calling this).
                if (j % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("Scanning for references", "This may take a lil' bit...", (float) j / (float) potentialReferencers.Count))
                {
                    if (onCancelButtonClicked != null)
                    {
                        onCancelButtonClicked();
                    }
                    break;
                }

                go = potentialReferencers[j];

                if (PrefabUtility.GetPrefabType(go) == PrefabType.PrefabInstance)
                {
                    CollectMatches(ref res, PrefabUtility.GetPrefabParent(go), go, referencees);
                }

                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    var c = components[i];
                    if (c == null)
                    {
                        continue;
                    }

                    var so = new SerializedObject(c);
                    var sp = so.GetIterator();

                    while (sp.NextVisible(true))
                    {
                        if (sp.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            CollectMatches(ref res, sp.objectReferenceValue, go, referencees);
                            
                            if (sp.objectReferenceValue != null && sp.objectReferenceValue.GetType() == typeof(Material)) // Iterate through the material's properties.
                            {
                                var mat = sp.objectReferenceValue as Material;
                                
                                for (int k = 0; k < ShaderUtil.GetPropertyCount(mat.shader); k++)
                                {
                                    if (ShaderUtil.GetPropertyType(mat.shader, k) == ShaderUtil.ShaderPropertyType.TexEnv)
                                    {
                                        CollectMatches(ref res, mat.GetTexture(Shader.PropertyToID(ShaderUtil.GetPropertyName(mat.shader, k))), go, referencees);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return res;
        }

        public static void CollectMatches(ref Dictionary<Object, List<Object>> output, Object potentialReference, GameObject referencingObject, Object[] objectsToReference, bool collectUnreferencedObjects = true)
        {
            if (potentialReference != null)
            {
                foreach (var referencee in objectsToReference)
                {
                    if (potentialReference == referencee)
                    {
                        if (output.ContainsKey(referencee))
                        {
                            if (!output[referencee].Contains(referencingObject))
                            {
                                output[referencee].Add(referencingObject);
                            }
                        }
                        else
                        {
                            output.Add(referencee, new List<Object>() { referencingObject });
                        }
                    }
                    else if (collectUnreferencedObjects)
                    {
                        // Ensure we have an entry for each object, even if it has no references (allows finding unreferenced objects).
                        if (!output.ContainsKey(referencee))
                        {
                            output.Add(referencee, new List<Object>());
                        }
                    }
                }
            }
        }
    }
}