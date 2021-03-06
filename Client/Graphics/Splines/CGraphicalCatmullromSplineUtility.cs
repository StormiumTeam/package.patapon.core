﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PataNext.Client.Graphics.Splines
{
    public static class CGraphicalCatmullromSplineUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetResultLength(int slices, int nodesLength)
        {
            return (slices * (nodesLength - 1)) + (nodesLength - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateCatmullromSpline(DynamicBuffer<float3> nodes,  int   nodesStart,  int  nodesEnd,
                                                     DynamicBuffer<float3> result,
                                                     int                   slices, float tension,     bool loop = false)
        {
            var nodesLength = nodesEnd - nodesStart;

            // yield the first point explicitly, if looping the first point
            // will be generated again in the step for loop when interpolating
            // from last point back to the first point
            var last = nodesLength - 1;
            for (var current = 0; current < last; ++current)
            {
                // wrap around when looping
                if (loop && current > last)
                {
                    current = 0;
                }

                // handle edge cases for looping and non-looping scenarios
                // when looping we wrap around, when not looping use start for previous
                // and end for next when you at the ends of the nodes array
                int end, next, nodePreviousIndex;
                // end...
                if (current == last)
                {
                    end = loop ? 0 : current;
                }
                else end = current + 1;

                // next...
                if (end == last)
                {
                    next = loop ? 0 : end;
                }
                else next = end + 1;

                // nodePreviousIndex...
                if (current == 0)
                {
                    nodePreviousIndex = loop ? last : current;
                }
                else nodePreviousIndex = current - 1;

                var nodePrevious = nodes[nodePreviousIndex + nodesStart];
                var nodeStart    = nodes[(current) + nodesStart];
                var nodeEnd      = nodes[(end) + nodesStart];
                var nodeNext     = nodes[(next) + nodesStart];

                // adding one guarantees yielding at least the end point
                int stepCount = slices + 1;
                for (int step = 1; step <= stepCount; ++step)
                {
                    result.Add(CatmullRom(ref nodePrevious,
                        ref nodeStart,
                        ref nodeEnd,
                        ref nodeNext,
                        step, stepCount, tension));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateCatmullromSpline(Transform[] nodes,  int   nodesStart, int nodesEnd, List<Vector3> m_fillerArray, int resultStart,
                                                     int         slices, float tension,
                                                     bool        loop = false)
        {
            var nodesLength = nodesEnd - nodesStart;
            var formula     = (slices * (nodesLength - 1)) + (nodesLength - 1);

            // yield the first point explicitly, if looping the first point
            // will be generated again in the step for loop when interpolating
            // from last point back to the first point
            int last = nodesLength - 1;
            int idx  = 0;
            for (int current = 0; loop || current < last; ++current)
            {
                // wrap around when looping
                if (loop && current > last)
                {
                    current = 0;
                }

                // handle edge cases for looping and non-looping scenarios
                // when looping we wrap around, when not looping use start for previous
                // and end for next when you at the ends of the nodes array
                int end  = (current == last) ? ((loop) ? 0 : current) : current + 1;
                int next = (end == last) ? ((loop) ? 0 : end) : end + 1;

                var nodePrevious = (float3) nodes[((current == 0) ? ((loop) ? last : current) : current - 1) + nodesStart].localPosition;
                var nodeStart    = (float3) nodes[(current) + nodesStart].localPosition;
                var nodeEnd      = (float3) nodes[(end) + nodesStart].localPosition;
                var nodeNext     = (float3) nodes[(next) + nodesStart].localPosition;

                // adding one guarantees yielding at least the end point
                int stepCount = slices + 1;
                for (int step = 1; step <= stepCount; ++step)
                {
                    if ((idx + resultStart) >= m_fillerArray.Count)
                        m_fillerArray.Add(CatmullRom(ref nodePrevious,
                            ref nodeStart,
                            ref nodeEnd,
                            ref nodeNext,
                            step, stepCount, tension));
                    else
                        m_fillerArray[idx + resultStart] = CatmullRom(ref nodePrevious,
                            ref nodeStart,
                            ref nodeEnd,
                            ref nodeNext,
                            step, stepCount, tension);

                    idx++;
                }
            }
        }

        /**
        * A Vector3 Catmull-Rom CatmullromSpline. Catmull-Rom CatmullromSplines are similar to bezier
        * CatmullromSplines but have the useful property that the generated curve will go
        * through each of the control points.
        *
        * NOTE: The NewCatmullRom() functions are an easier to use alternative to this
        * raw Catmull-Rom implementation.
        *
        * @param previous the point just before the start point or the start point
        *                 itself if no previous point is available
        * @param start generated when elapsedTime == 0
        * @param end generated when elapsedTime >= duration
        * @param next the point just after the end point or the end point itself if no
        *             next point is available
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CatmullRom(ref float3 previous,    ref float3 start,    ref float3 end, ref float3 next,
                                        float      elapsedTime, float      duration, float      tension = 0.5f)
        {
            // References used:
            // p.266 GemsV1
            //
            // tension is often set to 0.5 but you can use any reasonable value:
            // http://www.cs.cmu.edu/~462/projects/assn2/assn2/catmullRom.pdf
            //
            // bias and tension controls:
            // http://local.wasp.uwa.edu.au/~pbourke/miscellaneous/interpolation/

            var p0 = previous;
            var p1 = start;
            var p2 = end;
            var p3 = next;
            
            float3 safe(float3 val)
            {
                if (math.all(math.isnan(val))) 
                    return float3.zero;
                return val;
            }
            
            var t01 = math.pow(math.distance(p0, p1), 0.5f);
            var t12 = math.pow(math.distance(p1, p2), 0.5f);
            var t23 = math.pow(math.distance(p2, p3), 0.5f);

            var m1 = (1 - tension) * 
                     (p2 - p1 + t12 * (safe((p1 - p0) / t01) - safe((p2 - p0) / (t01 + t12))));
            var m2 = (1.0f - tension) * 
                     (p2 - p1 + t12 * (safe((p3 - p2) / t23) - safe((p3 - p1) / (t12 + t23))));
            
           var a = 2.0f * (p1 - p2) + m1 + m2;
            var b = -3.0f * (p1 - p2) - m1 - m1 - m2;
            var c = m1;
            var d = p1;

            var percentComplete = elapsedTime / duration;

            return a * percentComplete * percentComplete * percentComplete +
                   b * percentComplete * percentComplete +
                   c * percentComplete +
                   d;

            /*var percentComplete        = elapsedTime / duration;
            var percentCompleteSquared = math.lengthsq(percentComplete);
            var percentCompleteCubed   = math.dot(percentCompleteSquared, percentComplete);

            var p  = -tension * percentCompleteCubed + percentCompleteSquared - tension * percentComplete;
            var px = p * previous.x;
            var py = p * previous.y;
            var pz = p * previous.z;

            var s  = 1.5f * percentCompleteCubed + -2.5f * percentCompleteSquared + 1.0f;
            var sx = s * start.x;
            var sy = s * start.y;
            var sz = s * start.z;

            var e  = -1.5f * percentCompleteCubed + 2.0f * percentCompleteSquared + tension * percentComplete;
            var ex = e * end.x;
            var ey = e * end.y;
            var ez = e * end.z;

            var n  = tension * percentCompleteCubed - tension * percentCompleteSquared;
            var nx = n * next.x;
            var ny = n * next.y;
            var nz = n * next.z;

            return math.float3(px + sx + ex + nx,
                py + sy + ey + ny,
                pz + sz + ez + nz);*/
        }
    }
}