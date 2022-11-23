// Terrain exporter
// Modified by REghZy/AngryCarrot789 to support multiple terrains :)
// ----------------------------------------------------
// Converted from UnityScript to C# at http://www.M2H.nl/files/js_to_c.php - by Mike Hergaarden
// C # manual conversion work by Yun Kyu Choi
// https://github.com/highfidelity/unity-to-hifi-exporter/blob/master/Assets/UnityToHiFiExporter/Editor/TerrainObjExporter.cs

using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

internal enum SaveFormat {
    Triangles, Quads
}

internal enum SaveResolution {
    Full=0,
    Half,
    Quarter,
    Eighth,
    Sixteenth
}

internal class ExportTerrain : EditorWindow {
    //private static TerrainData terrain;
    // private static Vector3 terrainPos;

    private static List<Terrain> OUTPUT_TERRAINS;
    private static SaveFormat SAVE_FORMAT = SaveFormat.Triangles;
    private static SaveResolution SAVE_RESOLUTION = SaveResolution.Half;
    private static bool SAVE_POS = true;
    private static bool FLIP_POS_X = false;
    private static bool FLIP_POS_Y = false;
    private static bool FLIP_POS_Z = false;
    private static bool FLIP_POS_BEFORE_TERRAIN_POS = false;
    private static bool FLIP_SCALE_X = false;
    private static bool FLIP_SCALE_Y = false;
    private static bool FLIP_SCALE_Z = false;

    // false if none available
    private static bool CheckTerrainCount() {
        if (OUTPUT_TERRAINS == null || OUTPUT_TERRAINS.Count == 0) {
            Debug.Log("You have not selected any terrains");
            return false;
        }

        return true;
    }

    [MenuItem("Terrain/Export To Obj...")]
    private static void Init() {
        OUTPUT_TERRAINS?.Clear();
        OUTPUT_TERRAINS = new List<Terrain>();

        foreach (UnityEngine.Object value in Selection.objects) {
            if (value is GameObject obj && obj.TryGetComponent(out Terrain terrain)) {
                OUTPUT_TERRAINS.Add(terrain);
            }
        }

        if (!CheckTerrainCount()) {
            return;
        }

        Debug.Log($"{OUTPUT_TERRAINS.Count} terrains selected");

        GetWindow<ExportTerrain>().Show();
    }

    private void OnGUI() {
        if (CheckTerrainCount()) {
            SAVE_FORMAT = (SaveFormat) EditorGUILayout.EnumPopup("Export Format", SAVE_FORMAT);
            SAVE_RESOLUTION = (SaveResolution) EditorGUILayout.EnumPopup("Resolution", SAVE_RESOLUTION);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Saved the terrain's position in your world. This may ");
            EditorGUILayout.LabelField("look weird, because the terrain's center will be still ");
            EditorGUILayout.LabelField("be at 0,0,0 but the actual terrain may be away. This ");
            EditorGUILayout.LabelField("is more like 'use the terrain's position as an offset'");
            SAVE_POS = EditorGUILayout.Toggle("Save Terrain Positions", SAVE_POS);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Flips the vertices' positions along specific axis");
            FLIP_POS_X = EditorGUILayout.Toggle("Flip verts along X", FLIP_POS_X);
            FLIP_POS_Y = EditorGUILayout.Toggle("Flip verts along Y", FLIP_POS_Y);
            FLIP_POS_Z = EditorGUILayout.Toggle("Flip verts along Z", FLIP_POS_Z);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("If saving the terrain's position is enabled, then this");
            EditorGUILayout.LabelField("determines if the vertices' positions are flipped before");
            EditorGUILayout.LabelField("or after the terrain's position is added");
            FLIP_POS_BEFORE_TERRAIN_POS = EditorGUILayout.Toggle("Flip before terrain pos", FLIP_POS_BEFORE_TERRAIN_POS);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Flip the scale of the terrain's scale");
            FLIP_SCALE_X = EditorGUILayout.Toggle("Flip Scale Along X", FLIP_SCALE_X);
            FLIP_SCALE_Y = EditorGUILayout.Toggle("Flip Scale Along Y", FLIP_SCALE_Y);
            FLIP_SCALE_Z = EditorGUILayout.Toggle("Flip Scale Along Z", FLIP_SCALE_Z);

            if (GUILayout.Button("Export")) {
                ExportAll();
            }
        }
    }

