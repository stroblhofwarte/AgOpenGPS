using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public partial class CBoundaryList
    {
        //list of coordinates of boundary line
        public Polygon fenceLine = new Polygon();
        public List<vec2> fenceLineEar = new List<vec2>(128);
        public Polygon hdLine = new Polygon();
        public List<vec3> turnLine = new List<vec3>(128);

        //constructor
        public CBoundaryList()
        {
            area = 0;
            isDriveThru = false;
        }
    }

    public class Polygon
    {
        public List<vec3> Points = new List<vec3>(128);
        public List<int> Indexer = new List<int>(384);

        public bool ResetPoints, ResetIndexer;
        public int BufferPoints = int.MinValue, BufferIndex = int.MinValue, BufferPointsCnt = 0, BufferIndexCnt = 0;

        public void DrawPolygon(bool Triangles)
        {
            if (Points.Count > 0)
            {
                if (BufferPoints == int.MinValue || ResetPoints)
                {
                    if (BufferPoints == int.MinValue) GL.GenBuffers(1, out BufferPoints);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, BufferPoints);
                    GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Points.Count * 24), Points.ToArray(), BufferUsageHint.StaticDraw);
                    BufferPointsCnt = Points.Count;
                    ResetPoints = false;
                }

                if (Triangles && BufferIndex == int.MinValue || ResetIndexer)
                {
                    double area = 0;
                    int j = Points.Count - 1;
                    for (int i = 0; i < Points.Count; j = i++)
                    {
                        area += (Points[i].northing - Points[j].northing) * (Points[i].easting + Points[j].easting);
                    }
                    if (area > 0)
                    {
                        Points.Reverse();//force Clockwise rotation
                    }

                    double Area = Math.Abs(area / 2.0);

                    double Multiplier = Math.Max(1, Math.Min((Area / 10000) / 10000, 10));
                    double MinDist = 2 * Multiplier;
                    double distance;

                    int k = Points.Count - 1;
                    for (int l = 0; l < Points.Count; k = l++)
                    {
                        if (k < 0) k = Points.Count - 1;
                        //make sure distance isn't too small between points on turnLine
                        distance = glm.Distance(Points[k], Points[l]);
                        if (distance < MinDist)
                        {
                            Points.RemoveAt(l);
                            l--;
                        }
                    }

                    //lang simplification
                    Points.LangSimplify(0.05);
                    ResetPoints = true;


                    Indexer = Points.TriangulatePolygon();



                    if (BufferIndex == int.MinValue) GL.GenBuffers(1, out BufferIndex);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferIndex);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(Indexer.Count * 4), Indexer.ToArray(), BufferUsageHint.StaticDraw);

                    BufferIndexCnt = Indexer.Count;
                    ResetIndexer = false;
                }

                GL.BindBuffer(BufferTarget.ArrayBuffer, BufferPoints);
                GL.VertexPointer(3, VertexPointerType.Double, 0, IntPtr.Zero);
                GL.EnableClientState(ArrayCap.VertexArray);

                if (Triangles)
                {
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferIndex);
                    GL.DrawElements(PrimitiveType.Triangles, BufferIndexCnt, DrawElementsType.UnsignedInt, IntPtr.Zero);
                }
                else
                {
                    GL.DrawArrays(PrimitiveType.LineLoop, 0, BufferPointsCnt);
                }
            }
        }
    }

    public static class StaticClass
    {
        public static void LangSimplify(this List<vec3> PointList, double Tolerance)
        {
            int key = 0;
            int endP = PointList.Count - 1;
            while (key < PointList.Count)
            {
                if (key + 1 == endP)
                {
                    key++;
                    endP = PointList.Count - 1;
                    continue;
                }
                else
                {
                    double maxD = 0;
                    for (int i = key + 1; i < endP; i++)
                    {
                        double d = PointList[i].FindDistanceToSegment(PointList[key], PointList[endP]);
                        if (d > maxD)
                        {
                            maxD = d;
                            if (d > Tolerance)
                            {
                                break;
                            }
                        }
                    }

                    if (maxD > Tolerance)
                        endP--;
                    else
                    {
                        for (int i = endP - 1; i > key; i--)
                            PointList.RemoveAt(i);
                        key++;
                        endP = PointList.Count - 1;
                    }
                }
            }
        }

        public static double FindDistanceToSegment(this vec3 pt, vec3 p1, vec3 p2)
        {
            double dx = p2.northing - p1.northing;
            double dy = p2.easting - p1.easting;
            if ((dx == 0) && (dy == 0))
            {
                dx = pt.northing - p1.northing;
                dy = pt.easting - p1.easting;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            double Time = ((pt.northing - p1.northing) * dx + (pt.easting - p1.easting) * dy) / (dx * dx + dy * dy);

            if (Time < 0)
            {
                dx = pt.northing - p1.northing;
                dy = pt.easting - p1.easting;
            }
            else if (Time > 1)
            {
                dx = pt.northing - p2.northing;
                dy = pt.easting - p2.easting;
            }
            else
            {
                dx = pt.northing - (p1.northing + Time * dx);
                dy = pt.easting - (p1.easting + Time * dy);
            }
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static List<int> TriangulatePolygon(this List<vec3> Points)
        {
            List<int> Indexer = new List<int>();
            if (Points.Count < 3) return Indexer;
            List<int> OldIdx = new List<int>();

            for (int i = 0; i < Points.Count; i++)
            {
                OldIdx.Add(i);
            }

            int j = 0;
            while (OldIdx.Count > 3)
            {
                if (j >= OldIdx.Count) j = 0;

                int i = j < 1 ? OldIdx.Count - 1 : j - 1;
                int k = j >= OldIdx.Count - 1 ? 0 : j + 1;

                if (IsEar(Points[OldIdx[i]], Points[OldIdx[j]], Points[OldIdx[k]], Points))
                {
                    Indexer.Add(OldIdx[j]);
                    Indexer.Add(OldIdx[i]);
                    Indexer.Add(OldIdx[k]);

                    OldIdx.RemoveAt(j);
                }
                else j++;
            }

            if (IsEar(Points[OldIdx[0]], Points[OldIdx[1]], Points[OldIdx[2]], Points))
            {
                Indexer.Add(OldIdx[1]);
                Indexer.Add(OldIdx[0]);
                Indexer.Add(OldIdx[2]);
            }

            return Indexer;
        }

        public static bool IsTriangleOrientedClockwise(vec3 p1, vec3 p2, vec3 p3)
        {
            return p1.northing * p2.easting + p3.northing * p1.easting + p2.northing * p3.easting - p1.northing * p3.easting - p3.northing * p2.easting - p2.northing * p1.easting <= 0;
        }

        private static bool IsEar(vec3 PointA, vec3 PointB, vec3 PointC, List<vec3> vertices)
        {
            bool hasPointInside = IsTriangleOrientedClockwise(PointA, PointB, PointC);
            if (!hasPointInside)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (IsPointInTriangle(PointA, PointB, PointC, vertices[i]))
                    {
                        hasPointInside = true;
                        break;
                    }
                }
            }
            return !hasPointInside;
        }

        public static bool IsPointInTriangle(vec3 p1, vec3 p2, vec3 p3, vec3 p)
        {
            double Denominator = ((p2.easting - p3.easting) * (p1.northing - p3.northing) + (p3.northing - p2.northing) * (p1.easting - p3.easting));
            double a = ((p2.easting - p3.easting) * (p.northing - p3.northing) + (p3.northing - p2.northing) * (p.easting - p3.easting)) / Denominator;

            if (a > 0.0 && a < 1.0)
            {
                double b = ((p3.easting - p1.easting) * (p.northing - p3.northing) + (p1.northing - p3.northing) * (p.easting - p3.easting)) / Denominator;
                if (b > 0.0 && b < 1.0)
                {
                    double c = 1 - a - b;
                    if (c > 0.0 && c < 1.0)
                        return true;
                }
            }
            return false;
        }
    }
}