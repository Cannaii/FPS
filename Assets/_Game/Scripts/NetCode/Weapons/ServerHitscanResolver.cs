using System;
using System.Collections.Generic;
using UnityEngine;

namespace AFPS.NetCode.Weapons
{
    /// <summary>在服务器当前世界状态上解析最近的玩家胶囊命中。</summary>
    public static class ServerHitscanResolver
    {
        public static bool TryResolve(in AuthoritativeShot shot, IReadOnlyList<ServerCombatTarget> targets, out ServerCombatTarget target, out Vector3 hitPoint)
        {
            target = default;
            hitPoint = shot.Origin + shot.Direction * shot.Range;
            if (targets == null || shot.Range <= 0f)
            {
                return false;
            }

            bool found = false;
            float nearestDistance = shot.Range;
            Vector3 rayEnd = shot.Origin + shot.Direction * shot.Range;
            for (int i = 0; i < targets.Count; i++)
            {
                ServerCombatTarget candidate = targets[i];
                if (candidate.EntityId == 0 || candidate.EntityId == shot.EntityId || candidate.Health <= 0f || candidate.Radius <= 0f || candidate.Height < candidate.Radius * 2f)
                {
                    continue;
                }

                Vector3 capsuleStart = candidate.Position + Vector3.up * candidate.Radius;
                Vector3 capsuleEnd = candidate.Position + Vector3.up * (candidate.Height - candidate.Radius);
                ClosestPointsOnSegments(shot.Origin, rayEnd, capsuleStart, capsuleEnd, out float rayFraction, out _, out Vector3 rayPoint, out Vector3 capsulePoint);
                if ((rayPoint - capsulePoint).sqrMagnitude > candidate.Radius * candidate.Radius)
                {
                    continue;
                }

                float distance = rayFraction * shot.Range;
                if (!found || distance < nearestDistance)
                {
                    found = true;
                    nearestDistance = distance;
                    target = candidate;
                    hitPoint = rayPoint;
                }
            }

            return found;
        }

        private static void ClosestPointsOnSegments(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float t, out Vector3 c1, out Vector3 c2)
        {
            const float epsilon = 0.000001f;
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);

            if (a <= epsilon && e <= epsilon)
            {
                s = t = 0f;
            }
            else if (a <= epsilon)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= epsilon)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denominator = a * e - b * b;
                    s = denominator != 0f ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }
    }
}
