using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;
using System.IO;

public static class CoverageInspector
{
    [MenuItem("Tools/Generate Coverage Report V2")]
    public static void GenerateReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("## Safe Area null 실행 경로");
        sb.AppendLine("- CameraBackgroundConstraint.cs의 safeArea == null 분기 확인 요망");
        
        sb.AppendLine("\n## BG Sprite 실제 데이터");
        var bgObj = GameObject.Find("pixel_background_elven-hall_bg");
        if (bgObj != null)
        {
            var sr = bgObj.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                var sprite = sr.sprite;
                sb.AppendLine($"- Texture 크기: {sprite.texture.width} x {sprite.texture.height}");
                sb.AppendLine($"- Pixels Per Unit: {sprite.pixelsPerUnit}");
                sb.AppendLine($"- Sprite Rect: {sprite.rect}");
                sb.AppendLine($"- Pivot: {sprite.pivot} (Normalized: {new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height)})");
                
                var so = new SerializedObject(sprite);
                var meshTypeProp = so.FindProperty("m_MeshType"); // 0=full rect, 1=tight
                string meshType = meshTypeProp != null ? (meshTypeProp.intValue == 0 ? "FullRect" : "Tight") : "Unknown";
                sb.AppendLine($"- Mesh Type: {meshType}");
                
                sb.AppendLine($"- Bounds Center: {sprite.bounds.center}");
                sb.AppendLine($"- Bounds Size: {sprite.bounds.size}");
                sb.AppendLine($"- Renderer Draw Mode: {sr.drawMode}");
                sb.AppendLine($"- Flip X/Y: {sr.flipX} / {sr.flipY}");
                sb.AppendLine($"- 부모 Transform: {(sr.transform.parent != null ? sr.transform.parent.name : "None")}");
                sb.AppendLine($"- lossyScale: {sr.transform.lossyScale}");
            }
        }
        else
        {
            sb.AppendLine("- bg object not found!");
        }

        sb.AppendLine("\n## 투영 좌표계와 네 모서리");
        if (bgObj != null && Camera.main != null)
        {
            Camera cam = Camera.main;
            var sr = bgObj.GetComponent<SpriteRenderer>();
            
            // bg's pivot acts as the local origin. Let's find corners relative to this pivot.
            Vector2 extents = sr.sprite.bounds.extents;
            Vector2 center = sr.sprite.bounds.center;
            
            Vector3[] localCorners = new Vector3[] {
                new Vector3(center.x - extents.x, center.y - extents.y, 0),
                new Vector3(center.x + extents.x, center.y - extents.y, 0),
                new Vector3(center.x - extents.x, center.y + extents.y, 0),
                new Vector3(center.x + extents.x, center.y + extents.y, 0)
            };
            
            Plane camPlane = new Plane(cam.transform.forward, bgObj.transform.position); // Origin is bgObj pivot
            Vector3 camRight = cam.transform.right;
            Vector3 camUp = cam.transform.up;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            sb.AppendLine("- 투영 좌표계: bgObj pivot 기준 카메라 평면 (bgObj position이 원점)");
            foreach (var lc in localCorners)
            {
                Vector3 worldPt = sr.transform.TransformPoint(lc);
                Vector3 projected = camPlane.ClosestPointOnPlane(worldPt);
                Vector3 localToPlane = projected - bgObj.transform.position;
                
                float x = Vector3.Dot(localToPlane, camRight);
                float y = Vector3.Dot(localToPlane, camUp);
                
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                sb.AppendLine($"  - 코너 {lc} -> 투영 ({x:F4}, {y:F4})");
            }
            
            sb.AppendLine("\n## Safe Area Center/Size 재계산");
            sb.AppendLine($"- 투영 Min/Max: X [{minX:F4}, {maxX:F4}], Y [{minY:F4}, {maxY:F4}]");
            float cx = (minX + maxX) / 2f;
            float cy = (minY + maxY) / 2f;
            float w = maxX - minX;
            float h = maxY - minY;
            sb.AppendLine($"- Safe Area Center: ({cx:F4}, {cy:F4})");
            sb.AppendLine($"- Safe Area Size: ({w:F4}, {h:F4})");
        }

        sb.AppendLine("\n## 중앙 플레이 RectTransform 후보");
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            sb.AppendLine($"- Canvas 경로: {GetPath(canvas.transform)}");
            sb.AppendLine($"- Canvas Render Mode: {canvas.renderMode}");
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                sb.AppendLine($"- Canvas Scaler: UI Scale Mode={scaler.uiScaleMode}, Ref Res={scaler.referenceResolution}");
            }
            
            RectTransform[] rects = canvas.GetComponentsInChildren<RectTransform>();
            foreach(var r in rects)
            {
                string name = r.name.ToLower();
                if (name.Contains("play") || name.Contains("combat") || name.Contains("main") || name.Contains("viewport") || name.Contains("safe"))
                {
                    sb.AppendLine($"\n- 후보: {GetPath(r)}");
                    sb.AppendLine($"  - Anchor Min/Max: {r.anchorMin} / {r.anchorMax}");
                    sb.AppendLine($"  - Pivot: {r.pivot}");
                    sb.AppendLine($"  - Offset Min/Max: {r.offsetMin} / {r.offsetMax}");
                    sb.AppendLine($"  - 런타임 Rect: {r.rect}");
                }
            }
        }

        sb.AppendLine("\n## 런타임/직렬화 Projection Mode");
        if (Camera.main != null)
        {
            sb.AppendLine($"- 현재 Play Mode 런타임 Projection Mode: {(Camera.main.orthographic ? "Orthographic" : "Perspective")}");
        }
        
        // Find CameraSettings_Default in AssetDatabase
        string[] guids = AssetDatabase.FindAssets("CameraSettings_Default t:CameraSettings");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var settings = AssetDatabase.LoadAssetAtPath<InTheArena.Camera.CameraSettings>(path);
            if (settings != null)
            {
                sb.AppendLine($"- CameraSettings_Default.asset 직렬화 값: {settings.ProjMode}");
            }
        }
        
        File.WriteAllText("coverage_report.txt", sb.ToString());
        Debug.Log("Coverage Report Generated!");
    }

    private static string GetPath(Transform current)
    {
        if (current.parent == null)
            return "/" + current.name;
        return GetPath(current.parent) + "/" + current.name;
    }
}