    private static void ExportAll() {
        if (!CheckTerrainCount()) {
            return;
        }

        string folder = EditorUtility.SaveFolderPanel("Select a folder to export the terrain objects to", "", "my-terrains");
        if (!Directory.Exists(folder)) {
            GUILayout.Label("Directory does not exist: " + folder);
            return;
        }

        if (OUTPUT_TERRAINS.Any(a => string.IsNullOrEmpty(a.name))) {
            GUILayout.Label("One of the terrains does not have a name. Give your terrains a name first, then export");
            return;
        }

        Debug.Log($"Writing {OUTPUT_TERRAINS.Count} terrains to folder: {folder}");
        int success = 0;

        foreach (Terrain terrain in OUTPUT_TERRAINS) {
            success += Export(terrain, folder) ? 1 : 0;
        }

        Debug.Log($"Successfully wrote {success}/{OUTPUT_TERRAINS.Count} terrains");

        GetWindow<ExportTerrain>().Close();
    }

    private static bool Export(Terrain terrainObj, string folder) {
        try {
            TerrainData terrain = terrainObj.terrainData;
            int w = terrain.heightmapResolution;
            int h = terrain.heightmapResolution;

            Vector3 meshScale = terrain.size;
            int resolution = (int) Mathf.Pow(2, (int) SAVE_RESOLUTION);
            meshScale = new Vector3(meshScale.x / (w - 1) * resolution, meshScale.y, meshScale.z / (h - 1) * resolution);
            Vector2 uvScale = new Vector2(1.0f / (w - 1), 1.0f / (h - 1));
            float[,] data = terrain.GetHeights(0, 0, w, h);

            w = (w - 1) / resolution + 1;
            h = (h - 1) / resolution + 1;
            Vector3[] verts = new Vector3[w * h];
            Vector2[] uvs = new Vector2[w * h];

            int[] polys = SAVE_FORMAT == SaveFormat.Triangles ? new int[(w - 1) * (h - 1) * 6] : new int[(w - 1) * (h - 1) * 4];
            Vector3 pos = terrainObj.transform.position;
            // Build vertices and UVs

            bool savePos = SAVE_POS;
            bool flipX = FLIP_POS_X, flipY = FLIP_POS_Y, flipZ = FLIP_POS_Z;
            bool flipScaleX = FLIP_SCALE_X,flipScaleY = FLIP_SCALE_Y, flipScaleZ = FLIP_SCALE_Z;
            bool flipBeforePos = FLIP_POS_BEFORE_TERRAIN_POS;
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    Vector3 scaledPosition = Vector3.Scale(meshScale, new Vector3(-y, data[x * resolution, y * resolution], x));
                    // Could possibly increase performance by storing static fields locally?
                    void Flip() {
                        if (flipX)
                            scaledPosition.x = -scaledPosition.x;
                        if (flipY)
                            scaledPosition.y = -scaledPosition.y;
                        if (flipZ)
                            scaledPosition.z = -scaledPosition.z;
                    }

                    if (savePos) {
                        if (flipBeforePos) {
                            Flip();
                            scaledPosition += pos;
                        }
                        else {
                            scaledPosition += pos;
                            Flip();
                        }
                    }
                    else {
                        Flip();
                    }

                    Vector3 scaledScale = Vector2.Scale(new Vector2(x * resolution, y * resolution), uvScale);
                    if (flipScaleX)
                        scaledScale.x = -scaledScale.x;
                    if (flipScaleY)
                        scaledScale.y = -scaledScale.y;
                    if (flipScaleZ)
                        scaledScale.z = -scaledScale.z;

                    verts[y * w + x] = scaledPosition;
                    uvs[y * w + x] = scaledScale;
                }
            }

