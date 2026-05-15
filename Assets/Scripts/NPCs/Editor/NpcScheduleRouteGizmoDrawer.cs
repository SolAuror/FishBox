#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sol.AI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Sol.Editor
{
    internal static class NpcScheduleRouteGizmoDrawer
    {
        private const float MarkerSize = 0.35f;
        private const float ArrowSize = 0.55f;
        private static readonly Color MarkerColor = new(0.2f, 0.85f, 1f, 0.9f);
        private static readonly Color PathColor = new(0.15f, 0.65f, 1f, 0.85f);
        private static readonly Color MissingColor = new(1f, 0.45f, 0.2f, 0.9f);

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawScheduleRoute(AI_NPC npc, GizmoType gizmoType)
        {
            if (npc == null || !npc.ShowScheduleRoute || npc.ScheduleDefinition == null)
                return;

            if (npc.RoutePreviewMode == AI_NPC.ScheduleRoutePreviewMode.SelectedNpcOnly && !IsSelected(npc))
                return;

            List<RouteStop> stops = BuildStops(npc);
            if (stops.Count == 0)
                return;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            DrawPathSegments(npc, stops);
            DrawStops(npc, stops);
        }

        private static List<RouteStop> BuildStops(AI_NPC npc)
        {
            List<RouteStop> stops = new();
            IReadOnlyList<NpcScheduleEntry> entries = npc.ScheduleDefinition.Entries;
            if (entries == null)
                return stops;

            for (int i = 0; i < entries.Count; i++)
            {
                NpcScheduleEntry entry = entries[i];
                if (entry == null)
                    continue;

                RouteStop stop = new()
                {
                    Entry = entry,
                    EntryIndex = i,
                    Label = BuildLabel(entry)
                };

                if (NpcScheduleLocation.TryResolve(entry.LocationId, out NpcScheduleLocation location))
                {
                    stop.Resolved = location.TryGetNavigablePosition(ResolveAreaMask(npc), out stop.Position);
                    if (location.TryGetScheduleAnchor(out _, out Vector3 forward))
                    {
                        forward.y = 0f;
                        stop.Forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
                    }
                }

                if (!stop.Resolved)
                    stop.Position = npc.transform.position + Vector3.up * (2f + stops.Count * 0.35f);

                stops.Add(stop);
            }

            stops.Sort((a, b) =>
            {
                int startCompare = a.Entry.StartHour.CompareTo(b.Entry.StartHour);
                return startCompare != 0 ? startCompare : a.EntryIndex.CompareTo(b.EntryIndex);
            });

            return stops;
        }

        private static void DrawPathSegments(AI_NPC npc, List<RouteStop> stops)
        {
            if (stops.Count < 2)
                return;

            int areaMask = ResolveAreaMask(npc);
            for (int i = 0; i < stops.Count; i++)
            {
                RouteStop from = stops[i];
                RouteStop to = stops[(i + 1) % stops.Count];
                if (!from.Resolved || !to.Resolved)
                    continue;

                if ((to.Position - from.Position).sqrMagnitude <= 0.01f)
                    continue;

                Handles.color = PathColor;
                NavMeshPath path = new();
                if (NavMesh.CalculatePath(from.Position, to.Position, areaMask, path)
                    && path.status == NavMeshPathStatus.PathComplete
                    && path.corners != null
                    && path.corners.Length > 1)
                {
                    Handles.DrawAAPolyLine(4f, path.corners);
                    DrawArrow(path.corners);
                }
                else
                {
                    Handles.DrawDottedLine(from.Position, to.Position, 5f);
                    DrawArrow(from.Position, to.Position);
                }
            }
        }

        private static void DrawStops(AI_NPC npc, List<RouteStop> stops)
        {
            for (int i = 0; i < stops.Count; i++)
            {
                RouteStop stop = stops[i];
                Handles.color = stop.Resolved ? MarkerColor : MissingColor;
                Handles.SphereHandleCap(0, stop.Position, Quaternion.identity, MarkerSize, EventType.Repaint);

                if (stop.Resolved && stop.Forward.sqrMagnitude > 0.001f)
                {
                    Quaternion facing = Quaternion.LookRotation(stop.Forward, Vector3.up);
                    Handles.ArrowHandleCap(0, stop.Position + Vector3.up * 0.15f, facing, ArrowSize, EventType.Repaint);
                }

                string label = stop.Resolved
                    ? $"{i + 1}. {stop.Label}"
                    : $"{i + 1}. {stop.Label}\nMissing schedule location";
                Handles.Label(stop.Position + Vector3.up * 0.55f, label);
            }
        }

        private static void DrawArrow(IReadOnlyList<Vector3> corners)
        {
            if (corners == null || corners.Count < 2)
                return;

            int mid = Mathf.Clamp(corners.Count / 2, 1, corners.Count - 1);
            DrawArrow(corners[mid - 1], corners[mid]);
        }

        private static void DrawArrow(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                return;

            Vector3 position = Vector3.Lerp(from, to, 0.5f) + Vector3.up * 0.1f;
            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            Handles.ArrowHandleCap(0, position, rotation, ArrowSize, EventType.Repaint);
        }

        private static bool IsSelected(AI_NPC npc)
        {
            Transform[] selected = Selection.transforms;
            for (int i = 0; i < selected.Length; i++)
            {
                Transform transform = selected[i];
                if (transform == null)
                    continue;

                if (transform == npc.transform || transform.IsChildOf(npc.transform) || npc.transform.IsChildOf(transform))
                    return true;
            }

            return false;
        }

        private static int ResolveAreaMask(AI_NPC npc)
        {
            NavMeshAgent agent = npc.Agent;
            return agent != null && agent.areaMask != 0 ? agent.areaMask : NavMesh.AllAreas;
        }

        private static string BuildLabel(NpcScheduleEntry entry)
        {
            string locationId = string.IsNullOrWhiteSpace(entry.LocationId) ? "<no location>" : entry.LocationId.Trim();
            return $"{FormatHour(entry.StartHour)}-{FormatHour(entry.EndHour)}\n{entry.Activity} @ {locationId}";
        }

        private static string FormatHour(float hour)
        {
            float wrapped = Mathf.Repeat(hour, 24f);
            int wholeHours = Mathf.FloorToInt(wrapped);
            int minutes = Mathf.RoundToInt((wrapped - wholeHours) * 60f);
            if (minutes >= 60)
            {
                wholeHours = (wholeHours + 1) % 24;
                minutes = 0;
            }

            return $"{wholeHours:00}:{minutes:00}";
        }

        private sealed class RouteStop
        {
            public NpcScheduleEntry Entry;
            public int EntryIndex;
            public string Label;
            public Vector3 Position;
            public Vector3 Forward = Vector3.forward;
            public bool Resolved;
        }
    }
}
#endif