            int index = 0;
            if (SAVE_FORMAT == SaveFormat.Triangles) {
                // Build triangle indices: 3 indices into vertex array for each triangle
                for (int y = 0; y < h - 1; ++y) {
                    for (int x = 0; x < w - 1; ++x) {
                        // For each grid cell output two triangles
                        polys[index++] = (y * w) + x;
                        polys[index++] = ((y + 1) * w) + x;
                        polys[index++] = (y * w) + x + 1;

                        polys[index++] = ((y + 1) * w) + x;
                        polys[index++] = ((y + 1) * w) + x + 1;
                        polys[index++] = (y * w) + x + 1;
                    }
                }
            }
            else {
                // Build quad indices: 4 indices into vertex array for each quad
                for (int y = 0; y < h - 1; ++y) {
                    for (int x = 0; x < w - 1; ++x) {
                        // For each grid cell output one quad
                        polys[index++] =  (y * w) + x;
                        polys[index++] = ((y + 1) * w) + x;
                        polys[index++] = ((y + 1) * w) + x + 1;
                        polys[index++] =  (y * w) + x + 1;
                    }
                }
            }

            // Export to .obj

            using (StreamWriter stream = new StreamWriter(new BufferedStream(File.OpenWrite(Path.Combine(folder, terrainObj.name + ".obj")), 16384))) {
                stream.WriteLine("# Unity terrain OBJ File");

                // Write vertices
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                for (int i = 0, len = verts.Length; i < len; i++) {
                    StringBuilder sb = new StringBuilder("v ", 20);
                    // StringBuilder stuff is done this way because it's faster than using the "{0} {1} {2}" etc. format
                    // Which is important when you're exporting huge terrains.
                    sb.Append(verts[i].x.ToString()).Append(" ").
                       Append(verts[i].y.ToString()).Append(" ").
                       Append(verts[i].z.ToString());
                    stream.WriteLine(sb);
                }

                // Write UVs
                for (int i = 0, len = uvs.Length; i < len; i++) {
                    StringBuilder sb = new StringBuilder("vt ", 22);
                    sb.Append(uvs[i].x.ToString()).Append(" ").
                       Append(uvs[i].y.ToString());
                    stream.WriteLine(sb);
                }

                if (SAVE_FORMAT == SaveFormat.Triangles) {
                    // Write triangles
                    for (int i = 0, len = polys.Length; i < len; i += 3) {
                        StringBuilder sb = new StringBuilder("f ", 43);
                        sb.Append(polys[i] + 1).Append("/").Append(polys[i] + 1).Append(" ").
                           Append(polys[i + 1] + 1).Append("/").Append(polys[i + 1] + 1).Append(" ").
                           Append(polys[i + 2] + 1).Append("/").Append(polys[i + 2] + 1);
                        stream.WriteLine(sb);
                    }
                }
                else {
                    // Write quads
                    for (int i = 0, len = polys.Length; i < len; i += 4) {
                        StringBuilder sb = new StringBuilder("f ", 57);
                        sb.Append(polys[i] + 1).Append("/").Append(polys[i] + 1).Append(" ").
                           Append(polys[i + 1] + 1).Append("/").Append(polys[i + 1] + 1).Append(" ").
                           Append(polys[i + 2] + 1).Append("/").Append(polys[i + 2] + 1).Append(" ").
                           Append(polys[i + 3] + 1).Append("/").Append(polys[i + 3] + 1);
                        stream.WriteLine(sb);
                    }
                }
            }

            return true;
        }
        catch (Exception e) {
            Debug.Log("Error writing terrain: " + e);
        }

        return false;
    }
}
